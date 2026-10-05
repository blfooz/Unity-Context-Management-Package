
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace ContextManagement
{
	public class EntitiesWindow : EditorWindow
	{
		[SerializeField]
		private VisualTreeAsset m_VisualTreeAsset = default;
		[SerializeField]
		private VisualTreeAsset m_ItemWrapper = default;
		[SerializeField]
		IEntityManager _linkedEntityManager;

		public static void Open(IEntityManager em)
		{
			var wnd = CreateWindow<EntitiesWindow>();
			wnd._linkedEntityManager = em;
			wnd.titleContent = new GUIContent(em.EntityNamePlural);
			wnd.BuildGUI();
			wnd.Show();
		}

		public void BuildGUI()
		{
			if (_linkedEntityManager == null)
			{
				rootVisualElement.Add(new Label("No Linked Entity Manager"));
				return;
			}
			var root = rootVisualElement;
			root.Clear();

			var windowUI = m_VisualTreeAsset.Instantiate();

			_linkedEntityManager.Serialize();
			var valuesProp = new SerializedObject((Object)_linkedEntityManager).FindProperty("_serializedEntities");
			if (valuesProp == null)
			{
				root.Add(new Label("Failed to find serialized entities."));
				return;
			}
			if (valuesProp.arraySize > 0)
			{
				for (int i = 0; i < valuesProp.arraySize; i++)
				{
					var entryWrapper = m_ItemWrapper.Instantiate();
					var entry = valuesProp.GetArrayElementAtIndex(i);
					var entity = entry.FindPropertyRelative("entity");
					var id = entry.FindPropertyRelative("id").stringValue;

					entryWrapper.Q<Label>("wrapperLabel").text = $"{id}";
					entryWrapper.Q<Button>("deleteButton").clicked += () => Delete(id);

					var field = new PropertyField(entity);
					field.BindProperty(entity);
					entryWrapper.Q<VisualElement>("wrapper").Add(field);
					
					windowUI.Q<ScrollView>("entriesScrollView").Add(entryWrapper);
				}
			}
			else windowUI.Add(new Label($"No {_linkedEntityManager.EntityNamePlural}"));

			root.Add(windowUI);
		}


		void Delete(string id)
		{
			if (_linkedEntityManager is Object em)
			{
				Undo.RecordObject(em, $"Delete {_linkedEntityManager.EntityName}");
				_linkedEntityManager.Delete(id);
				EditorUtility.SetDirty(em);
			}
			BuildGUI();
		}
	}
}