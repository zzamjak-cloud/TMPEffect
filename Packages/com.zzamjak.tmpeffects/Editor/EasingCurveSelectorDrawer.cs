using System;
using UnityEditor;
using UnityEngine;

namespace CAT.UI.EasingTools
{
    [CustomPropertyDrawer(typeof(AnimationCurvePresetSelectorAttribute))]
    public class EasingCurveSelectorDrawer : PropertyDrawer
    {
        private const float ButtonWidth = 70f;
        private const float Gap = 2f;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var curveRect = new Rect(position.x, position.y, position.width - ButtonWidth - Gap, position.height);
            var buttonRect = new Rect(position.xMax - ButtonWidth, position.y, ButtonWidth, position.height);

            EditorGUI.PropertyField(curveRect, property, label);

            if (GUI.Button(buttonRect, "Preset ▼"))
                ShowMenu(property.Copy());
        }

        private static void ShowMenu(SerializedProperty prop)
        {
            var menu = new GenericMenu();

            Add(menu, "Linear",       prop, EasingAnimationCurves.Linear);
            Add(menu, "InSine",       prop, EasingAnimationCurves.InSine);
            Add(menu, "OutSine",      prop, EasingAnimationCurves.OutSine);
            Add(menu, "InOutSine",    prop, EasingAnimationCurves.InOutSine);
            Add(menu, "InQuad",       prop, EasingAnimationCurves.InQuad);
            Add(menu, "OutQuad",      prop, EasingAnimationCurves.OutQuad);
            Add(menu, "InOutQuad",    prop, EasingAnimationCurves.InOutQuad);
            Add(menu, "InCubic",      prop, EasingAnimationCurves.InCubic);
            Add(menu, "OutCubic",     prop, EasingAnimationCurves.OutCubic);
            Add(menu, "InOutCubic",   prop, EasingAnimationCurves.InOutCubic);
            Add(menu, "InQuart",      prop, EasingAnimationCurves.InQuart);
            Add(menu, "OutQuart",     prop, EasingAnimationCurves.OutQuart);
            Add(menu, "InOutQuart",   prop, EasingAnimationCurves.InOutQuart);
            Add(menu, "InQuint",      prop, EasingAnimationCurves.InQuint);
            Add(menu, "OutQuint",     prop, EasingAnimationCurves.OutQuint);
            Add(menu, "InOutQuint",   prop, EasingAnimationCurves.InOutQuint);
            Add(menu, "InExpo",       prop, EasingAnimationCurves.InExpo);
            Add(menu, "OutExpo",      prop, EasingAnimationCurves.OutExpo);
            Add(menu, "InOutExpo",    prop, EasingAnimationCurves.InOutExpo);
            Add(menu, "InCirc",       prop, EasingAnimationCurves.InCirc,       sampled: true);
            Add(menu, "OutCirc",      prop, EasingAnimationCurves.OutCirc,      sampled: true);
            Add(menu, "InOutCirc",    prop, EasingAnimationCurves.InOutCirc,    sampled: true);
            Add(menu, "InElastic",    prop, EasingAnimationCurves.InElastic,    sampled: true);
            Add(menu, "OutElastic",   prop, EasingAnimationCurves.OutElastic,   sampled: true);
            Add(menu, "InOutElastic", prop, EasingAnimationCurves.InOutElastic, sampled: true);
            Add(menu, "InBack",       prop, EasingAnimationCurves.InBack);
            Add(menu, "OutBack",      prop, EasingAnimationCurves.OutBack);
            Add(menu, "InOutBack",    prop, EasingAnimationCurves.InOutBack);
            Add(menu, "InBounce",     prop, EasingAnimationCurves.InBounce,     sampled: true);
            Add(menu, "OutBounce",    prop, EasingAnimationCurves.OutBounce,    sampled: true);
            Add(menu, "InOutBounce",  prop, EasingAnimationCurves.InOutBounce,  sampled: true);

            menu.ShowAsContext();
        }

        private static void Add(GenericMenu menu, string name, SerializedProperty prop,
            Func<AnimationCurve> factory, bool sampled = false)
        {
            menu.AddItem(new GUIContent(name), false, () =>
            {
                var curve = factory();
                var mode = sampled
                    ? AnimationUtility.TangentMode.Auto
                    : AnimationUtility.TangentMode.Free;
                for (int i = 0; i < curve.length; i++)
                {
                    AnimationUtility.SetKeyLeftTangentMode(curve, i, mode);
                    AnimationUtility.SetKeyRightTangentMode(curve, i, mode);
                }
                prop.animationCurveValue = curve;
                prop.serializedObject.ApplyModifiedProperties();
            });
        }
    }
}
