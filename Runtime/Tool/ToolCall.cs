using System;
using Newtonsoft.Json.Linq;

namespace ContextManagement
{

	[Serializable]
	public class ToolCall
	{
		public string id;
		public string functionName;
		public JObject arguments;
		/// <summary>Null until the tool response arrives. Once set, contains the response.</summary>
		public string result;

		public bool HasResult => result != null;

		public JObject ToJSON()
		{
			return new JObject
			{
				["id"] = id,
				["type"] = "function",
				["function"] = new JObject
				{
					["name"] = functionName,
					["arguments"] = arguments?.ToString() ?? "{}"
				}
			};
		}

		public JObject GetResponseJSON() => new()
		{
			["role"] = "tool",
			["tool_call_id"] = id,
			["content"] = result == null ? JValue.CreateNull() : new JValue(result)
		};
	}
}
