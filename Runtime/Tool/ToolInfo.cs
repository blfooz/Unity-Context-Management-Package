using System;
using System.Linq;
using System.Text;
using UnityEngine;

namespace ContextManagement
{
	[Serializable]
	public class ToolInfo
	{
		/// <summary>
		/// parameters are deep copied.
		/// </summary>
		public ToolInfo(string name, JSONSchema parameters, bool requireImmediateResponse, string description = "")
		{
			this.name = name;
			this.description = description;
			this.parameters = new(parameters);
			this.requireImmediateResponse = requireImmediateResponse;
		}

		public ToolInfo() { }
		public string name;

		[TextArea(3, 10)]
		public string description;
		public JSONSchema parameters = new(JSONSchema.DataType.Object, true);
		[Tooltip("Whether this tool's result should be sent to the LLM immediately.")]
		public bool requireImmediateResponse;

		public string GetSignature()
		{
			var parametersString = string.Join(", ", parameters.entries.Select(
				entry => $"{entry.DataType}{(entry.DataType == JSONSchema.DataType.Array ? $"<{entry.schema.ArrayType}>" : "")} {entry.name}"));
			return $"{name}({parametersString}):\n\t{description}";
		}
	}
}