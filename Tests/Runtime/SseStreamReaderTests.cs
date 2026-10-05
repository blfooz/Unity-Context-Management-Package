using System;
using System.Collections.Generic;
using System.Text;
using NUnit.Framework;

namespace ContextManagement.Tests
{
	/// <summary>
	/// The reader is fed the bytes of a response as they arrive, which means a line,
	/// an event and a multi-byte character can all be split across packets.
	/// </summary>
	[TestFixture]
	public class SseStreamReaderTests
	{
		private static List<string> Read(string text)
		{
			var reader = new SseStreamReader();
			var payloads = new List<string>();
			reader.Append(Encoding.UTF8.GetBytes(text), Encoding.UTF8.GetByteCount(text), payloads);
			reader.Flush(payloads);
			return payloads;
		}

		[Test]
		public void ReadsAnEventEndingInABlankLine()
		{
			var payloads = Read("data: {\"a\":1}\n\n");

			Assert.AreEqual(1, payloads.Count);
			Assert.AreEqual("{\"a\":1}", payloads[0]);
		}

		[Test]
		public void ReadsSeveralEventsOutOfOnePacket()
		{
			var payloads = Read("data: one\n\ndata: two\n\n");

			Assert.AreEqual(2, payloads.Count);
			Assert.AreEqual("one", payloads[0]);
			Assert.AreEqual("two", payloads[1]);
		}

		[Test]
		public void AcceptsDataFieldsWithoutTheSpaceAfterTheColon()
		{
			Assert.AreEqual("one", Read("data:one\n\n")[0]);
		}

		[Test]
		public void AcceptsCarriageReturnAndCarriageReturnLineFeedEndings()
		{
			Assert.AreEqual("one", Read("data: one\r\n\r\n")[0]);
			Assert.AreEqual("two", Read("data: two\r\r")[0]);
		}

		[Test]
		public void JoinsTheDataFieldsOfOneEventWithNewlines()
		{
			Assert.AreEqual("one\ntwo", Read("data: one\ndata: two\n\n")[0]);
		}

		[Test]
		public void IgnoresCommentsAndNonDataFields()
		{
			var payloads = Read(": keep-alive\nevent: message\nid: 1\nretry: 100\ndata: one\n\n");

			Assert.AreEqual(1, payloads.Count);
			Assert.AreEqual("one", payloads[0]);
		}

		[Test]
		public void ReportsAnEventThatWasNeverClosedByABlankLine()
		{
			Assert.AreEqual("one", Read("data: one\n")[0]);
			Assert.AreEqual("one", Read("data: one")[0]);
		}

		[Test]
		public void IgnoresAnEmptyDataField()
		{
			Assert.AreEqual(0, Read("data:\n\n").Count);
		}

		[Test]
		public void HoldsBackALineThatIsSplitAcrossPackets()
		{
			var reader = new SseStreamReader();
			var payloads = new List<string>();

			Feed(reader, payloads, "data: {\"a\"");
			Assert.AreEqual(0, payloads.Count, "an incomplete line should not be reported");

			Feed(reader, payloads, ":1}\n\n");
			Assert.AreEqual(1, payloads.Count);
			Assert.AreEqual("{\"a\":1}", payloads[0]);
		}

		[Test]
		public void HoldsBackACarriageReturnThatMayBeFollowedByALineFeed()
		{
			var reader = new SseStreamReader();
			var payloads = new List<string>();

			Feed(reader, payloads, "data: one\r");
			Feed(reader, payloads, "\n\r\n");
			reader.Flush(payloads);

			Assert.AreEqual(1, payloads.Count);
			Assert.AreEqual("one", payloads[0]);
		}

		[Test]
		public void DecodesACharacterThatIsSplitAcrossPackets()
		{
			// '中' is three bytes; splitting it must not produce a replacement character.
			var reader = new SseStreamReader();
			var payloads = new List<string>();
			byte[] bytes = Encoding.UTF8.GetBytes("data: 中文內容\n\n");

			Feed(reader, payloads, bytes, 0, 7);                    // splits '中' after its first byte
			Feed(reader, payloads, bytes, 7, bytes.Length - 7);
			reader.Flush(payloads);

			Assert.AreEqual(1, payloads.Count);
			Assert.AreEqual("中文內容", payloads[0]);
		}

		[Test]
		public void ReadsTheSameEventsWhateverThePacketBoundariesAre()
		{
			const string stream =
				"data: {\"choices\":[{\"delta\":{\"content\":\"中\"}}]}\n\n" +
				": keep-alive\n\n" +
				"data: {\"choices\":[{\"delta\":{\"content\":\"文\"}}]}\n\n" +
				"data: [DONE]\n\n";

			var expected = Read(stream);
			byte[] bytes = Encoding.UTF8.GetBytes(stream);

			for (int split = 0; split <= bytes.Length; split++)
			{
				var reader = new SseStreamReader();
				var payloads = new List<string>();
				Feed(reader, payloads, bytes, 0, split);
				Feed(reader, payloads, bytes, split, bytes.Length - split);
				reader.Flush(payloads);

				CollectionAssert.AreEqual(expected, payloads, $"split after byte {split}");
			}

			for (int size = 1; size <= 7; size++)
			{
				var reader = new SseStreamReader();
				var payloads = new List<string>();
				for (int offset = 0; offset < bytes.Length; offset += size)
					Feed(reader, payloads, bytes, offset, Math.Min(size, bytes.Length - offset));
				reader.Flush(payloads);

				CollectionAssert.AreEqual(expected, payloads, $"packets of {size} byte(s)");
			}
		}

		private static void Feed(SseStreamReader reader, List<string> payloads, string text)
			=> Feed(reader, payloads, Encoding.UTF8.GetBytes(text), 0, Encoding.UTF8.GetByteCount(text));

		private static void Feed(SseStreamReader reader, List<string> payloads, byte[] bytes, int offset, int length)
		{
			if (length <= 0) return;

			var packet = new byte[length];
			Array.Copy(bytes, offset, packet, 0, length);
			reader.Append(packet, length, payloads);
		}
	}
}
