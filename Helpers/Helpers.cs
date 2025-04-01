using NINA.DiscordNotification.Discord;
using NINA.Astrometry;
using NINA.Sequencer;
using NINA.Sequencer.Container;

namespace NINA.DiscordNotification.Helpers {
	public static class Helpers {
		private static IDiscordWebhook _discordWebhook;
		public static IDiscordWebhook DiscordWebhook {
			get {
				if (_discordWebhook == null) {
					_discordWebhook = new DiscordWebhook(Properties.Settings.Default.DiscordWebhookUrl);
				}
				return _discordWebhook;
			}
		}

		public static InputTarget GetSequenceTarget(this ISequenceContainer container) {
			if (container?.Parent == null) {
				return null;
			}

			return container is IDeepSkyObjectContainer targetContainer ? targetContainer.Target : container.Parent.GetSequenceTarget();
		}

		public static InputTarget GetSequenceTarget(this ISequenceEntity entity) {
			return entity.Parent == null ? null : entity.Parent.GetSequenceTarget();
		}
	}
}
