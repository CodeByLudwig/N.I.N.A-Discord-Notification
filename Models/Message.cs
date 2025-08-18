using Discord;
using NINA.Core.Utility;
using NINA.Core.Utility.Notification;
using NINA.DiscordNotification.Helpers;
using NINA.Equipment.Interfaces.Mediator;
using NINA.Image.FileFormat.FITS;
using NINA.Image.Interfaces;
using NINA.Profile.Interfaces;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace NINA.DiscordNotification.Models {
	public class Message {
		public string Text { get; set; }
		public ImageData ImageData { get; set; }
		public string TargetName { get; set; }
		public bool UseLiveStackedImage { get; set; }
		private string _filePath;
		private IEnumerable<KeyValuePair<string, object>> _extendedFields {
			get {
				return ImageData?.GetExtendedImageData();
			}
		}
		private readonly PrepareImageParameters _imageParameters = new(true, false);
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
				object image = null;
				var sendStopwatch = new Stopwatch();

				if (!UseLiveStackedImage) {
					_filePath = Path.Combine(path, $"image_{Guid.NewGuid()}.png");
					image = await _imageDataFactory.RenderImage(ImageData, _profileService.ActiveProfile.CameraSettings);
				} else {
					_filePath = Path.Combine(Path.GetDirectoryName(path), $"image_{Guid.NewGuid()}.png");
					image = await FITS.Load(new Uri(path), false, _imageDataFactory, CancellationToken.None);
				}

				if (image != null) {
					(await _imagingMediator.PrepareImage((IImageData)image, _imageParameters, CancellationToken.None)).EncodeImage(_filePath);
				}
				sendStopwatch.Start();
				await Send();
				sendStopwatch.Stop();

				if (_filePath != null && File.Exists(_filePath)) {
					File.Delete(_filePath);
				}
				Logger.Info($"Image send time={sendStopwatch.ElapsedMilliseconds}ms");
			} catch (Exception ex) {
				Notification.ShowWarning("Exception: " + ex);
				Logger.Error(ex);
			}
		}

		public async Task Send() {
			try {
				if (_filePath != null) {
					await GeneralHelpers.DiscordWebhook.SendFileMessage(_filePath, Text, _GetEmbedFields());
					return;
				}
				await GeneralHelpers.DiscordWebhook.SendMessage(Text, _GetEmbedFields());
			} catch (Exception ex) {
				Notification.ShowWarning("Exception: " + ex);
				Logger.Error(ex);
			}
		}

		private List<EmbedFieldBuilder> _GetEmbedFields() {
			var fields = new List<EmbedFieldBuilder>();

			if (!string.IsNullOrEmpty(TargetName)) {
				fields.Add(new EmbedFieldBuilder {
					Name = "Target",
					Value = TargetName
				});
			}

			if (_extendedFields != null) {
				foreach (var extendedField in _extendedFields) {
					if (extendedField.Value != null && !string.IsNullOrWhiteSpace(extendedField.Value.ToString())) {
						fields.Add(new EmbedFieldBuilder {
							Name = extendedField.Key,
							Value = extendedField.Value
						});
					}
				}
			}

			return fields.Count > 0 ? fields : null;
		}
	}
}
