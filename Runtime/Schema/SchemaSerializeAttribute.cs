using System;

namespace ContextManagement
{
	/// <summary>
	/// Denote a reference type field to be serialized by JSONSchema
	/// </summary>
	[AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
	public class SchemaSerializeAttribute : Attribute { }
}
