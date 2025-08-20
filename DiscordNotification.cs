using Microsoft.Win32;
using NINA.Core.Utility;
using NINA.Plugin;
using NINA.Plugin.Interfaces;
using NINA.Profile.Interfaces;
using NINA.WPF.Base.Interfaces.Mediator;
using NINA.WPF.Base.Interfaces.ViewModel;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using Settings = NINA.DiscordNotification.Properties.Settings;

namespace NINA.DiscordNotification {
	[Export(typeof(IPluginManifest))]
	public class DiscordNotification : PluginBase, INotifyPropertyChanged {
		[ImportingConstructor]
		public DiscordNotification(IProfileService profileService, IOptionsVM options, IImageSaveMediator imageSaveMediator) {
			if (Settings.Default.UpdateSettings) {
				Settings.Default.Upgrade();
				Settings.Default.UpdateSettings = false;
				CoreUtil.SaveSettings(Settings.Default);
			}

			OpenLiveStackedImageDirectoryDialogCommand = new GalaSoft.MvvmLight.Command.RelayCommand(OpenLiveStackedImageDirectoryDialog);
		}

		public override Task Teardown() {
			return base.Teardown();
		}

		public string DiscordWebhookUrl {
			get {
				return Settings.Default.DiscordWebhookUrl;
			}
			set {
				Settings.Default.DiscordWebhookUrl = value;
				CoreUtil.SaveSettings(Settings.Default);
				RaisePropertyChanged();
			}
		}

		public int ImageScaleFactor {
			get {
				return Settings.Default.ImageScaleFactor;
			}
			set {
				Settings.Default.ImageScaleFactor = value;
				CoreUtil.SaveSettings(Settings.Default);
				RaisePropertyChanged();
			}
		}

		public string LiveStackedImageDirectory {
			get {
				return Settings.Default.LiveStackedImageDirectory;
			}
			set {
				Settings.Default.LiveStackedImageDirectory = value;
				CoreUtil.SaveSettings(Settings.Default);
				RaisePropertyChanged();
			}
		}

		public event PropertyChangedEventHandler PropertyChanged;
		protected void RaisePropertyChanged([CallerMemberName] string propertyName = null) {
			this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
		}

		public ICommand OpenLiveStackedImageDirectoryDialogCommand { get; }

		private void OpenLiveStackedImageDirectoryDialog() {
			var dialog = new OpenFolderDialog();

			if (Directory.Exists(LiveStackedImageDirectory)) {
				dialog.InitialDirectory = LiveStackedImageDirectory;
			}

			var result = dialog.ShowDialog();
			if (result.HasValue && result.Value) {
				if (Directory.Exists(dialog.FolderName)) {
					LiveStackedImageDirectory = dialog.FolderName;
				}
			}
		}
	}
}
