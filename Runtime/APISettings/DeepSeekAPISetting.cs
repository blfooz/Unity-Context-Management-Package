using Newtonsoft.Json.Linq;
using UnityEngine;

namespace ContextManagement
{
	/// <summary>DeepSeek's chat-completion endpoint.</summary>
	public class DeepSeekAPISetting : APISetting
	{
		/// <summary>Whether DeepSeek is asked to return its reasoning.</summary>
		[Tooltip("Ask DeepSeek to return its reasoning.")]
		public bool reasoning;

		/// <inheritdoc />
		public override string BaseUrl => "https://api.deepseek.com";

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

		public override JObject ToJSON()
		{
			var json = base.ToJSON();
			json["reasoning"] = reasoning;
			return json;
		}

	}
}
