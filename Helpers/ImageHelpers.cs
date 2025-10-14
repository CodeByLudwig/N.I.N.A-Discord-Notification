using NINA.DiscordNotification.Models;
using NINA.Image.Interfaces;
using NINA.Profile.Interfaces;
using NINA.WPF.Base.Interfaces.Mediator;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using System.Windows.Media;
using System.Linq;
using System.Diagnostics;
using System.Threading;

namespace NINA.DiscordNotification.Helpers {
	public static class ImageHelpers {
		public static ImageData GetImageData(this ImageSavedEventArgs eventArgs) {
			return new ImageData(eventArgs);
		}

		public static FileInfo WaitForFile(string directory, string[] patterns, string targetName, int timeout = 8000, int pollInterval = 800) {
			var stopwatch = Stopwatch.StartNew();

			while (stopwatch.ElapsedMilliseconds < timeout) {
				var file = patterns.SelectMany(pattern => new DirectoryInfo(directory).GetFiles(pattern))
				   .Where(f => (string.IsNullOrEmpty(targetName) || Path.GetFileNameWithoutExtension(f.FullName) == targetName) && f.Length > 0)
				   .OrderByDescending(f => f.LastWriteTime)
				   .FirstOrDefault();

				if (file != null) {
					var tempPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + file.Extension);
					File.Copy(file.FullName, tempPath, overwrite: true);
					Thread.Sleep(pollInterval);
					return new FileInfo(tempPath);
				}

				Thread.Sleep(pollInterval);
			}

			return null;
		}

		public static bool CheckExtensions(this string path, string[] extenstions) {
			return extenstions.Any(extension => Path.GetExtension(path).Equals(extension, StringComparison.OrdinalIgnoreCase));
		}

		public static void EncodeImage(this IRenderedImage renderedImage, string filePath) {
			_Encode(renderedImage.Image, filePath);
		}

		public static void EncodeImage(this BitmapSource image, string filePath) {
			_Encode(image, filePath);
		}

		public static void EncodeImage(this string path, string filePath) {
			_Encode(_LoadAsBitmapSource(path), filePath);
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

		private static BitmapSource _LoadAsBitmapSource(string path) {
			var tempPath = Path.GetTempFileName();
			File.Copy(path, tempPath, overwrite: true);

			var bitmap = new BitmapImage();

			using (var stream = new FileStream(tempPath, FileMode.Open, FileAccess.Read, FileShare.Read)) {
				bitmap.BeginInit();
				bitmap.CacheOption = BitmapCacheOption.OnLoad;
				bitmap.StreamSource = stream;
				bitmap.EndInit();
				bitmap.Freeze();
			}

			try {
				File.Delete(tempPath);
			} catch {
			}

			return bitmap;
		}

		private static void _Encode(BitmapSource image, string filePath) {
			var encoder = new JpegBitmapEncoder();
			double scaleFactor = (double)Properties.Settings.Default.ImageScaleFactor / 100;
			encoder.Frames.Add(BitmapFrame.Create(new TransformedBitmap(image, new ScaleTransform(scaleFactor, scaleFactor))));
			if (File.Exists(filePath)) {
				throw new IOException($"{filePath} already exists");
			}

			using (var fileStream = new FileStream(filePath, FileMode.Create)) {
				encoder.Save(fileStream);
			}
		}
	}
}
