using UnityEditor;
using UnityEngine;

namespace ContextManagement
{
	/// <summary>
	/// Draws a <see cref="SecretAttribute"/> string field masked, with a reveal toggle, so a
	/// key is not on screen while the inspector is open.
	/// </summary>
	[CustomPropertyDrawer(typeof(SecretAttribute))]
	public class SecretDrawer : PropertyDrawer
	{
		static readonly GUIContent RevealLabel = new("Reveal", "Show the value in clear text.");
		static bool _revealed;

		public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
		{
			if (property.propertyType != SerializedPropertyType.String)
			{
				EditorGUI.PropertyField(position, property, label, true);
				return;
			}

			EditorGUI.BeginProperty(position, label, property);

			var fieldRect = new Rect(position.x, position.y,
				position.width - 54, EditorGUIUtility.singleLineHeight);
			var toggleRect = new Rect(fieldRect.xMax + 4, position.y, 50, EditorGUIUtility.singleLineHeight);

			EditorGUI.BeginChangeCheck();
			string value = _revealed
				? EditorGUI.TextField(fieldRect, label, property.stringValue)
				: EditorGUI.PasswordField(fieldRect, label, property.stringValue);
			if (EditorGUI.EndChangeCheck())
				property.stringValue = value;

			_revealed = EditorGUI.ToggleLeft(toggleRect, RevealLabel, _revealed);

			EditorGUI.EndProperty();
		}
	}
}
