using Discord;
using NINA.DiscordNotification.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

public sealed class DiscordWebhookSession {
	private readonly DiscordWebhook _webhook;
	private readonly string _targetName;
	private readonly string _filter;
	private readonly string _text;
	private readonly IThreadChannel _thread;
	private readonly IEnumerable<EmbedFieldBuilder> _fields = null;
	private readonly RMSData _rmsData = null;

	internal DiscordWebhookSession(
		DiscordWebhook webhook,
		string targetName,
		string filter = null,
		string text = null,
		IThreadChannel thread = null,
		IEnumerable<EmbedFieldBuilder> fields = null,
		RMSData rmsData = null) {
		_webhook = webhook;
		_targetName = targetName;
		_filter = filter;
		_text = text;
		_thread = thread;
		_fields = fields;
		_rmsData = rmsData;
	}

	public DiscordWebhookSession WithFilter(string filter) {
		return new DiscordWebhookSession(
			_webhook,
			_targetName,
			filter,
			_text,
			_thread,
			_fields,
			_rmsData);
	}

	public DiscordWebhookSession WithText(string text) {
		return new DiscordWebhookSession(
			_webhook,
			_targetName,
			_filter,
			text,
			_thread,
			_fields,
			_rmsData);
	}

	public DiscordWebhookSession WithRMS(RMSData rmsData) {
		return new DiscordWebhookSession(
			_webhook,
			_targetName,
			_filter,
			_text,
			_thread,
			_fields,
			rmsData);
	}

	public DiscordWebhookSession WithFields(IEnumerable<EmbedFieldBuilder> fields) {
		return new DiscordWebhookSession(
			_webhook,
			_targetName,
			_filter,
			_text,
			_thread,
			fields,
			_rmsData);
	}

	public DiscordWebhookSession InThread(IThreadChannel thread) {
		return new DiscordWebhookSession(
			_webhook,
			_targetName,
			_filter,
			_text,
			thread,
			_fields,
			_rmsData);
	}

	public Task SendMessage() {
		return _webhook.SendAsync(
			_text,
			_fields,
			_targetName,
			_filter,
			_thread,
			_rmsData);
	}

	public Task SendFileMessage(string filePath) {
		return _webhook.SendFileAsync(
			filePath,
			_text,
			_fields,
			_targetName,
			_filter,
			_thread,
			_rmsData);
	}
}
