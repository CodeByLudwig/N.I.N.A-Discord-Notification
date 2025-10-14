using System;
using System.ComponentModel;

namespace NINA.DiscordNotification.Models {
	public class FilterOption : INotifyPropertyChanged {
		public string Name { get; set; }

		private bool _isSelected;
		public bool IsSelected {
			get => _isSelected;
			set {
				if (_isSelected != value) {
					_isSelected = value;
					OnPropertyChanged(nameof(IsSelected));
					SelectionChanged?.Invoke();
				}
			}
		}

		public static Action SelectionChanged;

		public event PropertyChangedEventHandler PropertyChanged;
		protected void OnPropertyChanged(string name) =>
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
	}
}
