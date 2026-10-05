using Newtonsoft.Json.Linq;
using UnityEngine.Networking;

namespace ContextManagement.Tests
{
	/// <summary>An inspectable setting for request-building tests.</summary>
	public class TestAPISetting : APISetting
	{
		/// <summary>The directory settings files are written to while tests run.</summary>
		public static string DirectoryOverride;

		/// <inheritdoc />
		public override string BaseUrl => "https://example.com/";

		/// <inheritdoc />
		public override string SavePath => DirectoryOverride;

		/// <summary>The key that would be sent, or an empty string when none is set.</summary>
		public string KeyForTest => string.IsNullOrWhiteSpace(key) ? string.Empty : Key;

		public JObject BuildBody() => ExtraBody;

		public UnityWebRequest BuildRequest(JObject body) => AssembleRequest(body);

		public UnityWebRequest BuildStreamingRequest(JObject body, StreamingDownloadHandler handler)
			=> AssembleStreamingRequest(body, handler);
	}

	/// <summary>Exposes the DeepSeek request body for assertions.</summary>
	public class TestDeepSeekAPISetting : DeepSeekAPISetting
	{
		public JObject BuildBody() => ExtraBody;
	}

	/// <summary>Exposes the OpenRouter request for assertions.</summary>
	public class TestOpenRouterAPISetting : OpenRouterAPISetting
	{
		public UnityWebRequest BuildRequest(JObject body) => AssembleRequest(body);
	}

	/// <summary>A setting whose key is supplied by a credential store.</summary>
	public class CredentialTestAPISetting : TestAPISetting
	{
		public IApiCredentialStore Store;

		/// <inheritdoc />
		public override IApiCredentialStore Credential => Store;
	}
}
