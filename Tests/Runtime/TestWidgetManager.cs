namespace ContextManagement.Tests
{
	/// <summary>An entity manager with LLM-facing tools, as the base class cannot be instantiated.</summary>
	public class TestWidgetManager : LLMEntityManager<TestWidget>
	{
		public override string EntityName => "widget";

		public override string GetPrompt() => "widgets";

		public bool baseTools = true;
		public bool addendumTools = true;

		protected override bool EnableBaseTools => baseTools;
		protected override bool EnableAddendumTools => addendumTools;
	}
}
