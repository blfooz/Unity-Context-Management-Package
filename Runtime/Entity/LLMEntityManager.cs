using System;
using System.Collections.Generic;
using ContextManagement;
using Newtonsoft.Json.Linq;
using UnityEngine;

/// <summary>
/// EntityManager with LLM-facing tools.
/// </summary>
/// <typeparam name="T"></typeparam>
public abstract class LLMEntityManager<T> : EntityManager<T>, IToolProvider where T : class, new()
{
	/// <summary>
	/// Schema derived from <typeparamref name="T"/> via
	/// <see cref="JSONSchema.FromTypeWithId{T}"/>. Requires exactly one field
	/// annotated with <see cref="EntityIdentifierFieldAttribute"/>; when the data
	/// class is invalid this returns null and an error is logged (once).
	/// </summary>
	public JSONSchema Schema
	{
		get
		{
			if (_schemaCache == null && !_schemaInvalid)
			{
				_schemaCache = JSONSchema.FromTypeWithId<T>();
				_schemaInvalid = _schemaCache == null;
			}
			return _schemaCache;
		}
	}

	[NonSerialized] JSONSchema _schemaCache;
	[NonSerialized] bool _schemaInvalid;

	/// <summary>
	/// Name of the field that identifies an entity: the [EntityIdentifierField]
	/// on <typeparamref name="T"/>, falling back to the generic "id" when the
	/// data class has no explicit identifier.
	/// </summary>
	string IdentifierFieldName => ContextUtility.ToSnakeCase(Schema?.identifierField) ?? "id";

	protected abstract bool EnableBaseTools { get; }
	protected abstract bool EnableAddendumTools { get; }


	//Basic Operations as Tools

	public ToolDescriptor toolAdd;

	/// <summary>
	/// Attempt to cast the given JObject to an entity and add it.
	/// </summary>
	/// <param name="parameters"></param>
	/// <returns></returns>
	public string AddJson(JObject parameters)
	{
		try
		{
			var entity = Schema.ToObject<T>(parameters);

			if (!parameters.TryGetValue(IdentifierFieldName, out var id))
			{
				if (logOnOperations) Debug.Log($"Required {IdentifierFieldName} field missing");
				return $"Required {IdentifierFieldName} field missing";
			}
			if (Add(id.ToString(), entity)) return $"{id} successfully added.";
			else return $"{id} already exist:\n{entities[id.ToString()]}";
		}
		catch (Exception e)
		{
			return e.Message;
		}
	}

	public ToolDescriptor toolAddAddendum;
	public string ToolAddAddendum(JObject parameters)
	{
		if (!parameters.TryGetValue(IdentifierFieldName, out var id))
		{
			if (logOnOperations)
				Debug.Log($"Required {IdentifierFieldName} field missing");
			return $"Required {IdentifierFieldName} field missing";
		}

		if (!entities.TryGetValue(id.ToString(), out var entity))
		{
			if (logOnOperations) Debug.Log($"{id} does not exist.");
			return $"{id} does not exist.";
		}

		entity.addendums ??= new();
		entity.addendums.Add(parameters["addendum"].ToString());

		return $"Addendum added to {id}";
	}

	public ToolDescriptor toolModify;
	public string ToolModify(JObject parameters)
	{
		if (Schema == null) throw new Exception($"{EntityName} schema is invalid.");

		if (!parameters.TryGetValue(IdentifierFieldName, out var id))
		{
			if (logOnOperations)
				Debug.Log($"Required {IdentifierFieldName} field missing");
			return $"Required {IdentifierFieldName} field missing";
		}

		if (!entities.TryGetValue(id.ToString(), out var entity))
		{
			if (logOnOperations) Debug.Log($"{id} does not exist.");
			return $"{id} does not exist.";
		}

		Schema.OverwriteObject(entity.entity, parameters);

		if (logOnOperations) Debug.Log($"{id} successfully modified.");
		return $"{id} successfully modified.";
	}

	public ToolDescriptor toolDelete;
	public string ToolDelete(JObject parameters)
	{
		if (!parameters.TryGetValue(IdentifierFieldName, out var id))
		{
			if (logOnOperations) Debug.Log($"Required {IdentifierFieldName} field missing");
			return $"Required {IdentifierFieldName} field missing";
		}

		if (!Delete(id.ToString()))
		{
			if (logOnOperations) Debug.Log($"{id} does not exist.");
			return $"{id} does not exist.";
		}

		if (logOnOperations) Debug.Log($"{id} successfully deleted.");
		return $"{id} successfully deleted.";
	}

	protected virtual void BuildToolDescriptors()
	{
		string identifierField = Schema.identifierField;

		JSONSchema addSchema = new(Schema);
		if (identifierField == null) //Fallback
			addSchema.Add(JSONSchema.DataType.String, "id", $"unique identifier for {EntityNamePlural}.");
		toolAdd = new(new ToolInfo($"add_{EntityName}", addSchema, true,
			$"Create a new {EntityName}."), AddJson);

		JSONSchema modifySchema = new(Schema);
		foreach (var entry in modifySchema)
			if (entry.name != identifierField) entry.optional = true;
		toolModify = new(new($"modify_{EntityName}", modifySchema, true,
			$"Modify field(s) on an existing {EntityName}."), ToolModify);

		JSONSchema idOnly = new();
		if (identifierField != null)
		{
			var idEntry = Schema.entries.Find(e => e.name == identifierField);
			idOnly.Add(new(idEntry));
		}
		else idOnly.Add(JSONSchema.DataType.String, "id", $"unique identifier for {EntityNamePlural}.");
		toolDelete = new(new($"delete_{EntityName}", idOnly, true,
			$"Delete an existing {EntityName}."), ToolDelete);

		JSONSchema addendumSchema = new(idOnly)
		{
			{
				JSONSchema.DataType.String,
				"addendum",
				$"Additional text to attach to the {EntityName}."
			}
		};
		toolAddAddendum = new(new($"add_{EntityName}_addendum", addendumSchema, true,
			$"Append an addendum to an existing {EntityName}. Use this for important new informations."), ToolAddAddendum);
	}

	public virtual IEnumerable<ToolDescriptor> GetToolDescriptors()
	{
		if (toolAdd == null)
			BuildToolDescriptors();

		if (EnableBaseTools)
		{
			yield return toolAdd;
			yield return toolModify;
			yield return toolDelete;
		}
		if (EnableAddendumTools)
		{
			yield return toolAddAddendum;
		}
	}
}
