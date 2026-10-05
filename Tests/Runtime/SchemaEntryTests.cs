using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;

namespace ContextManagement.Tests
{
	[TestFixture]
	public class SchemaEntryTests
	{
		private enum Mood
		{
			Happy,
			Sad,
		}

		private class PlainEnumProvider : IEnumProvider
		{
			public IEnumerable<string> GetEnums() => new[] { "one" };
		}

		private TestEnumProvider _provider;

		[TearDown]
		public void TearDown()
		{
			if (_provider != null) UnityEngine.Object.DestroyImmediate(_provider);
		}

		private JSONSchema SchemaWith(params SchemaEntry[] entries)
		{
			var schema = new JSONSchema();
			foreach (var entry in entries)
				schema.Add(entry);
			return schema;
		}

		private static JToken Property(JSONSchema schema, string name)
			=> schema.Serialize()["properties"][name];

		[Test]
		public void StaticEnumEntriesTakeTheirNamesFromTheEnumType()
		{
			var entry = SchemaEntry.StaticEnumEntry<Mood>("mood", "how it feels");

			Assert.AreEqual(JSONSchema.DataType.StaticEnum, entry.DataType);
			CollectionAssert.AreEqual(new[] { "Happy", "Sad" }, entry.staticEnums);
			Assert.IsNotNull(entry.schema);
			Assert.IsFalse(entry.optional);
			Assert.AreEqual("how it feels", entry.description);
		}

		[Test]
		public void StaticEnumEntriesCanBeOptional()
		{
			Assert.IsTrue(SchemaEntry.StaticEnumEntry<Mood>("mood", required: false).optional);
		}

		[Test]
		public void ADynamicEnumEntryAsksItsProviderForTheNames()
		{
			_provider = ScriptableObject.CreateInstance<TestEnumProvider>();
			_provider.values = new[] { "alpha", "beta" };

			var entry = new SchemaEntry(JSONSchema.DataType.DynamicEnum, "title").SetEnumProvider(_provider);

			CollectionAssert.AreEqual(new[] { "alpha", "beta" },
				Property(SchemaWith(entry), "title")["enum"].ToObject<string[]>());
		}

		[Test]
		public void ADynamicEnumProviderThatIsNotASerializableObjectIsRejected()
		{
			Assert.Throws<Exception>(() => SchemaEntry.DynamicEnumEntry(new PlainEnumProvider(), "title"));
		}

		[Test]
		public void AnOptionalEntryIsLeftOutOfTheRequiredList()
		{
			var required = new SchemaEntry(JSONSchema.DataType.String, "kept");
			var optional = new SchemaEntry(JSONSchema.DataType.String, "dropped", required: false);

			var json = SchemaWith(required, optional).Serialize();

			CollectionAssert.AreEqual(new[] { "kept" }, json["required"].ToObject<string[]>());
		}

		[Test]
		public void ADynamicEnumIsNotListedAsRequired()
		{
			_provider = ScriptableObject.CreateInstance<TestEnumProvider>();
			var entry = new SchemaEntry(JSONSchema.DataType.DynamicEnum, "title", required: false)
				.SetEnumProvider(_provider);

			var json = SchemaWith(new SchemaEntry(JSONSchema.DataType.String, "kept"), entry).Serialize();

			CollectionAssert.AreEqual(new[] { "kept" }, json["required"].ToObject<string[]>());
		}

		[Test]
		public void ACopyDoesNotShareItsSchema()
		{
			var source = new SchemaEntry(JSONSchema.DataType.Object, "details", "the details");
			source.schema.Add(JSONSchema.DataType.String, "value", "");

			var copy = new SchemaEntry(source);

			Assert.AreNotSame(source.schema, copy.schema);
			Assert.AreEqual("details", copy.name);
			Assert.AreEqual("the details", copy.description);
			Assert.AreEqual(source.optional, copy.optional);
			Assert.IsTrue(JToken.DeepEquals(source.schema.Serialize(), copy.schema.Serialize()));

			source.schema.entries[0].name = "changed";

			Assert.AreEqual("value", copy.schema.entries[0].name);
		}

		[Test]
		public void ACopyOfAnEmptySchemaKeepsOnlyItsType()
		{
			var source = new SchemaEntry(JSONSchema.DataType.Number, "count");

			var copy = new SchemaEntry(source);

			Assert.AreNotSame(source.schema, copy.schema);
			Assert.AreEqual(JSONSchema.DataType.Number, copy.DataType);
			Assert.AreEqual("count", copy.name);
		}

		[Test]
		public void ACopyKeepsTheStaticEnumNames()
		{
			var source = SchemaEntry.StaticEnumEntry<Mood>("mood");

			var copy = new SchemaEntry(source);

			CollectionAssert.AreEqual(source.staticEnums, copy.staticEnums);
			Assert.AreEqual(JSONSchema.DataType.StaticEnum, copy.DataType);
		}

		[Test]
		public void AnEntryWithoutASchemaFallsBackToAStringType()
		{
			var entry = new SchemaEntry((JSONSchema)null, "x");

			Assert.IsNull(entry.schema);
			Assert.AreEqual(JSONSchema.DataType.String, entry.DataType);
			Assert.IsNull(Property(SchemaWith(entry), "x")["type"]);
		}

		[Test]
		public void DeserializingAnEntryWithoutASchemaBuildsADefaultObjectSchema()
		{
			var entry = new SchemaEntry((JSONSchema)null, "x");

			((ISerializationCallbackReceiver)entry).OnAfterDeserialize();

			Assert.IsNotNull(entry.schema);
			Assert.AreEqual(JSONSchema.DataType.Object, entry.DataType);
		}

		[Test]
		public void TheSchemaOfAnEntryCanBeReplacedThroughItsConstructor()
		{
			var schema = new JSONSchema(JSONSchema.DataType.Object);
			schema.Add(JSONSchema.DataType.String, "value", "");

			var entry = new SchemaEntry(schema, "details", "the details", required: false);

			Assert.AreSame(schema, entry.schema);
			Assert.IsTrue(entry.optional);
			Assert.AreEqual("object", Property(SchemaWith(entry), "details")["type"].ToString());
		}
	}
}
