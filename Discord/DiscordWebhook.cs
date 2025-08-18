
using System.Threading.Tasks;
using Discord.Webhook;
using System;
using NINA.Core.Utility;
using Discord;
using System.Collections.Generic;

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

		public async Task SendMessage(string text = null, IEnumerable<EmbedFieldBuilder> fields = null) {
			try {
				await _discordWebhookClient.SendMessageAsync(fields == null ? text : null, embeds: fields != null ? [_BuildEmbeds(text, fields).Build()] : null);
			} catch (Exception ex) {
				Logger.Error($"Could not send message", ex);
			}
		}

		public async Task SendFileMessage(string filePath, string text = null, IEnumerable<EmbedFieldBuilder> fields = null) {
			try {
				await _discordWebhookClient.SendFileAsync(filePath, fields == null ? text : null, embeds: fields != null ? [_BuildEmbeds(text, fields).Build()] : null);
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

			foreach (var field in fields) {
				embed.AddField(field);
			}

			return embed;
		}
	}
}
