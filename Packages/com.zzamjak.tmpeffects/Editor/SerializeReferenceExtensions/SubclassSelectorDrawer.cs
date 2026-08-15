using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.IMGUI.Controls;

namespace CAT.UI.SerializeReferenceExtensions
{
    [CustomPropertyDrawer(typeof(SubclassSelectorAttribute))]
    public class SubclassSelectorDrawer : PropertyDrawer
    {
        struct TypePopupCache
        {
            public AdvancedTypePopup TypePopup { get; }
            public AdvancedDropdownState State { get; }

            public TypePopupCache(AdvancedTypePopup typePopup, AdvancedDropdownState state)
            {
                TypePopup = typePopup;
                State = state;
            }
        }

        const int k_MaxTypePopupLineCount = 13;
        const float k_BoxPaddingV = 4f;
        const float k_DividerHeight = 1f;

        static GUIStyle s_RichTextPopupStyle;
        static GUIStyle RichTextPopupStyle =>
            s_RichTextPopupStyle ??= new GUIStyle(EditorStyles.popup) { richText = true };

        static readonly GUIContent k_NullDisplayName = new(TypeMenuUtility.k_NullDisplayName);
        static readonly GUIContent k_IsNotManagedReferenceLabel = new("프로퍼티 타입이 managed reference가 아닙니다.");

        readonly Dictionary<string, TypePopupCache> m_TypePopups = new();
        readonly Dictionary<string, GUIContent> m_TypeNameCaches = new();

        SerializedProperty m_TargetProperty;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            if (property.propertyType == SerializedPropertyType.ManagedReference)
            {
                var subclassSelectorAttribute = (SubclassSelectorAttribute)attribute;
                bool showBox = subclassSelectorAttribute.ShowBox;

                if (showBox)
                {
                    var prevColor = GUI.color;
                    GUI.color = new Color(prevColor.r, prevColor.g, prevColor.b, prevColor.a * 0.6f);
                    GUI.Box(position, GUIContent.none, EditorStyles.helpBox);
                    GUI.color = prevColor;
                }

                float pad = showBox ? k_BoxPaddingV : 0f;

                // 박스 내부 콘텐츠 영역 (수평/수직 패딩 적용)
                Rect innerRect = new Rect(
                    position.x + pad,
                    position.y + pad,
                    position.width - pad * 2,
                    position.height
                );

                // 리스트에서 라벨 겹침을 방지하기 위해 라벨을 먼저 렌더링
                Rect foldoutLabelRect = new Rect(innerRect)
                {
                    height = EditorGUIUtility.singleLineHeight
                };

                // NOTE: IndentedRect는 추가 들여쓰기를 유발하므로 비활성화해야 합니다.
                Rect popupPosition = EditorGUI.PrefixLabel(foldoutLabelRect, label);

#if UNITY_2021_3_OR_NEWER
                // managed reference의 ToString()으로 라벨 텍스트를 재정의합니다.
                if (subclassSelectorAttribute.UseToStringAsLabel && !property.hasMultipleDifferentValues)
                {
                    var managedReferenceValue = property.managedReferenceValue;
                    if (managedReferenceValue != null)
                        label.text = managedReferenceValue.ToString();
                }
#endif

                // 서브클래스 선택 팝업을 그립니다.
                if (EditorGUI.DropdownButton(popupPosition, GetTypeName(property), FocusType.Keyboard, RichTextPopupStyle))
                {
                    TypePopupCache popup = GetTypePopup(property);
                    m_TargetProperty = property;
                    popup.TypePopup.Show(popupPosition);
                }

                float childStartY = innerRect.y + EditorGUIUtility.singleLineHeight +
                                    EditorGUIUtility.standardVerticalSpacing;

                using (new EditorGUI.IndentLevelScope())
                {
                    // 이 타입에 대한 커스텀 프로퍼티 드로어가 있는지 확인합니다.
                    PropertyDrawer customDrawer = GetCustomPropertyDrawer(property);
                    if (customDrawer != null)
                    {
                        // 커스텀 프로퍼티 드로어로 프로퍼티를 그립니다.
                        Rect indentedRect = innerRect;
                        indentedRect.height = customDrawer.GetPropertyHeight(property, label);
                        indentedRect.y = childStartY;
                        customDrawer.OnGUI(indentedRect, property, label);
                    }
                    else
                    {
                        Rect childPosition = innerRect;
                        childPosition.y = childStartY;
                        foreach (SerializedProperty childProperty in property.GetChildProperties())
                        {
                            var height = EditorGUI.GetPropertyHeight(childProperty,
                                new GUIContent(childProperty.displayName, childProperty.tooltip), true);
                            childPosition.height = height;
                            EditorGUI.PropertyField(childPosition, childProperty, true);

                            childPosition.y += height + EditorGUIUtility.standardVerticalSpacing;
                        }
                    }
                }
            }
            else
            {
                EditorGUI.LabelField(position, label, k_IsNotManagedReferenceLabel);
            }

            EditorGUI.EndProperty();
        }

        PropertyDrawer GetCustomPropertyDrawer(SerializedProperty property)
        {
            Type propertyType = ManagedReferenceUtility.GetType(property.managedReferenceFullTypename);
            if (propertyType != null
                && PropertyDrawerCache.TryGetPropertyDrawer(propertyType, out PropertyDrawer drawer))
            {
                return drawer;
            }

            return null;
        }

        TypePopupCache GetTypePopup(SerializedProperty property)
        {
            // 이 문자열을 캐시합니다. 이 프로퍼티는 내부적으로 Assembly.GetName을 호출하여 큰 메모리 할당이 발생합니다.
            var managedReferenceFieldTypename = property.managedReferenceFieldTypename;

            if (!m_TypePopups.TryGetValue(managedReferenceFieldTypename, out TypePopupCache result))
            {
                var state = new AdvancedDropdownState();

                Type baseType = ManagedReferenceUtility.GetType(managedReferenceFieldTypename);
                var popup = new AdvancedTypePopup(
                    TypeSearch.GetTypes(baseType),
                    k_MaxTypePopupLineCount,
                    state
                );

                popup.OnItemSelected += item =>
                {
                    Type type = item.Type;

                    // 개별 직렬화된 오브젝트에 변경사항을 적용합니다.
                    foreach (var targetObject in m_TargetProperty.serializedObject.targetObjects)
                    {
                        SerializedObject individualObject = new SerializedObject(targetObject);
                        SerializedProperty individualProperty =
                            individualObject.FindProperty(m_TargetProperty.propertyPath);
                        var obj = individualProperty.SetManagedReference(type);
                        individualProperty.isExpanded = obj != null;

                        individualObject.ApplyModifiedProperties();
                        individualObject.Update();
                    }
                };

                result = new TypePopupCache(popup, state);
                m_TypePopups.Add(managedReferenceFieldTypename, result);
            }

            return result;
        }

        GUIContent GetTypeName(SerializedProperty property)
        {
            // 이 문자열을 캐시합니다.
            var managedReferenceFullTypename = property.managedReferenceFullTypename;

            if (string.IsNullOrEmpty(managedReferenceFullTypename))
                return k_NullDisplayName;

            if (m_TypeNameCaches.TryGetValue(managedReferenceFullTypename, out GUIContent cachedTypeName))
                return cachedTypeName;

            Type type = ManagedReferenceUtility.GetType(managedReferenceFullTypename);
            string displayName;
            string tooltip = null;

            // ISubclassSelectableName을 구현하는 경우 MenuName을 표시 이름으로 사용합니다.
            // 클래스 이름은 작은 반투명 텍스트로 함께 표시됩니다.
            if (property.managedReferenceValue is ISubclassSelectableName selectableName)
            {
                string className = ObjectNames.NicifyVariableName(type.Name);
                displayName = $"{selectableName.MenuName}  <size=10><color=#aaaaaacc>({className})</color></size>";
                tooltip = className;
            }
            else
            {
                displayName = ObjectNames.NicifyVariableName(type.Name);
            }

            GUIContent result = new GUIContent(displayName, tooltip);
            m_TypeNameCaches.Add(managedReferenceFullTypename, result);
            return result;
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            if (property.propertyType != SerializedPropertyType.ManagedReference)
                return EditorGUIUtility.singleLineHeight;

            var subclassSelectorAttribute = (SubclassSelectorAttribute)attribute;
            bool showBox = subclassSelectorAttribute.ShowBox;
            bool hasChildren = property.managedReferenceValue != null;

            var height = 0f;
            height += EditorGUIUtility.singleLineHeight;
            height += EditorGUIUtility.standardVerticalSpacing;

            if (showBox)
            {
                height += k_BoxPaddingV * 2;
                if (hasChildren)
                    height += k_DividerHeight + EditorGUIUtility.standardVerticalSpacing;
            }

            PropertyDrawer customDrawer = GetCustomPropertyDrawer(property);
            if (customDrawer != null)
            {
                height += customDrawer.GetPropertyHeight(property, label);
                return height;
            }

            foreach (SerializedProperty childProperty in property.GetChildProperties())
            {
                var childHeight = EditorGUI.GetPropertyHeight(
                    childProperty,
                    new GUIContent(childProperty.displayName, childProperty.tooltip),
                    true
                );

                height += childHeight + EditorGUIUtility.standardVerticalSpacing;
            }

            height -= EditorGUIUtility.standardVerticalSpacing;
            return height;
        }
    }
}