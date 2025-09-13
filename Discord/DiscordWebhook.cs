
using System.Threading.Tasks;
using Discord.Webhook;
using System;
using NINA.Core.Utility;
using Discord;
using System.Collections.Generic;
using Discord.Net;
using System.IO;

namespace NINA.DiscordNotification.Discord {

	public class DiscordWebhook : IDiscordWebhook {

		private DiscordWebhookClient _discordWebhookClient;

		public DiscordWebhook(string webhookUrl) {
			try {
				_discordWebhookClient = new DiscordWebhookClient(webhookUrl);
			} catch (Exception ex) {
				Logger.Error($"Could not create discord webhook client", ex);
			}
		}

		public async Task SendMessage(string text = null, IEnumerable<EmbedFieldBuilder> fields = null, IThreadChannel thread = null) {
			await _SendMessage(text, fields, thread);
		}

		public async Task SendFileMessage(string filePath, string text = null, IEnumerable<EmbedFieldBuilder> fields = null, IThreadChannel thread = null) {
			await _SendFile(filePath, text, fields, thread);
		}

		private async Task _SendMessage(string text, IEnumerable<EmbedFieldBuilder> fields, IThreadChannel thread = null) {
			try {
				if (thread != null) {
					await thread.SendMessageAsync(fields == null ? text : null, embeds: fields != null ? [_BuildEmbeds(text, fields).Build()] : null);
				} else {
					await _discordWebhookClient.SendMessageAsync(fields == null ? text : null, embeds: fields != null ? [_BuildEmbeds(text, fields).Build()] : null);
				}
			} catch (Exception ex) {
				Logger.Error($"Could not send message", ex);
			}
		}

		private async Task _SendFile(string filePath, string text, IEnumerable<EmbedFieldBuilder> fields, IThreadChannel thread = null) {
			try {
				if (thread != null) {
					await thread.SendFileAsync(filePath, fields == null ? text : null, embeds: fields != null ? [_BuildEmbeds(text, fields).Build()] : null);
				} else {
					await _discordWebhookClient.SendFileAsync(filePath, fields == null ? text : null, embeds: fields != null ? [_BuildEmbeds(text, fields).Build()] : null);
				}
			} catch (HttpException ex) {
				if (ex.DiscordCode == DiscordErrorCode.RequestEntityTooLarge) {
					var fileSize = new FileInfo(filePath).Length / (1024.0 * 1024.0);
					var errorField = new List<EmbedFieldBuilder>();
					errorField.AddRange(fields);
					errorField.Add(
						new EmbedFieldBuilder {
							Name = "Image",
							Value = $"The file is too large ({Math.Round(fileSize, 2)} MB)"
						});
					if (thread != null) {
						await thread.SendMessageAsync(fields == null ? text : null, embeds: errorField != null ? [_BuildEmbeds(text, errorField).Build()] : null);
					} else {
						await _discordWebhookClient.SendMessageAsync(fields == null ? text : null, embeds: errorField != null ? [_BuildEmbeds(text, errorField).Build()] : null);
					}
				}
				Logger.Error($"Could not send file message", ex);
			} catch (Exception ex) {
				Logger.Error($"Could not send file message", ex);
			}
		}

		private EmbedBuilder _BuildEmbeds(string text, IEnumerable<EmbedFieldBuilder> fields) {
			var embed = new EmbedBuilder {
				Timestamp = DateTime.UtcNow,
				Color = Color.Blue,
				Description = text
			};

			if (fields != null) {
				foreach (var field in fields) {
					embed.AddField(field);
				}
			}

			return embed;
		}
	}
}
