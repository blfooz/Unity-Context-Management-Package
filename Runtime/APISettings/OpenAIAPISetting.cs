namespace ContextManagement
{
	/// <summary>OpenAI's chat-completion endpoint.</summary>
	public class OpenAIAPISetting : APISetting
	{
		/// <inheritdoc />
		public override string BaseUrl => "https://api.openai.com/v1";
	}
}
