using System.Text;
using UnityEditor;
using UnityEngine;

namespace ContextManagement
{
	[CustomEditor(typeof(LLMRequestManager), true)]
	public class LLMRequestManagerInspector : Editor
	{
		string promptPreviewText = "";
		Vector2 promptPreviewScroll;

		public override void OnInspectorGUI()
		{
			DrawDefaultInspector();

			if (GUILayout.Button("Compile Preview"))
			{
				promptPreviewText = BuildPromptPreview((LLMRequestManager)target);
			}

			if (promptPreviewText.Length > 0)
			{
				EditorGUILayout.Space();
				EditorGUILayout.LabelField("Compiled Prompt", EditorStyles.boldLabel);

				promptPreviewScroll = EditorGUILayout.BeginScrollView(promptPreviewScroll, GUILayout.Height(250));
				EditorGUI.BeginDisabledGroup(true);
				EditorGUILayout.TextArea(promptPreviewText, GUILayout.ExpandHeight(true));
				EditorGUI.EndDisabledGroup();
				EditorGUILayout.EndScrollView();
			}

			var cm = (LLMRequestManager)target;
			// ── Messages Editor ──
			if (GUILayout.Button("Edit Messages"))
			{
				MessageHistoryWindow.Open(cm);
			}
		}

		string BuildPromptPreview(LLMRequestManager cm)
		{
			StringBuilder sb = new();

			// System prompt
			sb.AppendLine("── System Prompt ──");
			sb.AppendLine(cm.AssembleSystemPrompt().Trim());
			sb.AppendLine();

			if (cm.tools != null)
			{
				cm.tools.DiscoverProviders();
				var tools = cm.tools.GetToolInfos().Trim();
				if (!string.IsNullOrWhiteSpace(tools))
				{
					sb.AppendLine("── Tools ──");
					sb.AppendLine(tools);
					sb.AppendLine();
				}
			}
			else sb.AppendLine("This Request Manager has no Tools Manager");

			// Context providers
			if (cm.contexts != null)
			{
				cm.contexts.DiscoverProviders();
				var contexts = cm.contexts.GetContextProviderInfos().Trim();
				if (!string.IsNullOrWhiteSpace(contexts))
				{
					sb.AppendLine("── Context Providers ──");
					sb.AppendLine(contexts);
					sb.AppendLine();
				}
			}
			else sb.AppendLine("This Request Manager has no Contexts Manager");

			return sb.ToString();
		}
	}
}
