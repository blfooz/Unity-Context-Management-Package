using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace ContextManagement.Tests
{
	[TestFixture]
	public class ParametersTests
	{
		[Test]
		public void BuildsARequestBodyWithTheMessages()
		{
			var messages = new JArray { new JObject { ["role"] = "user", ["content"] = "hi" } };

			var json = new Parameters().ToRequestObject(messages);

			Assert.AreEqual(1f, json["temperature"].ToObject<float>());
			Assert.AreEqual(0.95f, json["top_p"].ToObject<float>());
			Assert.AreEqual(0.1f, json["presence_penalty"].ToObject<float>());
			Assert.AreEqual(0f, json["frequency_penalty"].ToObject<float>());
			Assert.AreEqual(1, json["n"].ToObject<int>());
			Assert.AreEqual(10000, json["max_tokens"].ToObject<int>());
			Assert.AreEqual("hi", json["messages"][0]["content"].ToString());
		}

		[Test]
		public void OmitsAndWarnsAboutInvalidNumbers()
		{
			LogAssert.Expect(LogType.Warning, "Invaild number of choices");
			LogAssert.Expect(LogType.Warning, "Invaild number for max tokens");

			var json = new Parameters { numberOfChoices = 0, maxTokens = -1 }.ToRequestObject();

			Assert.IsNull(json["n"]);
			Assert.IsNull(json["max_tokens"]);
			Assert.AreEqual(JTokenType.Null, json["messages"].Type);
		}

		[Test]
		public void SendsTheConfiguredNumbers()
		{
			var json = new Parameters { numberOfChoices = 3, maxTokens = 100 }.ToRequestObject();

			Assert.AreEqual(3, json["n"].ToObject<int>());
			Assert.AreEqual(100, json["max_tokens"].ToObject<int>());
		}

		[Test]
		public void KeepsThePenaltiesItWasGiven()
		{
			var json = new Parameters { presencePenalty = -1f, frequencyPenalty = 2f }.ToRequestObject();

			Assert.AreEqual(-1f, json["presence_penalty"].ToObject<float>());
			Assert.AreEqual(2f, json["frequency_penalty"].ToObject<float>());
		}
	}
}
