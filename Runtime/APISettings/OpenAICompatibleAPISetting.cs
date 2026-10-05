using UnityEngine;

namespace ContextManagement
{
	/// <summary>An OpenAI-compatible endpoint whose base URL is set in the inspector.</summary>
	public class OpenAICompatibleAPISetting : APISetting
	{
		[Tooltip("Base URL of the OpenAI-compatible endpoint.")]
		public string baseUrl = "";

		/// <inheritdoc />
		public override string BaseUrl => baseUrl;
	}
}
