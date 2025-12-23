using Discord;
using NINA.DiscordNotification.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace NINA.DiscordNotification.Discord {
	public interface IDiscordWebhook {
		DiscordWebhookSession ForTarget(string targetName);

		Task SendAsync(
			string text,
			IEnumerable<EmbedFieldBuilder> fields,
			string targetName,
			string filter,
			IThreadChannel thread,
			RMSData rmsData);

		Task SendFileAsync(string filePath, string text, IEnumerable<EmbedFieldBuilder> fields, string targetName, string filter, IThreadChannel thread, RMSData rmsData);
	}
}
