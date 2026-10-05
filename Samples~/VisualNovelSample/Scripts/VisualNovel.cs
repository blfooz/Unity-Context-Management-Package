using System.Linq;
using ContextManagement;
using UnityEngine;

[RequireComponent(typeof(VisualNovelInterface), typeof(ChatRequestManager))]
public class VisualNovel : MonoBehaviour
{
	VisualNovelInterface _interface;
	public ChatRequestManager rm;

	void Awake()
	{
		_interface = GetComponent<VisualNovelInterface>();
		rm = GetComponent<ChatRequestManager>();
	}
	void Start()
	{
		_interface.OnOption += OptionAction;
		_interface.OnSubmit += SubmitAction;
		Generate();
	}

	AwaitableCompletionSource<string> userResponse = new();
	async void Generate()
	{
		var (output, reasoning) = ContextUtility.ExtractResponse(await rm.SendRequest());
		_interface.SetLines(output);
	}

	void SubmitAction(string msg)
	{
		_interface.input = "";
		_interface.ClearOptions();
		_interface.SetDialogue("Response Submitted", msg);
		var ctx = rm.GetContext().ToArray();
		rm.messages.Add(new Message(Role.user, $"player input: {msg}", ctx));
		userResponse.TrySetResult(msg);
	}

	void OptionAction(string optionChosen)
	{
		_interface.ClearOptions();
		_interface.SetDialogue("Option Chosen", optionChosen);
		var ctx = rm.GetContext().ToArray();
		rm.messages.Add(new Message(Role.user, $"player input: {optionChosen}", ctx));
		userResponse.TrySetResult(optionChosen);
	}
}
