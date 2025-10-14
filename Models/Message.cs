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
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace NINA.DiscordNotification.Models {
	public class Message {
		public string Text { get; set; }
		public ImageData ImageData { get; set; }
		public string TargetName { get; set; }
		public bool UseLiveStackedImage { get; set; }
		public string Filter { get; set; }

		private string _filePath;

		private Dictionary<string, object> _extendedFields = new();
		private Dictionary<string, object> ExtendedFields {
			get {
				var baseFields = ImageData?.GetExtendedImageData() ?? new Dictionary<string, object>();
				return baseFields.Concat(_extendedFields)
								 .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
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

		public void AddExtendedField(string key, object value) {
			_extendedFields.Add(key, value);
		}

		public async Task Send(bool isThread, string path) {
			try {
				object image = null;
				var sendStopwatch = new Stopwatch();

				if (!UseLiveStackedImage) {
					_filePath = Path.Combine(path, $"image_{Guid.NewGuid()}.jpeg");
					image = await _imageDataFactory.RenderImage(ImageData, _profileService.ActiveProfile.CameraSettings);
				} else {
					_filePath = Path.Combine(Path.GetDirectoryName(path), $"image_{Guid.NewGuid()}.jpeg");

					if (path.CheckExtensions([".fits"])) {
						image = await FITS.Load(new Uri(path), false, _imageDataFactory, CancellationToken.None);
					}
				}

				if (UseLiveStackedImage && path.CheckExtensions([".png", ".jpeg", ".jpeg"])) {
					path.EncodeImage(_filePath);
				} else if (image != null) {
					(await _imagingMediator.PrepareImage((IImageData)image, _imageParameters, CancellationToken.None)).EncodeImage(_filePath);
				}

				sendStopwatch.Start();
				await Send(isThread);
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

		public async Task Send(bool isThread) {
			async Task ExecuteSend(bool isThread) {
				IThreadChannel thread = isThread ? await GetThread() : null;

				if (_filePath != null) {
					await GeneralHelpers.DiscordWebhook.SendFileMessage(_filePath, Text, _GetEmbedFields(), thread);
					return;
				}
				await GeneralHelpers.DiscordWebhook.SendMessage(Text, _GetEmbedFields(), thread);
			}

			async Task<IThreadChannel> GetThread() {
				if (GeneralHelpers.DiscordSocket.ConnectionState == ConnectionState.Connected) {
					return await GeneralHelpers.InitDiscordSocket(GeneralHelpers.DefineThreadName(TargetName, Filter), Properties.Settings.Default.DiscordChannelId);
				}

				var taskCompletion = new TaskCompletionSource<IThreadChannel>();

				Task Handler() {
					GeneralHelpers.DiscordSocket.Ready -= Handler;

					_ = Task.Run(async () => {
						var thread = await GeneralHelpers.InitDiscordSocket(GeneralHelpers.DefineThreadName(TargetName, Filter), Properties.Settings.Default.DiscordChannelId);

						taskCompletion.SetResult(thread);
					});

					return Task.CompletedTask;
				}

				GeneralHelpers.DiscordSocket.Ready += Handler;

				return await taskCompletion.Task;
			}

			try {
				await ExecuteSend(isThread);
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

			if (ExtendedFields != null) {
				foreach (var extendedField in ExtendedFields) {
					if (extendedField.Value != null && !string.IsNullOrWhiteSpace(extendedField.Value.ToString())) {
						fields.Add(new EmbedFieldBuilder {
							Name = extendedField.Key,
							Value = extendedField.Value
						});
					}
				}
			}

			return fields;
		}
	}
}
