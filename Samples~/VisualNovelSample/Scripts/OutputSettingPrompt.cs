using ContextManagement;
using UnityEngine;

public class OutputSettingPrompt : PromptComponent
{
	public SystemLanguage outputLanguage;
	public override string GetPrompt()
		=> $"# Output Setting\nLanguage: {outputLanguage}";
}