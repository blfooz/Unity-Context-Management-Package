using UnityEditor;
using UnityEngine;

namespace ContextManagement
{
	/// <summary>
	/// Modal prompt for an API key. <see cref="APISetting.key"/> is deliberately not
	/// serialized, so it has no inspector field and has to be entered here instead.
	/// </summary>
	class APIKeyPromptWindow : EditorWindow
	{
		string apiKey = "";
		bool revealed;
		bool accepted;

		/// <summary>
		/// Show the prompt and block until it closes.
		/// </summary>
		/// <param name="settingsId">The settings id, shown to make the target clear.</param>
		/// <param name="initialKey">The key to start from, usually the one in memory.</param>
		/// <param name="key">The entered key; only meaningful when this returns true.</param>
		/// <returns>Whether a key was accepted instead of cancelled.</returns>
		public static bool TryShow(string settingsId, string initialKey, out string key)
		{
			var window = CreateInstance<APIKeyPromptWindow>();
			window.apiKey = initialKey ?? "";
			window.titleContent = new GUIContent(
				string.IsNullOrEmpty(settingsId) ? "API Key" : $"API Key ({settingsId})");
			window.minSize = new Vector2(360f, 130f);
			window.ShowModalUtility();

			key = window.apiKey;
			return window.accepted;
		}

		void OnGUI()
		{
			if (Event.current.type == EventType.KeyDown)
			{
				if (Event.current.keyCode == KeyCode.Escape)
				{
					accepted = false;
					Close();
					Event.current.Use();
					return;
				}
				if (Event.current.keyCode == KeyCode.Return
					|| Event.current.keyCode == KeyCode.KeypadEnter)
				{
					accepted = true;
					Close();
					Event.current.Use();
					return;
				}
			}

			EditorGUILayout.Space();

			using (new EditorGUILayout.HorizontalScope())
			{
				EditorGUILayout.LabelField("API Key", EditorStyles.boldLabel, GUILayout.Width(55));
				apiKey = revealed
					? EditorGUILayout.TextField(apiKey)
					: EditorGUILayout.PasswordField(apiKey);
				revealed = GUILayout.Toggle(
					revealed, "Reveal", EditorStyles.miniButton, GUILayout.Width(55));
			}

			EditorGUILayout.LabelField(
				"The key is stored in the settings file.",
				EditorStyles.miniLabel);

			EditorGUILayout.Space();

			using (new EditorGUILayout.HorizontalScope())
			{
				GUILayout.FlexibleSpace();
				if (GUILayout.Button("Cancel"))
				{
					accepted = false;
					Close();
				}
				if (GUILayout.Button("Save"))
				{
					accepted = true;
					Close();
				}
			}
		}
	}
}
