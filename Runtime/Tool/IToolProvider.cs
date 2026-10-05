using System.Collections.Generic;

namespace ContextManagement
{
	/// <summary>
	/// Implement this on any component that exposes LLM-callable tools.
	/// Each descriptor returned by <see cref="GetToolDescriptors"/> becomes
	/// one callable tool name in the <see cref="LLMRequestManager"/>.
	/// A single component can yield any number of descriptors,
	/// you are advised to cache the tool descriptors.
	/// </summary>
	public interface IToolProvider
	{
		IEnumerable<ToolDescriptor> GetToolDescriptors();
	}
}