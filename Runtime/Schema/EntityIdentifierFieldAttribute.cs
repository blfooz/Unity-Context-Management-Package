using System;

namespace ContextManagement
{
	/// <summary>
	/// Mark exactly one public field on an entity data class as the entity's
	/// identifier. The marked field's name and <see cref="SchemaDescriptionAttribute"/>
	/// description are what the LLM sees and must provide for the
	/// add/modify/delete tools exposed by <see cref="EntityManager{T}"/> —
	/// replacing the generic injected "id" with a semantically meaningful key.
	/// </summary>
	/// <example>
	/// <code>
	/// public class CharacterData
	/// {
	///     [EntityIdentifierField]
	///     [SchemaDescription("Unique character name, e.g. 'gandalf'")]
	///     public string name;
	/// }
	/// </code>
	/// </example>
	/// <remarks>
	/// Enforced at runtime by <see cref="JSONSchema.FromTypeWithId{T}"/>: a type
	/// used as an entity schema must have exactly one field carrying this attribute,
	/// otherwise the schema is invalid and an error is logged. Plain data classes
	/// built with <see cref="JSONSchema.FromType{T}"/> are not subject to this rule.
	/// </remarks>
	[AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
	public class EntityIdentifierFieldAttribute : Attribute
	{
	}
}
