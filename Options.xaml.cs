using System.ComponentModel.Composition;
using System.Windows;

namespace NINA.DiscordNotification {
    [Export(typeof(ResourceDictionary))]
    partial class Options : ResourceDictionary {

        public Options() {
            InitializeComponent();
        }
    }
}