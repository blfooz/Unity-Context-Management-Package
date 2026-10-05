using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace ContextManagement.Tests
{
	[TestFixture]
	public class RequestManagerTests
	{
		private class FakeContextProvider : IContextProvider
		{
			private readonly string _context;
			private readonly bool _available;

			public FakeContextProvider(string context, bool available)
			{
				_context = context;
				_available = available;
			}

			public bool TryGetContext(LLMRequestManager caller, out string context)
			{
				context = _context;
				return _available;
			}

			public string ContextDescription => _context;
		}

		public class Documented
		{
			public string Value;
		}

		private readonly List<Object> _created = new();

		[TearDown]
		public void TearDown()
		{
			LogAssert.ignoreFailingMessages = false;
			foreach (var obj in _created)
				Object.DestroyImmediate(obj);
			_created.Clear();
		}

		private TextAsset Document(string text)
		{
			var asset = new TextAsset(text);
			_created.Add(asset);
			return asset;
		}

		private GameObject NewObject()
		{
			var go = new GameObject("request manager");
			go.SetActive(false);
			_created.Add(go);
			return go;
		}

		private LLMRequestManager Manager(string prompt = null)
		{
			var go = NewObject();
			var manager = go.AddComponent<LLMRequestManager>();
			manager.parameters = new Parameters();
			if (prompt != null) manager.systemPrompt.Add(Document(prompt));
			go.SetActive(true);
			return manager;
		}

		//==========================================================
		//  Prompt assembly
		//==========================================================

		[Test]
		public void WakingUpBuildsTheSystemPrompt()
		{
			var manager = Manager("be nice");

			Assert.AreEqual(1, manager.messages.Count);
			Assert.AreEqual(Role.system, manager.messages[0].role);
			StringAssert.Contains("be nice", manager.messages[0].content);
		}

		[Test]
		public void PromptComponentsOnTheSameObjectAreIncluded()
		{
			var go = NewObject();
			var manager = go.AddComponent<LLMRequestManager>();
			manager.systemPrompt.Add(Document("be nice"));
			var schemaPrompt = go.AddComponent<JSONSchemaPrompt>();
			schemaPrompt.schema = JSONSchema.FromType<Documented>();
			go.SetActive(true);

			var prompt = manager.AssembleSystemPrompt();

			StringAssert.Contains("be nice", prompt);
			StringAssert.Contains("# Schema", prompt);
			StringAssert.Contains("\"type\": \"object\"", prompt);
		}

		[Test]
		public void ThePromptJoinsEverySystemPromptDocument()
		{
			var manager = Manager("first");
			manager.systemPrompt.Add(Document("second"));

			var prompt = manager.GetPrompt();

			StringAssert.Contains("first", prompt);
			StringAssert.Contains("second", prompt);
		}

		[Test]
		public void InitializingResetsTheHistoryToTheAssembledPrompt()
		{
			var manager = Manager("be nice");
			manager.messages.Add(new Message(Role.user, "old"));

			manager.Initialize();

			Assert.AreEqual(1, manager.messages.Count);
			StringAssert.Contains("be nice", manager.messages[0].content);
		}

		[Test]
		public void AManagerWithoutAPromptFallsBackToTheDefaultOne()
		{
			LogAssert.Expect(LogType.Warning, new Regex("No PromptComponent"));

			var manager = Manager();

			Assert.AreEqual(LLMRequestManager.DefaultSystemPrompt, manager.messages.SystemPrompt);
			Assert.AreEqual(Role.system, manager.messages[0].role);
		}

		[Test]
		public void APromptThatIsEmptyDoesNotCountAsAPrompt()
		{
			LogAssert.ignoreFailingMessages = true;

			var go = NewObject();
			var manager = go.AddComponent<LLMRequestManager>();
			manager.parameters = new Parameters();
			manager.systemPrompt.Add(Document("   "));
			go.SetActive(true);

			Assert.AreEqual(LLMRequestManager.DefaultSystemPrompt, manager.messages.SystemPrompt);
		}

		//==========================================================
		//  Requests
		//==========================================================

		[Test]
		public void TheRequestBodyCarriesTheMessagesAndTheParameters()
		{
			var manager = Manager("be nice");
			manager.messages.Add(new Message(Role.user, "hi"));

			var json = manager.PrepareToSend();

			Assert.AreEqual(1f, json["temperature"].ToObject<float>());
			Assert.AreEqual(2, ((JArray)json["messages"]).Count);
			Assert.AreEqual("hi", json["messages"][1]["content"].ToString());
			Assert.IsNull(json["tools"]);
		}

		[Test]
		public async Task ARequestWithoutAProviderIsReportedInsteadOfThrowing()
		{
			LogAssert.Expect(LogType.Warning, new Regex("APISetting component"));

			var go = NewObject();
			var manager = go.AddComponent<ChatRequestManager>();
			manager.parameters = new Parameters();
			go.SetActive(true);

			Assert.IsNull(await manager.SendRequest());
		}

		[Test]
		public async Task AChatMessageWithoutAProviderIsEmptyInsteadOfThrowing()
		{
			LogAssert.ignoreFailingMessages = true;

			var go = NewObject();
			var manager = go.AddComponent<ChatRequestManager>();
			manager.parameters = new Parameters();
			go.SetActive(true);

			var (reply, reasoning) = await manager.Message("hi");

			Assert.IsNull(reply);
			Assert.IsNull(reasoning);
		}

		[Test]
		public void ToolProvidersOnTheSameObjectBecomeTools()
		{
			var go = NewObject();
			var manager = go.AddComponent<LLMRequestManager>();
			manager.parameters = new Parameters();
			var provider = go.AddComponent<TestToolProvider>();
			provider.tools.Add(new ToolDescriptor(
				new ToolInfo("ping", new JSONSchema(), false, "Ping."), _ => "pong"));
			manager.tools = go.AddComponent<ToolsManager>();
			go.SetActive(true);

			var json = manager.PrepareToSend();

			Assert.AreEqual(1, ((JArray)json["tools"]).Count);
			Assert.AreEqual("ping", json["tools"][0]["function"]["name"].ToString());
		}

		[Test]
		public void TheContextsOfTheProvidersAreCollected()
		{
			var manager = Manager();
			manager.contexts = manager.gameObject.AddComponent<ContextsManager>();
			manager.contexts.AddProviders(
				new FakeContextProvider("kept", available: true),
				new FakeContextProvider("dropped", available: false));

			CollectionAssert.AreEqual(new[] { "kept" }, manager.GetContext());
		}

		[Test]
		public void WithoutAContextsManagerThereIsNoContext()
		{
			var manager = Manager();

			CollectionAssert.IsEmpty(manager.GetContext());
		}

		[Test]
		public void TheOutgoingMessagesAreTheWholeHistory()
		{
			var manager = Manager();
			manager.messages.Add(new Message(Role.user, "hi"));

			var json = manager.GetOutgoingMessages();

			Assert.AreEqual(2, json.Count);
			Assert.AreEqual("system", json[0]["role"].ToString());
			Assert.AreEqual("hi", json[1]["content"].ToString());
		}

		//==========================================================
		//  Chat history window
		//==========================================================

		[Test]
		public void ChatRequestsSendOnlyTheMemoryWindow()
		{
			var go = NewObject();
			var manager = go.AddComponent<ChatRequestManager>();
			manager.parameters = new Parameters();
			manager.memoryWindowSize = 2;
			go.SetActive(true);

			manager.messages.Add(new Message(Role.user, "u1"));
			manager.messages.Add(new Message(Role.assistant, "a1"));
			manager.messages.Add(new Message(Role.user, "u2"));

			var json = manager.GetOutgoingMessages();

			// The system prompt is always kept, then only the newest two entries.
			Assert.AreEqual(3, json.Count);
			Assert.AreEqual("system", json[0]["role"].ToString());
			Assert.AreEqual("a1", json[1]["content"].ToString());
			Assert.AreEqual("u2", json[2]["content"].ToString());
		}

		[Test]
		public void AChatHistoryBelowTheWindowIsSentWhole()
		{
			var go = NewObject();
			var manager = go.AddComponent<ChatRequestManager>();
			manager.parameters = new Parameters();
			manager.memoryWindowSize = 10;
			go.SetActive(true);

			manager.messages.Add(new Message(Role.user, "only"));

			Assert.AreEqual(2, manager.GetOutgoingMessages().Count);
		}

		[Test]
		public void AWindowOfZeroSendsOnlyTheSystemPrompt()
		{
			var go = NewObject();
			var manager = go.AddComponent<ChatRequestManager>();
			manager.parameters = new Parameters();
			manager.memoryWindowSize = 0;
			go.SetActive(true);

			manager.messages.Add(new Message(Role.user, "dropped"));

			var json = manager.GetOutgoingMessages();

			Assert.AreEqual(1, json.Count);
			Assert.AreEqual("system", json[0]["role"].ToString());
		}
	}
}
