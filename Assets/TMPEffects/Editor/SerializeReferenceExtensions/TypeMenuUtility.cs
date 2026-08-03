using System;
using System.Linq;
using System.Collections.Generic;

namespace CAT.UI.SerializeReferenceExtensions
{
    public static class TypeMenuUtility
    {
        public const string k_NullDisplayName = "<null>";

        public static string[] GetSplittedTypePath(Type type)
        {
            var splitIndex = type.FullName.LastIndexOf('.');
            if (splitIndex >= 0)
                return new[] { type.FullName[..splitIndex], type.FullName[(splitIndex + 1)..] };
            return new[] { type.Name };
        }

        public static IEnumerable<Type> OrderByType(this IEnumerable<Type> source)
        {
            return source.OrderBy(type =>
            {
                if (type == null)
                    return -999;
                return 0;
            }).ThenBy(type => type?.Name);
        }
    }
}