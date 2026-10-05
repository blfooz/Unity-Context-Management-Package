using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace ContextManagement.Tests
{
	[TestFixture]
	public class ContextsManagerTests
	{
		private class Provider : IContextProvider
		{
			private readonly string _context;
			private readonly bool _available;

			public Provider(string context, bool available = true)
			{
				_context = context;
				_available = available;
			}

			public LLMRequestManager Caller { get; private set; }

			public bool TryGetContext(LLMRequestManager caller, out string context)
			{
				Caller = caller;
				context = _context;
				return _available;
			}

			public string ContextDescription => $"{_context} description";
		}

		private readonly List<GameObject> _created = new();

		[TearDown]
		public void TearDown()
		{
			foreach (var go in _created)
				Object.DestroyImmediate(go);
			_created.Clear();
		}

		/// <summary>
		/// A manager component whose provider list is exactly what a test adds: auto discovery
		/// is off, so nothing on the GameObject it lives on leaks into the test.
		/// </summary>
		private ContextsManager NewManager()
		{
			var go = new GameObject("contexts manager");
			_created.Add(go);
			var manager = go.AddComponent<ContextsManager>();
			manager.autoDiscoverProviders = false;
			return manager;
		}

		[Test]
		public void HoldsEveryProviderOnce()
		{
			LogAssert.Expect(LogType.Warning, "Adding provider that is already present.");
			var provider = new Provider("a");
			var contexts = NewManager();

			contexts.AddProviders(provider, provider);

			Assert.AreEqual(1, contexts.providers.Count);
		}

		[Test]
		public void ReportsOnlyTheContextsThatAreAvailable()
		{
			var contexts = NewManager();
			contexts.AddProviders(new Provider("dropped", available: false), new Provider("kept"));

			CollectionAssert.AreEqual(new[] { "kept" }, contexts.GetContexts(null).ToArray());
		}

		[Test]
		public void PassesTheCallerToEveryProvider()
		{
			var go = new GameObject("caller");
			try
			{
				var manager = go.AddComponent<LLMRequestManager>();
				var provider = new Provider("ctx");
				var contexts = NewManager();
				contexts.AddProviders(provider);

				contexts.GetContexts(manager).ToArray();

				Assert.AreSame(manager, provider.Caller);
			}
			finally
			{
				Object.DestroyImmediate(go);
			}
		}

		[Test]
		public void DescribesItsProviders()
		{
			var contexts = NewManager();
			contexts.AddProviders(new Provider("a"), new Provider("b"));

			var infos = contexts.GetContextProviderInfos();

			StringAssert.Contains("Provider:\n\ta description", infos);
			StringAssert.Contains("Provider:\n\tb description", infos);
		}

		[Test]
		public void WithoutProvidersItReportsThatThereAreNone()
		{
			var contexts = NewManager();

			Assert.AreEqual(0, contexts.GetContexts(null).Count());
			StringAssert.Contains("no context providers", contexts.GetContextProviderInfos());
		}
	}
}
