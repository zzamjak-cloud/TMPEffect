using System;
using UnityEngine;

namespace CAT.UI
{
    [Serializable]
    public class AnimationCurveValue : ICurve, ISubclassSelectableName
    {
        public string MenuName => "AnimationCurve";
        
        [AnimationCurvePresetSelector] 
        public AnimationCurve animationCurve = AnimationCurve.Linear(0, 0, 1, 1);
        
        public float Evaluate(float time01)
        {
            return animationCurve.Evaluate(time01);
        }
        
        public AnimationCurveValue() { }

        public AnimationCurveValue(AnimationCurve curve)
        {
            animationCurve = curve;
        }

        public static implicit operator AnimationCurveValue(AnimationCurve curve) => new(curve);
        public static implicit operator AnimationCurve(AnimationCurveValue value) => value.animationCurve;
    }
}