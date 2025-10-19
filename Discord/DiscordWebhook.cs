using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Discord;
using Discord.Net;
using Discord.Webhook;
using NINA.Core.Utility;
using NINA.Core.Utility.Notification;

namespace NINA.DiscordNotification.Discord {
	public class DiscordWebhook : IDiscordWebhook {
		private readonly DiscordWebhookClient _discordWebhookClient;

		public DiscordWebhook(string webhookUrl) {
			try {
				if (string.IsNullOrWhiteSpace(webhookUrl)) {
					Logger.Warning("Discord webhook URL is null or empty. Discord webhook client will not be created.");
					return;
				}

				_discordWebhookClient = new DiscordWebhookClient(webhookUrl);
			} catch (HttpException ex) {
				Logger.Error("Could not create discord webhook client due to HttpException.", ex);
				throw new Exception("Failed to create Discord webhook client: " + ex.Message, ex);
			} catch (Exception ex) {
				Logger.Error("Could not create discord webhook client due to unexpected exception.", ex);
				throw new Exception("Failed to create Discord webhook client: " + ex.Message, ex);
			}
		}

		public async Task SendMessage(string text = null, IEnumerable<EmbedFieldBuilder> fields = null, IThreadChannel thread = null) {
			await _SendMessageAsync(text, fields, thread);
		}

		public async Task SendFileMessage(string filePath, string text = null, IEnumerable<EmbedFieldBuilder> fields = null, IThreadChannel thread = null) {
			await _SendFileAsync(filePath, text, fields, thread);
		}

		private async Task _SendMessageAsync(string text, IEnumerable<EmbedFieldBuilder> fields, IThreadChannel thread = null) {
			try {
				Logger.Info("Attempting to send Discord message...");

				if (thread != null) {
					await thread.SendMessageAsync(
						text: fields == null ? text : null,
						embeds: fields != null ? new[] { _BuildEmbed(text, fields).Build() } : null);
				} else if (_discordWebhookClient != null) {
					await _discordWebhookClient.SendMessageAsync(
						text: fields == null ? text : null,
						embeds: fields != null ? new[] { _BuildEmbed(text, fields).Build() } : null);
				} else {
					Logger.Warning("DiscordWebhookClient and Thread are not set null. Message not sent.");
					return;
				}

				Logger.Info("Discord message sent successfully.");
			} catch (HttpException ex) {
				Notification.ShowError($"Discord API Error: {ex.Message}");
				Logger.Error("Failed to send Discord message due to HttpException.", ex);
			} catch (Exception ex) {
				Notification.ShowError($"Error sending Discord message: {ex.Message}");
				Logger.Error("Failed to send Discord message due to unexpected exception.", ex);
			}
		}

		private async Task _SendFileAsync(string filePath, string text, IEnumerable<EmbedFieldBuilder> fields, IThreadChannel thread = null) {
			try {
				Logger.Info($"Attempting to send Discord file message. File: {filePath}");

				if (thread != null) {
					await thread.SendFileAsync(
						filePath,
						text: fields == null ? text : null,
						embeds: fields != null ? new[] { _BuildEmbed(text, fields).Build() } : null);
				} else if (_discordWebhookClient != null) {
					await _discordWebhookClient.SendFileAsync(
						filePath,
						text: fields == null ? text : null,
						embeds: fields != null ? new[] { _BuildEmbed(text, fields).Build() } : null);
				} else {
					Logger.Warning("DiscordWebhookClient is null. File message not sent.");
					return;
				}

				Logger.Info("Discord file message sent successfully.");
			} catch (HttpException ex) when (ex.DiscordCode == DiscordErrorCode.RequestEntityTooLarge) {
				Logger.Warning("Discord file too large to send.");
				var fileSizeMB = new FileInfo(filePath).Length / (1024.0 * 1024.0);

				var errorFields = new List<EmbedFieldBuilder>(fields ?? Array.Empty<EmbedFieldBuilder>())
				{
					new EmbedFieldBuilder
					{
						Name = "Image",
						Value = $"The file is too large ({Math.Round(fileSizeMB, 2)} MB). Please reduce the size."
					}
				};

				if (thread != null) {
					await thread.SendMessageAsync(
						text: fields == null ? text : null,
						embeds: new[] { _BuildEmbed(text, errorFields).Build() });
				} else if (_discordWebhookClient != null) {
					await _discordWebhookClient.SendMessageAsync(
						text: fields == null ? text : null,
						embeds: new[] { _BuildEmbed(text, errorFields).Build() });
				}
			} catch (HttpException ex) {
				Notification.ShowError($"Discord API Error: {ex.Message}");
				Logger.Error("Failed to send Discord file message due to HttpException.", ex);
			} catch (Exception ex) {
				Notification.ShowError($"Error sending Discord file message: {ex.Message}");
				Logger.Error("Failed to send Discord file message due to unexpected exception.", ex);
			}
		}

		private EmbedBuilder _BuildEmbed(string text, IEnumerable<EmbedFieldBuilder> fields) {
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
