using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace ContextManagement
{
	[CustomEditor(typeof(APISetting), true)]
	public class APISettingInspector : Editor
	{
		string[] models;
		int index = 0;
		GUIStyle missingKeyStyle;
		GUIStyle availableKeyStyle;
		public async override void OnInspectorGUI()
		{
			DrawDefaultInspector();
			APISetting setting = (APISetting)target;

			if (setting.autoload && string.IsNullOrWhiteSpace(setting.key) && setting.TryGetPath(out var path))
			{
				try
				{
					var json = JObject.Parse(File.ReadAllText(path));
					setting.Load(json);
					Debug.Log($"Auto-loaded API settings from '{path}'");
				}
				catch (Exception e)
				{
					Debug.LogError($"Failed to auto-load API settings from '{path}': {e}");
				}
			}
			if (string.IsNullOrWhiteSpace(setting.key))
				GUILayout.Label("API Key Missing", KeyStatusStyle(false));
			else GUILayout.Label("API Key Available", KeyStatusStyle(true));

			if (GUILayout.Button("Get Available Models"))
			{
				var _models = await setting.GetAvailableModels();
				models = _models.Select(m => m["id"].ToString()).ToArray();
			}
			if (models?.Length > 0)
			{
				if (index > models.Length) index = 0;
				index = EditorGUILayout.Popup(index, models);
				setting.model = models[index];
			}
			if (GUILayout.Button("Save"))
			{
				if (!setting.TryGetPath(out path))
				{
					Debug.LogError("Setting has invalid save path.");
				}
				else if (setting.Credential == null)
				{
					// The key is not serialized, so the prompt is the only way to enter it.
					if (APIKeyPromptWindow.TryShow(setting.settingName, setting.key, out string enteredKey))
					{
						setting.key = enteredKey;
						if (setting.Save(path))
							Debug.Log($"Successfully saved at {path}");
						else Debug.LogError($"Failed to save at {path}");
					}
				}
				else if (setting.Save(path))
				{
					Debug.Log($"Successfully saved at {path}");
				}
				else Debug.LogError($"Failed to save at {path}");
			}

			if (GUILayout.Button("Load"))
			{
				if (setting.TryGetPath(out path))
				{
					path = Path.GetDirectoryName(path);
					path = EditorUtility.OpenFilePanel("Load API Setting", path, "json");
					if (!string.IsNullOrEmpty(path))
					{
						if (setting.Load(path))
							Debug.Log($"Successfully loaded from {path}");
						else Debug.LogError($"Failed to load from {path}");
					}
				}
			}
		}

		/// <summary>
		/// The banner for the key status: a bold box tinted red when the key is missing and
		/// green when it is available, so the state stands out in a busy inspector.
		/// </summary>
		/// <param name="hasKey">Whether the key is available.</param>
		GUIStyle KeyStatusStyle(bool hasKey)
		{
			GUIStyle style = hasKey ? availableKeyStyle : missingKeyStyle;
			if (style != null) return style;

			style = new GUIStyle(EditorStyles.helpBox)
			{
				alignment = TextAnchor.MiddleCenter,
				fontStyle = FontStyle.Bold,
				fontSize = EditorStyles.boldLabel.fontSize,
				padding = new RectOffset(8, 8, 6, 6)
			};
			style.normal.textColor = hasKey
				? (EditorGUIUtility.isProSkin ? new Color(0.35f, 0.9f, 0.35f) : new Color(0.05f, 0.45f, 0.05f))
				: (EditorGUIUtility.isProSkin ? new Color(1f, 0.4f, 0.4f) : new Color(0.65f, 0f, 0f));

			if (hasKey) availableKeyStyle = style;
			else missingKeyStyle = style;
			return style;
		}

	}
}
