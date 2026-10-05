using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using ContextManagement;
using Unity.Properties;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

[RequireComponent(typeof(UIDocument), typeof(ChatRequestManager))]
public class VisualNovelInterface : MonoBehaviour
{
	public VisualTreeAsset optionTemplate, dialogueBubbleTemplate;
	public InputAction nextAction;
	public TextAsset dialogueFile;

	Label nameLabel, dialogueLabel;

	VisualElement historyPanel;
	ScrollView historyScroll;

	List<string> lines;

	int lineIndex;

	public void SetLine(int i)
	{
		if (i < 0 || i >= lines.Count)
		{
			Debug.LogWarning("Line number out of bound.");
			return;
		}
		lineIndex = i;

		var rawline = lines[lineIndex].Trim();
		if (string.IsNullOrWhiteSpace(rawline)) { SetLine(++lineIndex); return; }
		var split = rawline.Split(':', 2);
		var op = split[0].Trim();
		var val = Regex.Unescape(split[1].Trim());

		enableInput = false;
		if (op[0] == '"' && op[^1] == '"' && val[0] == '"' && val[^1] == '"') //Regular Dialogue
		{
			SetDialogue(op[1..^1], val[1..^1]);
		}
		else if (op == "option")
		{
			AddOption(val);
			enableInput = true;
			SetLine(++lineIndex);
		}
		else
		{
			Debug.LogWarning($"Failed to parse line: {lines[lineIndex]}");
		}
	}

	public void SetDialogue(string name, string dialogue)
	{
		nameLabel.text = name;
		dialogueLabel.text = dialogue;
		AddDialogueToHistory(name, dialogue);
	}

	void AddDialogueToHistory(string speaker, string dialogue)
	{
		if (historyPanel == null || historyScroll == null)
			return;

		var dialogueBubble = dialogueBubbleTemplate.Instantiate();
		dialogueBubble.Q<Label>("speakerLabel").text = speaker;
		dialogueBubble.Q<Label>("dialogueLabel").text = dialogue;
		historyScroll.Add(dialogueBubble);
		historyScroll.schedule.Execute(ScrollHistoryToBottom);
	}

	void ScrollHistoryToBottom()
	{
		historyScroll.verticalScroller.value = historyScroll.verticalScroller.highValue;
	}

	void ToggleHistory()
	{
		var visible = historyPanel.style.display != DisplayStyle.None;
		historyPanel.style.display = visible ? DisplayStyle.None : DisplayStyle.Flex;
		if (!visible)
			historyScroll.schedule.Execute(ScrollHistoryToBottom);
	}

	// Options
	VisualElement optionsCollection;

	public void AddOption(string text)
	{
		var option = optionTemplate.Instantiate();
		option.style.width = new Length(80, LengthUnit.Percent);
		var b = option.Q<Button>("option");
		b.text = text;
		b.clicked += () => OnOption(text);

		optionsCollection.Add(option);
	}

	public Action<string> OnOption, OnSubmit;

	public void ClearOptions()
	{
		optionsCollection.Clear();
	}

	//Top bar

	public void SetTopBarLabelDataSource(object dataSource, string dataSourcePath)
	{
		var topBarLabel = GetComponent<UIDocument>().rootVisualElement.Q<Label>("topBarLabel");
		topBarLabel.SetBinding("text", new DataBinding()
		{
			dataSource = dataSource,
			dataSourcePath = new(dataSourcePath)
		});
	}
	//===

	[CreateProperty]
	public bool enableInput = false;
	[CreateProperty]
	public string input;

	void Awake()
	{
		var doc = GetComponent<UIDocument>().rootVisualElement;

		doc.Q("demoOption").RemoveFromHierarchy();
		optionsCollection = doc.Q<VisualElement>("options");

		nameLabel = doc.Q<Label>("name");
		dialogueLabel = doc.Q<Label>("dialogue");

		historyPanel = doc.Q<VisualElement>("historyPanel");
		historyScroll = doc.Q<ScrollView>("historyScroll");
		doc.Q("demoDialogueBubble").RemoveFromHierarchy();
		doc.Q<Button>("historyButton").clicked += ToggleHistory;
		doc.Q<Button>("closeHistoryButton").clicked += ToggleHistory;

		doc.dataSource = this;
		var button = doc.Q<Button>("sendButton");
		button.clicked += Submit;

		nextAction.performed += (_) => SetLine(++lineIndex);
	}

	public void SetLines(string rawLines)
	{
		if (string.IsNullOrWhiteSpace(rawLines)) throw new Exception("Given Lines is empty.");
		lines = rawLines.Split('\n').ToList();
		SetLine(0);
	}

	void Start()
	{
		if (dialogueFile != null)
			SetLines(dialogueFile.text);
	}

	void Submit()
	{
		if (!string.IsNullOrWhiteSpace(input)) OnSubmit(input);
		else Debug.LogWarning("Input is empty.");
	}

	void OnEnable()
	{
		nextAction.Enable();
	}

	void OnDisable()
	{
		nextAction.Disable();
	}
}
