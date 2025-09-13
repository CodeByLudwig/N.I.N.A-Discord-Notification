using Discord.WebSocket;
using NINA.Astrometry;
using NINA.Core.Utility;
using NINA.Core.Utility.Notification;
using NINA.DiscordNotification.Discord;
using NINA.Sequencer;
using NINA.Sequencer.Container;
using System;

namespace NINA.DiscordNotification.Helpers {
	public static class GeneralHelpers {
		public static IDiscordWebhook DiscordWebhook {
			get {
				return new DiscordWebhook(Properties.Settings.Default.DiscordWebhookUrl);
			}
		}

		private static DiscordSocketClient _discordSocket;
		public static DiscordSocketClient DiscordSocket {
			get {
				if (_discordSocket == null) {
					try {
						_discordSocket = new DiscordSocketClient();
						return _discordSocket;
					} catch (Exception ex) {
						Notification.ShowError(ex.Message);
						Logger.Error($"Could not create DiscordSocketClient -> Message:{ex.Message}");
					}
				}

				return _discordSocket;
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