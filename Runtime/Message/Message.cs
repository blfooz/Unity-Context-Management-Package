using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace ContextManagement
{
	[Serializable]
	public class Message : MessageEntry
	{
		public Role role;
		public string reasoning, content;

		[SerializeField]
		private List<ToolCall> _toolCalls;
		[SerializeField]
		List<string> _contexts;

		// ── Metadata ──
		[SerializeReference]
		public TokenUsage tokenUsage;
		[SerializeReference]
		public Timestamps timestamps;
		// ── Properties ──
		public IReadOnlyList<ToolCall> ToolCalls => _toolCalls;
		public IReadOnlyList<string> Contexts => _contexts;
		public bool HasToolCalls => _toolCalls != null && _toolCalls.Count > 0;
		public bool IsTool => role == Role.tool;

		// ── Constructors ──
		public Message() { }

		public Message(Role role, string content, params string[] contexts)
		{
			_contexts = contexts.ToList();
			this.role = role;
			this.content = content;
		}

		public Message(JObject json)
		{
			FromJSON(json);
		}

		// ── JSON parsing ──
		public void FromJSON(JObject json)
		{
			var roleStr = json["role"]?.ToString() ?? "user";
			if (!Enum.TryParse(roleStr, out role))
				role = Role.user;

			content = ToNullableString(json["content"]);
			reasoning = ToNullableString(json["reasoning"]) ?? ToNullableString(json["reasoning_content"]);

			if (json["tool_calls"] is JArray tcArray && tcArray.Count > 0)
			{
				_toolCalls = new List<ToolCall>();
				foreach (var tc in tcArray)
				{
					var func = tc["function"] as JObject;
					AddToolCall(new ToolCall
					{
						id = tc["id"]?.ToString(),
						functionName = func?["name"]?.ToString(),
						arguments = ParseArguments(func?["arguments"])
					});
				}
			}

			if (json["contexts"] is JArray contextArray)
				_contexts = contextArray.Select(c => c.ToString()).ToList();

			// Usage
			if (json["usage"] is JObject usageJson)
				tokenUsage = TokenUsage.FromJSON(usageJson);

			// Timestamps
			if (json["timestamps"] is JObject ts)
				timestamps = Timestamps.FromJSON(ts);
		}

		public void AddToolCall(ToolCall toolCall)
		{
			_toolCalls ??= new();
			_toolCalls.Add(toolCall);
		}

		/// <summary>
		/// Tool call arguments arrive as a JSON string. A provider may leave them out
		/// entirely, or send an empty string, for a call that takes no arguments: neither
		/// is a parse error and both become an empty argument object.
		/// </summary>
		internal static JObject ParseArguments(JToken token)
		{
			if (token == null || token.Type == JTokenType.Null) return new JObject();

			// A provider that sends the arguments as an object instead of a string is
			// still read as JSON.
			string text = token.Type == JTokenType.String ? token.Value<string>() : token.ToString();
			return string.IsNullOrWhiteSpace(text) ? new JObject() : JObject.Parse(text);
		}

		/// <summary>
		/// A JSON null means the value is absent rather than an empty string, so an
		/// assistant message without content and a tool call without a response both
		/// survive a save and load unchanged. Anything else keeps its JSON text.
		/// </summary>
		internal static string ToNullableString(JToken token)
			=> token == null || token.Type == JTokenType.Null ? null : token.ToString();

		/// <summary>Serialize for an outgoing API request.</summary>
		public JObject ToJSON() => ToJSON(full: false);

		/// <summary>
		/// Serialize for local persistence: reasoning, the original contexts, tool calls
		/// and the recorded metadata. Unlike the request payload, this keeps the message
		/// as it is stored, so a load reproduces it rather than the prompt it was
		/// expanded into.
		/// </summary>
		public virtual JObject ToJSONFull() => ToJSON(full: true);

		private JObject ToJSON(bool full)
		{
			var json = new JObject
			{
				["role"] = role.ToString(),
				// A message without content writes a JSON null, not an empty string.
				["content"] = content == null ? JValue.CreateNull() : new JValue(content),
			};

			if (!string.IsNullOrWhiteSpace(reasoning) && (full || HasToolCalls))
				json["reasoning_content"] = reasoning;

			if (HasToolCalls)
			{
				var tcArray = new JArray();
				foreach (var tc in _toolCalls)
					tcArray.Add(tc.ToJSON());
				json["tool_calls"] = tcArray;
			}

			// Local-only: contexts are expanded into the request instead of being sent as
			// data, and usage/timestamps are recorded for the editor, not for the API.
			if (full)
			{
				if (_contexts?.Count > 0)
					json["contexts"] = new JArray(_contexts);

				if (tokenUsage != null)
					json["usage"] = tokenUsage.ToJSON();

				if (timestamps != null)
					json["timestamps"] = timestamps.ToJSON();
			}

			return json;
		}

		public override IEnumerable<JObject> GetJObjects(ContextAttachementMode mode)
		{
			var self = ToJSON();
			if (Contexts?.Count > 0)
			{
				if (mode == ContextAttachementMode.AsOne)
					self["content"] = $"<contexts>\n{string.Join('\n', Contexts)}\n</contexts>\n{self["content"]}";
				else if (mode == ContextAttachementMode.Seperate)
					yield return new() { ["role"] = "user", ["content"] = $"<contexts>\n{string.Join('\n', Contexts)}\n</contexts>" };
			}
			yield return self;
			if (_toolCalls == null) yield break;
			foreach (var tc in _toolCalls)
				yield return tc.GetResponseJSON();
		}

		public override IEnumerable<JObject> GetFullJObjects()
		{
			yield return ToJSONFull();
			if (_toolCalls == null) yield break;
			foreach (var tc in _toolCalls)
				yield return tc.GetResponseJSON();
		}

		/// <summary>Set the result of a pending tool call by ID. Returns true if found.</summary>
		public bool SetToolCallResult(string id, string result)
		{
			if (_toolCalls == null) return false;
			for (int i = 0; i < _toolCalls.Count; i++)
			{
				if (_toolCalls[i].id == id)
				{
					var tc = _toolCalls[i];
					tc.result = result;
					_toolCalls[i] = tc;
					return true;
				}
			}
			return false;
		}
	}
}
