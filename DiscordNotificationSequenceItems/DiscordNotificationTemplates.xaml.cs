using NINA.Core.Utility.Notification;
using NINA.DiscordNotification.Models;
using System.ComponentModel.Composition;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace NINA.DiscordNotification.DiscordNotificationSequenceItems {
	[Export(typeof(ResourceDictionary))]
	public partial class PluginItemTemplate : ResourceDictionary {
		public PluginItemTemplate() {
			InitializeComponent();
		}

		private void ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e) {
			if (sender is ComboBox comboBox) {
				var text = string.Join(", ", comboBox.Items.Cast<FilterOption>().ToList().Where(item => item.IsSelected).Select(item => item.Name));
				comboBox.SelectedItem = null;
				comboBox.Text = text;
			}
		}

		private void FilterItem_PreviewMouseDown(object sender, MouseButtonEventArgs e) {
			if (sender is ComboBoxItem item && item.DataContext is FilterOption filter) {
				filter.IsSelected = !filter.IsSelected;

				e.Handled = true;
			}
		}
	}
}