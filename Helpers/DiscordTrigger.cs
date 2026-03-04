using NINA.Core.Model;
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
		public ImageData ImageData { get; set; }

		private int _exposuresDone;
		private bool _sendMessage;
		private List<string> _receivedFilters;
		private bool _isThread;

		private readonly IImagingMediator _imagingMediator;
		private readonly IImageDataFactory _imageDataFactory;
		private readonly IProfileService _profileService;

		public DiscordTrigger(IImagingMediator imagingMediator, IImageDataFactory imageDataFactory, IProfileService profileService) {
			_imagingMediator = imagingMediator;
			_imageDataFactory = imageDataFactory;
			_profileService = profileService;
		}

		public void Initialize(string message, bool sendImage, bool useLiveStackImage, int afterExposures, string targetName, ObservableCollection<FilterOption> availableFilters) {
			_Initialize(message, sendImage, useLiveStackImage, afterExposures, targetName, availableFilters);
		}

		public async Task InitializeThread(string message, bool sendImage, bool useLiveStackImage, int afterExposures, string targetName, ObservableCollection<FilterOption> availableFilters) {
			_Initialize(message, sendImage, useLiveStackImage, afterExposures, targetName, availableFilters);
			_isThread = true;

			if (string.IsNullOrEmpty(Properties.Settings.Default.DiscordChannelId)) {
				Notification.ShowWarning("DiscordChannelId needs to be set");
				Logger.Warning("DiscordChannelId needs to be set");
				return;
			}

			await GeneralHelpers.EnsureSocketStartedAsync();
		}

		public async Task Teardown(int timeout = 60000, CancellationToken cancellationToken = default) {
			var stopwatch = Stopwatch.StartNew();

			while (true) {
				bool noPendingCommands;
				bool noLiveStackCommands;

				lock (SendCommands.SendListLock) {
					noPendingCommands = SendCommands.PendingSendCommands.Count == 0;
				}

				lock (SendCommands.SendLiveStackListLock) {
					noLiveStackCommands = SendCommands.LiveStackCommands.Count == 0;
				}

				if (noPendingCommands && noLiveStackCommands && !SendQueue.HasPendingItems()) {
					break;
				}

				if (stopwatch.ElapsedMilliseconds > timeout) {
					Logger.Warning("Teardown timeout reached while waiting for pending sends.");
					break;
				}

				await Task.Delay(200, cancellationToken);
			}

			await GeneralHelpers.StopSocketAsync();

			_exposuresDone = 0;
			_sendMessage = false;
			_receivedFilters = new List<string>();

			await SendQueue.HardResetAsync();
		}

		public bool ShouldTrigger(ISequenceItem nextItem = null) {
			if (nextItem is IExposureItem) {
				_exposuresDone++;
				if ((_exposuresDone % AfterExposures) == 0) {
					_sendMessage = true;
					return true;
				}
			}

			_sendMessage = false;
			return false;
		}

		public void Execute() {
			if (!_sendMessage) return;

			if (!UseLiveStackImage) {
				SendCommands.AddSendCommand(new SendCommand());
			} else {
				SendCommands.AddLiveStackCommands(new LiveStackSendCommands());
			}

			Logger.Info($"Execute send discord notification -> use live stacked image: {UseLiveStackImage}");
		}

		public void MessageReceived(IMessage message) {
			if (!UseLiveStackImage) return;

			var content = message.Content.GetType().GetProperty("Filter");
			var filter = Regex.Replace(content.GetValue(message.Content).ToString(), $"{ImageHelpers.OSCFILTERPATTERN}$", "");

			lock (SendCommands.SendLiveStackListLock) {
				if (!_receivedFilters.Any(f => f == filter)) {
					_receivedFilters.Add(filter);
				} else if (SendCommands.LiveStackCommands.Count > 0) {
					var cmd = SendCommands.LiveStackCommands.Dequeue();

					foreach (var receivedFilter in _receivedFilters) {
						var progress = new Progress<ApplicationStatus>();
						var currentFilter = SelectedFilters.FirstOrDefault(sf => sf.IsSelected && sf.Name == GeneralHelpers.FilterPattern);
						if (currentFilter != null) {
							cmd.AddLiveStackSendCommand(new LiveStackSendCommand { Filter = currentFilter.Name.Replace(GeneralHelpers.FilterPattern, receivedFilter) });
						}

						var selectedFilter = SelectedFilters.FirstOrDefault(sf => sf.IsSelected && sf.Name != GeneralHelpers.FilterPattern && sf.Name == receivedFilter);
						if (selectedFilter != null) {
							cmd.AddLiveStackSendCommand(new LiveStackSendCommand { Filter = selectedFilter.Name });
						}
					}

					_receivedFilters = new List<string>();
					_OnBroadcastTriggered(cmd);
				}
			}
		}

		public void ImageSaved(ImageData imageData) {
			ImageData = imageData;

			if (UseLiveStackImage) return;

			_OnBroadcastTriggered(imageData);
		}

		private void _OnBroadcastTriggered(LiveStackSendCommands cmd) {
			void EnqueueNext() {
				LiveStackSendCommand nextCommand = null;

				lock (cmd.SendPendingLiveStackListLock) {
					if (cmd.PendingLiveStackSendCommands.Count > 0) {
						nextCommand = cmd.PendingLiveStackSendCommands.Dequeue();
					}
				}

				if (nextCommand == null) {
					return;
				}


				nextCommand.SendFunc = async () => await _SendLiveStackImage(nextCommand.Filter);

				var written = SendQueue.EnqueueSend(async () => {
					try {
						var latestFile = await nextCommand.SendFunc();

						if (latestFile != null && latestFile.Exists) {
							latestFile.Delete();
						}
					} finally {
						EnqueueNext();
					}
				});

				if (!written) {
					Logger.Error("Failed to enqueue send task.");
				}
			}

			EnqueueNext();
		}

		private void _OnBroadcastTriggered(ImageData imageData = null) {
			void EnqueueNext() {
				SendCommand cmd = null;

				lock (SendCommands.SendListLock) {
					if (SendCommands.PendingSendCommands.Count > 0) {
						cmd = SendCommands.PendingSendCommands.Dequeue();
					}
				}

				if (cmd == null) {
					return;
				}

				cmd.SendFunc = async () => await _Send(imageData);

				bool written = SendQueue.EnqueueSend(async () => {
					try {
						await cmd.SendFunc();
					} finally {
						EnqueueNext();
					}
				});

				if (!written) {
					Logger.Error("Failed to enqueue send task.");
				}
			}

			EnqueueNext();
		}

		private async Task<FileInfo> _SendLiveStackImage(string selectedFilterName) {
			FileInfo latestFile = null;
			var fileName = $"{TargetName}-{selectedFilterName}";

			try {
				latestFile = ImageHelpers.WaitForLiveStackFile(Properties.Settings.Default.LiveStackedImageDirectory, ["*.png"], fileName)
					?? ImageHelpers.WaitForLiveStackFile(Properties.Settings.Default.LiveStackedImageDirectory, ["*.fits"], fileName);

				if (string.IsNullOrEmpty(latestFile?.FullName)) {
					Notification.ShowWarning($"No Image from LiveStack found! -> using File Name: {fileName}");
					Logger.Error($"No Image from LiveStack found! -> using File Name: {fileName}");
					return latestFile;
				}

				var message = new Message(_imagingMediator, _imageDataFactory, _profileService) {
					Text = Message,
					TargetName = TargetName,
					ImageData = ImageData,
					UseLiveStackedImage = true,
					Filter = selectedFilterName
				};

				message.AddExtendedField("File Name", fileName);
				message.AddExtendedField("SelectedFilter", selectedFilterName);
				await message.Send(_isThread, latestFile.FullName);

				return latestFile;
			} catch (Exception ex) {
				Logger.Error($"Error while sending: {ex.Message}");
			}

			return latestFile;
		}

		private Task _Send(ImageData imageData) {
			var message = new Message(_imagingMediator, _imageDataFactory, _profileService) {
				Text = Message,
				TargetName = TargetName,
				ImageData = imageData,
				Filter = imageData.Filter,
			};

			if (SendImage) {
				return message.Send(_isThread, _profileService.ActiveProfile.ImageFileSettings.FilePath);
			}

			return message.Send(_isThread);
		}

		private void _Initialize(string message, bool sendImage, bool useLiveStackImage, int afterExposures, string targetName, ObservableCollection<FilterOption> availableFilters) {
			SendCommands.ClearAll();

			_receivedFilters = new List<string>();
			TargetName = string.IsNullOrEmpty(targetName) ? "No_target" : targetName;
			Message = message;
			SendImage = sendImage;
			UseLiveStackImage = useLiveStackImage;
			AfterExposures = afterExposures;
			SelectedFilters = availableFilters.Where(filter => filter.IsSelected).ToList();
		}
	}
}
