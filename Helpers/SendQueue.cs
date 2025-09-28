using NINA.Core.Utility;
using System;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

public class SendQueue : IDisposable {
	private readonly Channel<Func<Task>> _sendChannel;
	private readonly Task _worker;
	private readonly CancellationTokenSource _cts = new CancellationTokenSource();

	public SendQueue() {
		_sendChannel = Channel.CreateUnbounded<Func<Task>>(new UnboundedChannelOptions {
			SingleReader = true,
			SingleWriter = false
		});

		_worker = Task.Run(() => ProcessQueueAsync(_cts.Token));
	}

	public bool EnqueueSend(Func<Task> sendFunc) {
		return _sendChannel.Writer.TryWrite(sendFunc);
	}

	private async Task ProcessQueueAsync(CancellationToken cancellationToken) {
		try {
			await foreach (var sendFunc in _sendChannel.Reader.ReadAllAsync(cancellationToken)) {
				try {
					await sendFunc();
				} catch (Exception ex) {
					Logger.Error($"Error while sending: {ex.Message}");
				}
			}
		} catch (OperationCanceledException) {
			Logger.Error("SendQueue has been canceled");
		}
	}

	public async Task ShutdownAsync() {
		_sendChannel.Writer.Complete();

		try {
			await _worker;
		} catch{
			Logger.Error("Error while shutting down SendQueue");
		}
	}

	public void Dispose() {
		_cts.Cancel();
		_cts.Dispose();
	}
}
