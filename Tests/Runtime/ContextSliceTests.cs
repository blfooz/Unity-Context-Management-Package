using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace ContextManagement.Tests
{
	/// <summary>
	/// <see cref="Context.Slice"/> cuts a markdown document into one entry per heading,
	/// each owning the text up to the next heading of the same or a higher level.
	/// </summary>
	[TestFixture]
	public class ContextSliceTests
	{
		private readonly List<UnityEngine.Object> _created = new();

		[TearDown]
		public void TearDown()
		{
			foreach (var obj in _created)
				UnityEngine.Object.DestroyImmediate(obj);
			_created.Clear();
		}

		private Context WithDocument(string text)
		{
			var document = new TextAsset(text);
			var context = ScriptableObject.CreateInstance<Context>();
			context.sourceDocument = document;
			_created.Add(document);
			_created.Add(context);
			return context;
		}

		private static string[] Titles(Context context)
			=> context.contextEntries.Select(entry => entry.title).ToArray();

		[Test]
		public void SlicesOneEntryPerHeading()
		{
			var context = WithDocument("# A\naaa\n## A1\nbbb\n# B\nccc\n");

			context.Slice();

			CollectionAssert.AreEqual(new[] { "A", "A > A1", "B" }, Titles(context));
			CollectionAssert.AreEqual(new[] { 1, 2, 1 }, context.contextEntries.Select(e => e.depth).ToArray());
		}

		[Test]
		public void AnEntryOwnsTheTextUpToTheNextHeading()
		{
			var context = WithDocument("# A\naaa\n## A1\nbbb\n# B\nccc\n");

			context.Slice();

			var entries = context.contextEntries;
			Assert.AreEqual("aaa\n## A1\nbbb\n", entries[0].Content);
			Assert.AreEqual("bbb\n", entries[1].Content);
			Assert.AreEqual("ccc\n", entries[2].Content);
		}

		[Test]
		public void NestedTitlesCarryTheTitlesBeforeThem()
		{
			var context = WithDocument("# A\n## B\n### C\ntext\n");

			context.Slice();

			CollectionAssert.AreEqual(new[] { "A", "A > B", "A > B > C" }, Titles(context));
			Assert.AreEqual("### C\ntext\n", context.contextEntries[1].Content);
			Assert.AreEqual("text\n", context.contextEntries[2].Content);
		}

		[Test]
		public void ATitlePrefixIsAppliedToTheTopLevelTitles()
		{
			var context = WithDocument("# A\ntext\n");

			context.Slice(0, 0, "Document");

			CollectionAssert.AreEqual(new[] { "Document > A" }, Titles(context));
		}

		[Test]
		public void ADocumentWithoutHeadingsProducesNoEntries()
		{
			var context = WithDocument("just text\nand more\n");

			context.Slice();

			Assert.AreEqual(0, context.contextEntries.Count);
		}

		[Test]
		public void SlicingAgainReplacesThePreviousEntries()
		{
			var context = WithDocument("# A\naaa\n");
			context.Slice();

			var replacement = new TextAsset("# B\nbbb\n");
			_created.Add(replacement);
			context.sourceDocument = replacement;
			context.Slice();

			CollectionAssert.AreEqual(new[] { "B" }, Titles(context));
		}

		[Test]
		public void ADocumentWithoutContentClearsTheEntries()
		{
			var context = WithDocument("# A\naaa\n");
			context.Slice();

			context.sourceDocument = null;
			var result = context.Slice();

			Assert.AreEqual(0, result);
			Assert.AreEqual(0, context.contextEntries.Count);
		}

		[Test]
		public void NonConsecutiveHeadingLevelsAreRejected()
		{
			Assert.Throws<Exception>(() => WithDocument("# A\n### B\ntext\n").Slice());
			Assert.Throws<Exception>(() => WithDocument("## B\ntext\n").Slice());
		}

		[Test]
		public void AHashThatIsNotAHeadingIsJustText()
		{
			var context = WithDocument("#A\ntext\n#not a heading\n");

			context.Slice();

			Assert.AreEqual(0, context.contextEntries.Count);
		}

		[Test]
		public void AnEntryWithoutASourceDocumentHasNoContent()
		{
			var entry = new Context.ContextEntry();
			Assert.AreEqual(string.Empty, entry.Content);
		}

		[Test]
		public void AnEntryClampsItsRangeToTheDocument()
		{
			var document = new TextAsset("abcdef");
			_created.Add(document);

			var entry = new Context.ContextEntry { sourceDocument = document, start = 2, end = 99 };
			Assert.AreEqual("cdef", entry.Content);

			var reversed = new Context.ContextEntry { sourceDocument = document, start = 4, end = 2 };
			Assert.AreEqual(string.Empty, reversed.Content);
		}
	}
}
