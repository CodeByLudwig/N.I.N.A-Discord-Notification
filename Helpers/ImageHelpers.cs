using NINA.DiscordNotification.Models;
using NINA.Core.Utility;
using NINA.Image.Interfaces;
using NINA.Profile.Interfaces;
using NINA.WPF.Base.Interfaces.Mediator;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using System.Windows.Media;

namespace NINA.DiscordNotification.Helpers {
	public static class ImageHelpers {
		public static ImageData GetImageData(this ImageSavedEventArgs eventArgs) {
			return new ImageData(eventArgs);
		}

		public static void EncodeImage(this IRenderedImage renderedImage, string filePath) {
			var encoder = new JpegBitmapEncoder();
			encoder.Frames.Add(BitmapFrame.Create(new TransformedBitmap(renderedImage.Image, new ScaleTransform(0.8, 0.7))));
			if (Path.Exists(filePath)) {
				throw new IOException($"{filePath} already exists");
			}

			using (var fileStream = new FileStream(filePath, FileMode.Create)) {
				encoder.Save(fileStream);
			}
		}

		public static async Task<IImageData> RenderImage(this IImageDataFactory _imageDataFactory, ImageData imageData, ICameraSettings CameraSettings) {
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
