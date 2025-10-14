using Discord;
using Discord.WebSocket;
using NINA.Core.Utility;
using NINA.Core.Utility.Notification;
using NINA.DiscordNotification.Models;
using NINA.Equipment.Interfaces.Mediator;
using NINA.Image.Interfaces;
using NINA.Profile.Interfaces;
using NINA.Sequencer.Interfaces;
using NINA.Sequencer.SequenceItem;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using IMessage = NINA.Plugin.Interfaces.IMessage;
using Message = NINA.DiscordNotification.Models.Message;

namespace NINA.DiscordNotification.Helpers {
	public class DiscordTrigger {
		public string Message { get; set; }
		public bool SendImage { get; set; }
		public bool UseLiveStackImage { get; set; }
		public int AfterExposures { get; set; }
		public List<FilterOption> SelectedFilters { get; set; }
		public string TargetName { get; set; }

		private int _exposuresDone;
		private bool _sendMessage;
		private bool _initialized;
		private List<string> _receivedFilters;
		private bool _isThread;
		private SendQueue _sendQueue;
		private SendCommands _sendCommands;

		private readonly IImagingMediator _imagingMediator;
		private readonly IImageDataFactory _imageDataFactory;
		private readonly IProfileService _profileService;

		public DiscordTrigger(IImagingMediator imagingMediator, IImageDataFactory imageDataFactory, IProfileService profileService) {
			_imagingMediator = imagingMediator;
			_imageDataFactory = imageDataFactory;
			_profileService = profileService;
		}

		public void Initialize(string Message, bool SendImage, bool UseLiveStackImage, int AfterExposures, string TargetName, ObservableCollection<FilterOption> AvailableFilters) {
			_Initialize(Message, SendImage, UseLiveStackImage, AfterExposures, TargetName, AvailableFilters);
		}

		public async Task InitializeThread(string Message, bool SendImage, bool UseLiveStackImage, int AfterExposures, string TargetName, ObservableCollection<FilterOption> AvailableFilters) {
			_Initialize(Message, SendImage, UseLiveStackImage, AfterExposures, TargetName, AvailableFilters);

			_isThread = true;

			if (string.IsNullOrEmpty(Properties.Settings.Default.DiscordChannelId)) {
				Notification.ShowWarning("DiscordChannelId needs to be set");
				Logger.Info("DiscordChannelId needs to be set");
				return;
			}

			if (GeneralHelpers.DiscordSocket == null) {
				return;
			}

			if (GeneralHelpers.DiscordSocket != null) {
				await GeneralHelpers.DiscordSocket.LogoutAsync();
				await GeneralHelpers.DiscordSocket.StopAsync();
				GeneralHelpers.DiscordSocket.Dispose();
				GeneralHelpers.DiscordSocket = new DiscordSocketClient();
			}

			try {
				await GeneralHelpers.DiscordSocket.LoginAsync(TokenType.Bot, Properties.Settings.Default.DiscordBotToken);
				await GeneralHelpers.DiscordSocket.StartAsync();
			} catch (Exception ex) {
				Notification.ShowError(ex.Message);
			}
		}

		public async Task Teardown(int timeout = 60000, CancellationToken cancellationToken = default) {
			var stopwatch = Stopwatch.StartNew();

			while (true) {
				bool noPendingCommands;
				bool noLiveStackCommands;

				lock (_sendCommands.SendListLock) {
					noPendingCommands = _sendCommands.PendingSendCommands.Count == 0;
				}

				lock (_sendCommands.SendLiveStackListLock) {
					noLiveStackCommands = _sendCommands.LiveStackCommands.Count == 0;
				}

				if (noPendingCommands && noLiveStackCommands && (_sendQueue == null || !_sendQueue.HasPendingItems())) {
					break;
				}

				if (!noLiveStackCommands) {
					Logger.Debug($"Wait for {_sendCommands.LiveStackCommands.Count} LiveStack-Command(s)...");
				}

				if (stopwatch.ElapsedMilliseconds > timeout) {
					Logger.Error("Teardown timeout reached while waiting for pending sends.");
					break;
				}

				await Task.Delay(200);
			}

			await _refreshDiscordSocket();

			_exposuresDone = 0;
			_sendMessage = false;
			_initialized = false;
			_receivedFilters = new List<string>();
			_isThread = false;

			await _sendQueue?.ShutdownAsync();
			_sendQueue?.Dispose();
		}

		public bool ShouldTrigger(ISequenceItem nextItem) {
			if (nextItem is IExposureItem) {
				if (_initialized) {
					_exposuresDone++;

					if ((_exposuresDone % AfterExposures) == 0) {
						_sendMessage = true;
						return true;
					}
				}
			}

			_sendMessage = false;

			return false;
		}

		public void Execute() {
			if (_sendMessage) {
				if (!UseLiveStackImage) {
					_sendCommands.AddSendCommand(new SendCommand());
				} else {
					_sendCommands.AddLiveStackCommands(new LiveStackSendCommands());
				}

				Logger.Info($"Execute send discord notification -> use live stacked image:${UseLiveStackImage}");
			}
		}

		public void MessageReceived(IMessage message) {
			lock (_sendCommands.SendLiveStackListLock) {
				if (!UseLiveStackImage) {
					return;
				}

				var content = message.Content.GetType().GetProperty("Filter");
				var filter = Regex.Replace(content.GetValue(message.Content).ToString(), $"{ImageHelpers.OSCFILTERPATTERN}$", "");

				if (!_receivedFilters.Any(f => f == filter)) {
					_receivedFilters.Add(filter);
				} else {
					var cmd = _sendCommands.LiveStackCommands.Dequeue();

					_receivedFilters.ForEach(receivedFilter => {
						var currentFilter = SelectedFilters.FirstOrDefault(sf => sf.IsSelected && sf.Name == GeneralHelpers.FilterPattern);
						if (currentFilter != null) {
							cmd.AddLiveStackSendCommand(new LiveStackSendCommand { Filter = currentFilter.Name.Replace(GeneralHelpers.FilterPattern, receivedFilter) });
						}

						var selectedFilter = SelectedFilters.FirstOrDefault(sf => sf.IsSelected && sf.Name != GeneralHelpers.FilterPattern && sf.Name == receivedFilter);
						if (selectedFilter != null) {
							cmd.AddLiveStackSendCommand(new LiveStackSendCommand { Filter = selectedFilter.Name });
						}
					});

					_receivedFilters = new List<string>();
					_OnBroadcastTriggered(cmd);
				}
			}
		}

		public void ImageSaved(ImageData ImageData) {
			if (UseLiveStackImage) {
				return;
			}

			if (!SendImage) {
				_sendQueue.EnqueueSend(async () => await new Message(_imagingMediator, _imageDataFactory, _profileService) {
					Text = Message,
					TargetName = TargetName,
					ImageData = ImageData,
					Filter = ImageData.Filter,
				}.Send(_isThread));
				return;
			}

			_OnBroadcastTriggered(ImageData);
		}

		private void _OnBroadcastTriggered(LiveStackSendCommands cmd) {
			lock (cmd.SendPendingLiveStackListLock) {
				if (cmd.PendingLiveStackSendCommands.Count > 0) {
					var liveStackCmd = cmd.PendingLiveStackSendCommands.Dequeue();

					liveStackCmd.SendFunc = async () => await _SendLiveStackImage(liveStackCmd.Filter);

					FileInfo latestFile = null;

					bool written = _sendQueue.EnqueueSend(async () => {
						try {
							latestFile = await liveStackCmd.SendFunc();
						} finally {
							if (latestFile != null && latestFile.Exists) {
								latestFile.Delete();
							}

							lock (_sendCommands.SendListLock) {
								if (cmd.PendingLiveStackSendCommands.Count > 0) {
									Task.Run(() => _OnBroadcastTriggered(cmd));
								}
							}
						}
					});

					if (!written) {
						Logger.Error("Failed to enqueue send task.");
					}
				}
			}
		}

		private void _OnBroadcastTriggered(ImageData ImageData = null) {
			lock (_sendCommands.SendListLock) {
				if (_sendCommands.PendingSendCommands.Count > 0) {
					var cmd = _sendCommands.PendingSendCommands.Dequeue();

					cmd.SendFunc = async () => await _SendImage(ImageData);

					bool written = _sendQueue.EnqueueSend(async () => {
						try {
							await cmd.SendFunc();
						} finally {
							lock (_sendCommands.SendListLock) {
								if (_sendCommands.PendingSendCommands.Count > 0) {
									Task.Run(() => _OnBroadcastTriggered(ImageData));
								}
							}
						}
					});

					if (!written) {
						Logger.Error("Failed to enqueue send task.");
					}
				}
			}
		}

		private async Task<FileInfo> _SendLiveStackImage(string SelectedFilterName) {
			FileInfo latestFile = null;
			var fileName = $"{TargetName}-{SelectedFilterName}";

			try {
				latestFile = ImageHelpers.WaitForLiveStackFile(Properties.Settings.Default.LiveStackedImageDirectory, ["*.png"], fileName);

				if (string.IsNullOrEmpty(latestFile?.FullName)) {
					latestFile = ImageHelpers.WaitForLiveStackFile(Properties.Settings.Default.LiveStackedImageDirectory, ["*.fits"], fileName);
				}

				if (string.IsNullOrEmpty(latestFile?.FullName)) {
					Notification.ShowWarning($"No Image from LiveStack found! -> using File Name: {fileName}");
					Logger.Error($"No Image from LiveStack found! -> using File Name: {fileName}");
					return latestFile;
				}

				var message = new Message(_imagingMediator, _imageDataFactory, _profileService) {
					Text = Message,
					TargetName = TargetName,
					UseLiveStackedImage = true,
					Filter = SelectedFilterName
				};

				message.AddExtendedField("File Name", fileName);
				message.AddExtendedField("Filter", SelectedFilterName);
				await message.Send(_isThread, latestFile.FullName);

				return latestFile;
			} catch (Exception ex) {
				Logger.Error($"Error while sending: {ex.Message}");
			}

			return latestFile;
		}

		private Task _SendImage(ImageData ImageData) {
			return new Message(_imagingMediator, _imageDataFactory, _profileService) {
				Text = Message,
				TargetName = TargetName,
				ImageData = ImageData,
				Filter = ImageData.Filter,
			}.Send(_isThread, _profileService.ActiveProfile.ImageFileSettings.FilePath);
		}

		private void _Initialize(string Message, bool SendImage, bool UseLiveStackImage, int AfterExposures, string TargetName, ObservableCollection<FilterOption> AvailableFilters) {
			_sendQueue = new SendQueue();
			_sendCommands = new SendCommands();
			_initialized = true;
			_receivedFilters = new List<string>();
			this.TargetName = TargetName;
			this.Message = Message;
			this.SendImage = SendImage;
			this.UseLiveStackImage = UseLiveStackImage;
			this.AfterExposures = AfterExposures;
			this.SelectedFilters = AvailableFilters.Where(filter => filter.IsSelected).ToList();
		}

		private async Task _refreshDiscordSocket() {
			if (GeneralHelpers.DiscordSocket != null) {
				await GeneralHelpers.DiscordSocket.LogoutAsync();
				await GeneralHelpers.DiscordSocket.StopAsync();
				GeneralHelpers.DiscordSocket.Dispose();
				GeneralHelpers.DiscordSocket = new DiscordSocketClient();
			}
		}
	}
}
