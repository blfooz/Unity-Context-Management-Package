using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Events;

namespace ContextManagement
{
	[RequireComponent(typeof(ToolsManager))]
	public class Tool : MonoBehaviour, IToolProvider
	{
		public ToolInfo info;

		public UnityEvent<ToolCallContext> linkedFunctions;

		ToolDescriptor tool;
		public IEnumerable<ToolDescriptor> GetToolDescriptors()
		{
			tool ??= new ToolDescriptor(info, Call);
			yield return tool;
		}

		private void OnValidate()
		{
			tool = null;
		}

		async Task<string> Call(JObject parameters)
		{
			linkedFunctions.Invoke(ToolCallContext.Create(parameters, out var task));
			return await task;
		}
	}
}