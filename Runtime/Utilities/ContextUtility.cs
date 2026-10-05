using System;
using System.Collections;
using Newtonsoft.Json.Linq;

namespace ContextManagement
{
	public static class ContextUtility
	{
		/// <summary>
		/// Extract message content and reasoning from a response JObject.
		/// </summary>
		public static (string output, string reasoning) ExtractResponse(this JObject response, int choice = 0)
		{
			// A failed request has no response to read, and a filtered one has no choice.
			// Both report an empty result rather than throwing.
			if (response?["choices"] is not JArray choices || choice < 0 || choice >= choices.Count)
				return (null, null);

			if (choices[choice]?["message"] is not JObject msg)
				return (null, null);

			JToken reasoning = msg["reasoning_content"] ?? msg["reasoning"];
			return (msg["content"]?.ToString().Trim(), reasoning?.ToString().Trim());
		}

		/// <summary>
		/// Converts a Camel or Pascal case name to snake case.
		/// </summary>
		/// <param name="name"></param>
		/// <returns></returns>
		public static string ToSnakeCase(string name)
		{
			if (string.IsNullOrEmpty(name)) return name;

			var sb = new System.Text.StringBuilder(name.Length + 4);
			for (int i = 0; i < name.Length; i++)
			{
				char c = name[i];
				if (i > 0 && char.IsUpper(c))
				{
					char prev = name[i - 1];
					bool nextIsLower = i + 1 < name.Length && char.IsLower(name[i + 1]);
					if (char.IsLower(prev) || (char.IsUpper(prev) && nextIsLower))
						sb.Append('_');
				}
				sb.Append(char.ToLowerInvariant(c));
			}
			return sb.ToString();
		}

		public static bool IsTypeCollection(Type type, out Type elementType)
		{
			if (type != typeof(string) || typeof(IEnumerable).IsAssignableFrom(type))
			{
				if (type.IsArray)
				{
					elementType = type.GetElementType();
					return true;
				}

				if (type.IsGenericType)
				{
					elementType = type.GetGenericArguments()[0];
					return true;
				}
			}

			elementType = null;
			return false;
		}
	}
}
