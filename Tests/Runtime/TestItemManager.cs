namespace ContextManagement.Tests
{
	/// <summary>A minimal entity manager, as the base class cannot be instantiated.</summary>
	public class TestItemManager : EntityManager<TestItem>
	{
		public override string EntityName => "item";

		public override string GetPrompt() => "items";
	}
}
