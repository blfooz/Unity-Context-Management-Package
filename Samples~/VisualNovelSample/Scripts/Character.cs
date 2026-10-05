
using System;
using System.Text;
using ContextManagement;
using UnityEngine;

[Serializable]
public class Character
{
	[EntityIdentifierField, SchemaDescription("Unique name of the character, e.g. 'gandalf'")]
	public string name;
	public Gender gender;

	[TextArea, SchemaDescription("Character's pesonality, reflect the character's inner motives.")]
	public string personality;

	[TextArea, SchemaDescription("Character's demeanor, including language style, posture, overall vibe, etc.")]
	public string demeanor;

	public enum Gender
	{
		male,
		female
	}

	public override string ToString()
	{
		StringBuilder sb = new();
		sb.AppendLine($"{name}-{gender}");
		sb.AppendLine("Personality: " + personality);
		sb.AppendLine("Demeanor: " + demeanor);
		return sb.ToString();
	}
}