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
using NINA.DiscordNotification.Models;

namespace NINA.DiscordNotification.Helpers {
	public static class GeneralHelpers {
		public static readonly string DatePattern = "$$DATE$$";
		public static readonly string DateMinus12Pattern = "$$DATEMINUS12$$";
		public static readonly string DateTimePattern = "$$DATETIME$$";
		public static readonly string TimePattern = "$$TIME$$";
		public static readonly string TargetPattern = "$$TARGET$$";
		public static readonly string FilterPattern = "$$FILTER$$";
		public static readonly string RmsPattern = "$$RMSTOTAL$$";
		public static readonly string RmsDecPattern = "$$RMSDEC$$";
		public static readonly string RmsRaPattern = "$$RMSRA$$";

		private static readonly Lazy<IDiscordWebhook> _discordWebhook =
		new(() => new DiscordWebhook(Properties.Settings.Default.DiscordWebhookUrl));
		public static IDiscordWebhook DiscordWebhook => _discordWebhook.Value;

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

		public static async Task EnsureSocketStartedAsync() {
			try {
				await DiscordSocket.LoginAsync(TokenType.Bot, Properties.Settings.Default.DiscordBotToken);
				await DiscordSocket.StartAsync();
			} catch (Exception ex) {
				Notification.ShowError(ex.Message);
				Logger.Error($"Discord login failed: {ex.Message}");
			}
		}

		public static async Task StopSocketAsync() {
			if (DiscordSocket != null) {
				await DiscordSocket.LogoutAsync();
				await DiscordSocket.StopAsync();
				DiscordSocket.Dispose();
				DiscordSocket = new DiscordSocketClient();
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
			DateTime now = DateTime.Now;
			var today = now.ToLocalTime().ToString("yyyy-MM-dd");
			if (string.IsNullOrEmpty(Properties.Settings.Default.ThreadNameTemplate)) {
				return today;
			}

			var formatttedThreadName = ReplacePatterns(Properties.Settings.Default.ThreadNameTemplate, targetName, filter);
			return string.IsNullOrEmpty(formatttedThreadName) ? today : formatttedThreadName;
		}

		public static string ReplacePatterns(string text, string targetName, string filter, RMSData rmsData = null) {
			if (string.IsNullOrEmpty(text)) {
				return null;
			}

			DateTime now = DateTime.Now;

			string dateMinus12 = now.TimeOfDay >= TimeSpan.FromHours(12) ? now.ToLocalTime().AddHours(-12).ToString("yyyy-MM-dd") : now.ToLocalTime().AddDays(-1).ToString("yyyy-MM-dd");
			var formattedText = text.Replace(TargetPattern, targetName).Replace(FilterPattern, filter).Replace(DateMinus12Pattern, dateMinus12).Replace(DatePattern, now.ToLocalTime().ToString("yyyy-MM-dd")).Replace(DateTimePattern, now.ToLocalTime().ToString("yyyy-MM-dd_HH-mm")).Replace(TimePattern, now.ToLocalTime().ToString("HH-mm"));

			return rmsData != null
				? formattedText.Replace(RmsPattern, rmsData.TotalText).Replace(RmsDecPattern, rmsData.DecText).Replace(RmsRaPattern, rmsData.RAText)
				: formattedText;
		}

		public static async Task<IThreadChannel> InitDiscordSocket(string threadName, string id) {
			var couldParseArchiveDuration = Enum.TryParse<ThreadArchiveDuration>(Properties.Settings.Default.ArchiveDuration, out var archiveDuration);

			if (!couldParseArchiveDuration) {
				Notification.ShowWarning("Could not parse archive duration");
				Logger.Info("Could not parse archive duration");
				return null;
			}

			if (string.IsNullOrEmpty(threadName)) {
				Notification.ShowError("Could not create thread: no target set");
				Logger.Error("Could not create thread: no target set");
				return null;
			}

			ulong channelId = Convert.ToUInt64(id);
			var channel = DiscordSocket.GetChannel(channelId) as ITextChannel;

			if (channel != null) {
				var activeThreads = await channel.GetActiveThreadsAsync();
				var thread = activeThreads.FirstOrDefault(t => t.Name == threadName);

				if (thread == null) {
					thread = await channel.CreateThreadAsync(
						name: threadName,
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