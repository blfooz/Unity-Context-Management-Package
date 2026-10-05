using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;
using static ContextManagement.JSONSchema;
using Object = UnityEngine.Object;

namespace ContextManagement
{

	[Serializable]
	public class SchemaEntry : ISerializationCallbackReceiver
	{
		public string name;
		[TextArea]
		public string description;

		// NOTE: This field creates a recursive serialization cycle (JSONSchema ↔ SchemaEntry)
		// which exceeds Unity's serialization depth limit of 10, producing the warning:
		//   "Serialization depth limit 10 exceeded at 'ContextManagement::JSONSchema.type'"
		// This is intentional — LLM schemas rarely need >10 levels of nesting.
		public JSONSchema schema;

		public bool optional;

		public DataType DataType => schema != null ? schema.type : DataType.String;

		void ISerializationCallbackReceiver.OnAfterDeserialize()
		{
			schema ??= new JSONSchema();
		}

		void ISerializationCallbackReceiver.OnBeforeSerialize() { }

		public static SchemaEntry StaticEnumEntry<T>(string name, string description = "", bool required = true) where T : Enum
		{
			return new(DataType.StaticEnum, name, description, required)
			{ staticEnums = Enum.GetNames(typeof(T)) };
		}

		public static SchemaEntry DynamicEnumEntry(IEnumProvider enumProvider, string name, string description = "", bool required = true)
		{
			if (enumProvider is Object obj)
				return new(DataType.DynamicEnum, name, description, required) { enumProvider = obj };
			else
				throw new Exception("EnumProvider must inherit Unity Object to be serialized");
		}

		public SchemaEntry(SchemaEntry copyFrom)
		{
			name = copyFrom.name;
			description = copyFrom.description;
			if (!copyFrom.schema.IsEmpty())
				schema = new(copyFrom.schema);
			else schema = new(copyFrom.schema.type, copyFrom.schema.TypeLocked());
			optional = copyFrom.optional;
			staticEnums = copyFrom.staticEnums;
			enumProvider = copyFrom.enumProvider;
		}

		public SchemaEntry(DataType type, string name, string description = "", bool required = true)
		: this(new JSONSchema(type), name, description, required) { }

		public SchemaEntry(JSONSchema schema, string name, string description = "", bool required = true)
		{
			this.schema = schema;
			this.name = name;
			this.description = description;
			optional = !required;
		}
		//==========================================


		[SerializeField]
		public string[] staticEnums;
		public SchemaEntry SetStaticEnums(params string[] enums)
		{
			staticEnums = enums;
			return this;
		}
		[SerializeField]
		Object enumProvider;
		public SchemaEntry SetEnumProvider(IEnumProvider provider)
		{
			if (provider is Object obj)
				enumProvider = obj;
			return this;
		}

		List<string> GetEnums()
		{
			if (enumProvider is IEnumProvider provider)
			{
				return provider.GetEnums().ToList();
			}
			if (enumProvider is GameObject obj && obj.TryGetComponent<IEnumProvider>(out var p))
			{
				return p.GetEnums().ToList();
			}
			return null;
		}
		//=======================================================

		internal JObject Serialize()
		{
			JObject json = new();
			if (schema == null) return json;

			if (schema.type == DataType.Object)
				json = schema.Serialize();
			else if (schema.type == DataType.DynamicEnum)
				json.Add("enum", new JArray(GetEnums()));
			else if (schema.type == DataType.StaticEnum)
				json.Add("enum", new JArray(staticEnums));
			else
			{
				json.Add("type", schema.type switch
				{
					DataType.Number => "number",
					DataType.Boolean => "boolean",
					DataType.Array => "array",
					_ => "string"
				});
			}

			if (schema.type == DataType.Array)
				json.Add("items", schema.Serialize());

			if (!string.IsNullOrWhiteSpace(description))
				json.Add("description", description);

			return json;
		}
	}

}