using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace ContextManagement
{
	[CustomPropertyDrawer(typeof(Message))]
	public class MessageDrawer : PropertyDrawer
	{
		[SerializeField]
		protected VisualTreeAsset m_VisualTreeAsset = default;

		public override VisualElement CreatePropertyGUI(SerializedProperty property)
		{
			var root = m_VisualTreeAsset.Instantiate();

			// ── Core bindings ──
			root.Q<DropdownField>("role").BindProperty(property.FindPropertyRelative("role"));
			root.Q<TextField>("content").BindProperty(property.FindPropertyRelative("content"));

			root.dataSource = new MessageUIController(property.managedReferenceValue);

			var toolCallsProp = property.FindPropertyRelative("_toolCalls");
			PopulateToolCalls(root, toolCallsProp);

			return root;
		}

		// ═══════════════════════════════════════════════════════════
		//  Tool Calls
		// ═══════════════════════════════════════════════════════════

		static void PopulateToolCalls(VisualElement root, SerializedProperty toolCallsProp)
		{
			var list = root.Q<VisualElement>("toolCallsList");

			list.Clear();
			for (int i = 0; i < toolCallsProp.arraySize; i++)
			{
				var tc = toolCallsProp.GetArrayElementAtIndex(i);
				var fnName = tc.FindPropertyRelative("functionName").stringValue;
				var result = tc.FindPropertyRelative("result").stringValue;
				var args = tc.FindPropertyRelative("arguments");

				var tcEntry = new VisualElement { style = { flexDirection = FlexDirection.Column, marginBottom = 4 } };
				tcEntry.Add(new Label($"Tool: {fnName}")
				{
					style = { unityFontStyleAndWeight = FontStyle.Bold, fontSize = 11, color = new Color(1f, 0.8f, 0.4f) }
				});

				if (args != null && !string.IsNullOrEmpty(args.stringValue))
				{
					var argsLabel = new Label(args.stringValue)
					{
						style = { fontSize = 10, color = Color.gray, whiteSpace = WhiteSpace.Normal }
					};
					tcEntry.Add(argsLabel);
				}

				if (!string.IsNullOrEmpty(result))
				{
					tcEntry.Add(new Label("Result:")
					{
						style = { fontSize = 10, unityFontStyleAndWeight = FontStyle.Bold, marginTop = 2 }
					});
					tcEntry.Add(new Label(result)
					{
						style = { fontSize = 10, color = Color.gray, whiteSpace = WhiteSpace.Normal }
					});
				}

				list.Add(tcEntry);
			}
		}

	}
}