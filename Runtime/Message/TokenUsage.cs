using System;
using Newtonsoft.Json.Linq;

namespace ContextManagement
{
	[Serializable]
	public class TokenUsage
	{
		public int promptTokens;
		public int completionTokens;

		public static TokenUsage FromJSON(JObject json)
		{
			return new()
			{
				promptTokens = json["prompt_tokens"]?.ToObject<int>() ?? 0,
				completionTokens = json["completion_tokens"]?.ToObject<int>() ?? 0
			};
		}

		public JObject ToJSON() => new()
		{
			["prompt_tokens"] = promptTokens,
			["completion_tokens"] = completionTokens
		};
	}

}