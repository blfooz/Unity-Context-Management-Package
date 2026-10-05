using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace ContextManagement.Tests
{
	[TestFixture]
	public class LLMEntityManagerTests
	{
		private GameObject _go;
		private TestWidgetManager _manager;

		[SetUp]
		public void SetUp()
		{
			_go = new GameObject("entity manager");
			_go.SetActive(false);
			_manager = _go.AddComponent<TestWidgetManager>();
			_go.SetActive(true);
		}

		[TearDown]
		public void TearDown()
		{
			LogAssert.ignoreFailingMessages = false;
			if (_go != null) Object.DestroyImmediate(_go);
		}

		private static JObject Widget(string id, string name = "Widget", int value = 3)
			=> new() { ["id"] = id, ["name"] = name, ["value"] = value };

		[Test]
		public void TheSchemaComesFromTheEntityType()
		{
			Assert.IsNotNull(_manager.Schema);
			Assert.AreEqual("Id", _manager.Schema.identifierField);
			Assert.IsTrue(_manager.Schema.TypeLocked());
			Assert.IsFalse(_manager.Schema.IsEmpty());
		}

		[Test]
		public void ExposesTheCrudTools()
		{
			var names = _manager.GetToolDescriptors().Select(tool => tool.Info.name).ToArray();

			CollectionAssert.AreEquivalent(
				new[] { "add_widget", "modify_widget", "delete_widget", "add_widget_addendum" }, names);
		}

		[Test]
		public void TheToolsCanBeRestrictedToOneGroup()
		{
			_manager.baseTools = false;
			var addendum = _manager.GetToolDescriptors().Select(tool => tool.Info.name);
			CollectionAssert.AreEquivalent(new[] { "add_widget_addendum" }, addendum);

			_manager.baseTools = true;
			_manager.addendumTools = false;
			var basic = _manager.GetToolDescriptors().Select(tool => tool.Info.name);
			CollectionAssert.AreEquivalent(new[] { "add_widget", "modify_widget", "delete_widget" }, basic);
		}

		[Test]
		public void EveryToolAsksForAnImmediateResponse()
		{
			foreach (var tool in _manager.GetToolDescriptors())
				Assert.IsTrue(tool.RequireImmediateResponse, tool.Info.name);
		}

		[Test]
		public void TheAddToolTakesTheWholeEntity()
		{
			var add = _manager.GetToolDescriptors().First(tool => tool.Info.name == "add_widget");

			var parameters = add.Serialize()["function"]["parameters"];
			var properties = parameters["properties"];
			Assert.IsNotNull(properties["id"]);
			Assert.IsNotNull(properties["name"]);
			Assert.IsNotNull(properties["value"]);
			// Every field is required, so no required list is written.
			Assert.IsNull(parameters["required"]);
		}

		[Test]
		public void TheModifyToolMakesEveryFieldButTheIdentifierOptional()
		{
			var modify = _manager.GetToolDescriptors().First(tool => tool.Info.name == "modify_widget");

			var parameters = modify.Serialize()["function"]["parameters"];

			Assert.IsNotNull(parameters["properties"]["id"]);
			Assert.IsNotNull(parameters["properties"]["name"]);
			Assert.IsNotNull(parameters["properties"]["value"]);
			CollectionAssert.AreEquivalent(new[] { "id" }, parameters["required"].ToObject<string[]>());
		}

		[Test]
		public void TheAddendumToolTakesAnIdentifierAndTheAddendum()
		{
			var addendum = _manager.GetToolDescriptors().First(tool => tool.Info.name == "add_widget_addendum");

			var properties = addendum.Serialize()["function"]["parameters"]["properties"];
			Assert.IsNotNull(properties["id"]);
			Assert.IsNotNull(properties["addendum"]);
		}

		//==========================================================
		//  Running the tools
		//==========================================================

		[Test]
		public void AddingCreatesTheEntityFromJson()
		{
			Assert.AreEqual("w1 successfully added.", _manager.AddJson(Widget("w1")));

			Assert.IsTrue(_manager.TryGet("w1", out var entity));
			Assert.AreEqual("Widget", entity.Name);
			Assert.AreEqual(3, entity.Value);
		}

		[Test]
		public void IdsAreMatchedExactly()
		{
			_manager.AddJson(Widget("w1"));

			Assert.AreEqual("W1 successfully added.", _manager.AddJson(Widget("W1")));
			Assert.AreEqual(2, _manager.entities.Count);
		}

		[Test]
		public void AddingAnExistingEntityReportsIt()
		{
			_manager.AddJson(Widget("w1"));

			var result = _manager.AddJson(Widget("w1", "Other", 9));

			StringAssert.StartsWith("w1 already exist:", result);
			Assert.AreEqual("Widget", _manager.Get("w1").Name);
		}

		[Test]
		public void AddingWithoutAnIdentifierIsReported()
		{
			LogAssert.ignoreFailingMessages = true;

			var result = _manager.AddJson(JObject.Parse("{\"name\":\"Widget\",\"value\":3}"));

			StringAssert.Contains("Required field: Id is missing from data.", result);
			Assert.AreEqual(0, _manager.entities.Count);
		}

		[Test]
		public void ModifyingOverwritesOnlyTheGivenFields()
		{
			_manager.AddJson(Widget("w1"));

			var result = _manager.ToolModify(JObject.Parse("{\"id\":\"w1\",\"value\":9}"));

			Assert.AreEqual("w1 successfully modified.", result);
			Assert.AreEqual("Widget", _manager.Get("w1").Name);
			Assert.AreEqual(9, _manager.Get("w1").Value);
		}

		[Test]
		public void ModifyingWithoutAnIdentifierIsReported()
		{
			Assert.AreEqual("Required id field missing",
				_manager.ToolModify(JObject.Parse("{\"value\":9}")));
		}

		[Test]
		public void ModifyingAnUnknownEntityIsReported()
		{
			Assert.AreEqual("w1 does not exist.",
				_manager.ToolModify(JObject.Parse("{\"id\":\"w1\",\"value\":9}")));
		}

		[Test]
		public void DeletingReportsWhetherTheEntityWasThere()
		{
			_manager.AddJson(Widget("w1"));

			Assert.AreEqual("w1 successfully deleted.", _manager.ToolDelete(JObject.Parse("{\"id\":\"w1\"}")));
			Assert.AreEqual("w1 does not exist.", _manager.ToolDelete(JObject.Parse("{\"id\":\"w1\"}")));
		}

		[Test]
		public void DeletingWithoutAnIdentifierIsReported()
		{
			Assert.AreEqual("Required id field missing", _manager.ToolDelete(new JObject()));
		}

		[Test]
		public void AddendumsAreAppendedToAnExistingEntity()
		{
			_manager.AddJson(Widget("w1"));

			Assert.AreEqual("Addendum added to w1",
				_manager.ToolAddAddendum(JObject.Parse("{\"id\":\"w1\",\"addendum\":\"first\"}")));
			Assert.AreEqual("Addendum added to w1",
				_manager.ToolAddAddendum(JObject.Parse("{\"id\":\"w1\",\"addendum\":\"second\"}")));

			CollectionAssert.AreEqual(
				new[] { "first", "second" },
				_manager.entities["w1"].addendums);
		}

		[Test]
		public void AddingAnAddendumToAnUnknownEntityIsReported()
		{
			Assert.AreEqual("w9 does not exist.",
				_manager.ToolAddAddendum(JObject.Parse("{\"id\":\"w9\",\"addendum\":\"x\"}")));
		}
	}
}
