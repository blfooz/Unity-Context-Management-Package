using System;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace ContextManagement
{
	[Serializable]
	public class Parameters
	{
		//temperature: 0-2, top_p: 0-1, API recommend only changing one of the two
		public float temperature = 1f;
		public float topP = 0.95f;
		public int maxTokens = 10000;
		//Range: -2-+2, penalize topics and exact wordings respectively
		public float presencePenalty = 0.1f, frequencyPenalty = 0;

		public int numberOfChoices = 1;

		public JObject ToRequestObject(JArray messages = null)
		{
			JObject reqObj = new()
			{
				{ "temperature", temperature },
				{ "top_p",topP },
				{ "presence_penalty", presencePenalty },
				{ "frequency_penalty", frequencyPenalty }
			};
			if (numberOfChoices > 0)
				reqObj.Add("n", numberOfChoices);
			else Debug.LogWarning("Invaild number of choices");
			if (maxTokens > 0) reqObj.Add("max_tokens", maxTokens);
			else Debug.LogWarning("Invaild number for max tokens");
			reqObj.Add("messages", messages);

			return reqObj;
		}
	}
}