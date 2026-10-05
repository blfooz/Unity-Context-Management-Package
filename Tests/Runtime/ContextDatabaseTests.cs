using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;

namespace ContextManagement.Tests
{
	[TestFixture]
	public class ContextDatabaseTests
	{
		private readonly List<Object> _created = new();
		private ContextDatabase _database;

		[SetUp]
		public void SetUp()
		{
			var document = new TextAsset("# Alpha\nfirst body\n# Beta\nsecond body\n");
			var context = ScriptableObject.CreateInstance<Context>();
			context.sourceDocument = document;
			context.Slice();

			var go = new GameObject("context database");
			_database = go.AddComponent<ContextDatabase>();
			_database.contexts.Add(context);
			_database.BuildIndex();

			_created.Add(document);
			_created.Add(context);
			_created.Add(go);
		}

		[TearDown]
		public void TearDown()
		{
			foreach (var obj in _created)
				Object.DestroyImmediate(obj);
			_created.Clear();
		}

		[Test]
		public void IndexesEveryTitleOfEveryContext()
		{
			CollectionAssert.AreEquivalent(new[] { "Alpha", "Beta" }, _database.GetEnums());
		}

		[Test]
		public void AnswersWithTheContentOfATitle()
		{
			Assert.AreEqual("# Alpha:\nfirst body\n", _database.Query("Alpha"));
			Assert.AreEqual("# Beta:\nsecond body\n", _database.Query("Beta"));
		}

		[Test]
		public void ReportsTitlesItDoesNotKnow()
		{
			Assert.AreEqual("Invalid title, no context found.", _database.Query("Gamma"));
			Assert.AreEqual("Invalid title, no context found.", _database.Query(null));
			Assert.AreEqual("Invalid title, no context found.", _database.Query(""));
		}

		[Test]
		public void RebuildingPicksUpNewTitles()
		{
			var document = new TextAsset("# Gamma\nthird body\n");
			var context = ScriptableObject.CreateInstance<Context>();
			context.sourceDocument = document;
			context.Slice();
			_created.Add(document);
			_created.Add(context);

			_database.contexts.Add(context);
			_database.BuildIndex();

			CollectionAssert.AreEquivalent(new[] { "Alpha", "Beta", "Gamma" }, _database.GetEnums());
			Assert.AreEqual("# Gamma:\nthird body\n", _database.Query("Gamma"));
		}

		[Test]
		public void EntriesThatShareATitleAreJoined()
		{
			AddDocument("# Alpha\nsecond body\n");

			_database.BuildIndex();

			CollectionAssert.AreEquivalent(new[] { "Alpha", "Beta" }, _database.GetEnums());
			Assert.AreEqual("# Alpha:\nfirst body\nsecond body\n", _database.Query("Alpha"));
		}

		[Test]
		public void ATitleRepeatedInOneDocumentIsJoinedToo()
		{
			AddDocument("# Alpha\nfirst again\n# Alpha\nsecond again\n");

			_database.BuildIndex();

			CollectionAssert.AreEqual(new[] { "Alpha" },
				_database.GetEnums().Where(title => title == "Alpha").ToArray());
			Assert.AreEqual("# Alpha:\nfirst body\nfirst again\nsecond again\n", _database.Query("Alpha"));
		}

		[Test]
		public void AddingADocumentRebuildsTheIndexOnDemand()
		{
			AddDocument("# Gamma\nthird body\n");

			// No explicit BuildIndex: the document list changed, so it is rebuilt on demand.
			CollectionAssert.AreEquivalent(new[] { "Alpha", "Beta", "Gamma" }, _database.GetEnums());
			Assert.AreEqual("# Gamma:\nthird body\n", _database.Query("Gamma"));
		}

		private void AddDocument(string text)
		{
			var document = new TextAsset(text);
			var context = ScriptableObject.CreateInstance<Context>();
			context.sourceDocument = document;
			context.Slice();
			_created.Add(document);
			_created.Add(context);
			_database.contexts.Add(context);
		}

		[Test]
		public void ServesTheContextAsATool()
		{
			var tool = _database.GetToolDescriptors().Single();

			Assert.AreEqual("get_context", tool.Info.name);
			Assert.AreEqual("Get contexts about a specific subject.", tool.Info.description);
			Assert.IsTrue(tool.RequireImmediateResponse);
		}

		[Test]
		public void TheToolTakesTheTitleAsADynamicEnum()
		{
			var tool = _database.GetToolDescriptors().Single();

			var properties = tool.Serialize()["function"]["parameters"]["properties"];

			CollectionAssert.AreEquivalent(new[] { "Alpha", "Beta" },
				properties["title"]["enum"].ToObject<string[]>());
		}

		[Test]
		public async Task TheToolAnswersWithTheContextItWasAskedFor()
		{
			var tool = _database.GetToolDescriptors().Single();

			Assert.AreEqual("# Beta:\nsecond body\n",
				await tool.Handler(new JObject { ["title"] = "Beta" }));
			Assert.AreEqual("Invalid title, no context found.", await tool.Handler(new JObject()));
		}

		[Test]
		public void TheToolCanBeCalledDirectly()
		{
			Assert.AreEqual("# Alpha:\nfirst body\n",
				_database.ToolGetContext(new JObject { ["title"] = "Alpha" }));
			Assert.AreEqual("Invalid title, no context found.",
				_database.ToolGetContext(JObject.Parse("{\"other\":\"value\"}")));
		}
	}
}
