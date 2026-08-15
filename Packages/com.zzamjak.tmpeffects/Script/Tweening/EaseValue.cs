using System;

namespace CAT.UI
{
    [Serializable]
    public class EaseValue : ICurve, ISubclassSelectableName
    {
        public string MenuName => "Ease";
        
        public EaseType ease = EaseType.OutQuad;
        
        public float Evaluate(float time01)
        {
            return ease.Evaluate(time01);
        }

        public EaseValue() { }

        public EaseValue(EaseType ease) => this.ease = ease;

        public static implicit operator EaseValue(EaseType ease) => new(ease);

        public static implicit operator EaseType(EaseValue easeValue) => easeValue.ease;
    }
}