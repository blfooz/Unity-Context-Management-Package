
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace ContextManagement
{
	/// <summary>
	/// Holds the <see cref="IContextProvider"/>s a request manager asks for context and queries
	/// them while a request is built. Lives on the same GameObject as the
	/// <see cref="LLMRequestManager"/> and can be assigned to several of them, or to none of
	/// them and added to the GameObject on its own.
	/// </summary>
	public class ContextsManager : MonoBehaviour
	{
		[Tooltip("Include every IContextProvider on this GameObject.")]
		public bool autoDiscoverProviders = true;
		public List<Object> providerObjects = new();

		[HideInInspector]
		public List<IContextProvider> providers = new();
		readonly HashSet<IContextProvider> providersSet = new();

		void Awake() => DiscoverProviders();

		/// <summary>
		/// Register every <see cref="IContextProvider"/> on this GameObject that is not
		/// registered yet. Does nothing while <see cref="autoDiscoverProviders"/> is off.
		/// </summary>
		public void DiscoverProviders()
		{
			if (autoDiscoverProviders)
				AddProviders(GetComponents<IContextProvider>());
			AddProviders(providerObjects.OfType<IContextProvider>().ToArray());
			AddProviders(providerObjects.OfType<GameObject>()
				.Select(c => c.GetComponent<IContextProvider>()).ToArray());
		}

		public void AddProviders(params IContextProvider[] contextProviders)
		{
			foreach (var provider in contextProviders)
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

		public IEnumerable<string> GetContexts(LLMRequestManager caller)
		{
			foreach (var provider in providers)
			{
				if (provider.TryGetContext(caller, out string context))
					yield return context;
			}
		}

		public string GetContextProviderInfos()
		{
			if (providers.Count == 0) return "Context Manager currently has no context providers.";
			StringBuilder sb = new();
			foreach (var provider in providers)
			{
				sb.AppendLine($"{provider.GetType().Name}:\n\t{provider.ContextDescription}");
			}
			return sb.ToString();
		}
	}
}
