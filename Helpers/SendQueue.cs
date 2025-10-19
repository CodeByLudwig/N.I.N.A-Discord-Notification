using NINA.Core.Utility;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

public static class SendCommands {
	public static readonly object SendListLock = new();
	public static readonly object SendLiveStackListLock = new();
	public static readonly Queue<SendCommand> PendingSendCommands = new();
	public static readonly Queue<LiveStackSendCommands> LiveStackCommands = new();

	public static void AddSendCommand(SendCommand cmd) {
		lock (SendListLock) {
			PendingSendCommands.Enqueue(cmd);
		}
	}

	public static void AddLiveStackCommands(LiveStackSendCommands cmd) {
		lock (SendLiveStackListLock) {
			LiveStackCommands.Enqueue(cmd);
		}
	}

	public static void ClearAll() {
		lock (SendListLock) {
			PendingSendCommands.Clear();
		}
		lock (SendLiveStackListLock) {
			LiveStackCommands.Clear();
		}
	}
}

public class LiveStackSendCommands {
	public readonly object SendPendingLiveStackListLock = new();
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

public static class SendQueue {
	private static Channel<Func<Task>> _sendChannel;
	private static Task _worker;
	private static CancellationTokenSource _cts;
	private static int _queueCount = 0;
	private static readonly object _lock = new();

	static SendQueue() {
		InitializeChannelAndWorker();
	}

	private static void InitializeChannelAndWorker() {
		_cts = new CancellationTokenSource();
		_sendChannel = Channel.CreateUnbounded<Func<Task>>(new UnboundedChannelOptions {
			SingleReader = true,
			SingleWriter = false
		});
		_worker = Task.Run(() => ProcessQueueAsync(_cts.Token));
		_queueCount = 0;
	}

	public static bool EnqueueSend(Func<Task> sendFunc) {
		lock (_lock) {
			if (_cts == null || _cts.IsCancellationRequested) {
				return false;
			}
			bool written = _sendChannel.Writer.TryWrite(sendFunc);
			if (written) {
				Interlocked.Increment(ref _queueCount);
			}
			return written;
		}
	}

	private static async Task ProcessQueueAsync(CancellationToken cancellationToken) {
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
			Logger.Warning("SendQueue has been canceled");
		}
	}

	public static bool HasPendingItems() {
		return Volatile.Read(ref _queueCount) > 0;
	}

	public static async Task ShutdownAsync() {
		lock (_lock) {
			if (_cts == null || _cts.IsCancellationRequested)
				return;

			_sendChannel.Writer.Complete();
			_cts.Cancel();
		}

		try {
			await _worker;
		} catch {
			Logger.Error("Error while shutting down SendQueue");
		}

		lock (_lock) {
			_cts.Dispose();
			_cts = null;
			_worker = null;
			_sendChannel = null;
			_queueCount = 0;
		}
	}

	public static async Task HardResetAsync() {
		await ShutdownAsync();

		lock (_lock) {
			SendCommands.ClearAll();

			InitializeChannelAndWorker();
		}
	}
}
