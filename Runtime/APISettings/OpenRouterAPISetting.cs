using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;

namespace ContextManagement
{
	/// <summary>OpenRouter's chat-completion endpoint.</summary>
	public class OpenRouterAPISetting : APISetting
	{
		[Tooltip("The HTTP referer sent to OpenRouter.")]
		public string httpReferer = "";
		[Tooltip("The application title sent to OpenRouter.")]
		public string xTitle = "";
		[Tooltip("Ask OpenRouter to return the model's reasoning.")]
		public bool reasoning;

		/// <inheritdoc />
		public override string BaseUrl => "https://openrouter.ai/api/v1";

		/// <inheritdoc />
		protected override UnityWebRequest AssembleRequest(JObject body)
		{
			var request = base.AssembleRequest(body);

			if (!string.IsNullOrWhiteSpace(httpReferer))
				request.SetRequestHeader("HTTP-Referer", httpReferer);
			if (!string.IsNullOrWhiteSpace(xTitle))
				request.SetRequestHeader("X-OpenRouter-Title", xTitle);

			return request;
		}

		/// <inheritdoc />
		protected override JObject ExtraBody
		{
			get
			{
				var body = base.ExtraBody;
				body["thinking"] = new JObject { ["type"] = reasoning ? "enabled" : "disabled" };
				return body;
			}
		}
	}
}
