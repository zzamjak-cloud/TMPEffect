namespace CAT.UI
{
    // DOTween과 호환을 위해 1-based index로 처리합니다.
    // DOTween: Ease.Unset = 0
    public enum EaseType
    {
        Linear = 1,
        InSine,
        OutSine,
        InOutSine,
        InQuad,
        OutQuad,
        InOutQuad,
        InCubic,
        OutCubic,
        InOutCubic,
        InQuart,
        OutQuart,
        InOutQuart,
        InQuint,
        OutQuint,
        InOutQuint,
        InExpo,
        OutExpo,
        InOutExpo,
        InCirc,
        OutCirc,
        InOutCirc,
        InElastic,
        OutElastic,
        InOutElastic,
        InBack,
        OutBack,
        InOutBack,
        InBounce,
        OutBounce,
        InOutBounce
    }

    public static class EaseTypeExtensions
    {
        public static float Evaluate(this EaseType ease, float t) => Easing.Evaluate(ease, t);
    }
}