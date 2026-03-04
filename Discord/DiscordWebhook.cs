using Discord;
using Discord.Net;
using Discord.Webhook;
using NINA.Core.Utility;
using NINA.Core.Utility.Notification;
using NINA.DiscordNotification.Discord;
using NINA.DiscordNotification.Helpers;
using NINA.DiscordNotification.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

public class DiscordWebhook : IDiscordWebhook {
	private readonly DiscordWebhookClient _client;

	public DiscordWebhook(string webhookUrl) {
		if (string.IsNullOrWhiteSpace(webhookUrl)) {
			Logger.Warning("Discord webhook URL is null or empty.");
			return;
		}

		_client = new DiscordWebhookClient(webhookUrl);
	}

	public async Task SendAsync(
		string text,
		IEnumerable<EmbedFieldBuilder> fields,
		string targetName,
		string filter,
		IThreadChannel thread,
		RMSData rmsData) {
		text = GeneralHelpers.ReplacePatterns(text, targetName, filter, rmsData);

		if (thread != null) {
			await thread.SendMessageAsync(text, embeds: fields != null
			? new[] { _BuildEmbed(text, fields).Build() } : null);
		} else if (_client != null) {
			await _client.SendMessageAsync(text, embeds: fields != null
			? new[] { _BuildEmbed(text, fields).Build() } : null);
		}
	}

	public async Task SendFileAsync(string filePath, string text, IEnumerable<EmbedFieldBuilder> fields, string targetName, string filter, IThreadChannel thread, RMSData rmsData) {
		text = GeneralHelpers.ReplacePatterns(text, targetName, filter, rmsData);

		try {
			Logger.Info($"Attempting to send Discord file message. File: {filePath}");

			if (thread != null) {
				await thread.SendFileAsync(
					filePath,
					text: text,
					embeds: fields != null
		? new[] { _BuildEmbed(text, fields).Build() } : null);
			} else if (_client != null) {
				await _client.SendFileAsync(
					filePath,
					text: text,
					embeds: fields != null
		? new[] { _BuildEmbed(text, fields).Build() } : null);
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
					text: text,
					embeds: new[] { _BuildEmbed(text, errorFields).Build() });
			} else if (_client != null) {
				await _client.SendMessageAsync(
					text: text,
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

	public DiscordWebhookSession ForTarget(string targetName) {
		return new DiscordWebhookSession(this, targetName);
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
