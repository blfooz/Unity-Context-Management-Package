using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace ContextManagement.Tests
{
	[TestFixture]
	public class SummaryMessageTests
	{
		[TearDown]
		public void TearDown() => LogAssert.ignoreFailingMessages = false;

		private static MessageHistory History(params string[] contents)
		{
			var history = new MessageHistory();
			foreach (var content in contents)
				history.Add(new Message(Role.user, content));
			return history;
		}

		private static string[] Contents(MessageHistory history)
			=> history.GetAllEntries().Select(entry => ((Message)entry).content).ToArray();

		[Test]
		public void ASummaryWrapsItsTextAndOwnsTheEntriesItReplaced()
		{
			var archived = new List<MessageEntry>
			{
				new Message(Role.user, "a"),
				new Message(Role.assistant, "b"),
			};

			var summary = new SummaryMessage("they talked", archived);

			Assert.AreEqual(Role.assistant, summary.role);
			Assert.AreEqual("<summary>\nthey talked\n</summary>", summary.content);
			Assert.AreEqual(2, summary.SummarizedEntriesCount);
			CollectionAssert.AreEqual(archived, summary.GetSummarizedEntries());
		}

		[Test]
		public void SummarizingARangeReplacesItWithTheSummary()
		{
			var history = History("keep", "one", "two", "three");

			var summary = new SummaryMessage(history, 1, 2, "summarized");

			Assert.AreEqual(3, history.Count);
			Assert.AreSame(summary, history[1]);
			Assert.AreEqual(2, summary.SummarizedEntriesCount);
			CollectionAssert.AreEqual(
				new[] { "keep", "<summary>\nsummarized\n</summary>", "three" }, Contents(history));
			CollectionAssert.AreEqual(new[] { "one", "two" },
				summary.GetSummarizedEntries().Select(entry => ((Message)entry).content).ToArray());
		}

		[Test]
		public void SummarizingAnInvalidRangeLeavesTheHistoryAlone()
		{
			LogAssert.ignoreFailingMessages = true;
			var history = History("a", "b");

			var summary = new SummaryMessage(history, 1, 5, "nope");

			Assert.AreEqual(2, history.Count);
			Assert.AreEqual(0, summary.SummarizedEntriesCount);
		}

		[Test]
		public void SummarizingWithoutAHistoryReportsAnError()
		{
			LogAssert.Expect(LogType.Error, "Cannot summarize without a message history.");

			var summary = new SummaryMessage(null, 0, 0, "nope");

			Assert.AreEqual(0, summary.SummarizedEntriesCount);
		}

		[Test]
		public void RecoveringASummaryPutsTheArchivedEntriesBackInItsPlace()
		{
			var history = History("keep", "one", "two", "three");
			var summary = new SummaryMessage(history, 1, 2, "summarized");

			summary.RecoverArchived(history);

			CollectionAssert.AreEqual(new[] { "keep", "one", "two", "three" }, Contents(history));
			Assert.IsFalse(history.GetAllEntries().Any(entry => ReferenceEquals(entry, summary)));
		}

		[Test]
		public void RecoveringASummaryThatIsNotInTheHistoryOnlyWarns()
		{
			LogAssert.Expect(LogType.Warning, "Summary message not in the given messages");
			var history = History("a");
			var summary = new SummaryMessage("text", new List<MessageEntry> { new Message(Role.user, "b") });

			summary.RecoverArchived(history);

			Assert.AreEqual(1, history.Count);
		}

		[Test]
		public void RecoveringASummaryWithoutArchivesReportsAnError()
		{
			LogAssert.Expect(LogType.Error, "Summary has no archived entries.");
			var history = History("a", "b");
			var summary = new SummaryMessage();
			history.Add(summary);

			summary.RecoverArchived(history);

			// The empty summary is dropped and nothing is restored in its place.
			CollectionAssert.AreEqual(new[] { "a", "b" }, Contents(history));
		}

		[Test]
		public void TheRequestPayloadIsOnlyTheSummaryText()
		{
			var summary = new SummaryMessage("condensed", new List<MessageEntry>
			{
				new Message(Role.user, "q"),
			});

			var request = summary.ToJSON();

			Assert.AreEqual("<summary>\ncondensed\n</summary>", request["content"].ToString());
			Assert.IsNull(request["is_summary"]);
			Assert.IsNull(request["summarized_entries"]);
		}

		[Test]
		public void TheFullPayloadKeepsTheArchivedEntries()
		{
			var summary = new SummaryMessage("condensed", new List<MessageEntry>
			{
				new Message(Role.user, "q", "ctx"),
				new Message(Role.system, "s"),
			});

			var json = summary.ToJSONFull();

			Assert.IsTrue(json["is_summary"].ToObject<bool>());
			Assert.AreEqual(2, ((JArray)json["summarized_entries"]).Count);
			Assert.AreEqual("q", json["summarized_entries"][0]["content"].ToString());
			Assert.AreEqual("ctx", json["summarized_entries"][0]["contexts"][0].ToString());
		}

		[Test]
		public void ASummaryReloadsAsASummary()
		{
			var summary = new SummaryMessage("condensed", new List<MessageEntry>
			{
				new Message(Role.user, "q"),
				new Message(Role.assistant, "a"),
			});

			var entries = MessageHistory.ParseEntries(new JArray { summary.ToJSONFull() });

			Assert.AreEqual(1, entries.Count);
			var reloaded = entries[0] as SummaryMessage;
			Assert.IsNotNull(reloaded);
			Assert.AreEqual(2, reloaded.SummarizedEntriesCount);
			Assert.AreEqual("<summary>\ncondensed\n</summary>", reloaded.content);
		}
	}
}
