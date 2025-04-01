using NINA.Core.Utility;
using NINA.Plugin;
using NINA.Plugin.Interfaces;
using NINA.Profile.Interfaces;
using NINA.WPF.Base.Interfaces.Mediator;
using NINA.WPF.Base.Interfaces.ViewModel;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
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

        public string ImageDirectory {
            get {
                return Settings.Default.ImageDirectory;
            }
            set {
                Settings.Default.ImageDirectory = value;
                CoreUtil.SaveSettings(Settings.Default);
                RaisePropertyChanged();
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void RaisePropertyChanged([CallerMemberName] string propertyName = null) {
            this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
