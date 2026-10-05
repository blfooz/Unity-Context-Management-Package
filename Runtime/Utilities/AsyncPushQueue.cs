using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace ContextManagement
{
	/// <summary>
	/// Single-consumer queue that turns a push-based producer — a download handler
	/// reporting chunks from the Unity main thread — into an
	/// <see cref="IAsyncEnumerable{T}"/> consumer.
	/// </summary>
	/// <remarks>
	/// Pushed items are buffered until the consumer asks for them, and the consumer
	/// waits on a completion source that is completed only when there is something to
	/// read, so there is no polling and no timer per item. <see cref="Complete"/>
	/// ends the sequence; an error passed to it is thrown at the consumer.
	/// </remarks>
	public sealed class AsyncPushQueue<T>
	{
		private readonly Queue<T> _items = new();
		private readonly object _gate = new();
		private TaskCompletionSource<bool> _signal;
		private bool _completed;
		private Exception _error;

		/// <summary>Buffer an item. Pushes after completion are dropped.</summary>
		public void Push(T item)
		{
			lock (_gate)
			{
				if (_completed) return;
				_items.Enqueue(item);
				_signal?.TrySetResult(true);
			}
		}

		/// <summary>End the sequence, optionally with the error that ended it.</summary>
		public void Complete(Exception error = null)
		{
			lock (_gate)
			{
				if (_completed) return;
				_completed = true;
				_error = error;
				_signal?.TrySetResult(true);
			}
		}

		/// <summary>
		/// Complete the queue when <paramref name="task"/> finishes, forwarding its
		/// error (if any) to the consumer.
		/// </summary>
		public async Task RelayAsync(Task task)
		{
			try
			{
				await task;
				Complete();
			}
			catch (Exception ex)
			{
				Complete(ex);
			}
		}

		/// <summary>Read the buffered items until the sequence completes.</summary>
		public async IAsyncEnumerable<T> ReadAllAsync([EnumeratorCancellation] CancellationToken ct = default)
		{
			var cancelled = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

			using (ct.Register(() => cancelled.TrySetResult(true)))
			{
				while (true)
				{
					switch (TryRead(out var item, out var error, out var wait))
					{
						case ReadResult.Item:
							yield return item;
							break;

						case ReadResult.Completed:
							if (error != null) throw error;
							yield break;

						default:
							if (await Task.WhenAny(wait, cancelled.Task) == cancelled.Task)
								ct.ThrowIfCancellationRequested();
							break;
					}
				}
			}
		}

		private ReadResult TryRead(out T item, out Exception error, out Task wait)
		{
			lock (_gate)
			{
				if (_items.Count > 0)
				{
					item = _items.Dequeue();
					error = null;
					wait = null;
					return ReadResult.Item;
				}

				if (_completed)
				{
					item = default;
					error = _error;
					wait = null;
					return ReadResult.Completed;
				}

				item = default;
				error = null;
				_signal = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
				wait = _signal.Task;
				return ReadResult.Wait;
			}
		}

		private enum ReadResult
		{
			Item,
			Completed,
			Wait
		}
	}
}
