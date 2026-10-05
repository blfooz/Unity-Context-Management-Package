using System.Text;
using ContextManagement;
using UnityEngine;

public class CharacterManager : LLMEntityManager<Character>, IContextProvider
{
	public override string EntityName => "character";


	protected override bool EnableBaseTools => true;
	protected override bool EnableAddendumTools => true;

	public override string GetPrompt()
	{
		StringBuilder sb = new();

		sb.AppendLine("# Characters");
		sb.AppendLine("Call the add_character tool to record new recurring characters in the narrative.");
		return sb.ToString();
	}


	public string ContextDescription => "List the current characters.";
	public bool TryGetContext(LLMRequestManager caller, out string context)
	{
		StringBuilder sb = new();

		sb.AppendLine("# Characters");
		foreach (var character in GetAll())
		{
			sb.AppendLine("```");
			sb.AppendLine(character.ToString());
			sb.AppendLine("```");
		}

		context = sb.ToString();
		return true;
	}
}