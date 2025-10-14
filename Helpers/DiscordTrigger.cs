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
using System.Threading.Tasks;
using IMessage = NINA.Plugin.Interfaces.IMessage;

namespace NINA.DiscordNotification.Helpers {
	public class DiscordTrigger {
		public string Message { get; set; }
		public bool SendImage { get; set; }
		public bool UseLiveStackImage { get; set; }
		public int AfterExposures { get; set; }
		public List<FilterOption> SelectedFilters { get; set; }
		public string TargetName { get; set; }

		private IThreadChannel _thread;
		private int _exposuresDone;
		private bool _sendMessage;
		private bool _initialized;
		private bool _messageReceived;
		private bool _waitingForBroadcast;
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

			if (string.IsNullOrEmpty(Properties.Settings.Default.DiscordChannelId)) {
				Notification.ShowWarning("DiscordChannelId needs to be set");
				Logger.Info("DiscordChannelId needs to be set");
				return;
			}

			if (GeneralHelpers.DiscordSocket == null) {
				return;
			}

			try {
				GeneralHelpers.DiscordSocket.Ready += async () => {
					_thread = await GeneralHelpers.InitDiscordSocket(GeneralHelpers.DefineThreadName(TargetName), Properties.Settings.Default.DiscordChannelId);
				};

				await GeneralHelpers.DiscordSocket.LoginAsync(TokenType.Bot, Properties.Settings.Default.DiscordBotToken);
				await GeneralHelpers.DiscordSocket.StartAsync();
			} catch (Exception ex) {
				Notification.ShowError(ex.Message);
			}
		}

		public async Task Teardown(int timeout = 120000) {
			var stopwatch = Stopwatch.StartNew();

			while (true) {
				lock (_sendCommands.SendListLock) {
					if (_sendCommands.PendingSendCommands.Count == 0 && _waitingForBroadcast) {
						break;
					}
				}

				if (_sendQueue != null && _sendQueue.HasPendingItems()) {
					await Task.Delay(200);
					continue;
				}

				if (stopwatch.ElapsedMilliseconds > timeout) {
					Logger.Error("Teardown timeout reached while waiting for pending sends.");
					break;
				}

				await Task.Delay(200);
			}

			await _refreshDiscordSocket();

			_thread = null;
			_exposuresDone = 0;
			_sendMessage = false;
			_initialized = false;
			_messageReceived = false;
			_waitingForBroadcast = false;

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
				if (!SendImage && !UseLiveStackImage) {
					_sendQueue.EnqueueSend(async () => await new Message(_imagingMediator, _imageDataFactory, _profileService) {
						Text = Message,
						TargetName = TargetName,
						Thread = _thread
					}.Send());
				} else {
					_messageReceived = true;
					if (UseLiveStackImage) {
						SelectedFilters.ForEach(filter => {
							_sendCommands.AddSendCommand(new SendCommand { IsLiveStackImageSend = true, Filter = filter.Name });
						});
					} else {
						_sendCommands.AddSendCommand(new SendCommand { IsLiveStackImageSend = false });
					}
				}

				Logger.Info($"Execute send discord notification -> use live stacked image:${UseLiveStackImage}");
			}
		}

		public void MessageReceived(IMessage message) {
			if (!UseLiveStackImage || !_messageReceived) {
				return;
			}

			var t = message.Content.GetType().GetProperty("Filter");
			Notification.ShowSuccess("test" + t.GetValue(message.Content));

			_messageReceived = false;
			_OnBroadcastTriggered();
		}

		public void ImageSaved(ImageData ImageData) {
			if (!_sendMessage || !SendImage) {
				return;
			}

			_OnBroadcastTriggered(ImageData);
		}

		private void _OnBroadcastTriggered(ImageData ImageData = null) {
			lock (_sendCommands.SendListLock) {
				if (_waitingForBroadcast && _sendCommands.PendingSendCommands.Count > 0) {
					var cmd = _sendCommands.PendingSendCommands.Dequeue();

					if (cmd.IsLiveStackImageSend) {
						cmd.SendFunc = async () => await _SendLiveStackImage(cmd.Filter);
					} else {
						cmd.SendFunc = async () => await _SendImage(ImageData);
					}

					bool written = _sendQueue.EnqueueSend(async () => {
						try {
							await cmd.SendFunc();
						} finally {
							lock (_sendCommands.SendListLock) {
								_waitingForBroadcast = true;

								if (_sendCommands.PendingSendCommands.Count > 0) {
									Task.Run(() => _OnBroadcastTriggered(ImageData));
								}
							}
						}
					});

					if (!written) {
						Logger.Error("Failed to enqueue send task.");
						_waitingForBroadcast = true;
					} else {
						_waitingForBroadcast = false;
					}
				}
			}
		}

		private Task _SendLiveStackImage(string Filter) {
			FileInfo latestFile = null;
			var fileName = $"{TargetName}-{Filter}";

			try {
				latestFile = ImageHelpers.WaitForFile(Properties.Settings.Default.LiveStackedImageDirectory, ["*.png"], fileName);

				if (string.IsNullOrEmpty(latestFile?.FullName)) {
					latestFile = ImageHelpers.WaitForFile(Properties.Settings.Default.LiveStackedImageDirectory, ["*.fits"], fileName);
				}

				if (string.IsNullOrEmpty(latestFile?.FullName)) {
					Notification.ShowWarning($"No Image from LiveStack found! -> using File Name: {fileName}");
					Logger.Error($"No Image from LiveStack found! -> using File Name: {fileName}");
					return Task.CompletedTask;
				}

				var message = new Message(_imagingMediator, _imageDataFactory, _profileService) {
					Text = Message,
					TargetName = TargetName,
					UseLiveStackedImage = true,
					Thread = _thread
				};

				message.AddExtendedField("File Name", fileName);

				return message.Send(latestFile.FullName);
			} finally {
				if (latestFile != null && latestFile.Exists) {
					latestFile.Delete();
				}
			}
		}

		private Task _SendImage(ImageData ImageData) {
			return new Message(_imagingMediator, _imageDataFactory, _profileService) {
				Text = Message,
				TargetName = TargetName,
				ImageData = ImageData,
				Thread = _thread,
			}.Send(_profileService.ActiveProfile.ImageFileSettings.FilePath);
		}

		private void _Initialize(string Message, bool SendImage, bool UseLiveStackImage, int AfterExposures, string TargetName, ObservableCollection<FilterOption> AvailableFilters) {
			_sendQueue = new SendQueue();
			_sendCommands = new SendCommands();
			_waitingForBroadcast = true;
			_initialized = true;
			this.TargetName = TargetName;
			this.Message = Message;
			this.SendImage = SendImage;
			this.UseLiveStackImage = UseLiveStackImage;
			this.AfterExposures = AfterExposures;
			this.SelectedFilters = AvailableFilters.Where(filter => filter.IsSelected).ToList();
		}

		private async Task _refreshDiscordSocket() {
			if (_thread != null && GeneralHelpers.DiscordSocket != null) {
				await GeneralHelpers.DiscordSocket.LogoutAsync();
				await GeneralHelpers.DiscordSocket.StopAsync();
				GeneralHelpers.DiscordSocket.Dispose();
				GeneralHelpers.DiscordSocket = new DiscordSocketClient();
			}
		}
	}
}
