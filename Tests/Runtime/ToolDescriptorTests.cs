using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace ContextManagement.Tests
{
	[TestFixture]
	public class ToolDescriptorTests
	{
		public class SearchParameters
		{
			public string Query;
			public List<string> Tags;
		}

		[Test]
		public void SerializesAToolForTheApi()
		{
			var parameters = new JSONSchema();
			parameters.Add(JSONSchema.DataType.String, "location", "Where to look.");
			var descriptor = new ToolDescriptor(
				new ToolInfo("get_weather", parameters, false, "Looks up the weather."), _ => "sunny");

			var json = descriptor.Serialize();

			Assert.AreEqual("function", json["type"].ToString());
			Assert.AreEqual("get_weather", json["function"]["name"].ToString());
			Assert.AreEqual("Looks up the weather.", json["function"]["description"].ToString());
			Assert.AreEqual("string",
				json["function"]["parameters"]["properties"]["location"]["type"].ToString());
			Assert.AreEqual("Where to look.",
				json["function"]["parameters"]["properties"]["location"]["description"].ToString());
		}

		[Test]
		public void LeavesTheDescriptionOutWhenThereIsNone()
		{
			var descriptor = new ToolDescriptor(new ToolInfo("ping", new JSONSchema(), false), _ => "pong");

			Assert.IsNull(descriptor.Serialize()["function"]["description"]);
		}

		[Test]
		public void ReportsWhetherItNeedsAnImmediateResponse()
		{
			Assert.IsTrue(new ToolDescriptor(new ToolInfo("t", new JSONSchema(), true), _ => "x")
				.RequireImmediateResponse);
			Assert.IsFalse(new ToolDescriptor(new ToolInfo("t", new JSONSchema(), false), _ => "x")
				.RequireImmediateResponse);
		}

		[Test]
		public void RunsItsHandler()
		{
			var descriptor = new ToolDescriptor(
				new ToolInfo("echo", new JSONSchema(), false), args => args["text"].ToString());

			Assert.AreEqual("hi", descriptor.Handler(JObject.Parse("{\"text\":\"hi\"}")).Result);
		}

		[Test]
		public void RejectsAToolWithoutInfo()
		{
			Assert.Throws<System.ArgumentNullException>(
				() => new ToolDescriptor(null, _ => "x"));
		}

		[Test]
		public void DescribesItsParametersInTheSignature()
		{
			var info = new ToolInfo("search", JSONSchema.FromType<SearchParameters>(), false, "Searches.");
			var descriptor = new ToolDescriptor(info, _ => "ok");

			Assert.AreEqual("search(String Query, Array<String> Tags):\n\tSearches.", descriptor.GetSignature());
		}

		[Test]
		public void DeepCopiesTheParametersItWasGiven()
		{
			var parameters = new JSONSchema();
			parameters.Add(JSONSchema.DataType.String, "location", "");
			var info = new ToolInfo("get_weather", parameters, false);

			parameters.entries[0].name = "changed";

			Assert.AreEqual("location", info.parameters.entries[0].name);
		}

		//==========================================================
		//  Toggle tools
		//==========================================================

		[Test]
		public async Task AToggleToolAlternatesBetweenItsTwoInfos()
		{
			var on = new ToolInfo("turn_on", new JSONSchema(), true, "Turns it on.");
			var off = new ToolInfo("turn_off", new JSONSchema(), true, "Turns it off.");
			bool? stateSeen = null;
			var toggle = new ToggleToolDescriptor(on, off, (state, args) =>
			{
				stateSeen = state;
				return "done";
			});

			Assert.AreSame(on, toggle.Info);
			Assert.IsFalse(toggle.currentState);

			Assert.AreEqual("done", await toggle.Handler(new JObject()));
			Assert.IsTrue(stateSeen.Value);
			Assert.IsTrue(toggle.currentState);
			Assert.AreSame(off, toggle.Info);

			Assert.AreEqual("done", await toggle.Handler(new JObject()));
			Assert.IsFalse(stateSeen.Value);
			Assert.AreSame(on, toggle.Info);
		}

		[Test]
		public async Task AToggleToolAlsoAcceptsATaskReturningHandler()
		{
			bool? stateSeen = null;
			var toggle = new ToggleToolDescriptor(
				new ToolInfo("on", new JSONSchema(), true),
				new ToolInfo("off", new JSONSchema(), true),
				(state, args) =>
				{
					stateSeen = state;
					return Task.FromResult("toggled");
				});

			Assert.AreEqual("toggled", await toggle.Handler(new JObject()));
			Assert.IsTrue(stateSeen.Value);
			Assert.IsTrue(toggle.RequireImmediateResponse);
		}

		[Test]
		public void AToggleToolSignsBothOfItsStates()
		{
			var toggle = new ToggleToolDescriptor(
				new ToolInfo("turn_on", new JSONSchema(), true, "Turns it on."),
				new ToolInfo("turn_off", new JSONSchema(), true, "Turns it off."),
				(state, args) => "done");

			var signature = toggle.GetSignature();

			StringAssert.Contains("turn_on():", signature);
			StringAssert.Contains("turn_off():", signature);
			StringAssert.Contains("Turns it on./Turns it off.", signature);
		}
	}
}
