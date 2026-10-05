using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace ContextManagement
{
	[CustomPropertyDrawer(typeof(SummaryMessage))]
	public class SummaryMessageDrawer : MessageDrawer
	{
		public override VisualElement CreatePropertyGUI(SerializedProperty property)
		{
			var root = base.CreatePropertyGUI(property);

			if (property.managedReferenceValue is SummaryMessage)
			{
				root.style.backgroundColor = new Color(0.25f, 0.30f, 0.45f);
				var archives = root.Q<ScrollView>("archivedMessages");
				var entries = property.FindPropertyRelative("_summarizedEntries");
				for (int i = 0; i < entries.arraySize; i++)
				{
					var entry = entries.GetArrayElementAtIndex(i);
					var entryField = new PropertyField(entry);
					entryField.BindProperty(entry);
					archives.Add(entryField);
				}
			}

			return root;
		}
	}
}