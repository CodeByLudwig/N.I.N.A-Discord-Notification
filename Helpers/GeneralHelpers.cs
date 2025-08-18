using NINA.Astrometry;
using NINA.DiscordNotification.Discord;
using NINA.Sequencer;
using NINA.Sequencer.Container;

namespace NINA.DiscordNotification.Helpers {
	public static class GeneralHelpers {
		public static IDiscordWebhook DiscordWebhook {
			get {
				return new DiscordWebhook(Properties.Settings.Default.DiscordWebhookUrl);
			}
		}

		public static InputTarget GetSequenceTarget(this ISequenceContainer container) {
			if (container?.Parent == null) {
				return null;
			}
			return container is IDeepSkyObjectContainer targetContainer ? targetContainer.Target : container.Parent.GetSequenceTarget();
		}

		public static InputTarget GetSequenceTarget(this ISequenceEntity entity) {
			return entity.Parent?.GetSequenceTarget();
		}
	}
}