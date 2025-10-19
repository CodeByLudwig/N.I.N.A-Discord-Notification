using System.ComponentModel.Composition;
using NINA.DiscordNotification.Models;
using System.Windows;
using System;
using NINA.Core.Utility.Notification;
using NINA.DiscordNotification.Helpers;
using Discord.WebSocket;
using Discord;
using Discord.Net;
using System.Threading.Tasks;

namespace NINA.DiscordNotification {
	[Export(typeof(ResourceDictionary))]
	partial class Options : ResourceDictionary {

		public Options() {
			InitializeComponent();
		}

		private async void TestDiscordWebhook(object sender, RoutedEventArgs e) {
			try {
				var success = await new Message(null, null, null) {
					Text = "This is a message for testing",
					TargetName = "Test",
					Filter = null
				}.Send(false);

				if (success) {
					Notification.ShowSuccess("Message successfully sent");
				}
			} catch (Exception ex) {
				Notification.ShowError(ex.Message);
			}
		}

		private async void TestDiscordToken(object sender, RoutedEventArgs e) {
			bool logErrorShown = false;

			try {
				if (GeneralHelpers.DiscordSocket != null) {
					await GeneralHelpers.DiscordSocket.LogoutAsync();
					await GeneralHelpers.DiscordSocket.StopAsync();
					GeneralHelpers.DiscordSocket.Dispose();
					GeneralHelpers.DiscordSocket = new DiscordSocketClient();
				}

				GeneralHelpers.DiscordSocket.Ready += async () => {
					try {
						var result = await GeneralHelpers.InitDiscordSocket(GeneralHelpers.DefineThreadName(GeneralHelpers.TimePattern, null), Properties.Settings.Default.DiscordChannelId);
						if (result != null) {
							Notification.ShowSuccess("Thread successfully created");
						}
					} catch (Exception ex) {
						Notification.ShowError(ex.Message);
					}
				};

				GeneralHelpers.DiscordSocket.Log += (logMessage) => {
					if (logMessage.Exception != null && !logErrorShown && !logMessage.Exception.Message.Contains("A task was canceled")) {
						logErrorShown = true;
						Notification.ShowError(logMessage.Exception.Message);
					}

					return Task.CompletedTask;
				};

				await GeneralHelpers.DiscordSocket.LoginAsync(TokenType.Bot, Properties.Settings.Default.DiscordBotToken);
				await GeneralHelpers.DiscordSocket.StartAsync();
			} catch (HttpException ex) {
				Notification.ShowError(ex.Message);
			} catch (Exception ex) {
				Notification.ShowError(ex.Message);
			}
		}
	}
}
