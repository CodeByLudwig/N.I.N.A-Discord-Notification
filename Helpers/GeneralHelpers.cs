using Discord;
using Discord.WebSocket;
using NINA.Astrometry;
using NINA.Core.Utility;
using NINA.Core.Utility.Notification;
using NINA.DiscordNotification.Discord;
using NINA.Sequencer;
using NINA.Sequencer.Container;
using System;
using System.Threading.Tasks;
using System.Linq;

namespace NINA.DiscordNotification.Helpers {
	public static class GeneralHelpers {
		public static readonly string DatePattern = "$$DATE$$";
		public static readonly string DateMinus12Pattern = "$$DATEMINUS12$$";
		public static readonly string DateTimePattern = "$$DATETIME$$";
		public static readonly string TimePattern = "$$TIME$$";
		public static readonly string TargetPattern = "$$TARGET$$";
		public static readonly string FilterPattern = "$$FILTER$$";

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
			set {
				_discordSocket = value;
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

		public static string DefineThreadName(string targetName, string filter) {
			if (string.IsNullOrEmpty(Properties.Settings.Default.ThreadNameTemplate)) {
				return "";
			}
			DateTime now = DateTime.Now;
			string dateMinus12 = now.TimeOfDay >= TimeSpan.FromHours(12) ? now.ToLocalTime().AddHours(-12).ToString("yyyy-MM-dd") : now.ToLocalTime().AddDays(-1).ToString("yyyy-MM-dd");

			return Properties.Settings.Default.ThreadNameTemplate.Replace(TargetPattern, targetName).Replace(FilterPattern, filter).Replace(DateMinus12Pattern, dateMinus12).Replace(DatePattern, now.ToLocalTime().ToString("yyyy-MM-dd")).Replace(DateTimePattern, now.ToLocalTime().ToString("yyyy-MM-dd_HH-mm")).Replace(TimePattern, now.ToLocalTime().ToString("HH-mm"));
		}

		public static async Task<IThreadChannel> InitDiscordSocket(string targetName, string id) {
			var couldParseArchiveDuration = Enum.TryParse<ThreadArchiveDuration>(Properties.Settings.Default.ArchiveDuration, out var archiveDuration);

			if (!couldParseArchiveDuration) {
				Notification.ShowWarning("Could not parse archive duration");
				Logger.Info("Could not parse archive duration");
				return null;
			}

			if (string.IsNullOrEmpty(targetName)) {
				Notification.ShowError("Could not create thread: no target set");
				Logger.Error("Could not create thread: no target set");
				return null;
			}

			ulong channelId = Convert.ToUInt64(id);
			var channel = _discordSocket.GetChannel(channelId) as ITextChannel;

			if (channel != null) {
				var activeThreads = await channel.GetActiveThreadsAsync();
				var thread = activeThreads.FirstOrDefault(t => t.Name == targetName);

				if (thread == null) {
					thread = await channel.CreateThreadAsync(
						name: targetName,
						autoArchiveDuration: archiveDuration,
						invitable: false,
						type: ThreadType.PublicThread
					);
				}

				return thread;
			} else {
				Notification.ShowError($"Channel not found! -> channelId: {channelId}");
				Logger.Error($"Channel not found! -> channelId: {channelId}");
			}

			return null;
		}
	}
}