using UnityEngine;

namespace ContextManagement
{
	/// <summary>
	/// Put on the same game object as a LLMRequestManager to supply prompt to it.
	/// </summary>
	public abstract class PromptComponent : MonoBehaviour
	{
		public abstract string GetPrompt();

		public override string ToString() => GetPrompt();
	}
}