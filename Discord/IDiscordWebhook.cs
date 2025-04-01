using Discord;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace NINA.DiscordNotification.Discord {
	public interface IDiscordWebhook {
		Task SendMessage(string text = null, IEnumerable<EmbedFieldBuilder> fields = null);
		Task SendFileMessage(string filePath, string text = null, IEnumerable<EmbedFieldBuilder> fields = null);
	}
}
