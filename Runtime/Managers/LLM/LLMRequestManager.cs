using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace ContextManagement
{
	/// <summary>
	/// Sends requests through the <see cref="APISetting"/> on the same GameObject.
	/// System Prompt are only built once at Start,
	/// while Context Providers are queried before each message send.
	/// </summary>
	public class LLMRequestManager : PromptComponent
	{
		public Parameters parameters;

		public List<TextAsset> systemPrompt = new();

		[Tooltip("Resolves this manager's tool calls. Falls back to the ToolsManager on the same GameObject.")]
		public ToolsManager tools;
		[Tooltip("Supplies this manager's context. Falls back to the ContextsManager on the same GameObject.")]
		public ContextsManager contexts;


		[Tooltip("Whether to record token usage or not")]
		[SerializeField]
		bool recordTokenUsage = true;

		[HideInInspector]
		public MessageHistory messages = new();

		//==========================================================
		protected bool Waiting { get; private set; }

		public APISetting APISetting
		{
			get
			{
				if (api == null)
					api = GetComponent<APISetting>();
				if (api == null)
					Debug.LogWarning("Request Manager require APISetting component on the same gameobject.");
				return api;
			}
		}
		APISetting api;

		//=============================================

		public virtual void Awake()
		{
			if (!_initialized) Initialize();
		}

		bool _initialized = false;
		public void Initialize()
		{
			messages.Reset();
			messages.SystemPrompt = AssembleSystemPrompt();
			_initialized = true;
		}

		public override string GetPrompt()
		{
			StringBuilder sb = new();
			foreach (var p in systemPrompt)
				if (p != null)
					sb.AppendLine(p.text + '\n');
			return sb.ToString();
		}

		public virtual string AssembleSystemPrompt()
		{
			StringBuilder sb = new();
			foreach (var pc in GetComponents<PromptComponent>())
			{
				if (!pc.enabled) continue;

				var prompt = pc.GetPrompt();
				if (string.IsNullOrWhiteSpace(prompt)) continue;

				sb.AppendLine(prompt.Trim() + '\n');
			}

			if (sb.Length > 0) return sb.ToString();

			Debug.LogWarning(
				$"No PromptComponent on '{name}' supplied any prompt text; falling back to the " +
				$"default system prompt. Add a system prompt (a TextAsset on the manager, or " +
				$"another PromptComponent) to control the model's behaviour.", this);
			return DefaultSystemPrompt;
		}

		/// <summary>
		/// Used when no <see cref="PromptComponent"/> on this object supplies any text, so a
		/// manager added without a prompt still runs instead of failing to initialize.
		/// </summary>
		public const string DefaultSystemPrompt = "You are a helpful assistant.";

		public virtual JArray GetOutgoingMessages() => messages.ToJSON();
		public IEnumerable<string> GetContext()
			=> contexts == null ? Enumerable.Empty<string>() :
				contexts.GetContexts(this).ToArray();

		/// <summary>
		/// Send request via the <see cref="APISetting"/> on the same game object.
		/// </summary>
		/// <returns>Raw response, use ContextUtility.ExtractResponse() to extract the message or manipulate it yourself.</returns>
		public virtual async Task<JObject> SendRequest()
		{
			if (APISetting == null) return null;

			var json = PrepareToSend();

			Waiting = true;
			long sent = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
			try
			{
				var response = await APISetting.SendRequest(json);
				if (response == null) return null;

				response["sent"] = sent;
				return await Postprocessing(response);
			}
			finally
			{
				Waiting = false;
			}
		}

		public JObject PrepareToSend()
		{
			JObject json = parameters.ToRequestObject(GetOutgoingMessages());

			if (tools != null)
			{
				tools.CollectTools();
				var toolsArray = tools.GetToolsAsJArray();
				if (toolsArray.Count > 0)
					json.Add("tools", toolsArray);
			}

			return json;
		}

		/// <summary>
		/// Send a streaming request via the <see cref="APISetting"/> on the same game object.
		/// </summary>
		/// <param name="onDelta">
		/// Called for every chunk of the response as it arrives, on the Unity main thread.
		/// </param>
		/// <param name="ct">Cancels the request, aborting the connection.</param>
		/// <returns>
		/// The response assembled from the chunks — the same object <see cref="SendRequest"/>
		/// returns, with tool calls and usage merged in — or null when the request failed.
		/// </returns>
		public virtual async Task<JObject> SendRequestStreaming(
			Action<JObject> onDelta = null, CancellationToken ct = default)
		{
			if (APISetting == null) return null;

			var json = PrepareToSend();
			if (recordTokenUsage)
				json["stream_options"] = new JObject { ["include_usage"] = true };

			Waiting = true;
			long sent = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
			try
			{
				var response = await APISetting.SendRequestStreaming(json, onDelta, ct);
				if (response == null) return null;

				response["sent"] = sent;
				return await Postprocessing(response);
			}
			finally
			{
				Waiting = false;
			}
		}

		/// <summary>
		///  Handle toolcalls and record messages, token statistics, and timestamps.
		/// </summary>
		protected virtual async Task<JObject> Postprocessing(JObject response)
		{
			if (response?["choices"] is not JArray choices || choices.Count == 0)
			{
				Debug.LogWarning("Response has no choice to record.");
				return response;
			}

			var choice = choices[0];
			var finishReason = choice["finish_reason"]?.ToString();

			if (finishReason == "length")
				Debug.LogWarning("Response Incomplete due to length limit exhausted.");

			if (choice["message"] is JObject msgJson)
			{
				// Streaming only reports usage when the provider was asked for it.
				if (recordTokenUsage && response["usage"] != null)
					msgJson["usage"] = response["usage"];

				Message message = new(msgJson)
				{
					timestamps = new()
					{
						sent = response["sent"]?.ToObject<long>() ?? 0,
						// Not every provider reports a creation time; a response without one
						// records no created stamp rather than a bogus epoch zero.
						created = response["created"]?.ToObject<long>() ?? 0,
						received = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
					}
				};
				messages.Add(message);

				if (message.ToolCalls != null && message.ToolCalls.Count > 0)
				{
					if (tools != null)
					{
						bool requireImmediateResponse = await tools.ProcessToolcalls(message.ToolCalls);
						if (string.IsNullOrWhiteSpace(msgJson["content"]?.ToString()))
						{
							Debug.LogWarning("Empty content tool call message automatically deemed to require immediate response.");
							requireImmediateResponse = true;
						}
						if (requireImmediateResponse) return await SendRequest();
					}
					else Debug.LogError("Request Manager received tool call but has no Tools Manager");
				}
			}
			else Debug.LogError("Message not a JObject.");

			return response;
		}
	}
}
