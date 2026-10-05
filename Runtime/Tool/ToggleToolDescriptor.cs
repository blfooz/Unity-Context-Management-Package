
using System;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace ContextManagement
{
	public class ToggleToolDescriptor : ToolDescriptor
	{
		/// <param name="handler">boolean indicate updated state</param>
		public ToggleToolDescriptor(
			ToolInfo toggleOn, ToolInfo toggleOff,
			Func<bool, JObject, Task<string>> handler)
		{
			this.toggleOn = toggleOn;
			this.toggleOff = toggleOff;
			this.handler = handler;
		}


		/// <param name="handler">boolean indicate updated state</param>
		public ToggleToolDescriptor(
			ToolInfo onState, ToolInfo offState,
			Func<bool, JObject, string> handler) :
			this(onState, offState, (i, j) => Task.FromResult(handler(i, j)))
		{ }

		public bool currentState = false;
		public override ToolInfo Info => currentState ? toggleOff : toggleOn;
		ToolInfo toggleOn, toggleOff;

		public override Func<JObject, Task<string>> Handler => ToggleToolHandler;
		public Func<bool, JObject, Task<string>> handler;

		Task<string> ToggleToolHandler(JObject parameters)
		{
			currentState = !currentState;
			return handler(currentState, parameters);
		}

		public override string GetSignature()
		=> $"{toggleOn.GetSignature()}/{toggleOff.GetSignature()}\n\t{toggleOn.description}/{toggleOff.description}";
	}
}