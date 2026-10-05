using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace ContextManagement
{
	[Serializable]
	public class JSONSchema : IEnumerable<SchemaEntry>
	{
		const BindingFlags publicAndNonStatic = BindingFlags.Public | BindingFlags.Instance;

		public DataType type;

		public DataType? ArrayType => type == DataType.Array ? entries[0].DataType : null;

		[SerializeField]
		bool lockType;

		/// <summary>
		/// Name of the field on the data class that serves as the entity identifier.
		/// Recorded by <see cref="FromTypeWithId{T}"/> (or <see cref="FromType{T}"/>)
		/// when exactly one field carries <see cref="EntityIdentifierFieldAttribute"/>;
		/// null when the type has no explicit identifier (e.g. plain nested data classes)
		/// or the annotation is ambiguous.
		/// </summary>
		public string identifierField;

		public bool TypeLocked() => lockType;
		public List<SchemaEntry> entries = new();

		public bool IsEmpty() => entries.Count == 0;

		public JSONSchema(JSONSchema copyFrom)
		{
			type = copyFrom.type;
			lockType = copyFrom.lockType;
			identifierField = copyFrom.identifierField;
			foreach (var entry in copyFrom)
			{
				entries.Add(new(entry));
			}
		}

		public JSONSchema(DataType type = DataType.Object, bool lockType = false)
		{
			this.type = type;
			this.lockType = lockType;
		}

		public void Add(SchemaEntry entry) { entries.Add(entry); }

		public void Add(DataType type, string name, string description, bool required = true)
		{
			entries.Add(new(type, name, description) { optional = !required });
		}

		/// <summary>
		/// Extract a JSONSchema from the given class. General-purpose: no
		/// identifier is required, though an [EntityIdentifierField] is recorded
		/// when exactly one is present. For entity data classes that must declare
		/// an identifier, use <see cref="FromTypeWithId{T}"/> instead.
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <returns></returns>
		public static JSONSchema FromType<T>() => FromType(typeof(T));

		/// <summary>
		/// General-purpose entry point. Unlike <see cref="FromTypeWithId(Type)"/>,
		/// this does not require an [EntityIdentifierField] — the schema is valid
		/// for any plain data class. An identifier is recorded only when exactly
		/// one field is annotated; multiple annotations log a warning and leave
		/// <see cref="identifierField"/> null (ambiguous).
		/// </summary>
		public static JSONSchema FromType(Type type) => FromType(type, new());

		/// <summary>
		/// Entity entry point. Requires exactly one field annotated with
		/// <see cref="EntityIdentifierFieldAttribute"/>; returns null (with a
		/// logged error) when the identifier is missing or ambiguous. Nested data
		/// classes recursed into via [SchemaSerialize] are exempt — only the root
		/// entity type needs an identifier.
		/// </summary>
		public static JSONSchema FromTypeWithId<T>() => FromTypeWithId(typeof(T));

		/// <summary>
		/// Entity entry point (non-generic). See <see cref="FromTypeWithId{T}"/>.
		/// </summary>
		public static JSONSchema FromTypeWithId(Type type)
		{
			var schema = FromType(type, new());
			if (schema == null) return null;

			if (schema.identifierField == null)
			{
				Debug.LogError(
					$"Type '{type.Name}' is used as an entity schema but must have exactly one field " +
					"annotated with [EntityIdentifierField] to define the entity identifier.");
				return null;
			}

			return schema;
		}

		/// <summary>
		/// Internal recursive overload.  <paramref name="except"/> guards against
		/// circular references (A → B → A); a cycle returns null and warning.
		/// </summary>
		static JSONSchema FromType(Type type, HashSet<Type> except)
		{
			if (!except.Add(type))
			{
				Debug.LogWarning("Circular type references.");
				return null;
			}

			JSONSchema schema = new(DataType.Object, true);
			var fields = type.GetFields(publicAndNonStatic);

			foreach (var field in fields)
			{
				if (field.GetCustomAttribute<SchemaNonSerializedAttribute>() != null)
					continue;

				string[] enumNames = null;
				JSONSchema entrySchema = null;

				if (field.FieldType.IsEnum)
				{
					entrySchema = new(DataType.StaticEnum);
					enumNames = field.FieldType.GetEnumNames();
				}
				else if (ContextUtility.IsTypeCollection(field.FieldType, out Type elementType))
				{
					entrySchema = new(DataType.Array) { ElementSchema(elementType, except) };
				}
				else
				{
					var entryType = ToJSONDataType(field.FieldType);

					if (entryType == DataType.Object)
					{
						// Reference-type fields need explicit [SchemaExpand] to recurse
						if (field.GetCustomAttribute<SchemaSerializeAttribute>() != null)
							entrySchema = FromType(field.FieldType, new HashSet<Type>(except));
						else continue;
					}
					else entrySchema = new(entryType);
				}

				var entry = new SchemaEntry(entrySchema, field.Name,
				field.GetCustomAttribute<SchemaDescriptionAttribute>()?.Description ?? "",
				field.GetCustomAttribute<SchemaOptionalAttribute>() == null)
				{ staticEnums = enumNames };

				schema.entries.Add(entry);

				if (field.GetCustomAttribute<EntityIdentifierFieldAttribute>() != null)
				{
					if (schema.identifierField != null)
					{
						Debug.LogWarning(
							$"Type '{type.Name}' has multiple [EntityIdentifierField] fields " +
							$"('{schema.identifierField}' and '{field.Name}'). " +
							"The identifier is ambiguous and will be left unset.");
						schema.identifierField = null;
					}
					else
					{
						schema.identifierField = field.Name;
					}
				}
			}

			return schema;
		}

		/// <summary>
		/// Schema for elements of a collection, guarded against circular references.
		/// </summary>
		static SchemaEntry ElementSchema(Type elementType, HashSet<Type> except)
		{
			if (elementType.IsEnum)
				return new(DataType.StaticEnum, "_array_type")
				{
					staticEnums = elementType.GetEnumNames()
				};

			var dataType = ToJSONDataType(elementType);
			return dataType == DataType.Object
				? new(FromType(elementType, except), "_array_type")
				: new(dataType, "_array_type");
		}

		/// <summary>
		/// The data type of a value, or <see cref="DataType.Object"/> for a type that
		/// has to be described by its fields.
		/// </summary>
		static DataType ToJSONDataType(Type type) => Type.GetTypeCode(type) switch
		{
			TypeCode.Int32 or TypeCode.Int64 or TypeCode.Single
				or TypeCode.Double or TypeCode.Decimal => DataType.Number,
			TypeCode.Boolean => DataType.Boolean,
			TypeCode.String => DataType.String,
			_ => DataType.Object
		};

		/// <summary>
		/// snake case keys are expected, only fields recorded in the schema will be converted.
		/// </summary>
		public T ToObject<T>(JObject data) => (T)ToObjectInternal(data, typeof(T));

		/// <summary>
		/// Overwrites fields on an existing object with values from the JObject data.
		/// The data can be partial — only present fields are overwritten; missing fields are left intact.
		/// Nested objects are recursed into (overwritten in-place) rather than replaced.
		/// </summary>
		public void OverwriteObject<T>(T target, JObject data, bool reportMissingFields = false)
			=> OverwriteObjectInternal(target, data, typeof(T), reportMissingFields);

		/// <summary>
		/// Non-generic recursive entry point for overwriting.  Recurses into nested
		/// objects so existing sub-object references are preserved.
		/// </summary>
		void OverwriteObjectInternal(object target, JObject data, Type targetType, bool reportMissingFields)
		{
			foreach (var entry in entries)
			{
				var field = targetType.GetField(entry.name);
				if (field == null)
				{
					if (!entry.optional)
						Debug.LogError($"Required field: {entry.name} is missing on the overwrite target.");
					continue;
				}

				if (!TryGetDataValue(data, entry.name, out var value))
				{
					if (reportMissingFields)
						Debug.LogWarning($"Field: {entry.name} is missing from data.");
					continue;
				}

				SetFieldValue(target, field, value, entry.schema, reportMissingFields);
			}
		}

		/// <summary>
		/// Non-generic recursive entry point.  Handles nested objects, arrays,
		/// and enums before delegating leaf conversion to Newtonsoft.
		/// </summary>
		object ToObjectInternal(JObject data, Type targetType)
		{
			JObject compiled = new();
			StringBuilder errorMsg = new("Error:");
			foreach (var entry in entries)
			{
				var field = targetType.GetField(entry.name);
				if (field == null)
				{
					if (!entry.optional)
					{
						Debug.LogError($"Required field: {entry.name} is missing on the casting target.");
						errorMsg.AppendLine($"Required field: {entry.name} is missing on the casting target.");
					}
					continue;
				}

				if (!TryGetDataValue(data, entry.name, out var value))
				{
					if (!entry.optional)
					{
						Debug.LogError($"Required field: {entry.name} is missing from data.");
						errorMsg.AppendLine($"Required field: {entry.name} is missing from data.");
					}
					continue;
				}

				compiled[entry.name] = ConvertValue(value, entry.schema, field.FieldType);
			}
			if (errorMsg.Length > 6) throw new Exception(errorMsg.ToString());
			return compiled.ToObject(targetType);
		}

		static bool TryGetDataValue(JObject data, string key, out JToken value)
		{
			return data.TryGetValue(ContextUtility.ToSnakeCase(key), out value)
				|| data.TryGetValue(key, out value);
		}

		static JToken ConvertValue(JToken value, JSONSchema schema, Type fieldType)
		{
			if (schema == null || value == null) return value;

			return schema.type switch
			{
				DataType.Object when value is JObject nested && !schema.IsEmpty() =>
					JToken.FromObject(schema.ToObjectInternal(nested, fieldType)),

				DataType.Array when value is JArray arr =>
					ConvertArray(arr, schema, fieldType),

				DataType.StaticEnum when value.Type == JTokenType.String =>
					new JValue(Enum.Parse(fieldType, value.ToString())),

				_ => value
			};
		}

		static JArray ConvertArray(JArray arr, JSONSchema arraySchema, Type collectionType)
		{
			if (arraySchema.entries == null || arraySchema.entries.Count == 0) return arr;

			var itemSchema = arraySchema.entries[0]?.schema;
			var elementType = GetCollectionElementType(collectionType);
			if (itemSchema == null || elementType == null) return arr;

			JArray result = new();
			foreach (var item in arr)
				result.Add(ConvertValue(item, itemSchema, elementType));
			return result;
		}

		static void SetFieldValue(object target, FieldInfo field, JToken value, JSONSchema schema, bool reportMissingFields)
		{
			if (schema == null || value == null)
			{
				field.SetValue(target, value?.ToObject(field.FieldType));
				return;
			}

			switch (schema.type)
			{
				case DataType.Object when value is JObject nested && !schema.IsEmpty():
					var existing = field.GetValue(target);
					existing ??= Activator.CreateInstance(field.FieldType);
					schema.OverwriteObjectInternal(existing, nested, field.FieldType, reportMissingFields);
					field.SetValue(target, existing);
					break;

				case DataType.Array when value is JArray arr:
					field.SetValue(target, ConvertArrayToTarget(arr, schema, field.FieldType));
					break;

				case DataType.StaticEnum when value.Type == JTokenType.String:
					field.SetValue(target, Enum.Parse(field.FieldType, value.ToString()));
					break;

				default:
					field.SetValue(target, value.ToObject(field.FieldType));
					break;
			}
		}

		static object ConvertArrayToTarget(JArray arr, JSONSchema arraySchema, Type collectionType)
		{
			if (arraySchema.entries == null || arraySchema.entries.Count == 0)
				return arr.ToObject(collectionType);

			var itemSchema = arraySchema.entries[0]?.schema;
			var elementType = GetCollectionElementType(collectionType);
			if (itemSchema == null || elementType == null)
				return arr.ToObject(collectionType);

			JArray result = new();
			foreach (var item in arr)
				result.Add(ConvertValue(item, itemSchema, elementType));
			return result.ToObject(collectionType);
		}

		static Type GetCollectionElementType(Type collectionType)
		{
			if (collectionType.IsArray) return collectionType.GetElementType();
			if (collectionType.IsGenericType) return collectionType.GetGenericArguments()[0];
			return null;
		}

		public override string ToString() => Serialize().ToString();

		public JObject Serialize()
		{
			JObject props = new(), schema = new() { { "type", type.ToString().ToLower() } };

			if (type != DataType.Object && type != DataType.Array) return schema;

			bool needReqArr = false;
			JArray reqArr = new();

			if (type == DataType.Object)
			{
				if (entries != null)
				{
					foreach (var entry in entries)
					{
						try
						{
							var key = ContextUtility.ToSnakeCase(entry.name);
							props.Add(key, entry.Serialize());
							if (!entry.optional) reqArr.Add(key);
							else needReqArr = true;
						}
						catch (Exception e)
						{
							Debug.LogError(e);
						}
					}
				}
			}
			else if (type == DataType.Array)
			{
				bool hasEntry = entries != null && entries.Count > 0;
				bool hasArraySchema = hasEntry && entries[0] != null && entries[0].schema != null;
				if (hasArraySchema)
				{
					return entries[0].Serialize();
				}
				else Debug.LogError("Array field has no schema");
			}

			schema.Add(type == DataType.Object ? "properties" : "items", props);

			if (needReqArr) schema.Add("required", reqArr);

			return schema;
		}

		public IEnumerator<SchemaEntry> GetEnumerator()
		{
			foreach (var entry in entries)
				yield return entry;
		}

		IEnumerator IEnumerable.GetEnumerator() { return GetEnumerator(); }

		public enum DataType
		{
			String,
			Object,
			Number,
			Boolean,
			Array,
			DynamicEnum,
			StaticEnum,
		}
	}
}
