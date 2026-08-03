using JetBrains.Annotations;
using UnityEngine;

namespace CAT.UI
{
    [PublicAPI]
    public interface ICurve
    {
        public float Evaluate(float time01);

        public static ICurve Ease(EaseType ease) => new EaseValue(ease);

        public static ICurve AnimationCurve(AnimationCurve curve = null) =>
            new AnimationCurveValue(curve ?? UnityEngine.AnimationCurve.Linear(0, 0, 1, 1));
    }
} 