using UnityEditor;
using UnityEngine;
using TMPro;

namespace CAT.UI
{
    /// <summary>
    /// TMPMaskFlow의 Editor preview 컨트롤을 제공한다.
    /// </summary>
    [CustomEditor(typeof(TMPMaskFlow))]
    public class TMPMaskFlowEditor : Editor
    {
        private const string INTERVAL_PROPERTY_NAME = "_interval";
        private const string TEXT_ENTRIES_PROPERTY_NAME = "_textEntries";
        private const string PREVIEW_TEXT_PROPERTY_NAME = "_previewText";

        private TMPMaskFlow _target;
        private bool _showPlaybackSection = true;
        private bool _showPreviewTextSection = true;
        private bool _previewTextInitialized;
        private string _previewText = string.Empty;
        private int _previewEntryIndex;
        private int _lastPreviewEntryIndex = -1;
        private int _lastPreviewEntryCount = -1;
        private bool _isEditorPlaying;
        private double _lastEditorTime;

        private void OnEnable()
        {
            _target = (TMPMaskFlow)target;
        }

        private void OnDisable()
        {
            if (_isEditorPlaying)
            {
                StopEditorPreview();
            }
        }

        public override void OnInspectorGUI()
        {
            DrawPlaybackSection();

            EditorGUILayout.Space(5);

            serializedObject.Update();
            DrawPreviewTextSection();

            EditorGUILayout.Space(5);

            EditorGUI.BeginChangeCheck();
            DrawFlowProperties();
            if (EditorGUI.EndChangeCheck())
            {
                serializedObject.ApplyModifiedProperties();
                _target.Refresh();
                EditorApplication.QueuePlayerLoopUpdate();
                SceneView.RepaintAll();
                return;
            }

            serializedObject.ApplyModifiedProperties();
        }

        /// <summary>
        /// Editor 미리보기 입력값을 현재 테스트 대상에 적용한다.
        /// </summary>
        public static void ApplyPreviewTextForEditor(TMPMaskFlow flow, SerializedObject serializedFlow, string previewText, int entryIndex)
        {
            if (flow == null)
            {
                return;
            }

            string safeText = previewText ?? string.Empty;
            if (TryApplyPreviewTextToEntry(flow, serializedFlow, safeText, entryIndex))
            {
                flow.Refresh();
                return;
            }

            TextMeshProUGUI sourceText = flow.GetComponent<TextMeshProUGUI>();
            if (sourceText == null)
            {
                return;
            }

            Undo.RecordObject(sourceText, "Change TMP Mask Flow Preview Text");
            sourceText.text = safeText;
            EditorUtility.SetDirty(sourceText);
            flow.Refresh();
        }

        private static bool TryApplyPreviewTextToEntry(TMPMaskFlow flow, SerializedObject serializedFlow, string previewText, int entryIndex)
        {
            SerializedProperty textEntriesProperty = serializedFlow?.FindProperty(TEXT_ENTRIES_PROPERTY_NAME);
            if (textEntriesProperty == null || !textEntriesProperty.isArray || textEntriesProperty.arraySize <= 0)
            {
                return false;
            }

            int safeEntryIndex = Mathf.Clamp(entryIndex, 0, textEntriesProperty.arraySize - 1);
            SerializedProperty textEntryProperty = textEntriesProperty.GetArrayElementAtIndex(safeEntryIndex);
            SerializedProperty previewTextProperty = textEntryProperty?.FindPropertyRelative(PREVIEW_TEXT_PROPERTY_NAME);
            if (previewTextProperty == null)
            {
                return false;
            }

            Undo.RecordObject(flow, "Change TMP Mask Flow Preview Text");
            previewTextProperty.stringValue = previewText;
            serializedFlow.ApplyModifiedProperties();
            EditorUtility.SetDirty(flow);
            return true;
        }

        private void DrawPreviewTextSection()
        {
            _showPreviewTextSection = EditorGUILayout.BeginFoldoutHeaderGroup(_showPreviewTextSection, "Preview Text");

            if (_showPreviewTextSection)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                SerializedProperty textEntriesProperty = serializedObject.FindProperty(TEXT_ENTRIES_PROPERTY_NAME);
                int entryCount = textEntriesProperty != null && textEntriesProperty.isArray ? textEntriesProperty.arraySize : 0;
                if (entryCount > 0)
                {
                    _previewEntryIndex = Mathf.Clamp(_previewEntryIndex, 0, entryCount - 1);
                    _previewEntryIndex = EditorGUILayout.IntSlider("Entry Index", _previewEntryIndex, 0, entryCount - 1);
                    EditorGUILayout.LabelField("Target", GetEntryLabel(textEntriesProperty, _previewEntryIndex), EditorStyles.miniLabel);
                }
                else
                {
                    _previewEntryIndex = 0;
                    EditorGUILayout.LabelField("Target", "Source TMP Text", EditorStyles.miniLabel);
                }

                SyncPreviewTextBuffer(textEntriesProperty, entryCount);

                EditorGUI.BeginChangeCheck();
                string nextPreviewText = EditorGUILayout.TextArea(_previewText, GUILayout.MinHeight(54f));
                if (EditorGUI.EndChangeCheck())
                {
                    _previewText = nextPreviewText;
                    ApplyPreviewTextForEditor(_target, serializedObject, _previewText, _previewEntryIndex);
                    _lastPreviewEntryIndex = _previewEntryIndex;
                    _lastPreviewEntryCount = entryCount;
                    serializedObject.Update();
                    QueuePreviewRepaint();
                }

                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Reload Current"))
                {
                    _previewText = GetCurrentPreviewText(textEntriesProperty, entryCount, _previewEntryIndex);
                    _previewTextInitialized = true;
                }

                if (GUILayout.Button("Refresh Preview"))
                {
                    _target.Refresh();
                    QueuePreviewRepaint();
                }

                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        private void SyncPreviewTextBuffer(SerializedProperty textEntriesProperty, int entryCount)
        {
            if (_previewTextInitialized &&
                _lastPreviewEntryIndex == _previewEntryIndex &&
                _lastPreviewEntryCount == entryCount)
            {
                return;
            }

            _previewText = GetCurrentPreviewText(textEntriesProperty, entryCount, _previewEntryIndex);
            _previewTextInitialized = true;
            _lastPreviewEntryIndex = _previewEntryIndex;
            _lastPreviewEntryCount = entryCount;
        }

        private string GetCurrentPreviewText(SerializedProperty textEntriesProperty, int entryCount, int entryIndex)
        {
            if (entryCount > 0 && textEntriesProperty != null)
            {
                int safeEntryIndex = Mathf.Clamp(entryIndex, 0, entryCount - 1);
                SerializedProperty textEntryProperty = textEntriesProperty.GetArrayElementAtIndex(safeEntryIndex);
                SerializedProperty previewTextProperty = textEntryProperty?.FindPropertyRelative(PREVIEW_TEXT_PROPERTY_NAME);
                return previewTextProperty != null ? previewTextProperty.stringValue : string.Empty;
            }

            TextMeshProUGUI sourceText = _target != null ? _target.GetComponent<TextMeshProUGUI>() : null;
            return sourceText != null ? sourceText.text : string.Empty;
        }

        private static string GetEntryLabel(SerializedProperty textEntriesProperty, int entryIndex)
        {
            SerializedProperty textEntryProperty = textEntriesProperty.GetArrayElementAtIndex(entryIndex);
            SerializedProperty keyProperty = textEntryProperty?.FindPropertyRelative("_key");
            string key = keyProperty != null ? keyProperty.stringValue : string.Empty;
            return string.IsNullOrEmpty(key) ? $"Entry {entryIndex}" : key;
        }

        private static void QueuePreviewRepaint()
        {
            EditorApplication.QueuePlayerLoopUpdate();
            SceneView.RepaintAll();
        }

        private void DrawFlowProperties()
        {
            SerializedProperty property = serializedObject.GetIterator();
            bool enterChildren = true;
            while (property.NextVisible(enterChildren))
            {
                enterChildren = false;
                if (ShouldHideProperty(property))
                {
                    continue;
                }

                if (property.propertyPath == "m_Script")
                {
                    using (new EditorGUI.DisabledScope(true))
                    {
                        EditorGUILayout.PropertyField(property, true);
                    }
                    continue;
                }

                EditorGUILayout.PropertyField(property, true);
            }
        }

        private bool ShouldHideProperty(SerializedProperty property)
        {
            if (property.propertyPath != INTERVAL_PROPERTY_NAME)
            {
                return false;
            }

            SerializedProperty directionProperty = serializedObject.FindProperty("_direction");
            TMPMaskFlow.FlowDirection direction = directionProperty != null
                ? (TMPMaskFlow.FlowDirection)directionProperty.enumValueIndex
                : _target.Direction;

            return !TMPMaskFlow.UsesInterval(direction);
        }

        private void DrawPlaybackSection()
        {
            _showPlaybackSection = EditorGUILayout.BeginFoldoutHeaderGroup(_showPlaybackSection, "Playback Control");

            if (_showPlaybackSection)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                if (Application.isPlaying)
                {
                    DrawRuntimeControls();
                }
                else
                {
                    DrawEditorPreviewControls();
                }

                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        private void DrawRuntimeControls()
        {
            EditorGUILayout.BeginHorizontal();

            GUI.enabled = !_target.IsPlaying;
            if (GUILayout.Button("Play", GUILayout.Height(25)))
            {
                _target.Play();
            }

            GUI.enabled = _target.IsPlaying;
            if (GUILayout.Button("Stop", GUILayout.Height(25)))
            {
                _target.Stop();
            }

            GUI.enabled = true;
            if (GUILayout.Button("Restart", GUILayout.Height(25)))
            {
                _target.Restart();
            }

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.LabelField("Status", _target.IsPlaying ? "Playing" : "Stopped", EditorStyles.miniLabel);
        }

        private void DrawEditorPreviewControls()
        {
            EditorGUILayout.BeginHorizontal();

            if (!_isEditorPlaying)
            {
                GUI.backgroundColor = new Color(0.7f, 1f, 0.7f);
                if (GUILayout.Button("Play (직접)", GUILayout.Height(25)))
                {
                    StartEditorPreview();
                }
                GUI.backgroundColor = Color.white;

                if (GUILayout.Button("Restart (직접)", GUILayout.Height(25)))
                {
                    _target.Stop();
                    StartEditorPreview();
                }
            }
            else
            {
                GUI.backgroundColor = new Color(1f, 0.7f, 0.7f);
                if (GUILayout.Button("Stop", GUILayout.Height(25)))
                {
                    StopEditorPreview();
                }
                GUI.backgroundColor = Color.white;

                if (GUILayout.Button("Restart (직접)", GUILayout.Height(25)))
                {
                    _target.Restart();
                    _lastEditorTime = EditorApplication.timeSinceStartup;
                }
            }

            if (GUILayout.Button("Refresh", GUILayout.Height(25)))
            {
                _target.Refresh();
                EditorApplication.QueuePlayerLoopUpdate();
                SceneView.RepaintAll();
            }

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.LabelField("Status", _isEditorPlaying ? "Editor Preview" : "Stopped", EditorStyles.miniLabel);
        }

        private void StartEditorPreview()
        {
            if (_isEditorPlaying)
            {
                return;
            }

            _isEditorPlaying = true;
            _lastEditorTime = EditorApplication.timeSinceStartup;

            _target.Play();
            EditorApplication.update += EditorUpdate;
        }

        private void StopEditorPreview()
        {
            if (!_isEditorPlaying)
            {
                return;
            }

            _isEditorPlaying = false;
            EditorApplication.update -= EditorUpdate;

            if (_target != null)
            {
                _target.Stop();
            }

            EditorApplication.QueuePlayerLoopUpdate();
            SceneView.RepaintAll();
        }

        private void EditorUpdate()
        {
            if (!_isEditorPlaying)
            {
                return;
            }

            if (_target == null)
            {
                StopEditorPreview();
                return;
            }

            double currentTime = EditorApplication.timeSinceStartup;
            float deltaTime = (float)(currentTime - _lastEditorTime);
            _lastEditorTime = currentTime;

            _target.AdvanceFlow(deltaTime);

            EditorApplication.QueuePlayerLoopUpdate();
            Repaint();
            SceneView.RepaintAll();
        }
    }
}
