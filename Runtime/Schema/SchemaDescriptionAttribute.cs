using System;

namespace ContextManagement
{
	/// <summary>
	/// Attach to public fields on a data model class to provide
	/// a human-readable description that appears in the JSON schema
	/// sent to the LLM. Read by <see cref="JSONSchema.FromType"/>.
	/// </summary>
	/// <example>
	/// <code>
	/// public class CharacterData
	/// {
	///     [SchemaDescription("The character's display name.")]
	///     public string name;
	///
	///     [SchemaDescription("Current experience level (1-100).")]
	///     public int level;
	/// }
	/// </code>
	/// </example>
	[AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
	public class SchemaDescriptionAttribute : Attribute
	{
		public string Description { get; }

		public SchemaDescriptionAttribute(string description)
		{
			Description = description;
		}
	}
}
