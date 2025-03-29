using DanielLudwig.NINA.DiscordNotification.Models;
using NINA.Core.Utility;
using NINA.Image.Interfaces;
using NINA.Profile.Interfaces;
using NINA.WPF.Base.Interfaces.Mediator;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace DanielLudwig.NINA.DiscordNotification.Helpers {
	public static class ImageHelpers {
		public static ImageData GetImageData(this ImageSavedEventArgs eventArgs) {
			return new ImageData(eventArgs);
		}

		public static string EncodeImage(this IRenderedImage renderedImage, string filePath) {
			var encoder = new PngBitmapEncoder();
			encoder.Frames.Add(BitmapFrame.Create(renderedImage.Image));
			if (Path.Exists(filePath)) {
				throw new IOException($"{filePath} already exists");
			}

			using (var fileStream = new FileStream(filePath, FileMode.Create)) {
				encoder.Save(fileStream);
			}

			return filePath;
		}

		public static async Task<IImageData> RenderImage(this IImageDataFactory _imageDataFactory, ImageData imageData, ICameraSettings CameraSettings, PrepareImageParameters imageParameters) {
			var filename = Uri.UnescapeDataString(imageData.PathToImage.AbsolutePath);
			if (!File.Exists(filename)) {
				throw new FileNotFoundException("Image does not exist at the provided path", filename);
			}

			var image = await _imageDataFactory.CreateFromFile(filename, (int)CameraSettings.BitDepth, imageData.IsBayered, CameraSettings.RawConverter);
			image.StarDetectionAnalysis = imageData.StarDetectionAnalysis;
			return image;
		}
	}
}
