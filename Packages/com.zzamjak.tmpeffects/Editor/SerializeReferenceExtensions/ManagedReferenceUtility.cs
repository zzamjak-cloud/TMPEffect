using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace CAT.UI.SerializeReferenceExtensions
{
    public static class ManagedReferenceUtility
    {
        public static object SetManagedReference(this SerializedProperty property, Type type)
        {
            object result = null;

#if UNITY_2021_3_OR_NEWER
            // NOTE: managedReferenceValue getter는 Unity 2021.3 이상에서만 사용 가능합니다.
            if (type != null && property.managedReferenceValue != null)
            {
                // JSON에서 이전 값을 복원합니다.
                var json = JsonUtility.ToJson(property.managedReferenceValue);
                result = JsonUtility.FromJson(json, type);
            }
#endif

            result ??= 
                type != null
                    ? Activator.CreateInstance(type)
                    : null;

            property.managedReferenceValue = result;
            return result;
        }

        public static Type GetType(string typeName)
        {
            if (string.IsNullOrEmpty(typeName))
                return null;

            var splitIndex = typeName.IndexOf(' ');
            var assembly = Assembly.Load(typeName[..splitIndex]);
            return assembly.GetType(typeName[(splitIndex + 1)..]);
        }
    }
}