using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace ContextManagement.Tests
{
	[TestFixture]
	public class MessageHistoryTests
	{
		private string _path;

		[SetUp]
		public void SetUp()
		{
			_path = Path.Combine(Path.GetTempPath(), $"context-management-{Guid.NewGuid():N}.json");
		}

		[TearDown]
		public void TearDown()
		{
			LogAssert.ignoreFailingMessages = false;
			if (File.Exists(_path)) File.Delete(_path);
		}

		private static MessageHistory History(params string[] contents)
		{
			var history = new MessageHistory();
			for (int i = 0; i < contents.Length; i++)
				history.Add(i == 0 ? Role.system : Role.user, contents[i]);
			return history;
		}

		private static string[] Contents(MessageHistory history)
			=> history.GetAllEntries().Select(entry => ((Message)entry).content).ToArray();

		//==========================================================
		//  Access
		//==========================================================

		[Test]
		public void EntriesCanBeReadFromEitherEnd()
		{
			var history = History("s", "u1", "a1");

			Assert.AreEqual(3, history.Count);
			Assert.AreEqual("s", history[0].content);
			Assert.AreEqual("u1", history[1].content);
			Assert.AreEqual("a1", history[-1].content);
			Assert.AreSame(history.GetEntry(2), history.GetEntry(-1));
		}

		[Test]
		public void AnOutOfRangeIndexHasNoEntry()
		{
			var history = History("s");

			Assert.IsNull(history.GetEntry(5, noWarning: true));
			Assert.IsNull(history.GetEntry(-5, noWarning: true));
			Assert.IsNull(history.Get(5));
			Assert.IsNull(history[-5]);
		}

		[Test]
		public void TheSystemPromptIsTheFirstEntry()
		{
			var history = History("prompt", "u");

			Assert.AreEqual("prompt", history.SystemPrompt);

			history.SystemPrompt = "replaced";
			Assert.AreEqual("replaced", history.SystemPrompt);
			Assert.AreEqual("replaced", history[0].content);
			Assert.AreEqual(2, history.Count);
		}

		[Test]
		public void SettingTheSystemPromptOnAnEmptyHistoryCreatesIt()
		{
			var history = new MessageHistory();

			history.SetSystemPrompt("prompt");

			Assert.AreEqual(1, history.Count);
			Assert.AreEqual(Role.system, history[0].role);
			Assert.AreEqual("prompt", history.SystemPrompt);
		}

		//==========================================================
		//  Mutation
		//==========================================================

		[Test]
		public void EntriesCanBeInsertedAndRemovedFromEitherEnd()
		{
			var history = History("s", "a", "b");

			history.Insert(1, new Message(Role.user, "inserted"));
			CollectionAssert.AreEqual(new[] { "s", "inserted", "a", "b" }, Contents(history));

			history.Insert(-1, new Message(Role.user, "before last"));
			CollectionAssert.AreEqual(new[] { "s", "inserted", "a", "before last", "b" }, Contents(history));

			history.Remove(-1);
			CollectionAssert.AreEqual(new[] { "s", "inserted", "a", "before last" }, Contents(history));
		}

		[Test]
		public void InsertingOutOfRangeAppends()
		{
			var history = History("s");

			history.Insert(99, new Message(Role.user, "appended"));

			Assert.AreEqual("appended", history[-1].content);
		}

		[Test]
		public void InsertingARangePutsItAtTheIndex()
		{
			var history = History("s", "a");

			history.InsertRange(1, new MessageEntry[]
			{
				new Message(Role.user, "one"),
				new Message(Role.user, "two"),
			});

			CollectionAssert.AreEqual(new[] { "s", "one", "two", "a" }, Contents(history));
		}

		[Test]
		public void RemovingOutOfRangeDoesNothing()
		{
			var history = History("s", "a");

			history.Remove(99);
			history.Remove(-99);

			Assert.AreEqual(2, history.Count);
		}

		[Test]
		public void DeletingAnEntryReportsWhereItWas()
		{
			var history = History("s", "a", "b");

			Assert.AreEqual(1, history.Delete(history[1]));
			CollectionAssert.AreEqual(new[] { "s", "b" }, Contents(history));

			Assert.AreEqual(-1, history.Delete(new Message(Role.user, "not there")));
		}

		[Test]
		public void ResetLeavesOnlyAnEmptySystemPrompt()
		{
			var history = History("s", "a");

			history.Reset();

			Assert.AreEqual(1, history.Count);
			Assert.AreEqual(Role.system, history[0].role);
			Assert.AreEqual("empty system prompt", history[0].content);
		}

		//==========================================================
		//  Memory window
		//==========================================================

		[Test]
		public void TheMemoryWindowKeepsTheSystemPromptAndTheMostRecentMessages()
		{
			var history = History("s", "u1", "a1", "u2", "a2");

			var windowed = history.GetMemoryWindowedMessages(2);

			CollectionAssert.AreEqual(new[] { "s", "u2", "a2" }, Contents(windowed));
		}

		[Test]
		public void TheMemoryWindowCanDropTheFirstEntry()
		{
			var history = History("s", "u1", "a1");

			var windowed = history.GetMemoryWindowedMessages(10, keepFirstEntry: false);

			CollectionAssert.AreEqual(new[] { "u1", "a1" }, Contents(windowed));
		}

		[Test]
		public void AWindowOfZeroKeepsOnlyTheFirstEntry()
		{
			var history = History("s", "u1", "a1");

			CollectionAssert.AreEqual(new[] { "s" }, Contents(history.GetMemoryWindowedMessages(0)));
		}

		[Test]
		public void TheMemoryWindowOfAnEmptyHistoryIsEmpty()
		{
			Assert.AreEqual(0, new MessageHistory().GetMemoryWindowedMessages(5).Count);
		}

		//==========================================================
		//  Request serialization
		//==========================================================

		[Test]
		public void ContextsAreExpandedOnTheMostRecentMessageThatIsNotAToolCall()
		{
			var history = new MessageHistory();
			history.Add(new Message(Role.system, "s"));
			history.Add(new Message(Role.user, "question", "context"));

			var assistant = new Message(Role.assistant, "calling", "assistant context");
			assistant.AddToolCall(new ToolCall { id = "c1", functionName = "f", arguments = new JObject() });
			history.Add(assistant);
			assistant.SetToolCallResult("c1", "result");

			var json = history.ToJSON();

			// The tool call round moved the anchor back to the user message, so its
			// contexts are expanded there and the assistant's stay hidden.
			Assert.AreEqual(5, json.Count);
			Assert.AreEqual("system", json[0]["role"].ToString());
			Assert.AreEqual("<contexts>\ncontext\n</contexts>", json[1]["content"].ToString());
			Assert.AreEqual("question", json[2]["content"].ToString());
			Assert.AreEqual("assistant", json[3]["role"].ToString());
			Assert.AreEqual("tool", json[4]["role"].ToString());
			Assert.AreEqual("result", json[4]["content"].ToString());
		}

		[Test]
		public void ContextsFallBackToTheLastEntryWhenEveryEntryIsAToolCall()
		{
			var history = new MessageHistory();
			var assistant = new Message(Role.assistant, "calling", "context");
			assistant.AddToolCall(new ToolCall { id = "c1", functionName = "f", arguments = new JObject() });
			history.Add(assistant);

			var json = history.ToJSON();

			Assert.AreEqual(3, json.Count);
			Assert.AreEqual("<contexts>\ncontext\n</contexts>", json[0]["content"].ToString());
			Assert.AreEqual("assistant", json[1]["role"].ToString());
			Assert.AreEqual("tool", json[2]["role"].ToString());
		}

		[Test]
		public void AnEmptyHistorySerializesToAnEmptyArray()
		{
			Assert.AreEqual(0, new MessageHistory().ToJSON().Count);
			Assert.AreEqual(0, new MessageHistory().ToJSONFull().Count);
		}

		//==========================================================
		//  Parsing
		//==========================================================

		[Test]
		public void AToolResponseIsReattachedToTheAssistantMessageThatRequestedIt()
		{
			var entries = MessageHistory.ParseEntries(JArray.Parse(
				"[{\"role\":\"assistant\",\"content\":null,\"tool_calls\":[{\"id\":\"c1\",\"type\":\"function\"," +
				"\"function\":{\"name\":\"f\",\"arguments\":\"{}\"}}]}," +
				"{\"role\":\"tool\",\"tool_call_id\":\"c1\",\"content\":\"42\"}]"));

			Assert.AreEqual(1, entries.Count);
			var message = (Message)entries[0];
			Assert.AreEqual("42", message.ToolCalls[0].result);
		}

		[Test]
		public void AToolResponseWithoutARequestingMessageStaysOnItsOwn()
		{
			var entries = MessageHistory.ParseEntries(JArray.Parse(
				"[{\"role\":\"tool\",\"tool_call_id\":\"c1\",\"content\":\"42\"}]"));

			Assert.AreEqual(1, entries.Count);
			var message = (Message)entries[0];
			Assert.AreEqual(Role.tool, message.role);
			Assert.AreEqual("42", message.content);
		}

		[Test]
		public void APlainApiArrayParsesIntoMessages()
		{
			var entries = MessageHistory.ParseEntries(JArray.Parse(
				"[{\"role\":\"system\",\"content\":\"s\"},{\"role\":\"user\",\"content\":\"u\"}]"));

			Assert.AreEqual(2, entries.Count);
			Assert.IsFalse(entries[0] is SummaryMessage);
			Assert.IsFalse(entries[1] is SummaryMessage);
			Assert.AreEqual("u", ((Message)entries[1]).content);
		}

		[Test]
		public void ASummaryEntryParsesBackIntoASummaryMessage()
		{
			var entries = MessageHistory.ParseEntries(JArray.Parse(
				"[{\"role\":\"assistant\",\"content\":\"<summary>\\ns\\n</summary>\",\"is_summary\":true," +
				"\"summarized_entries\":[{\"role\":\"user\",\"content\":\"archived\"}]}]"));

			Assert.AreEqual(1, entries.Count);
			var summary = entries[0] as SummaryMessage;
			Assert.IsNotNull(summary);
			Assert.AreEqual(1, summary.SummarizedEntriesCount);
			Assert.AreEqual("archived", ((Message)summary.GetSummarizedEntries()[0]).content);
		}

		//==========================================================
		//  Persistence
		//==========================================================

		[Test]
		public void AHistorySurvivesASaveAndLoadUnchanged()
		{
			var original = SavedHistory();

			Assert.IsTrue(original.SaveAs(_path));
			Assert.AreEqual(_path, original.LastSavePath);

			var loaded = new MessageHistory();
			Assert.IsTrue(loaded.Load(_path));
			Assert.AreEqual(_path, loaded.LastSavePath);

			Assert.AreEqual(original.Count, loaded.Count);
			Assert.IsTrue(JToken.DeepEquals(original.ToJSONFull(), loaded.ToJSONFull()));
			Assert.IsTrue(JToken.DeepEquals(original.ToJSON(), loaded.ToJSON()));
		}

		[Test]
		public void LoadingRawApiMessagesBuildsTheSameHistory()
		{
			File.WriteAllText(_path,
				"[{\"role\":\"system\",\"content\":\"s\"},{\"role\":\"user\",\"content\":\"u\",\"contexts\":[\"ctx\"]}]");

			var history = new MessageHistory();
			Assert.IsTrue(history.Load(_path));

			Assert.AreEqual(2, history.Count);
			CollectionAssert.AreEqual(new[] { "ctx" }, history[1].Contexts);
		}

		[Test]
		public void SavingTwiceUsesTheRememberedPath()
		{
			var history = History("s", "u");
			Assert.IsTrue(history.SaveAs(_path));

			history.Add(new Message(Role.user, "later"));
			Assert.IsTrue(history.Save());

			var loaded = new MessageHistory();
			Assert.IsTrue(loaded.Load(_path));
			Assert.AreEqual(3, loaded.Count);
		}

		[Test]
		public void SavingWithoutAPathFails()
		{
			LogAssert.Expect(LogType.Error, "No last save path");
			LogAssert.Expect(LogType.Error, "Invalid save path:");
			LogAssert.Expect(LogType.Error, "Invalid save path:");

			var history = new MessageHistory();

			Assert.IsFalse(history.Save());
			Assert.IsFalse(history.SaveAs(null));
			Assert.IsFalse(history.SaveAs(""));
			Assert.IsNull(history.LastSavePath);
		}

		[Test]
		public void LoadingSomethingThatIsNotAHistoryFails()
		{
			File.WriteAllText(_path, "not a history");

			// The reader's exception is logged with its stack trace.
			LogAssert.ignoreFailingMessages = true;
			Assert.IsFalse(new MessageHistory().Load(_path));
			LogAssert.ignoreFailingMessages = false;

			LogAssert.Expect(LogType.Error, "Invalid load path");
			LogAssert.Expect(LogType.Error, "Invalid load path");

			Assert.IsFalse(new MessageHistory().Load(null));
			Assert.IsFalse(new MessageHistory().Load(""));
		}

		[Test]
		public void SavingAHistoryWithAnUnansweredToolCallStillWritesIt()
		{
			var history = new MessageHistory();
			var assistant = new Message(Role.assistant, "calling");
			assistant.AddToolCall(new ToolCall { id = "c1", functionName = "f", arguments = new JObject() });
			history.Add(assistant);

			Assert.IsTrue(history.SaveAs(_path));

			// The response is written with no content.
			var saved = JArray.Parse(File.ReadAllText(_path));
			Assert.AreEqual(JTokenType.Null, saved[1]["content"].Type);

			var loaded = new MessageHistory();
			Assert.IsTrue(loaded.Load(_path));
			var call = ((Message)loaded[0]).ToolCalls[0];
			Assert.AreEqual("c1", call.id);
			Assert.AreEqual("f", call.functionName);
			Assert.IsNull(call.result);
			Assert.IsFalse(call.HasResult);
		}

		[Test]
		public void AMessageWithoutContentReloadsWithoutOne()
		{
			File.WriteAllText(_path,
				"[{\"role\":\"assistant\",\"content\":null,\"tool_calls\":[{\"id\":\"c1\",\"type\":\"function\"," +
				"\"function\":{\"name\":\"f\",\"arguments\":\"{}\"}}]}]");

			var history = new MessageHistory();
			Assert.IsTrue(history.Load(_path));

			// An absent content stays absent instead of reloading as an empty string.
			Assert.IsNull(history[0].content);
			Assert.AreEqual(JTokenType.Null, history.ToJSONFull()[0]["content"].Type);
		}

		/// <summary>A history exercising every shape the local form has to preserve.</summary>
		private static MessageHistory SavedHistory()
		{
			var history = new MessageHistory();
			history.Add(new Message(Role.system, "system"));

			history.Add(new Message(Role.user, "question", "context one", "context two")
			{
				reasoning = "user reasoning",
				tokenUsage = new TokenUsage { promptTokens = 5 },
				timestamps = new Timestamps { sent = 1, created = 2, received = 3 },
			});

			var assistant = new Message(Role.assistant, "calling")
			{
				reasoning = "assistant reasoning",
				tokenUsage = new TokenUsage { promptTokens = 6, completionTokens = 2 },
				timestamps = new Timestamps { sent = 4, created = 5, received = 6 },
			};
			assistant.AddToolCall(new ToolCall
			{
				id = "c1",
				functionName = "f",
				arguments = JObject.Parse("{\"a\":1}"),
			});
			history.Add(assistant);
			assistant.SetToolCallResult("c1", "42");

			history.Add(new SummaryMessage("older things", new List<MessageEntry>
			{
				new Message(Role.user, "archived question"),
				new Message(Role.assistant, "archived answer"),
			}));

			return history;
		}
	}
}
