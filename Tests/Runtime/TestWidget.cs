using System;

namespace ContextManagement.Tests
{
	/// <summary>An entity data class with the identifier the entity tools require.</summary>
	[Serializable]
	public class TestWidget
	{
		[EntityIdentifierField]
		public string Id;

		public string Name;
		public int Value;
	}
}
