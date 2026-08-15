using UnityEngine;
using UnityEditor;
using TMPro;

namespace CAT.UI
{
    [CustomEditor(typeof(TMPCurve))]
    public class TMPCurveEditor : Editor
    {
        private const float GetButtonWidth = 44f;

        // 커브 편집기에 표시되는 값 범위 — X는 0~1 고정(텍스트 정규화 좌표),
        // Y는 -2~2로 확장하여 1을 초과하는 키프레임도 자유롭게 편집/평가할 수 있게 함
        private static readonly Rect CurveRanges = new Rect(0f, -2f, 1f, 4f);
        private static readonly Color CurveColor = new Color(0.4f, 0.85f, 0.4f);

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            DrawUnclampedCurveField();
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_curveScale"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_preserveVerticalCenter"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_enableDynamicCurveScale"));

            DrawReferenceVisibleCharacterCountRow();
            DrawReferenceFontSizeRow();
            DrawReferenceRectSizeRow();

            EditorGUILayout.PropertyField(serializedObject.FindProperty("_rotateAlongCurve"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_rotationStrength"));

            serializedObject.ApplyModifiedProperties();
        }

        /// <summary>
        /// _curve 필드를 확장된 Y 범위(-2..2)로 표시하여 1을 초과하는 값도 편집 가능하게 한다.
        /// 기본 PropertyField는 미리보기 그리드가 0..1로 고정되어 키프레임 상한이 제한된 것처럼 보인다.
        /// </summary>
        private void DrawUnclampedCurveField()
        {
            SerializedProperty prop = serializedObject.FindProperty("_curve");
            GUIContent label = new GUIContent(prop.displayName, prop.tooltip);

            EditorGUI.BeginChangeCheck();
            AnimationCurve newCurve = EditorGUILayout.CurveField(
                label,
                prop.animationCurveValue,
                CurveColor,
                CurveRanges);
            if (EditorGUI.EndChangeCheck())
            {
                prop.animationCurveValue = newCurve;
            }
        }

        private void DrawReferenceVisibleCharacterCountRow()
        {
            SerializedProperty prop = serializedObject.FindProperty("_referenceVisibleCharacterCount");
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PropertyField(prop);
            if (GUILayout.Button("Get", GUILayout.Width(GetButtonWidth)))
            {
                TMP_Text tmp = GetTmpText();
                if (tmp != null)
                {
                    Undo.RecordObject(target, "Get Reference Visible Character Count");
                    tmp.ForceMeshUpdate(true);
                    prop.floatValue = CountVisibleCharacters(tmp.textInfo);
                    serializedObject.ApplyModifiedProperties();
                    EditorUtility.SetDirty(target);
                }
            }
            EditorGUILayout.EndHorizontal();
        }

        private void DrawReferenceFontSizeRow()
        {
            SerializedProperty prop = serializedObject.FindProperty("_referenceFontSize");
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PropertyField(prop);
            if (GUILayout.Button("Get", GUILayout.Width(GetButtonWidth)))
            {
                TMP_Text tmp = GetTmpText();
                if (tmp != null)
                {
                    Undo.RecordObject(target, "Get Reference Font Size");
                    tmp.ForceMeshUpdate(true);
                    prop.floatValue = tmp.fontSize;
                    serializedObject.ApplyModifiedProperties();
                    EditorUtility.SetDirty(target);
                }
            }
            EditorGUILayout.EndHorizontal();
        }

        private void DrawReferenceRectSizeRow()
        {
            SerializedProperty prop = serializedObject.FindProperty("_referenceRectSize");
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PropertyField(prop);
            if (GUILayout.Button("Get", GUILayout.Width(GetButtonWidth)))
            {
                RectTransform rt = ((Component)target).GetComponent<RectTransform>();
                if (rt != null)
                {
                    Undo.RecordObject(target, "Get Reference Rect Size");
                    prop.vector2Value = rt.rect.size;
                    serializedObject.ApplyModifiedProperties();
                    EditorUtility.SetDirty(target);
                }
                else
                {
                    EditorUtility.DisplayDialog(
                        "TMP Curve",
                        "RectTransform이 없습니다. UI TextMeshPro(UGUI)에서만 Rect 크기를 가져올 수 있습니다.",
                        "확인");
                }
            }
            EditorGUILayout.EndHorizontal();
        }

        private TMP_Text GetTmpText()
        {
            return ((Component)target).GetComponent<TMP_Text>();
        }

        private static int CountVisibleCharacters(TMP_TextInfo textInfo)
        {
            if (textInfo == null) return 0;
            int n = 0;
            for (int i = 0; i < textInfo.characterCount; i++)
            {
                if (textInfo.characterInfo[i].isVisible) n++;
            }
            return n;
        }
    }
}
