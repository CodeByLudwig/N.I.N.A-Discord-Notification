using NINA.DiscordNotification.Models;
using NINA.Core.Utility;
using NINA.Image.Interfaces;
using NINA.Profile.Interfaces;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Discord;
using System.Collections.Generic;
using NINA.Equipment.Interfaces.Mediator;
using NINA.Core.Utility.Notification;
using System.Drawing;

namespace NINA.DiscordNotification.Helpers {
	public class Message {
		public string text { get; set; }
		public ImageData imageData { get; set; }
		public string targetName { get; set; }
		public bool useLiveStackedImage { get; set; }

		private string _filePath;
		private IEnumerable<KeyValuePair<string, object>> _extendedFields {
			get {
				if (imageData != null) {
					return imageData.GetExtendedImageData().AsEnumerable<KeyValuePair<string, object>>();
				}

				return null;
			}
		}
		private readonly PrepareImageParameters _imageParameters = new PrepareImageParameters(true, false);
		private readonly IImageDataFactory _imageDataFactory;
		private readonly IImagingMediator _imagingMediator;
		private readonly IProfileService _profileService;

		public Message(IImagingMediator imagingMediator, IImageDataFactory imageDataFactory, IProfileService profileService) {
			_imageDataFactory = imageDataFactory;
			_imagingMediator = imagingMediator;
			_profileService = profileService;
		}

		public async Task Send(string path) {
			try {
				var sendStopwatch = new Stopwatch();
				if ((useLiveStackedImage)) {
					_filePath = Path.Combine([path, $"image_{Guid.NewGuid()}.png"]);
					var image = (await _imageDataFactory.RenderImage(imageData, _profileService.ActiveProfile.CameraSettings));
					(await _imagingMediator.PrepareImage(image, _imageParameters, CancellationToken.None)).EncodeImage(_filePath);
				} else {
					_filePath = Path.Combine([Path.GetDirectoryName(path), $"image_{Guid.NewGuid()}.png"]);
					Notification.ShowWarning("path." + path);


					var file = File.ReadAllBytes(path);
					Notification.ShowWarning("file." + file);

					MemoryStream stream = new MemoryStream(file);
					Notification.ShowWarning("stream." + stream);

					Bitmap bitmap = new Bitmap(stream);
					Notification.ShowWarning("bitmap." + bitmap);

					bitmap.Save(_filePath);

					Notification.ShowWarning("latestFile?.FullName." + bitmap.Width);

				}

				sendStopwatch.Start();
				await Send();
				sendStopwatch.Stop();
				if (_filePath != null && File.Exists(_filePath)) {
					File.Delete(_filePath);
				}
				Logger.Info($"Image send time={sendStopwatch.ElapsedMilliseconds}ms");
			} catch (Exception ex) {
				Notification.ShowWarning("ex." + ex.Message);

				Logger.Error(ex);
			}
		}

		public async Task Send() {
			try {
				if (_filePath != null) {
					await Helpers.DiscordWebhook.SendFileMessage(_filePath, text, _GetEmbedFields());
					return;
				}

				await Helpers.DiscordWebhook.SendMessage(text, _GetEmbedFields());
			} catch (Exception ex) {
				Logger.Error(ex);
			}
		}

		private IEnumerable<EmbedFieldBuilder> _GetEmbedFields() {
			var fields = new List<EmbedFieldBuilder>();
			var field = new EmbedFieldBuilder();
			if (!string.IsNullOrEmpty(targetName)) {
				field.Name = "Target";
				field.Value = targetName;
				fields.Add(field);

				_extendedFields?.ToList()?.ForEach(extendedField => {
					if (!string.IsNullOrWhiteSpace(extendedField.Value.ToString())) {
						fields.Add(new EmbedFieldBuilder() { Name = extendedField.Key, Value = extendedField.Value });
					}
				});
			}

			return fields.Count > 0 ? fields : null;
		}
	}
}
