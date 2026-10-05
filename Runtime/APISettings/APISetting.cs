using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;

namespace ContextManagement
{
	/// <summary>
	/// Configuration and request behavior for an LLM API provider.
	/// </summary>
	/// <remarks>
	/// A setting is a component, so the provider is selected by adding the matching subclass
	/// (<see cref="DeepSeekAPISetting"/>, <see cref="OpenAIAPISetting"/>, and so on) to the
	/// same GameObject as the manager that uses it. The API key is deliberately not serialized
	/// by Unity, so it cannot enter a scene, prefab, or build: it is written to the settings
	/// file (unless an <see cref="IApiCredentialStore"/> keeps it elsewhere) and loaded from
	/// there at runtime.
	/// </remarks>
	public abstract class APISetting : MonoBehaviour
	{
		/// <summary>The folder created under <see cref="Application.persistentDataPath"/>.</summary>
		public const string DefaultFolderName = "APISettings";

		/// <summary>The endpoint requests are sent to.</summary>
		public abstract string BaseUrl { get; }

		/// <summary>The directory this setting is saved to and loaded from.</summary>
		public virtual string SavePath => Path.Combine(Application.persistentDataPath, DefaultFolderName);

		/// <summary>
		/// Resolves the API key outside the settings file, or null to keep the key in the file.
		/// Override to keep credentials in a platform key store, a backend, or anywhere else.
		/// </summary>
		public virtual IApiCredentialStore Credential => null;

		[Header("Debug Settings")]
		[Tooltip("Print every request body to the console.")]
		public bool logOnSend;
		[Tooltip("Print every response body to the console.")]
		public bool logOnReceive;
		[Tooltip("Automatically load with the setting name when api key is missing")]
		public bool autoload;

		[Header("API Settings")]
		[Tooltip("Name of the settings file this setting is saved to and loaded from.")]
		public string settingName;
		[Tooltip("The model identifier sent with every request.")]
		public string model;

		/// <summary>The API key used to authenticate requests.</summary>
		/// <remarks>
		/// <see cref="NonSerializedAttribute"/> keeps this value out of Unity serialization.
		/// </remarks>
		[NonSerialized]
		public string key;

		/// <summary>The configured key, or an error when none is set.</summary>
		protected string Key
		{
			get
			{
				if (string.IsNullOrWhiteSpace(key))
					throw new Exception("API Setting has no key");
				return key.Trim();
			}
		}

		/// <summary>
		/// The body fields merged into every request. The configured model is added here.
		/// </summary>
		protected virtual JObject ExtraBody
		{
			get
			{
				return new()
				{
					["model"] = model
				};
			}
		}

		/// <summary>
		/// Load the settings file when no key is in memory, so a player's saved key is
		/// available in a build without the editor inspector that loaded it there.
		/// </summary>
		protected virtual void Awake()
		{
			if (!string.IsNullOrEmpty(key) || string.IsNullOrWhiteSpace(settingName))
				return;

			if (!TryGetPath(out string path) || !File.Exists(path))
				return;

			try
			{
				var json = JObject.Parse(File.ReadAllText(path));
				Load(json);
			}
			catch (Exception e)
			{
				Debug.LogError($"Failed to load API settings from '{path}': {e}", this);
			}
		}

		/// <summary>Fetch the model identifiers advertised by the endpoint.</summary>
		/// <returns>The model list, or null when the request failed.</returns>
		public async Task<JArray> GetAvailableModels()
		{
			var endpoint = BaseUrl.TrimEnd('/') + "/models";
			using var request = UnityWebRequest.Get(endpoint);
			request.SetRequestHeader("Authorization", $"Bearer {Key}");
			await request.SendWebRequest();

			if (request.result != UnityWebRequest.Result.Success)
			{
				Debug.LogError($"{request.error}\n{request.downloadHandler?.text}");
				return null;
			}

			try
			{
				var json = JObject.Parse(request.downloadHandler.text);
				return json["data"]?.ToObject<JArray>();
			}
			catch (Exception e)
			{
				Debug.LogError($"Failed to parse the model list: {e}");
				return null;
			}
		}

		/// <summary>Resolve the settings file this setting is saved to and loaded from.</summary>
		/// <param name="path">The settings file path; null when the name is empty.</param>
		/// <returns>Whether a path was resolved.</returns>
		public bool TryGetPath(out string path)
		{
			path = null;
			if (string.IsNullOrWhiteSpace(settingName))
			{
				return false;
			}

			StringBuilder fileName = new(settingName.Length);
			foreach (char c in settingName.Trim())
			{
				if (char.IsLetterOrDigit(c) || c == '-' || c == '_')
					fileName.Append(c);
				else
					fileName.Append('_');
			}

			path = Path.Combine(SavePath, fileName.Append(".json").ToString());
			return true;
		}

		/// <summary>Convert the setting to the persistence format.</summary>
		/// <remarks>
		/// The key is included only when no <see cref="IApiCredentialStore"/> supplies it.
		/// Converting never writes to the store; use <see cref="Save"/> to persist.
		/// </remarks>
		/// <returns>The settings file contents.</returns>
		public virtual JObject ToJSON()
		{
			JObject json = new()
			{
				["setting_name"] = settingName,
				["model"] = model
			};
			if (Credential == null) json["key"] = key;
			return json;
		}

		/// <summary>Write the setting to the file at <paramref name="path"/>.</summary>
		/// <remarks>
		/// Set <see cref="key"/> before calling: this method never prompts. The key is written
		/// to the file unless an <see cref="IApiCredentialStore"/> supplies it, in which case
		/// the store is written instead.
		/// </remarks>
		/// <param name="path">The file to write.</param>
		/// <returns>Whether the file was written.</returns>
		public virtual bool Save(string path)
		{
			if (string.IsNullOrWhiteSpace(settingName))
			{
				Debug.LogError("A saved API setting must have a setting name.");
				return false;
			}

			try
			{
				Directory.CreateDirectory(Path.GetDirectoryName(path));

				string tempPath = path + ".tmp";
				string json = ToJSON().ToString(Formatting.Indented);
				Credential?.SetKey(this, key);
				File.WriteAllText(tempPath, json);
				File.Copy(tempPath, path, true);
				File.Delete(tempPath);
				return true;
			}
			catch (Exception e)
			{
				Debug.LogError($"Failed to save API settings to '{path}': {e}");
			}
			return false;
		}

		/// <summary>Read the setting from the file at <paramref name="path"/>.</summary>
		/// <param name="path">The file to read.</param>
		/// <returns>Whether the file existed and contained a usable setting.</returns>
		public virtual bool Load(string path)
		{
			try
			{
				return Load(JObject.Parse(File.ReadAllText(path)));
			}
			catch (Exception e)
			{
				Debug.LogError($"Failed to load API settings from '{path}': {e}");
				return false;
			}
		}

		/// <summary>Load the setting from the persistence format.</summary>
		/// <param name="json">The parsed settings file.</param>
		/// <returns>Whether the file contained a usable setting.</returns>
		public virtual bool Load(JObject json)
		{
			if (!json.TryGetValue("setting_name", out var nameToken))
				return false;
			if (!json.TryGetValue("model", out var modelToken))
				return false;
			string loadedKey;
			if (Credential != null)
			{
				loadedKey = Credential.GetKey(this);
				if (string.IsNullOrWhiteSpace(loadedKey)) return false;
			}
			else
			{
				if (!json.TryGetValue("key", out var keyToken))
					return false;
				loadedKey = keyToken.ToString();
			}

			settingName = nameToken.ToString();
			model = modelToken.ToString();
			key = loadedKey;

			return true;
		}

		/// <summary>Assemble a request for <paramref name="body"/>.</summary>
		/// <remarks>
		/// The default posts a chat completion to <see cref="BaseUrl"/> with a bearer token.
		/// Override for a provider that needs different headers or a different protocol.
		/// </remarks>
		protected virtual UnityWebRequest AssembleRequest(JObject body)
		{
			var endpoint = BaseUrl.TrimEnd('/') + "/chat/completions";
			var request = UnityWebRequest.Post(endpoint, body.ToString(), "application/json");
			request.SetRequestHeader("Authorization", $"Bearer {Key}");
			return request;
		}

		/// <summary>
		/// Assemble a streaming-capable <see cref="UnityWebRequest"/>.
		/// Default implementation calls <see cref="AssembleRequest"/> and attaches
		/// a <see cref="StreamingDownloadHandler"/>. Override if custom headers or
		/// URL modifications are needed for streaming.
		/// </summary>
		protected virtual UnityWebRequest AssembleStreamingRequest(
			JObject body, StreamingDownloadHandler downloadHandler)
		{
			var request = AssembleRequest(body);
			request.downloadHandler = downloadHandler;
			return request;
		}

		/// <summary>Send a non-streaming request.</summary>
		/// <param name="body">The request body.</param>
		/// <returns>The parsed response, or null when the request failed.</returns>
		public async Task<JObject> SendRequest(JObject body)
		{
			body ??= new JObject();
			body.Merge(ExtraBody);
			if (logOnSend) print($"Request Sent: {body}");

			using var request = AssembleRequest(body);
			await request.SendWebRequest();

			switch (request.result)
			{
				case UnityWebRequest.Result.Success:
					try
					{
						if (logOnReceive)
							print($"Request Received: {request.downloadHandler.text.Trim()}");
						return JObject.Parse(request.downloadHandler.text);
					}
					catch (Exception e)
					{
						Debug.LogError(e);
					}
					break;
				default:
					LogRequestError(request);
					break;
			}

			return null;
		}

		/// <summary>
		/// Send a streaming request. Chunks are reported through <paramref name="onDelta"/>
		/// as they arrive, and the assembled response is returned when the request
		/// completes: the same object <see cref="SendRequest"/> would have returned,
		/// arrived at incrementally.
		/// </summary>
		/// <param name="body">The request body; <c>stream</c> is set to true.</param>
		/// <param name="onDelta">Called for every chunk, on the Unity main thread.</param>
		/// <param name="ct">
		/// Cancels the request: the connection is aborted and an
		/// <see cref="OperationCanceledException"/> is thrown.
		/// </param>
		/// <returns>The assembled response, or null when the request failed.</returns>
		public async Task<JObject> SendRequestStreaming(
			JObject body, Action<JObject> onDelta = null, CancellationToken ct = default)
		{
			body ??= new JObject();
			body.Merge(ExtraBody);
			body["stream"] = true;

			if (logOnSend) print($"Request Sent: {body}");

			var stream = new StreamingDownloadHandler(onDelta);
			using var request = AssembleStreamingRequest(body, stream);

			using (ct.Register(request.Abort))
			{
				try
				{
					await request.SendWebRequest();
				}
				catch (Exception ex) when (ct.IsCancellationRequested)
				{
					Debug.LogWarning($"Streaming request aborted: {ex.Message}");
				}
			}

			ct.ThrowIfCancellationRequested();

			if (request.result != UnityWebRequest.Result.Success)
			{
				LogRequestError(request, stream.BodyPreview);
				return null;
			}

			var response = stream.Response;
			if (logOnReceive) print($"Request Received: {response}");

			if (response["error"] != null)
			{
				Debug.LogError($"Streaming request returned an error: {response["error"]}");
				return null;
			}

			if (!stream.HasChoices)
			{
				Debug.LogError("Streaming request completed without a response choice.");
				return null;
			}

			return response;
		}

		/// <summary>
		/// Report a failed request. <paramref name="responseBody"/> is the body when it
		/// has already been read out of the request (a streaming download handler does
		/// not keep it).
		/// </summary>
		protected void LogRequestError(UnityWebRequest request, string responseBody = null)
		{
			responseBody ??= request.downloadHandler?.text;

			string detail = responseBody;
			try
			{
				var error = JObject.Parse(responseBody);
				if (error["error"] != null) detail = error["error"].ToString();
			}
			catch (Exception)
			{
				// The body is not JSON; report it as it arrived.
			}

			Debug.LogError(string.IsNullOrWhiteSpace(detail)
				? request.error
				: $"{request.error}\n{detail}");
		}
	}
}
