using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

namespace ContextManagement.Tests
{
	[TestFixture]
	public class AsyncPushQueueTests
	{
		private static async Task<List<T>> ReadAll<T>(AsyncPushQueue<T> queue, CancellationToken ct = default)
		{
			var items = new List<T>();
			await foreach (var item in queue.ReadAllAsync(ct))
				items.Add(item);
			return items;
		}

		[Test]
		public async Task ReadsWhatWasPushedBeforeTheConsumerStarted()
		{
			var queue = new AsyncPushQueue<int>();
			queue.Push(1);
			queue.Push(2);
			queue.Complete();

			CollectionAssert.AreEqual(new[] { 1, 2 }, await ReadAll(queue));
		}

		[Test]
		public async Task ReadsWhatIsPushedWhileTheConsumerIsWaiting()
		{
			var queue = new AsyncPushQueue<int>();
			var reading = ReadAll(queue);

			await Task.Delay(10);
			queue.Push(1);
			await Task.Delay(10);
			queue.Push(2);
			queue.Complete();

			CollectionAssert.AreEqual(new[] { 1, 2 }, await reading);
		}

		[Test]
		public async Task EndsOnCompletionWithoutItems()
		{
			var queue = new AsyncPushQueue<int>();
			var reading = ReadAll(queue);

			await Task.Delay(10);
			queue.Complete();

			CollectionAssert.AreEqual(new int[0], await reading);
		}

		[Test]
		public async Task ThrowsTheErrorItWasCompletedWith()
		{
			var queue = new AsyncPushQueue<int>();
			queue.Push(1);
			queue.Complete(new InvalidOperationException("boom"));

			var items = new List<int>();
			try
			{
				await foreach (var item in queue.ReadAllAsync())
					items.Add(item);
				Assert.Fail("expected the error to be thrown");
			}
			catch (InvalidOperationException ex)
			{
				Assert.AreEqual("boom", ex.Message);
			}

			// What was buffered before the error is still delivered.
			CollectionAssert.AreEqual(new[] { 1 }, items);
		}

		[Test]
		public async Task ForwardsTheErrorOfTheTaskItRelays()
		{
			var queue = new AsyncPushQueue<int>();
			_ = queue.RelayAsync(Task.FromException(new InvalidOperationException("failed")));

			try
			{
				await ReadAll(queue);
				Assert.Fail("expected the error to be thrown");
			}
			catch (InvalidOperationException ex)
			{
				Assert.AreEqual("failed", ex.Message);
			}
		}

		[Test]
		public async Task EndsWhenTheTaskItRelaysCompletes()
		{
			var queue = new AsyncPushQueue<int>();
			var source = new TaskCompletionSource<bool>();
			_ = queue.RelayAsync(source.Task);
			var reading = ReadAll(queue);

			queue.Push(1);
			source.SetResult(true);

			CollectionAssert.AreEqual(new[] { 1 }, await reading);
		}

		[Test]
		public async Task DropsItemsPushedAfterCompletion()
		{
			var queue = new AsyncPushQueue<int>();
			queue.Complete();
			queue.Push(1);

			CollectionAssert.AreEqual(new int[0], await ReadAll(queue));
		}

		[Test]
		public async Task StopsReadingWhenCancelled()
		{
			var queue = new AsyncPushQueue<int>();
			using var cts = new CancellationTokenSource();
			var reading = ReadAll(queue, cts.Token);

			await Task.Delay(10);
			cts.Cancel();

			try
			{
				await reading;
				Assert.Fail("expected cancellation");
			}
			catch (OperationCanceledException)
			{
			}
		}
	}
}
