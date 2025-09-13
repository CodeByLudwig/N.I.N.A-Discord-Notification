using Discord;
using NINA.Core.Utility;
using NINA.Core.Utility.Notification;
using NINA.DiscordNotification.Models;
using NINA.Equipment.Interfaces.Mediator;
using NINA.Image.Interfaces;
using NINA.Profile.Interfaces;
using NINA.Sequencer.Interfaces;
using NINA.Sequencer.SequenceItem;
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using IMessage = NINA.Plugin.Interfaces.IMessage;

namespace NINA.DiscordNotification.Helpers {
	public class DiscordTrigger {
		public string Message { get; set; }
		public bool SendImage { get; set; }
		public bool UseLiveStackImage { get; set; }
		public int AfterExposures { get; set; }
		public string TargetName { get; set; }

		private IThreadChannel _thread;
		private bool _initialized;
		private bool _sendLiveStackedImageMessage;
		private int _exposuresDone;
		private bool _sendMessage;
		private bool _messageReceived;

		private readonly IImagingMediator _imagingMediator;
		private readonly IImageDataFactory _imageDataFactory;
		private readonly IProfileService _profileService;

		public DiscordTrigger(IImagingMediator imagingMediator, IImageDataFactory imageDataFactory, IProfileService profileService) {
			_imagingMediator = imagingMediator;
			_imageDataFactory = imageDataFactory;
			_profileService = profileService;
		}

		public void Initialize(string Message, bool SendImage, bool UseLiveStackImage, int AfterExposures, string TargetName) {
			_Initialize(Message, SendImage, UseLiveStackImage, AfterExposures, TargetName);
		}

		public async Task InitializeThread(string Message, bool SendImage, bool UseLiveStackImage, int AfterExposures, string TargetName) {
			_Initialize(Message, SendImage, UseLiveStackImage, AfterExposures, TargetName);

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
					await _DiscordSocketReady();
				};

				await GeneralHelpers.DiscordSocket.LoginAsync(TokenType.Bot, Properties.Settings.Default.DiscordBotToken);
				await GeneralHelpers.DiscordSocket.StartAsync();
			} catch (Exception ex) {
				Notification.ShowError(ex.Message);
			}
		}

		public void Teardown() {
			_thread = null;
			_exposuresDone = 0;
			_sendMessage = false;
			_sendLiveStackedImageMessage = false;
			_initialized = false;
			_messageReceived = false;
		}

		public bool ShouldTrigger(ISequenceItem nextItem) {
			if (nextItem is IExposureItem) {
				if (_initialized == false) {
					_exposuresDone++;

					if ((_exposuresDone % AfterExposures) == 0) {
						_sendMessage = true;
						return true;
					}
				}

				_initialized = false;
			}

			_sendLiveStackedImageMessage = false;
			_sendMessage = false;

			return false;
		}

		public void Execute(CancellationToken token) {
			if (_sendMessage) {
				if (string.IsNullOrEmpty(Message)) {
					Notification.ShowWarning("Message is empty. No message has been sent.");
					Logger.Error($"Message is empty. No message has been sent");
				} else if (!SendImage && !UseLiveStackImage) {
					Task.Run(async () => await new Message(_imagingMediator, _imageDataFactory, _profileService) {
						Text = Message,
						TargetName = TargetName,
						Thread = _thread
					}.Send(), token);
				} else if (UseLiveStackImage) {
					_messageReceived = true;
					_sendLiveStackedImageMessage = true;
				}

				Logger.Info($"Execute send discord notification -> with live stacked image:${_sendLiveStackedImageMessage}");
			}
		}

		public async Task MessageReceived(IMessage message) {
			if (!UseLiveStackImage || !_sendLiveStackedImageMessage || !_messageReceived) {
				return;
			}

			_messageReceived = false;

			var latestFile = new DirectoryInfo(Properties.Settings.Default.LiveStackedImageDirectory).GetFiles()
			 .OrderByDescending(f => f.LastWriteTime)
			 .FirstOrDefault(f => !string.IsNullOrEmpty(TargetName) ? f.FullName.Contains(TargetName) : true);

			if (string.IsNullOrEmpty(latestFile?.FullName)) {
				Notification.ShowWarning("No Image from LiveStack found!");
				Logger.Error($"No Image from LiveStack found!");
				return;
			}

			await new Message(_imagingMediator, _imageDataFactory, _profileService) {
				Text = Message,
				TargetName = TargetName,
				UseLiveStackedImage = true,
				Thread = _thread
			}.Send(latestFile.FullName);
		}

		public void ImageSaved(ImageData imageData) {
			if (!_sendMessage || !SendImage || _sendLiveStackedImageMessage) {
				return;
			}

			Task.Run(async () => {
				await new Message(_imagingMediator, _imageDataFactory, _profileService) {
					Text = Message,
					TargetName = TargetName,
					ImageData = imageData,
					Thread = _thread,
				}.Send(_profileService.ActiveProfile.ImageFileSettings.FilePath);
			});
		}

		private void _Initialize(string Message, bool SendImage, bool UseLiveStackImage, int AfterExposures, string TargetName) {
			_initialized = true;
			this.TargetName = TargetName;
			this.Message = Message;
			this.SendImage = SendImage;
			this.UseLiveStackImage = UseLiveStackImage;
			this.AfterExposures = AfterExposures;
		}

		private async Task _DiscordSocketReady() {
			var couldParseArchiveDuration = Enum.TryParse<ThreadArchiveDuration>(Properties.Settings.Default.ArchiveDuration, out var archiveDuration);

			if (!couldParseArchiveDuration) {
				Notification.ShowWarning("Could not parse archive duration");
				Logger.Info("Could not parse archive duration");
				return;
			}

			ulong channelId = Convert.ToUInt64(Properties.Settings.Default.DiscordChannelId);
			var channel = GeneralHelpers.DiscordSocket.GetChannel(channelId) as ITextChannel;

			if (channel != null) {
				var activeThreads = await channel.GetActiveThreadsAsync();
				_thread = activeThreads.FirstOrDefault(t => t.Name == TargetName);

				if (_thread == null) {
					_thread = await channel.CreateThreadAsync(
						name: TargetName,
						autoArchiveDuration: archiveDuration,
						invitable: false,
						type: ThreadType.PublicThread
					);
				}
			} else {
				Notification.ShowError($"Channel not found! -> channelId: {channelId}");
				Logger.Error($"Channel not found! -> channelId: {channelId}");
			}
		}
	}
}
