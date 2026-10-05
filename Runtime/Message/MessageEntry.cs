using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace ContextManagement
{
	[System.Serializable]
	public abstract class MessageEntry
	{
		public enum ContextAttachementMode
		{
			Hidden,
			AsOne,
			Seperate
		}

		/// <summary>
		/// Serialize for an outgoing API request. Contexts are expanded or hidden
		/// according to <paramref name="mode"/>.
		/// </summary>
		public abstract IEnumerable<JObject> GetJObjects(ContextAttachementMode mode);

		/// <summary>
		/// Serialize for local persistence. Everything needed to rebuild the entry is
		/// written, including data that is never sent to the API.
		/// </summary>
		public abstract IEnumerable<JObject> GetFullJObjects();
	}
}
