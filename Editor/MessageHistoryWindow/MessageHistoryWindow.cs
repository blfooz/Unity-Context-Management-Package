using System;
using System.IO;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace ContextManagement
{
    public class MessageHistoryWindow : EditorWindow
    {
        [SerializeField]
        private VisualTreeAsset m_VisualTreeAsset = default;
        [SerializeField]
        VisualTreeAsset m_entryWrapper;

        [SerializeField]
        LLMRequestManager _linkedRequestManager;

        private bool _showMemoryWindowedView = false;
        private int _windowSize = 20;

        private const int MinWindowSize = 1;
        private const int MaxWindowSize = 100;

        private SerializedObject SerializedObject => new(_linkedRequestManager);

        private int WindowSize
        {
            get => _linkedRequestManager is ChatRequestManager chatRequestManager
                ? chatRequestManager.memoryWindowSize
                : _windowSize;
            set
            {
                int clamped = Mathf.Clamp(value, MinWindowSize, MaxWindowSize);

                if (_linkedRequestManager is ChatRequestManager chatRequestManager)
                {
                    Undo.RecordObject(chatRequestManager, "Change Memory Window Size");
                    chatRequestManager.memoryWindowSize = clamped;
                    EditorUtility.SetDirty(chatRequestManager);
                }
                else
                {
                    _windowSize = clamped;
                }
            }
        }

        public string WindowTitle =>
            _linkedRequestManager == null
                ? "Messages"
                : $"Messages of {_linkedRequestManager.gameObject.name}";

        // ═══════════════════════════════════════════════════════════
        //  Open / Create
        // ═══════════════════════════════════════════════════════════

        public static void Open(LLMRequestManager cm)
        {
            MessageHistoryWindow wnd = CreateWindow<MessageHistoryWindow>();
            wnd._linkedRequestManager = cm;
            wnd.titleContent = new GUIContent("Messages");
            wnd.BuildGUI();
            wnd.Show();
        }

        public void CreateGUI()
        {
            if (_linkedRequestManager != null)
                BuildGUI();
            else
                rootVisualElement.Add(new Label("No Linked Context Manager"));
        }

        // ═══════════════════════════════════════════════════════════
        //  Build / Rebuild
        // ═══════════════════════════════════════════════════════════

        public void BuildGUI()
        {
            var root = rootVisualElement;
            root.Clear();

            var windowUI = m_VisualTreeAsset.Instantiate();

            // ── Title ──
            var titleLabel = windowUI.Q<Label>("titleLabel");
            titleLabel.text = WindowTitle;

            BuildMemoryWindowRelatedGUI(windowUI);

            // ── Buttons ──
            windowUI.Q<Button>("saveButton").clicked += Save;
            windowUI.Q<Button>("saveAsButton").clicked += SaveAs;
            windowUI.Q<Button>("loadButton").clicked += Load;
            windowUI.Q<Button>("addButton").clicked += Add;
            windowUI.Q<Button>("clearButton").clicked += Clear;

            // ── Entries ──
            var entriesContainer = windowUI.Q<VisualElement>("entries");
            var so = SerializedObject;
            var entries = so.FindProperty("messages").FindPropertyRelative("_entries");

            int totalCount = entries.arraySize;
            int startIndex = 0;
            if (_showMemoryWindowedView && totalCount > 0)
            {
                // Find system prompt (always at index 0) + last N entries
                startIndex = Mathf.Max(0, totalCount - WindowSize);
            }

            int totalTokens = 0;

            for (int i = startIndex; i < totalCount; i++)
            {
                var entry = entries.GetArrayElementAtIndex(i);

                var entryWrapper = m_entryWrapper.Instantiate();
                entryWrapper.Q<Label>("messageLabel").text = $"Message {i + 1}";
                int j = i;
                entryWrapper.Q<Button>("deleteButton").clicked += () => Delete(j);

                var entryField = new PropertyField(entry);
                entryField.BindProperty(entry);
                entryWrapper.Q<VisualElement>("wrapper").Add(entryField);
                entriesContainer.Add(entryWrapper);

                // Accumulate token count
                if (entry.managedReferenceValue is Message msg && msg.tokenUsage != null)
                    totalTokens += msg.tokenUsage.promptTokens + msg.tokenUsage.completionTokens;
            }

            // ── Footer info ──
            var footerLabel = windowUI.Q<Label>("footerInfoLabel");
            string footerText = $"Total Messages: {totalCount}";
            if (totalTokens > 0)
                footerText += $"  |  Total tokens consumed: {totalTokens}";
            if (_showMemoryWindowedView && totalCount > WindowSize)
                footerText += $"  (showing last {Mathf.Min(WindowSize, totalCount - startIndex)} entries)";
            footerLabel.text = footerText;

            root.Add(windowUI);
        }

        void BuildMemoryWindowRelatedGUI(TemplateContainer windowUI)
        {

            // ── Windowed view controls ──
            var windowedToggle = windowUI.Q<Toggle>("memoryWindowToggle");
            var windowSizeField = windowUI.Q<IntegerField>("windowSizeField");

            windowedToggle.value = _showMemoryWindowedView;
            windowedToggle.RegisterValueChangedCallback(evt =>
            {
                _showMemoryWindowedView = evt.newValue;
                BuildGUI();
            });

            windowSizeField.SetEnabled(_showMemoryWindowedView);
            windowSizeField.value = WindowSize;
            windowSizeField.RegisterValueChangedCallback(evt =>
            {
                int clamped = Mathf.Clamp(evt.newValue, MinWindowSize, MaxWindowSize);
                WindowSize = clamped;
                windowSizeField.value = clamped;
                if (_showMemoryWindowedView) BuildGUI();
            });
        }

        // ═══════════════════════════════════════════════════════════
        //  Actions
        // ═══════════════════════════════════════════════════════════

        /// <summary>
        /// Add a new empty message to the linked message history
        /// </summary>
        void Add()
        {
            Undo.RecordObject(_linkedRequestManager, "Add Message");
            _linkedRequestManager.messages.Add(new Message(Role.user, "Content"));
            EditorUtility.SetDirty(_linkedRequestManager);
            BuildGUI();
        }

        void Delete(int i)
        {
            if (_linkedRequestManager.messages[i] is SummaryMessage summary)
            {
                Undo.RecordObject(_linkedRequestManager, "Recover Archived Entries");
                summary.RecoverArchived(_linkedRequestManager.messages);
                EditorUtility.SetDirty(_linkedRequestManager);
            }
            else
            {
                Undo.RecordObject(_linkedRequestManager, "Delete Message");
                _linkedRequestManager.messages.Remove(i);
                EditorUtility.SetDirty(_linkedRequestManager);
            }
            BuildGUI();
        }

        void Clear()
        {
            Undo.RecordObject(_linkedRequestManager, "Clear Messages");
            _linkedRequestManager.messages.Reset();
            EditorUtility.SetDirty(_linkedRequestManager);
            BuildGUI();
        }

        void Save()
        {
            if (_linkedRequestManager.messages.Save())
                Debug.Log("Saved successfully");
            else
                SaveAs();
        }

        void SaveAs()
        {
            var messages = _linkedRequestManager.messages;
            string directory = string.IsNullOrEmpty(messages.LastSavePath)
                ? Application.persistentDataPath
                : Path.GetDirectoryName(messages.LastSavePath);

            var path = EditorUtility.SaveFilePanel("Save As", directory,
                $"messages-{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.json", "json");

            if (!string.IsNullOrEmpty(path) && messages.SaveAs(path))
                Debug.Log("Saved successfully");
        }

        void Load()
        {
            var messages = _linkedRequestManager.messages;
            string directory = string.IsNullOrEmpty(messages.LastSavePath)
                ? Application.persistentDataPath
                : Path.GetDirectoryName(messages.LastSavePath);

            var path = EditorUtility.OpenFilePanel("Load", directory, "json");

            if (!string.IsNullOrEmpty(path) && messages.Load(path))
            {
                Debug.Log("Loaded successfully");
                BuildGUI();
            }
        }
    }
}
