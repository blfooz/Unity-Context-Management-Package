using System.Collections.Generic;
using UnityEngine;

namespace ContextManagement
{
	/// <summary>
	/// Provides dynamic enum for JSONSchema
	/// </summary>
	public interface IEnumProvider
	{
		public IEnumerable<string> GetEnums();
	}
}