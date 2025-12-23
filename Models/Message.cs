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

		private readonly Dictionary<string, object> _extendedFields = new();
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
			if (string.IsNullOrWhiteSpace(key)) {
				Logger.Warning("Attempted to add extended field with null or whitespace key.");
				return;
			}

			_extendedFields[key] = value;
		}

		public async Task Send(bool isThread, string path) {
			try {
				Logger.Info("Starting send process...");

				object image = null;
				var sendStopwatch = Stopwatch.StartNew();

				if (!UseLiveStackedImage) {
					_filePath = Path.Combine(path, $"image_{Guid.NewGuid()}.jpeg");
					image = await _imageDataFactory.RenderImage(ImageData, _profileService.ActiveProfile.CameraSettings);
				} else {
					var directory = Path.GetDirectoryName(path) ?? path;
					_filePath = Path.Combine(directory, $"image_{Guid.NewGuid()}.jpeg");

					if (path.CheckExtensions(new[] { ".fits" })) {
						image = await FITS.Load(new Uri(path), false, _imageDataFactory, CancellationToken.None);
					}
				}

				if (UseLiveStackedImage && path.CheckExtensions(new[] { ".png", ".jpeg", ".jpg" })) {
					path.EncodeImage(_filePath);
				} else if (image is IImageData imageData) {
					var preparedImage = await _imagingMediator.PrepareImage(imageData, _imageParameters, CancellationToken.None);
					preparedImage.EncodeImage(_filePath);
				}

				await Send(isThread);

				sendStopwatch.Stop();

				if (!string.IsNullOrEmpty(_filePath) && File.Exists(_filePath)) {
					try {
						File.Delete(_filePath);
					} catch (Exception ex) {
						Logger.Warning($"Could not delete temp file '{_filePath}': {ex.Message}");
					}
				}

				Logger.Info($"Image send completed in {sendStopwatch.ElapsedMilliseconds} ms.");
			} catch (Exception ex) {
				Notification.ShowWarning("Exception during send: " + ex.Message);
				Logger.Error("Exception during send:", ex);
			}
		}

		public async Task<bool> Send(bool isThread) {
			try {
				var session = GeneralHelpers.DiscordWebhook.ForTarget(TargetName).WithText(Text).WithFilter(Filter);

				if (ImageData != null) {
					session = session.WithRMS(ImageData.GetRMS());
				}

				if (isThread) {
					var thread = await GetThreadAsync();
					session = session.InThread(thread);
				}

				var fields = _GetEmbedFields();
				Notification.ShowError($"Embed fields count: {Properties.Settings.Default.SendEmbeds}");
				if (fields.Any() && Properties.Settings.Default.SendEmbeds) {
					session = session.WithFields(fields);
				}

				if (!string.IsNullOrEmpty(_filePath) && File.Exists(_filePath)) {
					await session.SendFileMessage(_filePath);
				} else {
					await session.SendMessage();
				}
			} catch (Exception ex) {
				Notification.ShowWarning("Exception during Discord send: " + ex.Message);
				Logger.Error("Exception during Discord send:", ex);
				return false;
			}

			return true;
		}

		private async Task<IThreadChannel> GetThreadAsync() {
			if (GeneralHelpers.DiscordSocket.ConnectionState == ConnectionState.Connected) {
				return await GeneralHelpers.InitDiscordSocket(GeneralHelpers.DefineThreadName(TargetName, Filter), Properties.Settings.Default.DiscordChannelId);
			}

			var tcs = new TaskCompletionSource<IThreadChannel>();

			Task Handler() {
				GeneralHelpers.DiscordSocket.Ready -= Handler;

				_ = Task.Run(async () => {
					try {
						var thread = await GeneralHelpers.InitDiscordSocket(GeneralHelpers.DefineThreadName(TargetName, Filter), Properties.Settings.Default.DiscordChannelId);
						tcs.SetResult(thread);
					} catch (Exception ex) {
						tcs.SetException(ex);
					}
				});

				return Task.CompletedTask;
			}

			GeneralHelpers.DiscordSocket.Ready += Handler;

			return await tcs.Task;
		}

		private List<EmbedFieldBuilder> _GetEmbedFields() {
			var fields = new List<EmbedFieldBuilder>();
			var addedKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

			if (!string.IsNullOrWhiteSpace(TargetName)) {
				fields.Add(new EmbedFieldBuilder {
					Name = "Target",
					Value = TargetName
				});
				addedKeys.Add("Target");
			}

			if (ExtendedFields != null) {
				foreach (var extendedField in ExtendedFields) {
					if (extendedField.Value != null && !string.IsNullOrWhiteSpace(extendedField.Value.ToString())) {
						if (!addedKeys.Contains(extendedField.Key)) {
							fields.Add(new EmbedFieldBuilder {
								Name = extendedField.Key,
								Value = extendedField.Value
							});
							addedKeys.Add(extendedField.Key);
						} else {
							Logger.Warning($"Skipped duplicate embed field key: {extendedField.Key}");
						}
					}
				}
			}

			return fields;
		}
	}
}
