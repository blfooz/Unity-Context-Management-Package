using System;
using System.Collections.Generic;
using System.Text;

namespace ContextManagement
{
	/// <summary>
	/// Turns the bytes of a Server-Sent Events response into event payloads.
	/// </summary>
	/// <remarks>
	/// Network packets do not respect character or line boundaries, so text is fed
	/// in as it arrives and partial input is buffered: the decoder keeps the leading
	/// bytes of a multi-byte character until the rest of it arrives, and an
	/// unterminated line waits for its terminator. Only <c>data:</c> fields carry a
	/// payload; an event ends at a blank line and its payload is the event's data
	/// fields joined by newlines. Everything else — comments, <c>event:</c>,
	/// <c>id:</c>, <c>retry:</c> — is ignored. Nothing here depends on Unity, so the
	/// framing can be tested on its own.
	/// </remarks>
	public sealed class SseStreamReader
	{
		private readonly Decoder _decoder = Encoding.UTF8.GetDecoder();
		private readonly StringBuilder _pending = new();   // completed lines removed, trailing partial line kept
		private readonly StringBuilder _data = new();      // data fields of the event being assembled
		private char[] _chars = new char[1024];
		private bool _hasData;

		/// <summary>
		/// Decode the next <paramref name="length"/> bytes and add every payload that
		/// the bytes completed to <paramref name="payloads"/>.
		/// </summary>
		public void Append(byte[] bytes, int length, List<string> payloads)
		{
			if (bytes == null) throw new ArgumentNullException(nameof(bytes));
			if (payloads == null) throw new ArgumentNullException(nameof(payloads));
			if (length <= 0) return;

			// A character never needs more chars than it took bytes.
			if (_chars.Length < length) _chars = new char[length];

			int charCount = _decoder.GetChars(bytes, 0, length, _chars, 0, flush: false);
			if (charCount > 0)
				Append(_chars.AsSpan(0, charCount), payloads);
		}

		/// <summary>
		/// Report the end of the response: a final line without a terminator and an
		/// event whose closing blank line never arrived are both delivered.
		/// </summary>
		public void Flush(List<string> payloads)
		{
			if (payloads == null) throw new ArgumentNullException(nameof(payloads));

			// Emits U+FFFD if the response was cut in the middle of a character.
			int charCount = _decoder.GetChars(Array.Empty<byte>(), 0, 0, _chars, 0, flush: true);
			if (charCount > 0) Append(_chars.AsSpan(0, charCount), payloads);

			if (_pending.Length > 0)
			{
				int length = _pending.Length;
				if (_pending[length - 1] == '\r') length--;
				ConsumeLine(0, length, payloads);
				_pending.Clear();
			}

			Dispatch(payloads);
		}

		/// <summary>Discard all buffered state.</summary>
		public void Reset()
		{
			_decoder.Reset();
			_pending.Clear();
			_data.Clear();
			_hasData = false;
		}

		private void Append(ReadOnlySpan<char> text, List<string> payloads)
		{
			_pending.Append(text);

			int start = 0;
			int i = 0;
			while (i < _pending.Length)
			{
				char c = _pending[i];

				if (c == '\n')
				{
					ConsumeLine(start, i - start, payloads);
					i++;
					start = i;
					continue;
				}

				if (c == '\r')
				{
					// A trailing CR may be the first half of a CRLF pair whose LF is in
					// the next packet, so it is left in the buffer.
					if (i == _pending.Length - 1) break;

					ConsumeLine(start, i - start, payloads);
					i += _pending[i + 1] == '\n' ? 2 : 1;
					start = i;
					continue;
				}

				i++;
			}

			if (start > 0) _pending.Remove(0, start);
		}

		/// <summary>Handle one line of the event stream. <paramref name="length"/> may be zero.</summary>
		private void ConsumeLine(int start, int length, List<string> payloads)
		{
			if (length == 0)
			{
				Dispatch(payloads);
				return;
			}

			// Lines starting with a colon are comments, which some providers send as keep-alives.
			if (_pending[start] == ':') return;

			int colon = -1;
			for (int i = start; i < start + length; i++)
				if (_pending[i] == ':') { colon = i; break; }

			string field;
			string value;
			if (colon < 0)
			{
				field = _pending.ToString(start, length);
				value = string.Empty;
			}
			else
			{
				field = _pending.ToString(start, colon - start);
				int valueStart = colon + 1;
				int valueLength = start + length - valueStart;
				// The spec allows a single space after the colon and it is not part of the value.
				if (valueLength > 0 && _pending[valueStart] == ' ')
				{
					valueStart++;
					valueLength--;
				}
				value = _pending.ToString(valueStart, valueLength);
			}

			if (field != "data") return;

			_data.Append(value).Append('\n');
			_hasData = true;
		}

		private void Dispatch(List<string> payloads)
		{
			if (!_hasData) return;

			int length = _data.Length;
			if (length > 0 && _data[length - 1] == '\n') length--;
			if (length > 0) payloads.Add(_data.ToString(0, length));

			_data.Clear();
			_hasData = false;
		}
	}
}
