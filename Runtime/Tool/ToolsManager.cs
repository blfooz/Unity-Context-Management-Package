using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace ContextManagement
{
	/// <summary>
	/// Holds the <see cref="IToolProvider"/>s a request manager offers to the model, collects
	/// their descriptors, and runs the tool calls the model makes. Lives on the same GameObject
	/// as the <see cref="LLMRequestManager"/> and can be assigned to several of them, or to none
	/// of them and added to the GameObject on its own.
	/// </summary>
	public class ToolsManager : MonoBehaviour, IEnumerable<ToolDescriptor>
	{
		[Tooltip("Add every IToolProvider on this GameObject when the component wakes up.")]
		public bool autoDiscoverProviders = true;
		public List<UnityEngine.Object> providerObjects = new();


		readonly List<IToolProvider> providers = new();
		readonly HashSet<IToolProvider> providersSet = new();
		readonly Dictionary<string, ToolDescriptor> tools = new();

		void Awake() => DiscoverProviders();

		public void DiscoverProviders()
		{
			if (autoDiscoverProviders)
				AddProviders(GetComponents<IToolProvider>());
			AddProviders(providerObjects.OfType<IToolProvider>().ToArray());
			AddProviders(providerObjects.OfType<GameObject>()
				.Select(c => c.GetComponent<IToolProvider>()).ToArray());
		}

		public void AddProviders(params IToolProvider[] toolProviders)
		{
			foreach (var provider in toolProviders)
			{
				if (providersSet.Contains(provider))
				{
					Debug.LogWarning("Adding provider that is already present.");
					continue;
				}
				providers.Add(provider);
				providersSet.Add(provider);
			}
		}

		public void CollectTools()
		{
			if (providers.Count == 0)
			{
				Debug.LogWarning("Add tool providers to the manager first before collecting tools.");
				return;
			}

			tools.Clear();
			foreach (var provider in providers)
			{
				if (provider is MonoBehaviour c && !c.enabled) continue;
				foreach (var descriptor in provider.GetToolDescriptors())
					if (!tools.TryAdd(descriptor.Info.name, descriptor))
						Debug.LogWarning("Multiple tools have the same name, only the first one will be added.");
			}
		}

		public string GetToolInfos()
		{
			CollectTools();

			StringBuilder str = new();
			foreach (var desc in tools.Values)
			{
				str.AppendLine($"{desc.GetSignature()}\n");
			}
			return str.ToString();
		}

		public JArray GetToolsAsJArray()
		{
			var toolsArray = new JArray();
			foreach (var desc in tools.Values)
				if (desc.Enabled)
					toolsArray.Add(desc.Serialize());

			return toolsArray;
		}

		/// <summary>
		/// Run a tool call and store its result on the call. 
		/// A call that failed gets an error response
		/// </summary>
		/// <returns> whether the tool result should be immediately returned or can be bundled with the next user message.</returns>
		public async Task<bool> ProcessToolcall(ToolCall call)
		{
			if (!tools.TryGetValue(call.functionName, out var tool))
			{
				Debug.LogError($"tool: {call.functionName} not found.");
				call.result = FailedResult(call.functionName, "no tool with that name is registered.");
				return false;
			}

			try
			{
				call.result = await tool.Handler(call.arguments);

				if (call.result == null)
				{
					Debug.LogError($"tool: {call.functionName} returned no result.");
					call.result = FailedResult(call.functionName, "the tool returned no result.");
				}
			}
			catch (Exception e)
			{
				Debug.LogError(e);
				call.result = FailedResult(call.functionName,
					string.IsNullOrWhiteSpace(e.Message) ? e.GetType().Name : e.Message);
			}

			return tool.RequireImmediateResponse;
		}

		/// <summary>
		/// Result handed to the LLM when a tool call fails, so the model can react to the
		/// failure instead of the round being abandoned mid-way.
		/// </summary>
		public static string FailedResult(string toolName, string reason)
			=> $"ERROR: tool call \"{toolName}\" failed: {reason}";

		/// <returns> whether immediate response is needed</returns>
		public async Task<bool> ProcessToolcalls(IEnumerable<ToolCall> toolCalls)
		{
			var tasks = toolCalls.Select(tc => ProcessToolcall(tc)).ToArray();
			var results = await Task.WhenAll(tasks);
			return results.Any(r => r);
		}

		public IEnumerator<ToolDescriptor> GetEnumerator()
		{
			CollectTools();
			return tools.Values.GetEnumerator();
		}

		IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
	}
}
