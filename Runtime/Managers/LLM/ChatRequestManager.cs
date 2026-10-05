using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace ContextManagement
{
	public class ChatRequestManager : LLMRequestManager
	{
		public int memoryWindowSize = 10;
		// MessageHistoryWindow reads/writes this value when it is linked to this manager.

		/// <summary>
		/// Send a message to the LLM and return the response. 
		/// </summary>
		/// <param name="message">message sent to the LLM</param>
		/// <returns>The output and reasoning (if present) of the response</returns>
		public async Task<(string output, string reasoning)> Message(string message)
		{
			messages.Add(new Message(Role.user, message, GetContext().ToArray()));
			return ContextUtility.ExtractResponse(await SendRequest());
		}

		/// <summary>
		/// Send a message and stream the assistant's reply as it is generated.
		/// </summary>
		/// <remarks>
		/// The reply is added to <see cref="LLMRequestManager.messages"/> — including
		/// any tool call round it triggered — before this sequence ends, so the
		/// history is consistent with what the caller has already seen.
		/// </remarks>
		/// <param name="message">The user message to send.</param>
		/// <param name="ct">Cancellation token to abort mid-stream.</param>
		/// <returns>The reply and reasoning (if present) of each streamed chunk.</returns>
		public async IAsyncEnumerable<(string reply, string reasoning)> MessageStreaming(
			string message, [EnumeratorCancellation] CancellationToken ct = default)
		{
			messages.Add(new Message(Role.user, message, GetContext().ToArray()));

			var deltas = new AsyncPushQueue<(string reply, string reasoning)>();
			var request = SendRequestStreaming(chunk =>
			{
				var delta = chunk["choices"]?[0]?["delta"];
				if (delta == null) return;

				string reply = delta["content"]?.ToString();
				string reasoning = delta["reasoning_content"]?.ToString() ?? delta["reasoning"]?.ToString();
				if (string.IsNullOrEmpty(reply) && string.IsNullOrEmpty(reasoning)) return;

				deltas.Push((reply, reasoning));
			}, ct);

			_ = deltas.RelayAsync(request);

			await foreach (var delta in deltas.ReadAllAsync(ct))
				yield return delta;
		}

		public override JArray GetOutgoingMessages()
			=> messages.GetMemoryWindowedMessages(memoryWindowSize).ToJSON();
	}
}
