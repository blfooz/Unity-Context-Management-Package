using System;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace ContextManagement.Tests
{
	[TestFixture]
	public class ToolCallContextTests
	{
		[Test]
		public async Task ExposesTheParametersAndCompletesWithTheReturnedValue()
		{
			var context = ToolCallContext.Create(JObject.Parse("{\"a\":1}"), out var task);

			Assert.AreEqual("1", context["a"].ToString());

			context.Return("done");

			Assert.AreEqual("done", await task);
		}

		[Test]
		public async Task SurfacesAnExceptionOnTheTask()
		{
			var context = ToolCallContext.Create(new JObject(), out var task, timeout: 5);
			context.Throw(new InvalidOperationException("failed"));

			try
			{
				await task;
				Assert.Fail("expected the exception to surface");
			}
			catch (InvalidOperationException ex)
			{
				Assert.AreEqual("failed", ex.Message);
			}
		}

		[Test]
		public async Task TimesOutWhenNothingAnswers()
		{
			ToolCallContext.Create(new JObject(), out var task, timeout: 0.05f);

			try
			{
				await task;
				Assert.Fail("expected the call to time out");
			}
			catch (Exception ex)
			{
				Assert.AreEqual("Tool call timed out.", ex.Message);
			}
		}

		[Test]
		public async Task WaitsIndefinitelyWhenTheTimeoutIsZero()
		{
			var context = ToolCallContext.Create(new JObject(), out var task, timeout: 0);

			context.Return("late");

			Assert.AreEqual("late", await task);
		}

		[Test]
		public async Task IgnoresASecondAnswer()
		{
			var context = ToolCallContext.Create(new JObject(), out var task);

			context.Return("first");
			context.Return("second");
			context.Throw(new InvalidOperationException("late"));

			Assert.AreEqual("first", await task);
		}
	}
}
