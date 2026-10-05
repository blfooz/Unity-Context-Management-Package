using Newtonsoft.Json.Linq;

namespace ContextManagement
{
	/// <summary>
	/// Rebuilds the response object a non-streaming call would have returned by
	/// merging the chunks of a streamed one.
	/// </summary>
	/// <remarks>
	/// A chunk holds a delta: the part of the answer generated since the previous
	/// chunk. Text is concatenated, tool call fragments are joined by their index
	/// (the id and name arrive once, the arguments arrive in pieces), and the
	/// top-level fields — <c>usage</c>, <c>finish_reason</c> — are kept as they
	/// arrive, so the result can be handed to the same post-processing as a
	/// non-streaming response. Nothing here depends on Unity.
	/// </remarks>
	public sealed class StreamAccumulator
	{
		private readonly JObject _response = new();
		private JArray _choices;

		/// <summary>The response assembled from the chunks added so far.</summary>
		public JObject Response => _response;

		/// <summary>Whether any choice has been seen, i.e. whether a message was generated.</summary>
		public bool HasChoices => _choices != null && _choices.Count > 0;

		/// <summary>Merge one streamed chunk into the accumulated response.</summary>
		public void Add(JObject chunk)
		{
			if (chunk == null) return;

			Initialize(chunk);

			if (chunk["choices"] is JArray chunkChoices)
				foreach (var token in chunkChoices)
					if (token is JObject choice)
						MergeChoice(choice);

			if (chunk["usage"] is JObject usage)
				_response["usage"] = usage.DeepClone();

			if (chunk["error"] != null)
				_response["error"] = chunk["error"].DeepClone();
		}

		private void Initialize(JObject chunk)
		{
			if (_choices != null) return;

			// The first chunk carries the response envelope: id, object, created, model...
			foreach (var property in chunk.Properties())
				if (property.Name != "choices")
					_response[property.Name] = property.Value?.DeepClone();

			_choices = new JArray();
			_response["choices"] = _choices;
		}

		private void MergeChoice(JObject choice)
		{
			int index = choice["index"]?.Value<int>() ?? 0;
			while (_choices.Count <= index)
				_choices.Add(new JObject());

			var accumulated = (JObject)_choices[index];
			accumulated["index"] = index;

			if (choice["delta"] is JObject delta)
				MergeDelta(accumulated, delta);
			else if (choice["message"] is JObject message)
				// Some providers answer a streaming request with whole messages.
				accumulated["message"] = message.DeepClone();

			if (choice["finish_reason"] != null)
				accumulated["finish_reason"] = choice["finish_reason"].DeepClone();
		}

		private static void MergeDelta(JObject accumulated, JObject delta)
		{
			if (accumulated["message"] is not JObject message)
				accumulated["message"] = message = new JObject();

			foreach (var property in delta.Properties())
			{
				switch (property.Name)
				{
					// Sent once, at the start of the stream.
					case "role":
						if (message["role"] == null) message["role"] = property.Value?.DeepClone();
						break;

					// Sent in pieces.
					case "content":
					case "reasoning":
					case "reasoning_content":
						AppendText(message, property.Name, property.Value);
						break;

					case "tool_calls":
						if (property.Value is JArray toolCalls)
							MergeToolCalls(message, toolCalls);
						break;

					default:
						if (message[property.Name] == null) message[property.Name] = property.Value?.DeepClone();
						break;
				}
			}
		}

		private static void AppendText(JObject message, string name, JToken value)
		{
			if (value == null || value.Type == JTokenType.Null) return;

			string fragment = value.Type == JTokenType.String ? value.Value<string>() : value.ToString();
			if (string.IsNullOrEmpty(fragment)) return;

			string current = message[name]?.Value<string>() ?? message[name]?.ToString();
			message[name] = current + fragment;
		}

		/// <summary>
		/// Merge tool call fragments. Each fragment repeats the index of the call it
		/// belongs to; the arguments of one call are split across fragments and only
		/// parse once every fragment has been appended.
		/// </summary>
		private static void MergeToolCalls(JObject message, JArray fragments)
		{
			if (message["tool_calls"] is not JArray toolCalls)
				message["tool_calls"] = toolCalls = new JArray();

			foreach (var token in fragments)
			{
				if (token is not JObject fragment) continue;

				// A provider that omits the index continues the call it started last.
				int index = fragment["index"]?.Value<int>() ?? (toolCalls.Count > 0 ? toolCalls.Count - 1 : 0);
				while (toolCalls.Count <= index)
					toolCalls.Add(new JObject());

				var call = (JObject)toolCalls[index];
				foreach (var property in fragment.Properties())
				{
					switch (property.Name)
					{
						case "index":
							break;

						case "function":
							if (call["function"] is not JObject function)
								call["function"] = function = new JObject();
							MergeFunction(function, property.Value as JObject);
							break;

						default:
							if (call[property.Name] == null) call[property.Name] = property.Value?.DeepClone();
							break;
					}
				}
			}
		}

		private static void MergeFunction(JObject function, JObject fragment)
		{
			if (fragment == null) return;

			foreach (var property in fragment.Properties())
			{
				if (property.Name == "arguments")
					AppendText(function, property.Name, property.Value);
				else if (function[property.Name] == null)
					function[property.Name] = property.Value?.DeepClone();
			}
		}
	}
}
