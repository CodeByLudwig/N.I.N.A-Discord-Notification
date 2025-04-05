using NINA.DiscordNotification.Helpers;
using NINA.DiscordNotification.Models;
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

namespace NINA.DiscordNotification.DiscordNotificationSequenceItems {
	[ExportMetadata("Name", "Discord Notification: Send message after exposures")]
	[ExportMetadata("Description", "This trigger will send a message to discord after a given amount of exposures")]
	[ExportMetadata("Icon", "DiscordSVG")]
	[ExportMetadata("Category", "Discord Notification")]
	[Export(typeof(ISequenceTrigger))]
	[JsonObject(MemberSerialization.OptIn)]
	public class DiscordNotificationMessageAfterExposuresTrigger : SequenceTrigger {
		[JsonProperty]
		public string Message { get; set; } = "";
		[JsonProperty]
		public bool SendImage { get; set; } = false;
		[JsonProperty]
		public int AfterExposures { get; set; } = 5;

		private int _exposuresDone = 0;
		private bool _sendMessage = false;
		private readonly IImageSaveMediator _imageSaveMediator;
		private readonly IImageDataFactory _imageDataFactory;
		private readonly IImagingMediator _imagingMediator;
		private readonly IProfileService _profileService;

		[ImportingConstructor]
		public DiscordNotificationMessageAfterExposuresTrigger(IImageSaveMediator imageSaveMediator, IImagingMediator imagingMediator, IImageDataFactory imageDataFactory, IProfileService profileService) {
			_imageSaveMediator = imageSaveMediator;
			_imageDataFactory = imageDataFactory;
			_imagingMediator = imagingMediator;
			_profileService = profileService;
		}

		public override object Clone() {
			return new DiscordNotificationMessageAfterExposuresTrigger(_imageSaveMediator, _imagingMediator, _imageDataFactory, _profileService) {
				Icon = Icon,
				Name = Name,
				Category = Category,
				Description = Description
			};
		}

		public override void SequenceBlockInitialize() {
			_imageSaveMediator.ImageSaved -= ImagingMediator_ImageSaved;
			_imageSaveMediator.ImageSaved += ImagingMediator_ImageSaved;
			base.SequenceBlockInitialize();
		}

		public override void SequenceBlockTeardown() {
			_exposuresDone = 0;
			_sendMessage = false;
			_imageSaveMediator.ImageSaved -= ImagingMediator_ImageSaved;
			base.SequenceBlockTeardown();
		}

		public override Task Execute(ISequenceContainer context, IProgress<ApplicationStatus> progress, CancellationToken token) {
			if (token.IsCancellationRequested) {
				return Task.CompletedTask;
			}

			_exposuresDone++;

			if (_sendMessage) {
				if (string.IsNullOrEmpty(Message)) {
					Notification.ShowWarning("Message is empty. No message has been sent.");
				} else if (!SendImage) {
					Task.Run(async () => await new Message(_imagingMediator, _imageDataFactory, _profileService) {
						text = Message,
						targetName = this.GetSequenceTarget()?.TargetName
					}.Send());
				}
			}

			return Task.CompletedTask;
		}

		private void ImagingMediator_ImageSaved(object sender, ImageSavedEventArgs e) {
			if (!_sendMessage || !SendImage) {
				return;
			}

			Task.Run(async () => {
				await new Message(_imagingMediator, _imageDataFactory, _profileService) {
					text = Message,
					targetName = this.GetSequenceTarget()?.TargetName,
					imageData = e.GetImageData(),
				}.Send(_profileService.ActiveProfile.ImageFileSettings.FilePath);
			});
		}

		public override bool ShouldTrigger(ISequenceItem previousItem, ISequenceItem nextItem) {
			if (nextItem is IExposureItem) {
				if (previousItem != null && (_exposuresDone % AfterExposures) == 0) {
					_sendMessage = true;
					return true;
				}

				_exposuresDone++;
			}

			_sendMessage = false;
			return false;
		}

		public override string ToString() {
			return $"Category: {Category}, Item: {nameof(DiscordNotificationMessageAfterExposuresTrigger)}";
		}
	}
}