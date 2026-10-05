using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace ContextManagement.Tests
{
	[TestFixture]
	public class ToolsManagerTests
	{
		private class Provider : IToolProvider
		{
			private readonly ToolDescriptor[] _tools;

			public Provider(params ToolDescriptor[] tools) => _tools = tools;

			/// <summary>How many times a manager asked this provider for its descriptors.</summary>
			public int TimesQueried { get; private set; }

			public IEnumerable<ToolDescriptor> GetToolDescriptors()
			{
				TimesQueried++;
				return _tools;
			}
		}

		private class MutableDescriptor : ToolDescriptor
		{
			public MutableDescriptor(ToolInfo info) : base(info, _ => Task.FromResult("ok")) { }

			public void SetEnabled(bool enabled) => Enabled = enabled;
		}

		private static ToolDescriptor Descriptor(string name, bool immediate = false, Func<JObject, string> handler = null)
			=> new(new ToolInfo(name, new JSONSchema(), immediate, $"{name} description"),
				handler ?? (_ => "ok"));

		private readonly List<GameObject> _created = new();

		[TearDown]
		public void TearDown()
		{
			LogAssert.ignoreFailingMessages = false;
			foreach (var go in _created)
				UnityEngine.Object.DestroyImmediate(go);
			_created.Clear();
		}

		/// <summary>
		/// A manager component whose provider list is exactly what the test passes in: auto
		/// discovery is off, so nothing on the GameObject it lives on leaks into the test.
		/// </summary>
		private ToolsManager NewManager(params IToolProvider[] providers)
		{
			var go = new GameObject("tools manager");
			_created.Add(go);
			var manager = go.AddComponent<ToolsManager>();
			manager.autoDiscoverProviders = false;
			if (providers.Length > 0)
				manager.AddProviders(providers);
			return manager;
		}

		[Test]
		public void CollectsTheToolsOfItsProviders()
		{
			var manager = NewManager(new Provider(Descriptor("one"), Descriptor("two")));

			manager.CollectTools();

			CollectionAssert.AreEquivalent(new[] { "one", "two" },
				manager.Select(descriptor => descriptor.Info.name));
		}

		[Test]
		public void KeepsTheFirstToolWhenTwoProvidersUseTheSameName()
		{
			LogAssert.Expect(LogType.Warning, "Multiple tools have the same name, only the first one will be added.");
			var first = new ToolDescriptor(new ToolInfo("same", new JSONSchema(), false, "first"), _ => "one");
			var second = new ToolDescriptor(new ToolInfo("same", new JSONSchema(), false, "second"), _ => "two");
			var manager = NewManager(new Provider(first), new Provider(second));

			manager.CollectTools();
			var tools = manager.GetToolsAsJArray();

			Assert.AreEqual(1, tools.Count);
			Assert.AreEqual("first", tools[0]["function"]["description"].ToString());
		}

		[Test]
		public void AddingAProviderTwiceKeepsItOnce()
		{
			LogAssert.Expect(LogType.Warning, "Adding provider that is already present.");
			var provider = new Provider(Descriptor("one"));
			var manager = NewManager(provider);

			manager.AddProviders(provider);
			manager.CollectTools();

			// A provider that had been registered twice would be queried for its tools twice.
			Assert.AreEqual(1, provider.TimesQueried);
		}

		[Test]
		public void CollectingWithoutProvidersOnlyWarns()
		{
			LogAssert.Expect(LogType.Warning, "Add tool providers to the manager first before collecting tools.");
			var manager = NewManager();

			manager.CollectTools();

			Assert.AreEqual(0, manager.GetToolsAsJArray().Count);
		}

		[Test]
		public void SkipsAProviderWhoseComponentIsDisabled()
		{
			var go = new GameObject("tools");
			try
			{
				var provider = go.AddComponent<TestToolProvider>();
				provider.tools.Add(Descriptor("one"));
				var manager = NewManager(provider);

				manager.CollectTools();
				Assert.AreEqual(1, manager.GetToolsAsJArray().Count);

				provider.enabled = false;
				manager.CollectTools();
				Assert.AreEqual(0, manager.GetToolsAsJArray().Count);

				provider.enabled = true;
				manager.CollectTools();
				Assert.AreEqual(1, manager.GetToolsAsJArray().Count);
			}
			finally
			{
				UnityEngine.Object.DestroyImmediate(go);
			}
		}

		[Test]
		public void KeepsDisabledDescriptorsOutOfTheToolsArray()
		{
			var descriptor = new MutableDescriptor(new ToolInfo("one", new JSONSchema(), false));
			var manager = NewManager(new Provider(descriptor));

			manager.CollectTools();
			Assert.AreEqual(1, manager.GetToolsAsJArray().Count);

			descriptor.SetEnabled(false);
			Assert.AreEqual(0, manager.GetToolsAsJArray().Count);

			descriptor.SetEnabled(true);
			Assert.AreEqual(1, manager.GetToolsAsJArray().Count);
		}

		[Test]
		public void TheToolSummaryListsEverySignature()
		{
			var manager = NewManager(new Provider(Descriptor("one"), Descriptor("two")));

			var infos = manager.GetToolInfos();

			StringAssert.Contains("one():", infos);
			StringAssert.Contains("one description", infos);
			StringAssert.Contains("two():", infos);
		}

		//==========================================================
		//  Running tool calls
		//==========================================================

		[Test]
		public async Task RunsAToolAndReportsWhetherItsResultIsNeededImmediately()
		{
			var manager = NewManager(new Provider(
				Descriptor("one", immediate: true, handler: args => $"got {args["q"]}")));
			manager.CollectTools();

			var call = new ToolCall { id = "c", functionName = "one", arguments = JObject.Parse("{\"q\":\"x\"}") };

			Assert.IsTrue(await manager.ProcessToolcall(call));
			Assert.AreEqual("got x", call.result);
		}

		[Test]
		public async Task AToolThatDoesNotNeedAnImmediateResponseSaysSo()
		{
			var manager = NewManager(new Provider(Descriptor("one")));
			manager.CollectTools();

			var call = new ToolCall { id = "c", functionName = "one", arguments = new JObject() };

			Assert.IsFalse(await manager.ProcessToolcall(call));
			Assert.AreEqual("ok", call.result);
		}

		[Test]
		public async Task AnUnknownToolAnswersWithAnError()
		{
			LogAssert.Expect(LogType.Error, "tool: missing not found.");
			var manager = NewManager(new Provider(Descriptor("one")));
			manager.CollectTools();

			var call = new ToolCall { id = "c", functionName = "missing", arguments = new JObject() };

			Assert.IsFalse(await manager.ProcessToolcall(call));
			Assert.AreEqual(ToolsManager.FailedResult("missing", "no tool with that name is registered."),
				call.result);
		}

		[Test]
		public async Task AToolThatThrowsAnswersWithTheErrorMessage()
		{
			LogAssert.ignoreFailingMessages = true;
			try
			{
				var manager = NewManager(new Provider(Descriptor("one",
					handler: _ => throw new InvalidOperationException("boom"))));
				manager.CollectTools();

				var call = new ToolCall { id = "c", functionName = "one", arguments = new JObject() };

				Assert.IsFalse(await manager.ProcessToolcall(call));
				Assert.AreEqual(ToolsManager.FailedResult("one", "boom"), call.result);
			}
			finally
			{
				LogAssert.ignoreFailingMessages = false;
			}
		}

		[Test]
		public async Task AToolThatReturnsNothingAnswersWithAnError()
		{
			LogAssert.ignoreFailingMessages = true;
			try
			{
				var manager = NewManager(new Provider(
					new ToolDescriptor(new ToolInfo("one", new JSONSchema(), false), _ => (string)null)));
				manager.CollectTools();

				var call = new ToolCall { id = "c", functionName = "one", arguments = new JObject() };

				await manager.ProcessToolcall(call);

				Assert.AreEqual(ToolsManager.FailedResult("one", "the tool returned no result."), call.result);
			}
			finally
			{
				LogAssert.ignoreFailingMessages = false;
			}
		}

		[Test]
		public async Task AToolFailureWithoutAMessageIsReportedByItsType()
		{
			LogAssert.ignoreFailingMessages = true;
			try
			{
				var manager = NewManager(new Provider(Descriptor("one",
					handler: _ => throw new Exception("   "))));
				manager.CollectTools();

				var call = new ToolCall { id = "c", functionName = "one", arguments = new JObject() };

				await manager.ProcessToolcall(call);

				Assert.AreEqual(ToolsManager.FailedResult("one", "Exception"), call.result);
			}
			finally
			{
				LogAssert.ignoreFailingMessages = false;
			}
		}

		[Test]
		public async Task RunsEveryCallAndReportsWhetherAnyNeedsAnImmediateResponse()
		{
			var manager = NewManager(new Provider(
				Descriptor("calm", immediate: false),
				Descriptor("eager", immediate: true)));
			manager.CollectTools();

			var calm = new ToolCall { id = "1", functionName = "calm", arguments = new JObject() };
			var eager = new ToolCall { id = "2", functionName = "eager", arguments = new JObject() };

			Assert.IsTrue(await manager.ProcessToolcalls(new[] { calm, eager }));
			Assert.AreEqual("ok", calm.result);
			Assert.AreEqual("ok", eager.result);

			Assert.IsFalse(await manager.ProcessToolcalls(
				new[] { new ToolCall { id = "3", functionName = "calm", arguments = new JObject() } }));
		}

		[Test]
		public void ReportsAToolFailureInOneLine()
		{
			Assert.AreEqual("ERROR: tool call \"f\" failed: because",
				ToolsManager.FailedResult("f", "because"));
		}
	}
}
