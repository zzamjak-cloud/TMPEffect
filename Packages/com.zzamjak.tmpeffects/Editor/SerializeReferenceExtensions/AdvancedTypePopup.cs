using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.IMGUI.Controls;

namespace CAT.UI.SerializeReferenceExtensions
{
    public class AdvancedTypePopupItem : AdvancedDropdownItem
    {
        public Type Type { get; }

        public AdvancedTypePopupItem(Type type, string name) : base(name)
        {
            Type = type;
        }
    }

    /// <summary>
    /// 퍼지 검색 기능이 있는 타입 팝업.
    /// </summary>
    public class AdvancedTypePopup : AdvancedDropdown
    {
        const int kMaxNamespaceNestCount = 16;

        public static void AddTo(AdvancedDropdownItem root, IEnumerable<Type> types)
        {
            var itemCount = 0;

            // null 항목 추가.
            var nullItem = new AdvancedTypePopupItem(null, TypeMenuUtility.k_NullDisplayName)
            {
                id = itemCount++
            };
            root.AddChild(nullItem);

            var typeArray = types.OrderByType().ToArray();

            // 루트에 하나의 네임스페이스만 있고 중첩이 분기되지 않은 경우 단일 네임스페이스.
            var isSingleNamespace = true;
            var namespaces = new string[kMaxNamespaceNestCount];
            foreach (Type type in typeArray)
            {
                var splittedTypePath = TypeMenuUtility.GetSplittedTypePath(type);
                if (splittedTypePath.Length <= 1)
                    continue;

                for (var k = 0; splittedTypePath.Length - 1 > k; k++)
                {
                    var ns = namespaces[k];
                    if (ns == null)
                    {
                        namespaces[k] = splittedTypePath[k];
                    }
                    else if (ns != splittedTypePath[k])
                    {
                        isSingleNamespace = false;
                        break;
                    }
                }

                if (!isSingleNamespace)
                    break;
            }

            // 타입 항목 추가.
            foreach (Type type in typeArray)
            {
                var splittedTypePath = TypeMenuUtility.GetSplittedTypePath(type);
                if (splittedTypePath.Length == 0)
                    continue;

                AdvancedDropdownItem parent = root;

                // 네임스페이스 항목 추가.
                if (!isSingleNamespace)
                {
                    for (var k = 0; splittedTypePath.Length - 1 > k; k++)
                    {
                        AdvancedDropdownItem foundItem = GetItem(parent, splittedTypePath[k]);
                        if (foundItem != null)
                        {
                            parent = foundItem;
                        }
                        else
                        {
                            var newItem = new AdvancedDropdownItem(splittedTypePath[k])
                            {
                                id = itemCount++,
                            };
                            parent.AddChild(newItem);
                            parent = newItem;
                        }
                    }
                }

                // 타입 항목 추가.
                var className = ObjectNames.NicifyVariableName(splittedTypePath[^1]);
                var itemName = GetItemDisplayName(type, className);
                var item = new AdvancedTypePopupItem(type, itemName)
                {
                    id = itemCount++
                };
                parent.AddChild(item);
            }
        }

        // ISubclassSelectableName을 구현하는 타입의 MenuName을 가져옵니다.
        // 임시 인스턴스를 생성해 MenuName을 얻고, 클래스 이름을 괄호 안에 함께 표시합니다.
        static string GetItemDisplayName(Type type, string className)
        {
            if (!typeof(ISubclassSelectableName).IsAssignableFrom(type))
                return className;
            try
            {
                var instance = System.Activator.CreateInstance(type) as ISubclassSelectableName;
                if (instance?.MenuName is { Length: > 0 } menuName)
                    return menuName;
            }
            catch { }
            return className;
        }

        static AdvancedDropdownItem GetItem(AdvancedDropdownItem parent, string name)
        {
            foreach (AdvancedDropdownItem item in parent.children)
            {
                if (item.name == name)
                    return item;
            }

            return null;
        }

        static readonly float k_HeaderHeight = EditorGUIUtility.singleLineHeight * 2f;

        Type[] m_Types;

        public event Action<AdvancedTypePopupItem> OnItemSelected;

        public AdvancedTypePopup(IEnumerable<Type> types, int maxLineCount, AdvancedDropdownState state) : base(state)
        {
            SetTypes(types);
            minimumSize = new Vector2(minimumSize.x, EditorGUIUtility.singleLineHeight * maxLineCount + k_HeaderHeight);
        }

        public void SetTypes(IEnumerable<Type> types)
        {
            m_Types = types.ToArray();
        }

        protected override AdvancedDropdownItem BuildRoot()
        {
            var root = new AdvancedDropdownItem("타입 선택");
            AddTo(root, m_Types);
            return root;
        }

        protected override void ItemSelected(AdvancedDropdownItem item)
        {
            base.ItemSelected(item);
            if (item is AdvancedTypePopupItem typePopupItem) 
                OnItemSelected?.Invoke(typePopupItem);
        }
    }
}