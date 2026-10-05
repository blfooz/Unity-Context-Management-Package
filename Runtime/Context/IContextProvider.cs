namespace ContextManagement
{
	/// <summary>
	/// Provides variable contexts to the LLM
	/// </summary>
	public interface IContextProvider
	{
		/// <summary>
		/// Try Getting the context, return false if not available
		/// </summary>
		public bool TryGetContext(LLMRequestManager caller, out string context);

		/// <summary>
		/// Description shown in the preview.
		/// </summary>
		public string ContextDescription { get; }
	}
}