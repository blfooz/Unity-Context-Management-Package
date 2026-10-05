using System.Collections.Generic;
using UnityEngine;

namespace ContextManagement.Tests
{
	/// <summary>A tool provider that lives on a component, so it can be disabled.</summary>
	public class TestToolProvider : MonoBehaviour, IToolProvider
	{
		public List<ToolDescriptor> tools = new();

		public IEnumerable<ToolDescriptor> GetToolDescriptors() => tools;
	}
}
