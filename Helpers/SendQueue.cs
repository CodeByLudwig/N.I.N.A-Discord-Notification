using NINA.Core.Utility;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

public class SendCommands {
	public readonly object SendListLock = new();
	public readonly object SendLiveStackListLock = new object();
	public readonly Queue<SendCommand> PendingSendCommands = new();
	public readonly Queue<LiveStackSendCommands> LiveStackCommands = new();

	public void AddSendCommand(SendCommand cmd) {
		lock (SendListLock) {
			PendingSendCommands.Enqueue(cmd);
		}
	}

	public void AddLiveStackCommands(LiveStackSendCommands cmd) {
		lock (SendLiveStackListLock) {
			LiveStackCommands.Enqueue(cmd);
		}
	}
}

public class LiveStackSendCommands {
	public readonly object SendPendingLiveStackListLock = new object();
	public readonly Queue<LiveStackSendCommand> PendingLiveStackSendCommands = new();

	public void AddLiveStackSendCommand(LiveStackSendCommand cmd) {
		lock (SendPendingLiveStackListLock) {
			PendingLiveStackSendCommands.Enqueue(cmd);
		}
	}
}

public class SendCommand {
	public Func<Task> SendFunc { get; set; }
}

public class LiveStackSendCommand {
	public Func<Task<FileInfo>> SendFunc { get; set; }
	public string Filter { get; init; }
}

public class SendQueue : IDisposable {
	private readonly Channel<Func<Task>> _sendChannel;
	private readonly Task _worker;
	private readonly CancellationTokenSource _cts = new CancellationTokenSource();
	private int _queueCount = 0;

	public SendQueue() {
		_sendChannel = Channel.CreateUnbounded<Func<Task>>(new UnboundedChannelOptions {
			SingleReader = true,
			SingleWriter = false
		});

		_worker = Task.Run(() => ProcessQueueAsync(_cts.Token));
	}

	public bool EnqueueSend(Func<Task> sendFunc) {
		bool written = _sendChannel.Writer.TryWrite(sendFunc);
		if (written) {
			Interlocked.Increment(ref _queueCount);
		}

		return written;
	}

	private async Task ProcessQueueAsync(CancellationToken cancellationToken) {
		try {
			await foreach (var sendFunc in _sendChannel.Reader.ReadAllAsync(cancellationToken)) {
				try {
					await sendFunc();
				} catch (Exception ex) {
					Logger.Error($"Error while sending: {ex.Message}");
				} finally {
					Interlocked.Decrement(ref _queueCount);
				}
			}
		} catch (OperationCanceledException) {
			Logger.Error("SendQueue has been canceled");
		}
	}

	public bool HasPendingItems() {
		return Volatile.Read(ref _queueCount) > 0;
	}

	public async Task ShutdownAsync() {
		_sendChannel.Writer.Complete();

		try {
			await _worker;
		} catch {
			Logger.Error("Error while shutting down SendQueue");
		}
	}

	public void Dispose() {
		_cts.Cancel();
		_cts.Dispose();
	}
}
