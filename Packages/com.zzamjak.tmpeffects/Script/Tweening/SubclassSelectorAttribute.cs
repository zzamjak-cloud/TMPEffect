using System;
using UnityEngine;

namespace CAT.UI
{
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class SubclassSelectorAttribute : PropertyAttribute
    {
        public bool UseToStringAsLabel { get; set; }
        public bool ShowBox { get; set; } = true;
    }
}