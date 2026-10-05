using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace ContextManagement.Tests
{
	[TestFixture]
	public class JSONSchemaTests
	{
		public enum Mood
		{
			Happy,
			Sad,
		}

		public class Nested
		{
			public string Value;
		}

		public class Plain
		{
			public string Name;
			public int Count;
			public float Ratio;
			public bool Flag;
			public Mood Feeling;

			[SchemaDescription("A documented field")]
			public string Documented;

			[SchemaOptional]
			public string Note;

			[SchemaNonSerialized]
			public string Cached;

			public Nested Skipped;

			[SchemaSerialize]
			public Nested Details;

			public List<int> Numbers;
			public string[] Tags;

			[EntityIdentifierField]
			public string Id;
		}

		public class Ambiguous
		{
			[EntityIdentifierField]
			public string First;

			[EntityIdentifierField]
			public string Second;
		}

		public class Node
		{
			public string Name;

			[SchemaSerialize]
			public Node Next;
		}

		[TearDown]
		public void TearDown() => LogAssert.ignoreFailingMessages = false;

		private static SchemaEntry Entry(JSONSchema schema, string name)
			=> schema.entries.Find(entry => entry.name == name);

		//==========================================================
		//  Reading a type
		//==========================================================

		[Test]
		public void ReadsThePublicFieldsOfAType()
		{
			var names = JSONSchema.FromType<Plain>().entries.Select(entry => entry.name).ToArray();

			CollectionAssert.Contains(names, "Name");
			CollectionAssert.Contains(names, "Feeling");
			CollectionAssert.Contains(names, "Numbers");
			CollectionAssert.Contains(names, "Tags");
			CollectionAssert.Contains(names, "Details");
			CollectionAssert.Contains(names, "Id");
			CollectionAssert.DoesNotContain(names, "Cached");
			CollectionAssert.DoesNotContain(names, "Skipped");
		}

		[Test]
		public void MapsFieldTypesToDataTypes()
		{
			var schema = JSONSchema.FromType<Plain>();

			Assert.AreEqual(JSONSchema.DataType.String, Entry(schema, "Name").DataType);
			Assert.AreEqual(JSONSchema.DataType.Number, Entry(schema, "Count").DataType);
			Assert.AreEqual(JSONSchema.DataType.Number, Entry(schema, "Ratio").DataType);
			Assert.AreEqual(JSONSchema.DataType.Boolean, Entry(schema, "Flag").DataType);
			Assert.AreEqual(JSONSchema.DataType.StaticEnum, Entry(schema, "Feeling").DataType);
			Assert.AreEqual(JSONSchema.DataType.Array, Entry(schema, "Numbers").DataType);
			Assert.AreEqual(JSONSchema.DataType.Object, Entry(schema, "Details").DataType);
			Assert.AreEqual(JSONSchema.DataType.String, Entry(schema, "Id").DataType);
		}

		[Test]
		public void RecordsEnumsArraysDescriptionsAndOptionalFields()
		{
			var schema = JSONSchema.FromType<Plain>();

			CollectionAssert.AreEqual(new[] { "Happy", "Sad" }, Entry(schema, "Feeling").staticEnums);
			Assert.AreEqual(JSONSchema.DataType.Number, Entry(schema, "Numbers").schema.ArrayType);
			Assert.AreEqual(JSONSchema.DataType.String, Entry(schema, "Tags").schema.ArrayType);

			Assert.AreEqual("A documented field", Entry(schema, "Documented").description);
			Assert.IsTrue(Entry(schema, "Note").optional);
			Assert.IsFalse(Entry(schema, "Name").optional);

			Assert.IsFalse(schema.IsEmpty());
			Assert.IsTrue(schema.TypeLocked());
		}

		[Test]
		public void RecursionOnlyFollowsAnnotatedReferenceFields()
		{
			var schema = JSONSchema.FromType<Plain>();

			Assert.IsNotNull(Entry(schema, "Details").schema);
			Assert.AreEqual("Value", Entry(schema, "Details").schema.entries.Single().name);
		}

		[Test]
		public void TheIdentifierIsRecordedWhenExactlyOneFieldCarriesIt()
		{
			Assert.AreEqual("Id", JSONSchema.FromType<Plain>().identifierField);
			Assert.IsNull(JSONSchema.FromType<Nested>().identifierField);
		}

		[Test]
		public void AnEntitySchemaRequiresAnIdentifier()
		{
			Assert.IsNotNull(JSONSchema.FromTypeWithId<Plain>());

			LogAssert.ignoreFailingMessages = true;
			Assert.IsNull(JSONSchema.FromTypeWithId<Nested>());
		}

		[Test]
		public void AnAmbiguousIdentifierLeavesTheFieldUnset()
		{
			LogAssert.ignoreFailingMessages = true;

			Assert.IsNull(JSONSchema.FromType<Ambiguous>().identifierField);
			Assert.IsNull(JSONSchema.FromTypeWithId<Ambiguous>());
		}

		[Test]
		public void CircularReferencesAreDroppedInsteadOfRecursingForever()
		{
			LogAssert.ignoreFailingMessages = true;

			var schema = JSONSchema.FromType<Node>();

			Assert.IsNotNull(schema);
			Assert.AreEqual(2, schema.entries.Count);
			Assert.IsNull(Entry(schema, "Next").schema);
			Assert.DoesNotThrow(() => schema.Serialize());
		}

		//==========================================================
		//  Serializing
		//==========================================================

		[Test]
		public void ObjectSchemasUseSnakeCaseKeysAndListTheRequiredProperties()
		{
			var schema = new JSONSchema();
			schema.Add(JSONSchema.DataType.String, "displayName", "the name");
			schema.Add(JSONSchema.DataType.Number, "age", "the age", required: false);

			var json = schema.Serialize();

			Assert.AreEqual("object", json["type"].ToString());
			Assert.AreEqual("string", json["properties"]["display_name"]["type"].ToString());
			Assert.AreEqual("the name", json["properties"]["display_name"]["description"].ToString());
			CollectionAssert.AreEqual(new[] { "display_name" }, json["required"].ToObject<string[]>());
		}

		[Test]
		public void ASchemaWithoutOptionalFieldsHasNoRequiredList()
		{
			var schema = new JSONSchema();
			schema.Add(JSONSchema.DataType.String, "name", "");

			Assert.IsNull(schema.Serialize()["required"]);
		}

		[Test]
		public void NonObjectSchemasSerializeToTheirType()
		{
			Assert.AreEqual("number", new JSONSchema(JSONSchema.DataType.Number).Serialize()["type"].ToString());
			Assert.AreEqual("boolean", new JSONSchema(JSONSchema.DataType.Boolean).Serialize()["type"].ToString());

			var array = new JSONSchema(JSONSchema.DataType.Array);
			array.Add(new SchemaEntry(JSONSchema.DataType.String, "_array_type"));
			Assert.AreEqual("string", array.Serialize()["type"].ToString());
		}

		[Test]
		public void NestedObjectsArraysAndEnumsSerializeRecursively()
		{
			var json = JSONSchema.FromType<Plain>().Serialize();

			Assert.AreEqual("object", json["properties"]["details"]["type"].ToString());
			Assert.AreEqual("string", json["properties"]["details"]["properties"]["value"]["type"].ToString());

			Assert.AreEqual("array", json["properties"]["numbers"]["type"].ToString());
			Assert.AreEqual("number", json["properties"]["numbers"]["items"]["type"].ToString());
			Assert.AreEqual("string", json["properties"]["tags"]["items"]["type"].ToString());

			CollectionAssert.AreEqual(new[] { "Happy", "Sad" },
				json["properties"]["feeling"]["enum"].ToObject<string[]>());
			Assert.IsNull(json["properties"]["feeling"]["type"]);
		}

		[Test]
		public void CollectionsOfPrimitivesKeepTheirElementType()
		{
			var schema = JSONSchema.FromType<Plain>();
			var json = schema.Serialize();

			Assert.AreEqual(JSONSchema.DataType.Number, Entry(schema, "Numbers").schema.ArrayType);
			Assert.AreEqual(JSONSchema.DataType.String, Entry(schema, "Tags").schema.ArrayType);
			Assert.AreEqual("number", json["properties"]["numbers"]["items"]["type"].ToString());
			Assert.AreEqual("string", json["properties"]["tags"]["items"]["type"].ToString());
		}

		public class WithEnums
		{
			public List<Mood> Moods;
		}

		[Test]
		public void CollectionsOfEnumsCarryTheirNames()
		{
			var schema = JSONSchema.FromType<WithEnums>();

			Assert.AreEqual(JSONSchema.DataType.StaticEnum, Entry(schema, "Moods").schema.ArrayType);
			CollectionAssert.AreEqual(new[] { "Happy", "Sad" },
				schema.Serialize()["properties"]["moods"]["items"]["enum"].ToObject<string[]>());
		}

		[Test]
		public void ACopyIsIndependentOfItsSource()
		{
			var source = JSONSchema.FromType<Plain>();
			var copy = new JSONSchema(source);

			Assert.IsTrue(JToken.DeepEquals(source.Serialize(), copy.Serialize()));
			Assert.AreNotSame(source.entries[0], copy.entries[0]);
			Assert.AreEqual(source.identifierField, copy.identifierField);
			Assert.AreEqual(source.type, copy.type);
			Assert.AreEqual(source.TypeLocked(), copy.TypeLocked());

			source.entries[0].description = "changed";
			source.entries[0].schema.type = JSONSchema.DataType.Boolean;

			Assert.AreNotEqual("changed", copy.entries[0].description);
			Assert.AreEqual(JSONSchema.DataType.String, copy.entries[0].schema.type);
		}

		[Test]
		public void ASchemaCanBeIteratedAndMeasured()
		{
			var schema = new JSONSchema();
			Assert.IsTrue(schema.IsEmpty());
			Assert.IsFalse(schema.TypeLocked());
			Assert.IsNull(schema.ArrayType);

			schema.Add(new SchemaEntry(JSONSchema.DataType.String, "x"));

			Assert.IsFalse(schema.IsEmpty());
			Assert.AreEqual(1, schema.Count());
			Assert.AreEqual(schema.Serialize().ToString(), schema.ToString());

			Assert.IsTrue(new JSONSchema(JSONSchema.DataType.Object, lockType: true).TypeLocked());
		}

		//==========================================================
		//  Converting data
		//==========================================================

		[Test]
		public void ConvertsSnakeCaseDataIntoATypedObject()
		{
			var json = JObject.Parse(
				"{\"name\":\"Ada\",\"count\":3,\"ratio\":1.5,\"flag\":true,\"feeling\":\"Sad\"," +
				"\"documented\":\"doc\",\"details\":{\"value\":\"inner\"}," +
				"\"numbers\":[1,2],\"tags\":[\"x\"],\"id\":\"p1\"}");

			var plain = JSONSchema.FromType<Plain>().ToObject<Plain>(json);

			Assert.AreEqual("Ada", plain.Name);
			Assert.AreEqual(3, plain.Count);
			Assert.AreEqual(1.5f, plain.Ratio);
			Assert.IsTrue(plain.Flag);
			Assert.AreEqual(Mood.Sad, plain.Feeling);
			Assert.AreEqual("doc", plain.Documented);
			Assert.AreEqual("inner", plain.Details.Value);
			CollectionAssert.AreEqual(new[] { 1, 2 }, plain.Numbers);
			CollectionAssert.AreEqual(new[] { "x" }, plain.Tags);
			Assert.AreEqual("p1", plain.Id);
			Assert.IsNull(plain.Note);
		}

		[Test]
		public void MissingRequiredDataIsReported()
		{
			LogAssert.ignoreFailingMessages = true;

			var schema = JSONSchema.FromType<Nested>();

			Assert.Throws<Exception>(() => schema.ToObject<Nested>(new JObject()));
		}

		[Test]
		public void OverwritingKeepsFieldsTheDataDoesNotMention()
		{
			var schema = JSONSchema.FromType<Nested>();
			var target = new Nested { Value = "original" };

			schema.OverwriteObject(target, new JObject());
			Assert.AreEqual("original", target.Value);

			schema.OverwriteObject(target, JObject.Parse("{\"value\":\"updated\"}"));
			Assert.AreEqual("updated", target.Value);
		}

		[Test]
		public void OverwritingRecursesIntoNestedObjectsInPlace()
		{
			var schema = JSONSchema.FromType<Holder>();
			var target = new Holder { Details = new Nested { Value = "original" } };
			var reference = target.Details;

			schema.OverwriteObject(target, JObject.Parse("{\"details\":{\"value\":\"updated\"}}"));

			Assert.AreSame(reference, target.Details);
			Assert.AreEqual("updated", target.Details.Value);
		}

		[Test]
		public void OverwritingCreatesANestedObjectThatIsMissing()
		{
			var schema = JSONSchema.FromType<Holder>();
			var target = new Holder();

			schema.OverwriteObject(target, JObject.Parse("{\"details\":{\"value\":\"created\"}}"));

			Assert.IsNotNull(target.Details);
			Assert.AreEqual("created", target.Details.Value);
		}

		private class Holder
		{
			[SchemaSerialize]
			public Nested Details;
		}
	}
}
