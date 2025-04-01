using System.ComponentModel.Composition;
using System.Windows;

namespace NINA.DiscordNotification.DiscordNotificationSequenceItems {
    [Export(typeof(ResourceDictionary))]
    public partial class PluginItemTemplate : ResourceDictionary {
        public PluginItemTemplate() {
            InitializeComponent();
        }
    }
}