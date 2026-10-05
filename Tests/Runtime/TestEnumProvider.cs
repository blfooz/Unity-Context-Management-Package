using System.Collections.Generic;
using UnityEngine;

namespace ContextManagement.Tests
{
	/// <summary>A serializable enum provider, as a dynamic enum entry needs one.</summary>
	public class TestEnumProvider : ScriptableObject, IEnumProvider
	{
		public string[] values = { "alpha", "beta" };

		public IEnumerable<string> GetEnums() => values;
	}
}
