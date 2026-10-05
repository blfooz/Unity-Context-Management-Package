using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace ContextManagement.Tests
{
	[TestFixture]
	public class MessageTests
	{
		/// <summary>An assistant message with every field the API can send back.</summary>
		private const string ChatCompletion =
			"{\"role\":\"assistant\",\"content\":\"answer\",\"reasoning\":\"thoughts\"," +
			"\"contexts\":[\"ctx one\",\"ctx two\"]," +
			"\"usage\":{\"prompt_tokens\":11,\"completion_tokens\":7}," +
			"\"timestamps\":{\"sent\":10,\"created\":12,\"received\":15}," +
			"\"tool_calls\":[{\"id\":\"call_1\",\"type\":\"function\"," +
			"\"function\":{\"name\":\"get_weather\",\"arguments\":\"{\\\"location\\\":\\\"Taipei\\\"}\"}}]}";

		private static Message Parsed() => new(JObject.Parse(ChatCompletion));

		//==========================================================
		//  Parsing
		//==========================================================

		[Test]
		public void ReadsEveryFieldTheApiSent()
		{
			var message = Parsed();

			Assert.AreEqual(Role.assistant, message.role);
			Assert.AreEqual("answer", message.content);
			Assert.AreEqual("thoughts", message.reasoning);
			CollectionAssert.AreEqual(new[] { "ctx one", "ctx two" }, message.Contexts);

			Assert.AreEqual(11, message.tokenUsage.promptTokens);
			Assert.AreEqual(7, message.tokenUsage.completionTokens);
			Assert.AreEqual(10L, message.timestamps.sent);
			Assert.AreEqual(15L, message.timestamps.received);
		}

		[Test]
		public void ReadsToolCallsWithTheirArguments()
		{
			var message = Parsed();

			Assert.IsTrue(message.HasToolCalls);
			Assert.AreEqual(1, message.ToolCalls.Count);
			Assert.AreEqual("call_1", message.ToolCalls[0].id);
			Assert.AreEqual("get_weather", message.ToolCalls[0].functionName);
			Assert.AreEqual("Taipei", message.ToolCalls[0].arguments["location"].ToString());
			Assert.IsFalse(message.ToolCalls[0].HasResult);
			Assert.IsFalse(message.IsTool);
		}

		[Test]
		public void AToolCallWithoutArgumentsIsStillReadable()
		{
			// Providers leave the arguments out, send an empty string, or send a null for a
			// call that takes none: none of them is a parse error.
			var message = new Message(JObject.Parse(
				"{\"role\":\"assistant\",\"content\":null,\"tool_calls\":[" +
				"{\"id\":\"c1\",\"function\":{\"name\":\"omitted\"}}," +
				"{\"id\":\"c2\",\"function\":{\"name\":\"empty\",\"arguments\":\"\"}}," +
				"{\"id\":\"c3\",\"function\":{\"name\":\"null\",\"arguments\":null}}]}"));

			Assert.AreEqual(3, message.ToolCalls.Count);
			foreach (var call in message.ToolCalls)
				Assert.AreEqual(0, call.arguments.Count, call.functionName);
		}

		[Test]
		public void AToolCallWhoseArgumentsArriveAsAnObjectIsReadToo()
		{
			var message = new Message(JObject.Parse(
				"{\"role\":\"assistant\",\"tool_calls\":[{\"id\":\"c\"," +
				"\"function\":{\"name\":\"f\",\"arguments\":{\"a\":1}}}]}"));

			Assert.AreEqual(1, message.ToolCalls[0].arguments["a"].ToObject<int>());
		}

		[Test]
		public void DefaultsToUserWhenTheRoleIsUnknownOrMissing()
		{
			Assert.AreEqual(Role.user, new Message(JObject.Parse("{\"content\":\"hi\"}")).role);
			Assert.AreEqual(Role.user, new Message(JObject.Parse("{\"role\":\"robot\",\"content\":\"hi\"}")).role);
		}

		[Test]
		public void PrefersTheReasoningFieldOverReasoningContent()
		{
			var message = new Message(JObject.Parse(
				"{\"role\":\"assistant\",\"content\":\"a\",\"reasoning\":\"old\",\"reasoning_content\":\"new\"}"));

			Assert.AreEqual("old", message.reasoning);
		}

		[Test]
		public void ReadsReasoningContentWhenThereIsNoReasoningField()
		{
			var message = new Message(JObject.Parse(
				"{\"role\":\"assistant\",\"content\":\"a\",\"reasoning_content\":\"new\"}"));

			Assert.AreEqual("new", message.reasoning);
		}

		[Test]
		public void AToolMessageKnowsItIsAToolMessage()
		{
			var message = new Message(Role.tool, "result");

			Assert.IsTrue(message.IsTool);
			Assert.AreEqual(1, message.GetJObjects(MessageEntry.ContextAttachementMode.Hidden).Count());
		}

		//==========================================================
		//  Serialization
		//==========================================================

		[Test]
		public void TheRequestPayloadDropsLocalOnlyData()
		{
			var json = Parsed().ToJSON();

			Assert.AreEqual("assistant", json["role"].ToString());
			Assert.AreEqual("answer", json["content"].ToString());
			Assert.IsNull(json["contexts"]);
			Assert.IsNull(json["usage"]);
			Assert.IsNull(json["timestamps"]);
		}

		[Test]
		public void TheRequestPayloadKeepsReasoningOnlyWhenItCarriesToolCalls()
		{
			// Providers expect reasoning_content echoed back so a tool call round can continue.
			Assert.AreEqual("thoughts", Parsed().ToJSON()["reasoning_content"].ToString());

			var plain = new Message(Role.assistant, "answer") { reasoning = "thoughts" };
			Assert.IsNull(plain.ToJSON()["reasoning_content"]);
		}

		[Test]
		public void TheRequestPayloadCarriesTheToolCalls()
		{
			var toolCalls = Parsed().ToJSON()["tool_calls"] as JArray;

			Assert.AreEqual(1, toolCalls.Count);
			Assert.AreEqual("call_1", toolCalls[0]["id"].ToString());
			Assert.AreEqual("get_weather", toolCalls[0]["function"]["name"].ToString());
			Assert.AreEqual("Taipei",
				JObject.Parse(toolCalls[0]["function"]["arguments"].ToString())["location"].ToString());
		}

		[Test]
		public void TheFullPayloadKeepsEverythingNeededToReload()
		{
			var json = Parsed().ToJSONFull();

			Assert.AreEqual("thoughts", json["reasoning_content"].ToString());
			Assert.AreEqual("ctx one", json["contexts"][0].ToString());
			Assert.AreEqual(11, json["usage"]["prompt_tokens"].ToObject<int>());
			Assert.AreEqual(15L, json["timestamps"]["received"].ToObject<long>());
			Assert.IsNotNull(json["tool_calls"]);
		}

		[Test]
		public void HiddenModeSendsTheMessageOnItsOwn()
		{
			var message = new Message(Role.user, "hi", "ctx");

			var objects = message.GetJObjects(MessageEntry.ContextAttachementMode.Hidden).ToList();

			Assert.AreEqual(1, objects.Count);
			Assert.AreEqual("hi", objects[0]["content"].ToString());
		}

		[Test]
		public void AsOneModePrependsTheContextsToTheContent()
		{
			var message = new Message(Role.user, "hi", "one", "two");

			var objects = message.GetJObjects(MessageEntry.ContextAttachementMode.AsOne).ToList();

			Assert.AreEqual(1, objects.Count);
			Assert.AreEqual("<contexts>\none\ntwo\n</contexts>\nhi", objects[0]["content"].ToString());
		}

		[Test]
		public void SeparateModeEmitsTheContextsAsTheirOwnUserMessage()
		{
			var message = new Message(Role.user, "hi", "ctx");

			var objects = message.GetJObjects(MessageEntry.ContextAttachementMode.Seperate).ToList();

			Assert.AreEqual(2, objects.Count);
			Assert.AreEqual("user", objects[0]["role"].ToString());
			Assert.AreEqual("<contexts>\nctx\n</contexts>", objects[0]["content"].ToString());
			Assert.AreEqual("hi", objects[1]["content"].ToString());
		}

		[Test]
		public void ToolResponsesFollowTheAssistantMessage()
		{
			var message = Parsed();
			message.SetToolCallResult("call_1", "25 degrees");

			var objects = message.GetJObjects(MessageEntry.ContextAttachementMode.Hidden).ToList();

			Assert.AreEqual(2, objects.Count);
			Assert.AreEqual("assistant", objects[0]["role"].ToString());
			Assert.AreEqual("tool", objects[1]["role"].ToString());
			Assert.AreEqual("call_1", objects[1]["tool_call_id"].ToString());
			Assert.AreEqual("25 degrees", objects[1]["content"].ToString());
		}

		[Test]
		public void APendingToolCallIsSentWithANullResult()
		{
			var objects = Parsed().GetJObjects(MessageEntry.ContextAttachementMode.Hidden).ToList();

			Assert.AreEqual(2, objects.Count);
			Assert.AreEqual(JTokenType.Null, objects[1]["content"].Type);
			Assert.IsNull(objects[1]["content"].Value<string>());
		}

		[Test]
		public void TheFullObjectsCarryTheStoredMessageAndItsToolResponses()
		{
			var message = Parsed();
			message.SetToolCallResult("call_1", "25 degrees");

			var objects = message.GetFullJObjects().ToList();

			Assert.AreEqual(2, objects.Count);
			Assert.AreEqual("ctx one", objects[0]["contexts"][0].ToString());
			Assert.AreEqual("25 degrees", objects[1]["content"].ToString());
		}

		//==========================================================
		//  Tool calls
		//==========================================================

		[Test]
		public void AddingAToolCallMakesTheMessageAToolCallMessage()
		{
			var message = new Message(Role.assistant, "calling");
			Assert.IsFalse(message.HasToolCalls);

			message.AddToolCall(new ToolCall { id = "c1", functionName = "f", arguments = new JObject() });

			Assert.IsTrue(message.HasToolCalls);
			Assert.AreEqual(1, message.ToolCalls.Count);
		}

		[Test]
		public void SettingAToolCallResultReportsWhetherItFoundTheCall()
		{
			var message = Parsed();

			Assert.IsTrue(message.SetToolCallResult("call_1", "done"));
			Assert.AreEqual("done", message.ToolCalls[0].result);

			Assert.IsFalse(message.SetToolCallResult("missing", "done"));
			Assert.IsFalse(new Message(Role.assistant, "no calls").SetToolCallResult("call_1", "done"));
		}

		[Test]
		public void AToolCallSerializesForTheRequestAndForTheResponse()
		{
			var call = new ToolCall { id = "c", functionName = "f", arguments = JObject.Parse("{\"a\":1}") };

			Assert.IsFalse(call.HasResult);

			var request = call.ToJSON();
			Assert.AreEqual("c", request["id"].ToString());
			Assert.AreEqual("function", request["type"].ToString());
			Assert.AreEqual("f", request["function"]["name"].ToString());
			Assert.AreEqual(1, JObject.Parse(request["function"]["arguments"].ToString())["a"].ToObject<int>());

			call.result = "r";
			Assert.IsTrue(call.HasResult);

			var response = call.GetResponseJSON();
			Assert.AreEqual("tool", response["role"].ToString());
			Assert.AreEqual("c", response["tool_call_id"].ToString());
			Assert.AreEqual("r", response["content"].ToString());
		}
	}
}
