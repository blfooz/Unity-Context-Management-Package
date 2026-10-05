using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace ContextManagement.Tests
{
	[TestFixture]
	public class MessageMetadataTests
	{
		[Test]
		public void TokenUsageRoundTrips()
		{
			var usage = TokenUsage.FromJSON(JObject.Parse("{\"prompt_tokens\":11,\"completion_tokens\":7}"));

			Assert.AreEqual(11, usage.promptTokens);
			Assert.AreEqual(7, usage.completionTokens);

			var json = usage.ToJSON();
			Assert.AreEqual(11, json["prompt_tokens"].ToObject<int>());
			Assert.AreEqual(7, json["completion_tokens"].ToObject<int>());
		}

		[Test]
		public void TokenUsageDefaultsToZeroWhenFieldsAreMissing()
		{
			var usage = TokenUsage.FromJSON(new JObject());

			Assert.AreEqual(0, usage.promptTokens);
			Assert.AreEqual(0, usage.completionTokens);
		}

		[Test]
		public void TimestampsRoundTripAndReportTheirLatency()
		{
			var stamps = Timestamps.FromJSON(JObject.Parse("{\"sent\":100,\"created\":102,\"received\":105}"));

			Assert.AreEqual(100L, stamps.sent);
			Assert.AreEqual(102L, stamps.created);
			Assert.AreEqual(105L, stamps.received);
			Assert.AreEqual(5L, stamps.Latency);

			var json = stamps.ToJSON();
			Assert.AreEqual(100L, json["sent"].ToObject<long>());
			Assert.AreEqual(105L, json["received"].ToObject<long>());
		}

		[Test]
		public void TimestampsDefaultToZeroWhenFieldsAreMissing()
		{
			var stamps = Timestamps.FromJSON(new JObject());

			Assert.AreEqual(0L, stamps.sent);
			Assert.AreEqual(0L, stamps.Latency);
			Assert.IsFalse(stamps.HasCreated);
		}

		[Test]
		public void TimestampsWithoutACreationTimeLeaveItOut()
		{
			var stamps = Timestamps.FromJSON(JObject.Parse("{\"sent\":100,\"received\":105}"));

			Assert.IsFalse(stamps.HasCreated);

			// Omitted rather than persisted as a bogus epoch zero.
			var json = stamps.ToJSON();
			Assert.IsNull(json["created"]);
			Assert.AreEqual(100L, json["sent"].ToObject<long>());
			Assert.AreEqual(105L, json["received"].ToObject<long>());

			StringAssert.Contains("Time Created: -", stamps.ToString());
		}

		[Test]
		public void TimestampsWithACreationTimeKeepIt()
		{
			var stamps = Timestamps.FromJSON(JObject.Parse("{\"sent\":100,\"created\":102,\"received\":105}"));

			Assert.IsTrue(stamps.HasCreated);
			Assert.AreEqual(102L, stamps.ToJSON()["created"].ToObject<long>());
		}

		[Test]
		public void TimestampsOnTheSameDayAreFormattedAsClockTimes()
		{
			var stamps = new Timestamps { sent = 0, created = 3600, received = 7200 };
			var text = stamps.ToString();

			StringAssert.Contains("Time Sent: 1970-01-01-00:00:00", text);
			StringAssert.Contains("Time Created: 01:00:00", text);
			StringAssert.Contains("Time Received: 02:00:00", text);
		}

		[Test]
		public void TimestampsOnAnotherDayCarryTheirDate()
		{
			var stamps = new Timestamps { sent = 0, created = 86400, received = 172800 };

			StringAssert.Contains("1970-01-02-00:00:00", stamps.ToString());
		}
	}
}
