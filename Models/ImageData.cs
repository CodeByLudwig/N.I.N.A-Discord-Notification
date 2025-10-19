using NINA.Core.Enum;
using NINA.Image.ImageData;
using NINA.Image.Interfaces;
using NINA.WPF.Base.Interfaces.Mediator;
using System;
using System.Collections.Generic;
using System.Windows.Media.Imaging;

namespace NINA.DiscordNotification.Models {
	public class ImageData {
		public BitmapSource Image { get; }
		public CameraParameter CameraParameter { get; }
		public IStarDetectionAnalysis StarDetectionAnalysis { get; }
		public Uri PathToImage { get; }
		public bool IsBayered { get; }
		public double Duration { get; }
		public FileTypeEnum FileType { get; }
		public string Filter { get; }

		public ImageData(ImageSavedEventArgs eventArgs) {
			Image = eventArgs.Image;
			CameraParameter = eventArgs.MetaData.Camera;
			StarDetectionAnalysis = eventArgs.StarDetectionAnalysis;
			PathToImage = eventArgs.PathToImage;
			IsBayered = eventArgs.IsBayered;
			Duration = eventArgs.Duration;
			FileType = eventArgs.FileType;
			Filter = eventArgs.Filter;
		}

		public Dictionary<string, object> GetExtendedImageData() {
			var dict = new Dictionary<string, object>
			{
				{ "Gain", CameraParameter.Gain },
				{ "Temperature", CameraParameter.Temperature },
				{ "Binning", CameraParameter.Binning },
				{ "HFR", StarDetectionAnalysis.HFR },
				{ "DetectedStars", StarDetectionAnalysis.DetectedStars },
				{ "IsBayered", IsBayered },
				{ "Duration", Duration },
				{ "Filter", Filter }
			};
			return dict;
		}
	}
}
