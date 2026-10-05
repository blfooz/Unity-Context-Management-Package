
using System;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace ContextManagement
{
	[CustomEditor(typeof(Context), true)]
	public class ContextInspector : Editor
	{
		[SerializeField]
		protected VisualTreeAsset contextInspector, entryWrapper = default;

		VisualElement entries;
		SerializedProperty contextEntries;

		public override VisualElement CreateInspectorGUI()
		{
			if (contextInspector == null)
				return new Label("Context inspector template is missing.");

			var root = contextInspector.Instantiate();

			var properties = root.Q<VisualElement>("properties");
			var sourceDocumentProperty = serializedObject.FindProperty("sourceDocument");

			var sourceDocumentField = new PropertyField(sourceDocumentProperty);
			sourceDocumentField.BindProperty(sourceDocumentProperty);
			properties.Add(sourceDocumentField);



			entries = root.Q<VisualElement>("entries");
			entries.Clear();

			contextEntries = serializedObject.FindProperty("contextEntries");
			var sliceButton = root.Q<Button>("slice");
			sliceButton.clicked += Slice;
			PopulateEntries();

			return root;
		}

		void Slice()
		{
			if (target is not Context ctx)
				return;

			serializedObject.ApplyModifiedProperties();
			var previousEntries = ctx.contextEntries.ToArray();
			Undo.RecordObject(ctx, "Slice Context");

			ctx.contextEntries.Clear();
			try
			{
				ctx.Slice();
			}
			catch (Exception e)
			{
				ctx.contextEntries.Clear();
				ctx.contextEntries.AddRange(previousEntries);
				Debug.LogError($"Failed to slice context '{ctx.name}': {e.Message}");
			}

			EditorUtility.SetDirty(ctx);
			serializedObject.Update();
			contextEntries = serializedObject.FindProperty("contextEntries");
			RebuildEntries();
		}

		void RebuildEntries()
		{
			if (entries == null)
				return;

			entries.Clear();
			PopulateEntries();
		}

		void PopulateEntries()
		{
			contextEntries ??= serializedObject.FindProperty("contextEntries");

			if (contextEntries == null || entries == null)
				return;

			if (entryWrapper == null)
			{
				entries.Add(new Label("Entry template is missing."));
				return;
			}
			var ctxEntries = ((Context)target).contextEntries;
			for (int i = 0; i < ctxEntries.Count; i++)
			{
				var entry = ctxEntries[i];
				var wrapper = entryWrapper.Instantiate();

				wrapper.dataSource = entry;
				wrapper.style.marginLeft = new StyleLength((entry.depth - 1) * 18f);
				entries.Add(wrapper);
			}
		}
	}
}
