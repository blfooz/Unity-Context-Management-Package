using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;

namespace ContextManagement
{
	/// <summary>
	/// Receives a streaming (Server-Sent Events) response from an OpenAI-compatible
	/// API: every chunk is decoded, reported through <c>onDelta</c> and merged into
	/// the response object the non-streaming endpoint would have returned.
	/// </summary>
	public class StreamingDownloadHandler : DownloadHandlerScript
	{
		private const int BufferSize = 4096;
		private const int MaxBodyPreview = 512;

		private readonly Action<JObject> _onDelta;
		private readonly SseStreamReader _reader = new();
		private readonly StreamAccumulator _accumulator = new();
		private readonly List<string> _payloads = new();
		private readonly StringBuilder _bodyPreview = new();
		private bool _disposed;

		/// <param name="onDelta">
		/// Called with each parsed chunk as it arrives. Optional: the chunks are
		/// accumulated either way.
		/// </param>
		public StreamingDownloadHandler(Action<JObject> onDelta = null)
			: base(new byte[BufferSize])
		{
			_onDelta = onDelta;
		}

		/// <summary>The response assembled from the chunks received so far.</summary>
		public JObject Response => _accumulator.Response;

		/// <summary>Whether a choice was received, i.e. the stream carried a generated message.</summary>
		public bool HasChoices => _accumulator.HasChoices;

		/// <summary>
		/// The start of the raw body, kept for error messages: a rejected request
		/// answers with an ordinary response body rather than with events, which
		/// would otherwise leave no trace of why it failed.
		/// </summary>
		public string BodyPreview => _bodyPreview.ToString();

		protected override bool ReceiveData(byte[] data, int dataLength)
		{
			if (_disposed) return false;
			if (data == null || dataLength <= 0) return true;

			try
			{
				Capture(data, dataLength);
				_reader.Append(data, dataLength, _payloads);
				Dispatch();
			}
			catch (Exception ex)
			{
				Debug.LogError($"Streaming receive error: {ex}");
			}

			return true;
		}

		protected override void CompleteContent()
		{
			if (_disposed) return;

			try
			{
				_reader.Flush(_payloads);
				Dispatch();
			}
			catch (Exception ex)
			{
				Debug.LogError($"Streaming completion error: {ex}");
			}
		}

		public override void Dispose()
		{
			_disposed = true;
			_payloads.Clear();
			base.Dispose();
		}

		private void Dispatch()
		{
			// Removed one at a time so that a payload cannot be reported twice if a
			// callback throws on the way out.
			while (_payloads.Count > 0)
			{
				string payload = _payloads[0];
				_payloads.RemoveAt(0);
				Accept(payload);
			}
		}

		private void Accept(string payload)
		{
			// The end of stream sentinel carries no data; the request completing is
			// what ends the download.
			if (payload == "[DONE]") return;

			JObject chunk;
			try
			{
				chunk = JObject.Parse(payload);
			}
			catch (Exception ex)
			{
				Debug.LogError($"Failed to parse streamed chunk: {ex.Message}\n{payload}");
				return;
			}

			_accumulator.Add(chunk);
			_onDelta?.Invoke(chunk);
		}

		private void Capture(byte[] data, int dataLength)
		{
			if (_bodyPreview.Length >= MaxBodyPreview) return;

			int length = Math.Min(dataLength, MaxBodyPreview - _bodyPreview.Length);
			_bodyPreview.Append(Encoding.UTF8.GetString(data, 0, length));
		}
	}
}
