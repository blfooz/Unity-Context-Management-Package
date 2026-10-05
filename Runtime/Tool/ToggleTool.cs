using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Events;

namespace ContextManagement
{

	[RequireComponent(typeof(LLMRequestManager))]
	public class ToggleTool : MonoBehaviour, IToolProvider
	{
		public ToolInfo toggleOff;
		public UnityEvent<JObject> linkedFunctionsOff;

		public ToolInfo toggleOn;
		public UnityEvent<JObject> linkedFunctionsOn;

		ToolDescriptor tool;
		void OnValidate()
		{
			tool = null;
		}

		public IEnumerable<ToolDescriptor> GetToolDescriptors()
		{
			tool ??= new ToggleToolDescriptor(toggleOn, toggleOff, Call);

			yield return tool;
		}

		Task<string> Call(bool updatedState, JObject parameters)
		{
			if (updatedState)
				linkedFunctionsOn.Invoke(parameters);
			else linkedFunctionsOff.Invoke(parameters);
			return GetResult(parameters);
		}

		static Task<string> GetResult(JObject parameters)
			=> Task.FromResult(parameters.TryGetValue("function_call_result", out var result) ? result.ToString() : "Function Call Succeed.");
	}
}