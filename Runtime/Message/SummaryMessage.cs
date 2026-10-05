using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;

/// <summary>
/// A Message that summarizes a range of archived entries.
/// The archived originals live on the summary itself rather than
/// in a separate list with fragile index mapping.
/// </summary>
namespace ContextManagement
{
	[Serializable]
	public class SummaryMessage : Message
	{
		[SerializeReference]
	private List<MessageEntry> _summarizedEntries = new();
		public SummaryMessage() { }

public SummaryMessage(string content, List<MessageEntry> entries) :
		base(Role.assistant, $"<summary>\n{content}\n</summary>")
	{
		_summarizedEntries = new List<MessageEntry>(entries);
		}

		public SummaryMessage(MessageHistory messages, int startIndex, int endIndex, string summaryText)
			: base(Role.assistant, $"<summary>\n{summaryText}\n</summary>")
		{
			if (messages == null)
			{
				Debug.LogError("Cannot summarize without a message history.");
				return;
			}

			if (startIndex < 0 || endIndex >= messages.Count || startIndex > endIndex)
			{
				Debug.LogError($"Invalid summarization range: [{startIndex}, {endIndex}]");
				return;
			}

			int count = endIndex - startIndex + 1;
			_summarizedEntries = messages.Entries.GetRange(startIndex, count);

			messages.Entries.RemoveRange(startIndex, count);
			messages.Insert(startIndex, this);
		}

		public SummaryMessage(JObject json) : base(json)
		{
			if (json["summarized_entries"] is JArray archived)
				_summarizedEntries = MessageHistory.ParseEntries(archived);
		}

		/// <summary>
		/// Persist the archived originals next to the summary text. This is local-only:
		/// the request payload (<see cref="Message.ToJSON"/>) is just the summary text,
		/// which is all the API sees.
		/// </summary>
		public override JObject ToJSONFull()
		{
			var json = base.ToJSONFull();
			json["is_summary"] = true;

			var archived = new JArray();
			if (_summarizedEntries != null)
				foreach (var entry in _summarizedEntries)
					foreach (var obj in entry.GetFullJObjects())
						archived.Add(obj);

			json["summarized_entries"] = archived;
			return json;
		}

		public IReadOnlyList<MessageEntry> GetSummarizedEntries() => _summarizedEntries;

		public int SummarizedEntriesCount => _summarizedEntries.Count;

		/// <summary>Expand a summary entry back into its archived raw entries.</summary>
		public void RecoverArchived(MessageHistory messages)
		{
			var i = messages.Delete(this);
			if (i < 0)
			{
				Debug.LogWarning("Summary message not in the given messages");
				return;
			}

			if (_summarizedEntries == null || _summarizedEntries.Count == 0)
			{
				Debug.LogError("Summary has no archived entries.");
				return;
			}

			messages.InsertRange(i, _summarizedEntries);
		}
	}
}
