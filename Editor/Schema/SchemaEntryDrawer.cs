using UnityEditor;
using UnityEngine;
using static ContextManagement.JSONSchema;

namespace ContextManagement
{
	[CustomPropertyDrawer(typeof(SchemaEntry))]
	public class SchemaEntryDrawer : PropertyDrawer
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

			// Find all properties with null checks
			var nameProp = property.FindPropertyRelative("name");
			var descProp = property.FindPropertyRelative("description");
			var requiredProp = property.FindPropertyRelative("optional");
			var schema = property.FindPropertyRelative("schema");

			var dataType = schema.FindPropertyRelative("type");

			DrawProperty(nameProp);
			DrawProperty(descProp);
			DrawProperty(requiredProp);

			switch (dataType.enumValueIndex)
			{
				case (int)DataType.Array:
				case (int)DataType.Object:
					EditorGUI.indentLevel++;
					DrawProperty(schema);
					EditorGUI.indentLevel--;
					break;

				case (int)DataType.DynamicEnum:

					DrawProperty(dataType);
					DrawProperty(property.FindPropertyRelative("enumProvider"), new GUIContent("Dynamic Enum Provider"));
					break;

				case (int)DataType.StaticEnum:

					DrawProperty(dataType);
					DrawProperty(property.FindPropertyRelative("staticEnums"), new GUIContent("Static Enums"));
					break;

				default:

					DrawProperty(dataType);
					break;
			}
			EditorGUI.EndProperty();
		}

		public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
		{
			float height = EditorGUIUtility.singleLineHeight;
			height += EditorGUI.GetPropertyHeight(property.FindPropertyRelative("name"));
			height += EditorGUI.GetPropertyHeight(property.FindPropertyRelative("description"));
			height += EditorGUI.GetPropertyHeight(property.FindPropertyRelative("optional"));

			var schema = property.FindPropertyRelative("schema");

			SerializedProperty type = schema.FindPropertyRelative("type");
			height += type.enumValueIndex switch
			{
				(int)DataType.Array or (int)DataType.Object => EditorGUI.GetPropertyHeight(schema),
				(int)DataType.DynamicEnum => EditorGUI.GetPropertyHeight(property.FindPropertyRelative("enumProvider"))
					+ EditorGUI.GetPropertyHeight(type),
				(int)DataType.StaticEnum => EditorGUI.GetPropertyHeight(property.FindPropertyRelative("staticEnums"))
					+ EditorGUI.GetPropertyHeight(type),
				_ => EditorGUI.GetPropertyHeight(type),
			};
			return height;
		}

	}
}