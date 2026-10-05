using UnityEditor;
using UnityEngine;
using static ContextManagement.JSONSchema;

namespace ContextManagement
{
	[CustomPropertyDrawer(typeof(JSONSchema))]
	public class JSONSchemaDrawer : PropertyDrawer
	{
		public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
		{
			void DrawProperty(SerializedProperty prop, GUIContent label = null)
			{
				position.height = EditorGUI.GetPropertyHeight(prop);
				if (label == null) EditorGUI.PropertyField(position, prop, true);
				else EditorGUI.PropertyField(position, prop, label, true);
				position.y += position.height;
			}

			EditorGUI.BeginProperty(position, label, property);

			position.height = EditorGUIUtility.singleLineHeight;
			EditorGUI.LabelField(position, "JSON Schema", EditorStyles.boldLabel);
			position.y += position.height;
			EditorGUI.indentLevel++;

			var entries = property.FindPropertyRelative("entries");
			var type = property.FindPropertyRelative("type");

			EditorGUI.BeginDisabledGroup(property.FindPropertyRelative("lockType").boolValue);
			DrawProperty(type, new GUIContent("Data Type"));
			EditorGUI.EndDisabledGroup();

			if (type.enumValueIndex == (int)DataType.Object)
			{
				DrawProperty(entries, new GUIContent("Schema Entries"));
			}
			else
			{
				if (type.enumValueIndex == (int)DataType.Array)
				{
					EditorGUI.indentLevel++;
					//As schema does not hold schema, add and use first children's schema instead.
					if (entries.arraySize == 0) entries.InsertArrayElementAtIndex(0);
					var arraySchema = entries.GetArrayElementAtIndex(0).FindPropertyRelative("schema");
					
					DrawProperty(arraySchema, new GUIContent("Array Schema"));
					EditorGUI.indentLevel--;
				}
			}

			EditorGUI.indentLevel--;
			EditorGUI.EndProperty();
		}

		public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
		{
			var entries = property.FindPropertyRelative("entries");
			var type = property.FindPropertyRelative("type");

			float height = EditorGUIUtility.singleLineHeight;
			height += EditorGUI.GetPropertyHeight(type, true);
			if (type.enumValueIndex == (int)DataType.Object)
				height += EditorGUI.GetPropertyHeight(entries, true);
			else
			{
				if (type.enumValueIndex == (int)DataType.Array && entries.arraySize > 0)
				{
					var arraySchema = entries.GetArrayElementAtIndex(0).FindPropertyRelative("schema");
					height += EditorGUI.GetPropertyHeight(arraySchema, true);
				}
			}
			return height;
		}
	}
}