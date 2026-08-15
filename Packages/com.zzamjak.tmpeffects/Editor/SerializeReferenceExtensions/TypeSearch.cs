using System;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using System.Reflection;

namespace CAT.UI.SerializeReferenceExtensions
{
    public static class TypeSearch
    {
#if UNITY_2023_2_OR_NEWER
        static readonly Dictionary<Type, List<Type>> m_TypeCache = new();
#endif

        public static IEnumerable<Type> GetTypes(Type baseType)
        {
#if UNITY_2023_2_OR_NEWER
            // NOTE: Unity 2023.2 이상을 위한 제네릭 솔루션입니다.
            // 2023.2부터 SerializeReference가 제네릭 타입 인스턴스를 지원하고 동작이 안정적이기 때문입니다.
            if (baseType.IsGenericType)
            {
                return GetTypesWithGeneric(baseType);
            }

            return GetTypesUsingTypeCache(baseType);
#else
			return GetTypesUsingTypeCache(baseType);
#endif
        }

        static IEnumerable<Type> GetTypesUsingTypeCache(Type baseType)
        {
            return TypeCache.GetTypesDerivedFrom(baseType)
                .Append(baseType)
                .Where(IsValidType);
        }

#if UNITY_2023_2_OR_NEWER
        static IEnumerable<Type> GetTypesWithGeneric(Type baseType)
        {
            if (m_TypeCache.TryGetValue(baseType, out var result))
            {
                return result;
            }

            result = new List<Type>();
            Type genericTypeDefinition;
            Type[] targetTypeArguments;
            Type[] genericTypeParameters;

            if (baseType.IsGenericType)
            {
                genericTypeDefinition = baseType.GetGenericTypeDefinition();
                targetTypeArguments = baseType.GetGenericArguments();
                genericTypeParameters = genericTypeDefinition.GetGenericArguments();
            }
            else
            {
                genericTypeDefinition = baseType;
                targetTypeArguments = Type.EmptyTypes;
                genericTypeParameters = Type.EmptyTypes;
            }

            var types = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(x => x.GetTypes())
                .Where(IsValidType);

            foreach (Type type in types)
            {
                var interfaceTypes = type.GetInterfaces();
                foreach (Type interfaceType in interfaceTypes)
                {
                    if (!interfaceType.IsGenericType
                        || interfaceType.GetGenericTypeDefinition() != genericTypeDefinition)
                    {
                        continue;
                    }

                    var sourceTypeArguments = interfaceType.GetGenericArguments();

                    var allParametersMatch = true;

                    for (var i = 0; i < genericTypeParameters.Length; i++)
                    {
                        var variance = genericTypeParameters[i].GenericParameterAttributes
                                       & GenericParameterAttributes.VarianceMask;

                        Type sourceTypeArgument = sourceTypeArguments[i];
                        Type targetTypeArgument = targetTypeArguments[i];

                        if (variance == GenericParameterAttributes.Contravariant)
                        {
                            if (!sourceTypeArgument.IsAssignableFrom(targetTypeArgument))
                            {
                                allParametersMatch = false;
                                break;
                            }
                        }
                        else if (variance == GenericParameterAttributes.Covariant)
                        {
                            if (!targetTypeArgument.IsAssignableFrom(sourceTypeArgument))
                            {
                                allParametersMatch = false;
                                break;
                            }
                        }
                        else
                        {
                            if (sourceTypeArgument != targetTypeArgument)
                            {
                                allParametersMatch = false;
                                break;
                            }
                        }
                    }

                    if (allParametersMatch)
                    {
                        result.Add(type);
                        break;
                    }
                }
            }

            m_TypeCache.Add(baseType, result);
            return result;
        }
#endif

        static bool IsValidType(Type type)
        {
            return
                (type.IsPublic || type.IsNestedPublic || type.IsNestedPrivate) &&
                !type.IsAbstract &&
                !type.IsGenericType &&
                !typeof(UnityEngine.Object).IsAssignableFrom(type) &&
                Attribute.IsDefined(type, typeof(SerializableAttribute));
        }
    }
}