namespace ContextManagement
{
	public class JSONSchemaPrompt : PromptComponent
	{
		public JSONSchema schema;
		public override string GetPrompt()
			=> "# Schema\nDo not surround with code block.\n" + schema.ToString();
	}
}