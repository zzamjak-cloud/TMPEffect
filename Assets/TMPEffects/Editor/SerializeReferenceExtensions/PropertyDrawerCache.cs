using System;
using System.Collections.Generic;
using UnityEditor;
using System.Reflection;

namespace CAT.UI.SerializeReferenceExtensions
{
    public static class PropertyDrawerCache
    {
        static readonly Dictionary<Type, PropertyDrawer> s_Caches = new();

        public static bool TryGetPropertyDrawer(Type type, out PropertyDrawer drawer)
        {
            if (s_Caches.TryGetValue(type, out drawer))
                return drawer != null;

            Type drawerType = GetCustomPropertyDrawerType(type);
            drawer = drawerType != null ? (PropertyDrawer)Activator.CreateInstance(drawerType) : null;

            s_Caches.Add(type, drawer);
            return drawer != null;
        }

        static Type GetCustomPropertyDrawerType(Type type)
        {
            var interfaceTypes = type.GetInterfaces();

            var types = TypeCache.GetTypesWithAttribute<CustomPropertyDrawer>();
            foreach (Type drawerType in types)
            {
                var customPropertyDrawerAttributes = drawerType.GetCustomAttributes(typeof(CustomPropertyDrawer), true);
                foreach (CustomPropertyDrawer customPropertyDrawer in customPropertyDrawerAttributes)
                {
                    var field = customPropertyDrawer.GetType()
                        .GetField("m_Type", BindingFlags.NonPublic | BindingFlags.Instance);
                    if (field != null)
                    {
                        if (field.GetValue(customPropertyDrawer) is Type fieldType)
                        {
                            if (fieldType == type)
                                return drawerType;

                            // PropertyDrawer가 자식 클래스에도 적용 가능한 경우, 일치하는지 확인합니다
                            var useForChildrenField = customPropertyDrawer.GetType().GetField("m_UseForChildren",
                                BindingFlags.NonPublic | BindingFlags.Instance);
                            if (useForChildrenField != null)
                            {
                                var useForChildrenValue = useForChildrenField.GetValue(customPropertyDrawer);
                                if (useForChildrenValue is true)
                                {
                                    // 인터페이스 확인
                                    if (Array.Exists(interfaceTypes, interfaceType => interfaceType == fieldType))
                                        return drawerType;

                                    // 파생 타입 확인
                                    Type baseType = type.BaseType;
                                    while (baseType != null)
                                    {
                                        if (baseType == fieldType)
                                            return drawerType;

                                        baseType = baseType.BaseType;
                                    }
                                }
                            }
                        }
                    }
                }
            }

            return null;
        }
    }
}