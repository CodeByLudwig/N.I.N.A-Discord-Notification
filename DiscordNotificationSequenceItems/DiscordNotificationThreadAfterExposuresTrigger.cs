using NINA.DiscordNotification.Helpers;
using Newtonsoft.Json;
using NINA.Core.Model;
using NINA.Sequencer.Container;
using NINA.Sequencer.SequenceItem;
using NINA.Sequencer.Trigger;
using NINA.WPF.Base.Interfaces.Mediator;
using System;
using System.ComponentModel.Composition;
using System.Threading;
using System.Threading.Tasks;
using NINA.Image.Interfaces;
using NINA.Equipment.Interfaces.Mediator;
using NINA.Profile.Interfaces;
using NINA.Plugin.Interfaces;
using System.ComponentModel;
using NINA.Core.Utility;
using NINA.DiscordNotification.Models;
using System.Collections.ObjectModel;
using System.Linq;

namespace NINA.DiscordNotification.DiscordNotificationSequenceItems {
	[ExportMetadata("Name", "Create thread and send message after exposures")]
	[ExportMetadata("Description", "This trigger will create a thread named after the target and send a message to Discord after a specified number of exposures")]
	[ExportMetadata("Icon", "DiscordSVG")]
	[ExportMetadata("Category", "Discord Notification")]
	[Export(typeof(ISequenceTrigger))]
	[JsonObject(MemberSerialization.OptIn)]
	public class DiscordNotificationThreadAfterExposuresTrigger : SequenceTrigger, ISubscriber, INotifyPropertyChanged {
		string message = "";
		[JsonProperty]
		public string Message {
			get { return message; }
			set {
				message = value;
				_discordTrigger.Message = value;
			}
		}

		bool sendImage;
		[JsonProperty]
		public bool SendImage {
			get { return sendImage; }
			set {
				sendImage = value;
				OnPropertyChanged(new PropertyChangedEventArgs(nameof(ShowUseLiveStackImage)));
				_discordTrigger.SendImage = value;
			}
		}

		int afterExposures = 5;
		[JsonProperty]
		public int AfterExposures {
			get { return afterExposures; }
			set {
				afterExposures = value;
				_discordTrigger.AfterExposures = value;
			}
		}

		bool useLiveStackImage;
		[JsonProperty]
		public bool UseLiveStackImage {
			get { return ShowUseLiveStackImage && useLiveStackImage; }
			set {
				useLiveStackImage = value;
				OnPropertyChanged(new PropertyChangedEventArgs(nameof(ShowFilterSelection)));
				_discordTrigger.UseLiveStackImage = value;
			}
		}

		[JsonProperty]
		public bool ShowUseLiveStackImage {
			get { return !string.IsNullOrEmpty(Properties.Settings.Default.LiveStackedImageDirectory) ? sendImage : false; }
		}

		[JsonProperty]
		public bool ShowFilterSelection {
			get { return useLiveStackImage; }
		}

		[JsonProperty]
		ObservableCollection<FilterOption> availableFilters;
		public ObservableCollection<FilterOption> AvailableFilters {
			get {
				if (availableFilters == null) {
					availableFilters = new ObservableCollection<FilterOption> {
						new FilterOption { Name = "RGB", IsSelected = true },
						new FilterOption { Name = "R" },
						new FilterOption { Name = "G" },
						new FilterOption { Name = "B" },
						new FilterOption { Name = "S" },
						new FilterOption { Name = "H" },
						new FilterOption { Name = "O" },
					};
				}

				FilterOption.SelectionChanged = () => {
					if (availableFilters.All(x => !x.IsSelected)) {
						availableFilters.FirstOrDefault(f => f.Name == "RGB").IsSelected = true;
					}

					OnPropertyChanged(nameof(SelectedFiltersText));
				};

				return availableFilters;
			}
			set {
				availableFilters = value;
			}
		}

		[JsonProperty]
		string selectedFiltersText;
		public string SelectedFiltersText {
			get {
				selectedFiltersText = string.Join(", ", AvailableFilters.Where(f => f.IsSelected).Select(f => f.Name));

				return selectedFiltersText;
			}
			set {
				selectedFiltersText = value;
			}
		}

		private readonly IImageSaveMediator _imageSaveMediator;
		private readonly IImageDataFactory _imageDataFactory;
		private readonly IImagingMediator _imagingMediator;
		private readonly IProfileService _profileService;
		private readonly IMessageBroker _messageBroker;
		private readonly DiscordTrigger _discordTrigger;

		[ImportingConstructor]
		public DiscordNotificationThreadAfterExposuresTrigger(IImageSaveMediator imageSaveMediator, IImagingMediator imagingMediator, IImageDataFactory imageDataFactory, IProfileService profileService, IMessageBroker messageBroker) {
			_imageSaveMediator = imageSaveMediator;
			_imageDataFactory = imageDataFactory;
			_imagingMediator = imagingMediator;
			_profileService = profileService;
			_messageBroker = messageBroker;
			_discordTrigger = new DiscordTrigger(_imagingMediator, _imageDataFactory, _profileService);
		}

		public override object Clone() {
			return new DiscordNotificationThreadAfterExposuresTrigger(_imageSaveMediator, _imagingMediator, _imageDataFactory, _profileService, _messageBroker) {
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
			Task.Run(async () => await _discordTrigger.InitializeThread(Message, SendImage, UseLiveStackImage, AfterExposures, this.GetSequenceTarget()?.TargetName, AvailableFilters));

			base.SequenceBlockInitialize();
		}

		public Task OnMessageReceived(IMessage message) {
			_discordTrigger.MessageReceived(message);
			return Task.CompletedTask;
		}

		public override void SequenceBlockTeardown() {
			_discordTrigger.Teardown().GetAwaiter().GetResult();
			_imageSaveMediator.ImageSaved -= ImagingMediator_ImageSaved;
			_messageBroker.Unsubscribe("Livestack_LivestackDockable_StackUpdateBroadcast", this);
			base.SequenceBlockTeardown();
		}

		public override Task Execute(ISequenceContainer context, IProgress<ApplicationStatus> progress, CancellationToken token) {
			if (token.IsCancellationRequested) {
				return Task.CompletedTask;
			}

			_discordTrigger.Execute();

			return Task.CompletedTask;
		}

		private void ImagingMediator_ImageSaved(object sender, ImageSavedEventArgs e) {
			_discordTrigger.ImageSaved(e.GetImageData());
		}

		public override bool ShouldTrigger(ISequenceItem previousItem, ISequenceItem nextItem) {
			return _discordTrigger.ShouldTrigger(nextItem);
		}

		public override string ToString() {
			return $"Category: {Category}, Item: {nameof(DiscordNotificationThreadAfterExposuresTrigger)}";
		}
	}
}