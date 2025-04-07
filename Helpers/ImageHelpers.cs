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
using NINA.Image.ImageData;
using System.Runtime.CompilerServices;

namespace NINA.DiscordNotification.Helpers {
	public static class ImageHelpers {
		public static ImageData GetImageData(this ImageSavedEventArgs eventArgs) {
			return new ImageData(eventArgs);
		}

		public static void EncodeImage(this IRenderedImage renderedImage, string filePath) {
			_encode(filePath, renderedImage.Image);
		}

		public static void EncodeImage(this BitmapImage image, string filePath) {
			_encode(filePath, image);
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

		private static  void _encode(string filePath, BitmapSource image) {
			var encoder = new JpegBitmapEncoder();
			encoder.Frames.Add(BitmapFrame.Create(new TransformedBitmap(image, new ScaleTransform(0.8, 0.7))));
			if (Path.Exists(filePath)) {
				throw new IOException($"{filePath} already exists");
			}

			using (var fileStream = new FileStream(filePath, FileMode.Create)) {
				encoder.Save(fileStream);
			}
		}
	}
}
