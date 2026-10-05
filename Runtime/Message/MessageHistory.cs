using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace ContextManagement
{
	/// <summary>
	/// An ordered list of <see cref="MessageEntry"/> objects. Two serialization shapes are
	/// available: <see cref="ToJSON"/> for API requests and <see cref="ToJSONFull"/> for
	/// local persistence via <see cref="SaveAs"/>.
	/// </summary>
	[Serializable]
	public class MessageHistory
	{
		[SerializeReference]
		private List<MessageEntry> _entries = new();

		public List<MessageEntry> Entries => _entries;
		// ── Counts ──
		public int Count => _entries != null ? _entries.Count : 0;

		public MessageHistory()
		{
			_entries = new List<MessageEntry>() { };
		}

		// ═══════════════════════════════════════════════════════════
		//  Access
		// ═══════════════════════════════════════════════════════════

		public MessageEntry GetEntry(int index, bool noWarning = false)
		{
			if (_entries == null) return null;
			if (index < 0) index += _entries.Count;
			if (index >= 0 && index < _entries.Count)
				return _entries[index];
			if (!noWarning)
				Debug.LogWarning("Index out of bound");
			return null;
		}
		public Message Get(int index)
		{
			var entry = GetEntry(index);
			if (entry is Message msg)
				return msg;
			return null;
		}

		public Message this[int index] => Get(index);

		public IReadOnlyList<MessageEntry> GetAllEntries() => _entries;

		// ═══════════════════════════════════════════════════════════
		//  Mutation
		// ═══════════════════════════════════════════════════════════

		public string SystemPrompt
		{
			get => Get(0)?.content;
			set
			{
				Set(0, value);
			}
		}

		public void SetSystemPrompt(string prompt) => Set(0, prompt);

		/// <summary>
		/// Note that adding message this way bypasses context attachment by context manager.
		/// </summary>
		public void Add(MessageEntry entry)
		{
			_entries.Add(entry);
		}

		/// <summary>
		/// Note that adding message this way bypasses context attachment by context manager.
		/// </summary>
		/// <param name="role"></param>
		/// <param name="content"></param>
		public void Add(Role role, string content) => Add(new Message(role, content));

		public void Insert(int index, MessageEntry entry)
		{
			if (index < 0) index += _entries.Count;
			if (index >= 0 && index <= _entries.Count)
				_entries.Insert(index, entry);
			else
				Add(entry);
		}

		public void InsertRange(int index, IEnumerable<MessageEntry> collection)
		{
			if (index < 0) index += _entries.Count;
			if (index >= 0 && index <= _entries.Count)
				_entries.InsertRange(index, collection);
			else
				_entries.AddRange(collection);
		}

		public void Set(int index, string newContent)
		{
			var msg = GetEntry(index, true);
			if (msg is Message message)
				message.content = newContent;
			else if (msg != null)
				Debug.LogWarning("Attempting to set non concrete message.");
			else
				Add(new Message(Role.system, newContent));
		}

		public void Remove(int index)
		{
			if (index < 0) index += _entries.Count;
			if (index >= 0 && index < _entries.Count)
				_entries.RemoveAt(index);
		}

		public int Delete(MessageEntry message)
		{
			var i = _entries.IndexOf(message);
			if (i >= 0) _entries.RemoveAt(i);
			return i;
		}

		public void Reset()
		{
			_entries.Clear();
			Add(new Message(Role.system, "empty system prompt"));
		}

		// ═══════════════════════════════════════════════════════════
		//  Memory Window
		// ═══════════════════════════════════════════════════════════

		/// <summary>
		/// Get the system prompt (optional) plus the most recent messages within the memory window.
		/// Tool calls and their responses are stored as a single Message, so no orphan fix-up needed.
		/// </summary>
		/// <param name="windowSize">Excluding the system prompt</param>
		/// <param name="keepFirstEntry">Whether to include the first entry, usually system prompt</param>
		public MessageHistory GetMemoryWindowedMessages(int windowSize, bool keepFirstEntry = true)
		{
			var result = new MessageHistory();
			if (_entries.Count == 0) return result;

			if (keepFirstEntry && GetEntry(0) != null)
				result.Add(_entries[0]);

			int startIdx = Mathf.Max(_entries.Count - windowSize, 1);
			for (int i = startIdx; i < _entries.Count; i++)
				result.Add(_entries[i]);

			return result;
		}

		/// <summary>
		/// Serialize the history to API message objects. Contexts supplied by context
		/// providers are emitted for the anchor entry only (see <see cref="GetContextAnchorIndex"/>),
		/// as a separate message right before it.
		/// </summary>
		public JArray ToJSON()
		{
			var array = new JArray();
			if (_entries == null) return array;

			int anchor = GetContextAnchorIndex();
			for (int i = 0; i < _entries.Count; i++)
			{
				var mode = i == anchor
					? MessageEntry.ContextAttachementMode.Seperate
					: MessageEntry.ContextAttachementMode.Hidden;
				foreach (var m in _entries[i].GetJObjects(mode))
					array.Add(m);
			}
			return array;
		}

		/// <summary>
		/// Serialize the history for local persistence. Entries are written as they are
		/// stored rather than as they would be sent, so contexts stay on their message,
		/// metadata and archived summary entries survive, and a load reproduces the same
		/// history. Nothing here is shaped for an API.
		/// </summary>
		public JArray ToJSONFull()
		{
			var array = new JArray();
			if (_entries == null) return array;

			foreach (var entry in _entries)
				foreach (var m in entry.GetFullJObjects())
					array.Add(m);

			return array;
		}

		/// <summary>
		/// Whether the entry's payload is part of a tool call round: an assistant message
		/// carrying tool calls (which also emits the tool responses), or a lone tool message.
		/// </summary>
		private static bool IsToolCallEntry(MessageEntry entry)
			=> entry is Message msg && (msg.HasToolCalls || msg.role == Role.tool);

		/// <summary>
		/// The entry that receives contexts: the most recent entry that is not part of a
		/// tool call round. A tool call appends the assistant message carrying it (plus its
		/// tool responses) after the user message, so anchoring on the plain last entry
		/// would conceal the contexts for the follow-up request that tool call triggers.
		/// Falls back to the last entry when every entry belongs to a tool call round.
		/// </summary>
		private int GetContextAnchorIndex()
		{
			if (_entries == null || _entries.Count == 0) return -1;
			for (int i = _entries.Count - 1; i >= 0; i--)
				if (!IsToolCallEntry(_entries[i]))
					return i;
			return _entries.Count - 1;
		}

		// ═══════════════════════════════════════════════════════════
		//  File I/O
		// ═══════════════════════════════════════════════════════════

		public string LastSavePath { get; private set; }

		public bool Save()
		{
			if (string.IsNullOrEmpty(LastSavePath))
			{
				Debug.LogError("No last save path");
				return false;
			}
			return SaveAs(LastSavePath);
		}

		public bool SaveAs(string path)
		{
			if (string.IsNullOrEmpty(path))
			{
				Debug.LogError($"Invalid save path:{path}");
				return false;
			}
			WarnOnPendingToolCalls();
			try
			{
				File.WriteAllText(path, ToJSONFull().ToString());
			}
			catch (Exception e)
			{
				Debug.LogError(e);
				return false;
			}
			LastSavePath = path;
			return true;
		}

		/// <summary>
		/// A tool call that has no result is saved without one, and a history loaded from
		/// that file serializes it as {"role":"tool","content":null}, which some APIs
		/// reject. Saving usually happens between turns, so a pending call means the round
		/// was interrupted.
		/// </summary>
		private void WarnOnPendingToolCalls()
		{
			if (_entries == null) return;

			List<string> pending = null;
			foreach (var entry in _entries)
			{
				if (entry is not Message msg || msg.ToolCalls == null) continue;
				foreach (var call in msg.ToolCalls)
				{
					if (call.HasResult) continue;
					pending ??= new List<string>();
					pending.Add($"{call.functionName} ({call.id})");
				}
			}

			if (pending == null) return;
			Debug.LogWarning($"Saving a history with {pending.Count} unanswered tool call(s): " +
				$"{string.Join(", ", pending)}. They are saved without a result and serialize " +
				"as {\"role\":\"tool\",\"content\":null} once loaded, which some APIs reject.");
		}

		public bool Load(string path)
		{
			if (string.IsNullOrEmpty(path))
			{
				Debug.LogError("Invalid load path");
				return false;
			}
			try
			{
				var array = JArray.Parse(File.ReadAllText(path));
				_entries.Clear();
				_entries.AddRange(ParseEntries(array));
			}
			catch (Exception e)
			{
				Debug.LogError(e);
				return false;
			}
			LastSavePath = path;
			return true;
		}

		/// <summary>
		/// Rebuild entries from an array of message objects, reattaching tool responses to
		/// the assistant message that requested them. Shared by <see cref="Load"/> and by
		/// the archived entries of a <see cref="SummaryMessage"/>.
		/// </summary>
		public static List<MessageEntry> ParseEntries(IEnumerable<JToken> tokens)
		{
			var entries = new List<MessageEntry>();
			if (tokens == null) return entries;

			Message lastAssistantMsg = null;
			foreach (var token in tokens)
			{
				if (token is not JObject jObj) continue;

				var roleStr = jObj["role"]?.ToString();

				if (roleStr == "tool")
				{
					string toolId = jObj["tool_call_id"]?.ToString();
					string toolResult = Message.ToNullableString(jObj["content"]);

					if (lastAssistantMsg != null && toolId != null)
						lastAssistantMsg.SetToolCallResult(toolId, toolResult);
					else
					{
						// Orphaned tool message: add as standalone entry
						entries.Add(new Message(Role.tool, toolResult));
					}
				}
				else
				{
					var msg = FromJSON(jObj);
					entries.Add(msg);
					lastAssistantMsg = (msg.role == Role.assistant && msg.HasToolCalls) ? msg : null;
				}
			}
			return entries;
		}

		/// <summary>
		/// Create the appropriate Message subtype from a JSON object.
		/// Summary entries are instantiated as SummaryMessage so archived
		/// entries are preserved on deserialization.
		/// </summary>
		public static Message FromJSON(JObject json)
		{
			if (json["is_summary"]?.ToObject<bool>() == true)
				return new SummaryMessage(json);
			return new Message(json);
		}
	}
}
