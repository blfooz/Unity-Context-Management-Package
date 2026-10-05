using System;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace ContextManagement
{
	public class ToolDescriptor
	{
		public virtual ToolInfo Info { get; }
		public virtual Func<JObject, Task<string>> Handler { get; }

		public bool Enabled { get; protected set; } = true;
		public bool RequireImmediateResponse => Info.requireImmediateResponse;

		public ToolDescriptor(
			ToolInfo info, Func<JObject, Task<string>> handler)
		{
			Info = info ?? throw new ArgumentNullException(nameof(info));
			Handler = handler;
		}

		protected ToolDescriptor() { }

		public ToolDescriptor(
			ToolInfo info, Func<JObject, string> handler) :
			this(info, (j) => Task.FromResult(handler(j)))
		{ }

		public JObject Serialize()
		{
			JObject func = new(
				new JProperty("name", Info.name),
				new JProperty("parameters", Info.parameters.Serialize()));
			if (!string.IsNullOrWhiteSpace(Info.description))
				func.Add("description", Info.description);
			return new(new JProperty("type", "function"), new JProperty("function", func));
		}

		public virtual string GetSignature() => Info.GetSignature();
	}
}
