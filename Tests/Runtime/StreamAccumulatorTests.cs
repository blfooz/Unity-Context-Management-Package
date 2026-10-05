using NUnit.Framework;
using Newtonsoft.Json.Linq;

namespace ContextManagement.Tests
{
	[TestFixture]
	public class StreamAccumulatorTests
	{
		private static JObject Parse(string json) => JObject.Parse(json);

		private static JObject Accumulate(params string[] chunks)
		{
			var accumulator = new StreamAccumulator();
			foreach (var chunk in chunks)
				accumulator.Add(Parse(chunk));
			return accumulator.Response;
		}

		[Test]
		public void IgnoresANullChunk()
		{
			var accumulator = new StreamAccumulator();
			accumulator.Add(null);

			Assert.IsFalse(accumulator.HasChoices);
		}

		[Test]
		public void ConcatenatesTextDeltasIntoOneMessage()
		{
			var response = Accumulate(
				"{\"id\":\"1\",\"object\":\"chat.completion.chunk\",\"created\":5,\"model\":\"m\"," +
				"\"choices\":[{\"index\":0,\"delta\":{\"role\":\"assistant\",\"content\":\"Hel\"}}]}",
				"{\"choices\":[{\"index\":0,\"delta\":{\"content\":\"lo\",\"reasoning_content\":\"why\"}}]}",
				"{\"choices\":[{\"index\":0,\"delta\":{},\"finish_reason\":\"stop\"}]}",
				"{\"choices\":[],\"usage\":{\"prompt_tokens\":3,\"completion_tokens\":2}}");

			var choice = response["choices"][0];
			Assert.AreEqual("1", response["id"].ToString());
			Assert.AreEqual(5, response["created"].ToObject<int>());
			Assert.AreEqual("assistant", choice["message"]["role"].ToString());
			Assert.AreEqual("Hello", choice["message"]["content"].ToString());
			Assert.AreEqual("why", choice["message"]["reasoning_content"].ToString());
			Assert.AreEqual("stop", choice["finish_reason"].ToString());
			Assert.AreEqual(3, response["usage"]["prompt_tokens"].ToObject<int>());
		}

		[Test]
		public void HasChoicesOnceAChoiceArrives()
		{
			var accumulator = new StreamAccumulator();
			accumulator.Add(Parse("{\"choices\":[{\"index\":0,\"delta\":{\"content\":\"hi\"}}]}"));

			Assert.IsTrue(accumulator.HasChoices);
		}

		[Test]
		public void MergesTheFragmentsOfAStreamedToolCall()
		{
			// The arguments of a tool call arrive in pieces that only parse once every
			// piece has been appended, and each piece repeats the call's index.
			var response = Accumulate(
				"{\"choices\":[{\"index\":0,\"delta\":{\"tool_calls\":[{\"index\":0,\"id\":\"call_1\"," +
				"\"type\":\"function\",\"function\":{\"name\":\"get_weather\",\"arguments\":\"{\\\"loc\"}}]}}]}",
				"{\"choices\":[{\"index\":0,\"delta\":{\"tool_calls\":[{\"index\":0," +
				"\"function\":{\"arguments\":\"ation\\\":\\\"Taipei\\\"}\"}}]}}]}");

			var toolCalls = (JArray)response["choices"][0]["message"]["tool_calls"];
			Assert.AreEqual(1, toolCalls.Count);
			Assert.AreEqual("call_1", toolCalls[0]["id"].ToString());
			Assert.AreEqual("get_weather", toolCalls[0]["function"]["name"].ToString());

			var arguments = Parse(toolCalls[0]["function"]["arguments"].ToString());
			Assert.AreEqual("Taipei", arguments["location"].ToString());
		}

		[Test]
		public void KeepsParallelToolCallsApartByIndex()
		{
			var response = Accumulate(
				"{\"choices\":[{\"index\":0,\"delta\":{\"tool_calls\":[{\"index\":0,\"id\":\"a\"," +
				"\"function\":{\"name\":\"one\",\"arguments\":\"{\"}},{\"index\":1,\"id\":\"b\"," +
				"\"function\":{\"name\":\"two\",\"arguments\":\"{\"}}]}}]}",
				"{\"choices\":[{\"index\":0,\"delta\":{\"tool_calls\":[{\"index\":1," +
				"\"function\":{\"arguments\":\"}\"}}]}}]}");

			var toolCalls = (JArray)response["choices"][0]["message"]["tool_calls"];
			Assert.AreEqual(2, toolCalls.Count);
			Assert.AreEqual("one", toolCalls[0]["function"]["name"].ToString());
			Assert.AreEqual("two", toolCalls[1]["function"]["name"].ToString());
			Assert.AreEqual("{}", toolCalls[1]["function"]["arguments"].ToString());
		}

		[Test]
		public void ContinuesTheLastToolCallWhenTheIndexIsMissing()
		{
			var response = Accumulate(
				"{\"choices\":[{\"index\":0,\"delta\":{\"tool_calls\":[{\"index\":0,\"id\":\"a\"," +
				"\"function\":{\"name\":\"one\",\"arguments\":\"{\"}}]}}]}",
				"{\"choices\":[{\"index\":0,\"delta\":{\"tool_calls\":[{" +
				"\"function\":{\"arguments\":\"}\"}}]}}]}");

			var toolCalls = (JArray)response["choices"][0]["message"]["tool_calls"];
			Assert.AreEqual(1, toolCalls.Count);
			Assert.AreEqual("{}", toolCalls[0]["function"]["arguments"].ToString());
		}

		[Test]
		public void RecordsAnErrorChunk()
		{
			var response = Accumulate("{\"error\":{\"message\":\"rate limited\"}}");

			Assert.AreEqual("rate limited", response["error"]["message"].ToString());
		}

		[Test]
		public void KeepsChoicesSeparateByIndex()
		{
			var response = Accumulate(
				"{\"choices\":[{\"index\":0,\"delta\":{\"content\":\"a\"}},{\"index\":1,\"delta\":{\"content\":\"b\"}}]}",
				"{\"choices\":[{\"index\":1,\"delta\":{\"content\":\"c\"}}]}");

			Assert.AreEqual("a", response["choices"][0]["message"]["content"].ToString());
			Assert.AreEqual("bc", response["choices"][1]["message"]["content"].ToString());
		}
	}
}
