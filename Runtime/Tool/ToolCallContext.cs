using System;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace ContextManagement
{
	public class ToolCallContext
	{
		protected JObject parameters;
		protected TaskCompletionSource<string> completion;

		protected ToolCallContext() { }
		public static ToolCallContext Create(JObject parameters, out Task<string> task, float timeout = 60)
		{
			ToolCallContext tcc = new()
			{
				parameters = parameters,
				completion = new(TaskCreationOptions.RunContinuationsAsynchronously)
			};
			task = tcc.Wait(timeout);
			return tcc;
		}

		async Task<string> Wait(float timeout)
		{
			if (timeout > 0)
			{
				if (await Task.WhenAny(completion.Task, Task.Delay(TimeSpan.FromSeconds(timeout))) != completion.Task)
					throw new("Tool call timed out.");
			}

			return await completion.Task;
		}

		public void Return(string returnValue)
			=> completion.TrySetResult(returnValue);

		public void Throw(Exception exception)
			=> completion.TrySetException(exception);


		public JToken this[string key] => parameters[key];
	}
}