using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace ContextManagement.Tests
{
	[TestFixture]
	public class APISettingTests
	{
		private class TestCredentialStore : IApiCredentialStore
		{
			public string Key;
			public string SavedKey;

			public string GetKey(APISetting setting) => Key;

			public void SetKey(APISetting setting, string key) => SavedKey = key;
		}

		private string _directory;
		private readonly List<UnityEngine.Object> _created = new();

		[SetUp]
		public void SetUp()
		{
			_directory = Path.Combine(
				Application.temporaryCachePath,
				"context-management-tests",
				Guid.NewGuid().ToString("N"));
			System.IO.Directory.CreateDirectory(_directory);
			TestAPISetting.DirectoryOverride = _directory;
		}

		[TearDown]
		public void TearDown()
		{
			LogAssert.ignoreFailingMessages = false;
			TestAPISetting.DirectoryOverride = null;
			foreach (var obj in _created)
				UnityEngine.Object.DestroyImmediate(obj);
			_created.Clear();
			if (System.IO.Directory.Exists(_directory))
				System.IO.Directory.Delete(_directory, true);
		}

		private T New<T>() where T : Component
		{
			var go = new GameObject("api setting");
			go.SetActive(false);
			_created.Add(go);
			return go.AddComponent<T>();
		}

		[Test]
		public void AnOpenAICompatibleRequestPostsToTheCompletionEndpoint()
		{
			var setting = New<TestAPISetting>();
			setting.model = "gpt-test";
			setting.key = "secret";

			using var request = setting.BuildRequest(new JObject { ["messages"] = new JArray() });

			Assert.AreEqual("https://example.com/chat/completions", request.url);
			Assert.AreEqual("POST", request.method);
			Assert.AreEqual("Bearer secret", request.GetRequestHeader("Authorization"));

			var posted = JObject.Parse(Encoding.UTF8.GetString(request.uploadHandler.data));
			// The model is merged in by SendRequest, so it is not part of what
			// AssembleRequest posts on its own (see TheModelIsMergedIntoTheBody).
			Assert.IsNotNull(posted["messages"]);
		}

		[Test]
		public void TheModelIsMergedIntoTheBody()
		{
			var setting = New<TestAPISetting>();
			setting.model = "gpt-test";

			var body = setting.BuildBody();

			Assert.AreEqual("gpt-test", body["model"].ToString());
		}

		[Test]
		public void TheKeyIsTrimmedBeforeItIsSent()
		{
			var setting = New<TestAPISetting>();
			setting.key = "  secret  ";

			using var request = setting.BuildRequest(new JObject { ["messages"] = new JArray() });

			Assert.AreEqual("secret", setting.KeyForTest);
			Assert.AreEqual("Bearer secret", request.GetRequestHeader("Authorization"));
		}

		[Test]
		public void ThereIsNoKeyWhenNothingIsSet()
		{
			var setting = New<TestAPISetting>();

			Assert.IsTrue(string.IsNullOrEmpty(setting.KeyForTest));
		}

		[Test]
		public void AStreamingRequestCarriesTheStreamingDownloadHandler()
		{
			var setting = New<TestAPISetting>();
			setting.model = "gpt-test";
			setting.key = "secret";
			var handler = new StreamingDownloadHandler();
			try
			{
				using var request = setting.BuildStreamingRequest(new JObject(), handler);

				Assert.AreSame(handler, request.downloadHandler);
				Assert.AreEqual("https://example.com/chat/completions", request.url);
			}
			finally
			{
				handler.Dispose();
			}
		}

		[Test]
		public void DeepSeekSendsItsModelAndThinkingFlag()
		{
			var setting = New<TestDeepSeekAPISetting>();
			setting.model = "deepseek-flash";

			var body = setting.BuildBody();

			Assert.AreEqual("https://api.deepseek.com", setting.BaseUrl);
			Assert.AreEqual("deepseek-flash", body["model"].ToString());
			Assert.AreEqual("disabled", body["thinking"]["type"].ToString());
		}

		[Test]
		public void DeepSeekAsksForReasoningWhenEnabled()
		{
			var setting = New<TestDeepSeekAPISetting>();
			setting.reasoning = true;

			Assert.AreEqual("enabled", setting.BuildBody()["thinking"]["type"].ToString());
		}

		[Test]
		public void OpenRouterKnowsItsEndpointAndOptionalHeaders()
		{
			var setting = New<TestOpenRouterAPISetting>();
			setting.key = "secret";
			setting.httpReferer = "https://example.com";
			setting.xTitle = "Example";

			using var request = setting.BuildRequest(new JObject());

			Assert.AreEqual("https://openrouter.ai/api/v1", setting.BaseUrl);
			Assert.AreEqual("https://example.com", request.GetRequestHeader("HTTP-Referer"));
			Assert.AreEqual("Example", request.GetRequestHeader("X-OpenRouter-Title"));
		}

		[Test]
		public void TheSettingsFileRoundTripsAndKeepsTheKeyByDefault()
		{
			var setting = New<TestAPISetting>();
			setting.settingName = "player";
			setting.model = "deepseek-flash";
			setting.key = "secret";

			Assert.IsTrue(setting.TryGetPath(out string path));
			File.WriteAllText(path, setting.ToJSON().ToString());

			var written = JObject.Parse(File.ReadAllText(path));
			Assert.AreEqual("player", written["setting_name"].ToString());
			Assert.AreEqual("deepseek-flash", written["model"].ToString());
			Assert.AreEqual("secret", written["key"].ToString());

			var loaded = New<TestAPISetting>();
			Assert.IsTrue(loaded.Load(written));
			Assert.AreEqual("player", loaded.settingName);
			Assert.AreEqual("deepseek-flash", loaded.model);
			Assert.AreEqual("secret", loaded.key);
		}

		[Test]
		public void ToJSONOmitsTheKeyWhenACredentialStoreSuppliesIt()
		{
			var credentials = new TestCredentialStore { Key = "from-store" };
			var setting = New<CredentialTestAPISetting>();
			setting.Store = credentials;
			setting.settingName = "custom-store";
			setting.model = "deepseek-flash";
			setting.key = "entered-key";

			var json = setting.ToJSON();

			Assert.IsNull(json["key"]);
			Assert.IsNull(credentials.SavedKey, "Converting a setting must not write to the store.");
		}

		[Test]
		public void SaveWritesTheKeyToTheCredentialStoreAndLoadReadsItBack()
		{
			var credentials = new TestCredentialStore { Key = "from-store" };
			var setting = New<CredentialTestAPISetting>();
			setting.Store = credentials;
			setting.settingName = "custom-store";
			setting.model = "deepseek-flash";
			setting.key = "entered-key";

			Assert.IsTrue(setting.TryGetPath(out string path));
			Assert.IsTrue(setting.Save(path));

			Assert.IsNull(JObject.Parse(File.ReadAllText(path))["key"]);
			Assert.AreEqual("entered-key", credentials.SavedKey);

			var loaded = New<CredentialTestAPISetting>();
			loaded.Store = credentials;
			Assert.IsTrue(loaded.Load(path));
			Assert.AreEqual("custom-store", loaded.settingName);
			Assert.AreEqual("deepseek-flash", loaded.model);
			Assert.AreEqual("from-store", loaded.key);
		}

		[Test]
		public void SavingWithoutANameFailsInsteadOfThrowing()
		{
			var setting = New<TestAPISetting>();

			Assert.IsFalse(setting.TryGetPath(out _));
		}

		[Test]
		public void SaveWithoutANameIsReportedInsteadOfThrowing()
		{
			LogAssert.Expect(LogType.Error, "A saved API setting must have a setting name.");

			var setting = New<TestAPISetting>();

			Assert.IsFalse(setting.Save(Path.Combine(_directory, "unnamed.json")));
		}

		[Test]
		public void LoadOfAMissingFileIsReportedInsteadOfThrowing()
		{
			LogAssert.Expect(LogType.Error, new Regex("Failed to load API settings from"));

			var setting = New<TestAPISetting>();

			Assert.IsFalse(setting.Load(Path.Combine(_directory, "missing.json")));
		}

		[Test]
		public void TheSavedKeyIsLoadedWhenTheSettingWakesUp()
		{
			var saved = New<TestAPISetting>();
			saved.settingName = "player";
			saved.model = "deepseek-flash";
			saved.key = "secret";
			Assert.IsTrue(saved.TryGetPath(out string path));
			File.WriteAllText(path, saved.ToJSON().ToString());

			var go = new GameObject("api setting");
			go.SetActive(false);
			_created.Add(go);
			var setting = go.AddComponent<TestAPISetting>();
			setting.settingName = "player";
			go.SetActive(true);

			Assert.AreEqual("secret", setting.key);
			Assert.AreEqual("deepseek-flash", setting.model);
		}
	}
}
