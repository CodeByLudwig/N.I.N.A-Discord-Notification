using NINA.DiscordNotification.Helpers;
using Newtonsoft.Json;
using NINA.Core.Model;
using NINA.Core.Utility.Notification;
using NINA.Sequencer.Container;
using NINA.Sequencer.SequenceItem;
using NINA.Sequencer.Trigger;
using NINA.WPF.Base.Interfaces.Mediator;
using System;
using NINA.Sequencer.Interfaces;
using System.ComponentModel.Composition;
using System.Threading;
using System.Threading.Tasks;
using NINA.Image.Interfaces;
using NINA.Equipment.Interfaces.Mediator;
using NINA.Profile.Interfaces;
using NINA.Plugin.Interfaces;
using System.Linq;
using System.ComponentModel;
using System.IO;
using NINA.Core.Utility;
using NINA.DiscordNotification.Models;

namespace NINA.DiscordNotification.DiscordNotificationSequenceItems {
	[ExportMetadata("Name", "Send message after exposures")]
	[ExportMetadata("Description", "This trigger will send a message to discord after a given amount of exposures")]
	[ExportMetadata("Icon", "DiscordSVG")]
	[ExportMetadata("Category", "Discord Notification")]
	[Export(typeof(ISequenceTrigger))]
	[JsonObject(MemberSerialization.OptIn)]
	public class DiscordNotificationMessageAfterExposuresTrigger : SequenceTrigger, ISubscriber, INotifyPropertyChanged {
		[JsonProperty]
		public string Message { get; set; } = "";

		bool sendImage;
		[JsonProperty]
		public bool SendImage {
			get { return sendImage; }
			set {
				sendImage = value;
				this.OnPropertyChanged(new PropertyChangedEventArgs(nameof(ShowUseLiveStackImage)));
			}
		}

		bool useLiveStackImage;
		[JsonProperty]
		public bool UseLiveStackImage { get { return ShowUseLiveStackImage && useLiveStackImage; } set { useLiveStackImage = value; } }

		[JsonProperty]
		public bool ShowUseLiveStackImage {
			get { return !string.IsNullOrEmpty(Properties.Settings.Default.LiveStackedImageDirectory) ? sendImage : false; }
		}

		[JsonProperty]
		public int AfterExposures { get; set; } = 5;

		private int _exposuresDone = 0;
		private bool _sendMessage = false;
		private bool _initializing = false;
		private bool _sendLiveStackedImageMessage = false;
		private bool _messageReceive = false;
		private readonly IImageSaveMediator _imageSaveMediator;
		private readonly IImageDataFactory _imageDataFactory;
		private readonly IImagingMediator _imagingMediator;
		private readonly IProfileService _profileService;
		private readonly IMessageBroker _messageBroker;

		[ImportingConstructor]
		public DiscordNotificationMessageAfterExposuresTrigger(IImageSaveMediator imageSaveMediator, IImagingMediator imagingMediator, IImageDataFactory imageDataFactory, IProfileService profileService, IMessageBroker messageBroker) {
			_imageSaveMediator = imageSaveMediator;
			_imageDataFactory = imageDataFactory;
			_imagingMediator = imagingMediator;
			_profileService = profileService;
			_messageBroker = messageBroker;
		}

		public override object Clone() {
			return new DiscordNotificationMessageAfterExposuresTrigger(_imageSaveMediator, _imagingMediator, _imageDataFactory, _profileService, _messageBroker) {
				Icon = Icon,
				Name = Name,
				Category = Category,
				Description = Description
			};
		}

		public override void SequenceBlockInitialize() {
			_messageBroker.Unsubscribe("Livestack_LivestackDockable_StackUpdateBroadcast", this);
			_messageBroker.Subscribe("Livestack_LivestackDockable_StackUpdateBroadcast", this);
			_imageSaveMediator.ImageSaved -= ImagingMediator_ImageSaved;
			_imageSaveMediator.ImageSaved += ImagingMediator_ImageSaved;
			_initializing = true;

			base.SequenceBlockInitialize();
		}

		public async Task OnMessageReceived(IMessage message) {
			if (!UseLiveStackImage || !_sendLiveStackedImageMessage || !_messageReceive) {
				return;
			}

			_messageReceive = false;

			var targetName = this.GetSequenceTarget()?.TargetName;
			var latestFile = new DirectoryInfo(Properties.Settings.Default.LiveStackedImageDirectory).GetFiles()
			 .OrderByDescending(f => f.LastWriteTime)
			 .FirstOrDefault(f => !string.IsNullOrEmpty(targetName) ? f.FullName.Contains(targetName) : true);

			if (string.IsNullOrEmpty(latestFile?.FullName)) {
				Notification.ShowWarning("No Image from LiveStack found!");
				Logger.Error($"No Image from LiveStack found!");
				return;
			}

			await new Message(_imagingMediator, _imageDataFactory, _profileService) {
				Text = Message,
				TargetName = this.GetSequenceTarget()?.TargetName,
				UseLiveStackedImage = true
			}.Send(latestFile.FullName);
		}

		public override void SequenceBlockTeardown() {
			_exposuresDone = 0;
			_sendMessage = false;
			_sendLiveStackedImageMessage = false;
			_initializing = false;
			_messageReceive = false;
			_imageSaveMediator.ImageSaved -= ImagingMediator_ImageSaved;
			_messageBroker.Unsubscribe("Livestack_LivestackDockable_StackUpdateBroadcast", this);
			base.SequenceBlockTeardown();
		}

		public override Task Execute(ISequenceContainer context, IProgress<ApplicationStatus> progress, CancellationToken token) {
			if (token.IsCancellationRequested) {
				return Task.CompletedTask;
			}

			if (_sendMessage) {
				if (string.IsNullOrEmpty(Message)) {
					Notification.ShowWarning("Message is empty. No message has been sent.");
					Logger.Error($"Message is empty. No message has been sent");
				} else if (!SendImage && !UseLiveStackImage) {
					Task.Run(async () => await new Message(_imagingMediator, _imageDataFactory, _profileService) {
						Text = Message,
						TargetName = this.GetSequenceTarget()?.TargetName
					}.Send(), token);
				} else if (UseLiveStackImage) {
					_messageReceive = true;
					_sendLiveStackedImageMessage = true;
				}

				Logger.Info($"Execute send discord notification -> with live stacked image:${_sendLiveStackedImageMessage}");
			}

			return Task.CompletedTask;
		}

		private void ImagingMediator_ImageSaved(object sender, ImageSavedEventArgs e) {
			if (!_sendMessage || !SendImage || _sendLiveStackedImageMessage) {
				return;
			}

			Task.Run(async () => {
				await new Message(_imagingMediator, _imageDataFactory, _profileService) {
					Text = Message,
					TargetName = this.GetSequenceTarget()?.TargetName,
					ImageData = e.GetImageData(),
				}.Send(_profileService.ActiveProfile.ImageFileSettings.FilePath);
			});
		}

		public override bool ShouldTrigger(ISequenceItem previousItem, ISequenceItem nextItem) {
			if (nextItem is IExposureItem) {
				if (_initializing == false) {
					_exposuresDone++;

					if ((_exposuresDone % AfterExposures) == 0) {
						_sendMessage = true;
						return true;
					}
				}

				_initializing = false;
			}

			_sendLiveStackedImageMessage = false;
			_sendMessage = false;

			return false;
		}

		public override string ToString() {
			return $"Category: {Category}, Item: {nameof(DiscordNotificationMessageAfterExposuresTrigger)}";
		}
	}
}