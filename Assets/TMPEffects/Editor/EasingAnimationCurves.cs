using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace CAT.UI.EasingTools
{
    internal static class EasingAnimationCurves
    {
        // ── 내부 헬퍼 ─────────────────────────────────────────────────────────

        private static Keyframe K(float t, float v, float slope) => new(t, v, slope, slope);

        private static Keyframe K(float t, float v, float inSlope, float outSlope) => new(t, v, inSlope, outSlope);

        private static AnimationCurve C(params Keyframe[] keys) => new(keys);

        // 고해상도 샘플에서 t 위치를 adaptive하게 선택.
        // Phase 1: 극값(local extrema) 탐색 → Phase 2: Farthest Point Sampling으로 예산 채우기.
        private static SortedSet<float> AdaptiveSelectPositions(Func<float, float> fn, int maxKeys,
            int hiRes = 512)
        {
            float[] ts = new float[hiRes + 1];
            float[] vs = new float[hiRes + 1];
            for (int i = 0; i <= hiRes; i++)
            {
                ts[i] = i / (float)hiRes;
                vs[i] = fn(ts[i]);
            }

            // Phase 1: 극값 탐색
            var extrema = new List<float>();
            for (int i = 1; i < hiRes; i++)
            {
                float d0 = vs[i] - vs[i - 1];
                float d1 = vs[i + 1] - vs[i];
                if (d0 * d1 < 0f)
                    extrema.Add(ts[i]);
            }

            var selected = new SortedSet<float> { 0f, 1f };
            int internalBudget = maxKeys - 2;

            if (extrema.Count <= internalBudget)
            {
                foreach (float et in extrema)
                    selected.Add(et);
            }
            else
            {
                // 전역 선형 기준선(t=0→t=1) 대비 편차가 큰 극값 우선 선택
                float v0 = vs[0], v1 = vs[hiRes];
                var ranked = extrema
                    .OrderByDescending(et =>
                    {
                        int idx = Mathf.RoundToInt(et * hiRes);
                        float baseline = Mathf.LerpUnclamped(v0, v1, et);
                        return Mathf.Abs(vs[idx] - baseline);
                    })
                    .Take(internalBudget);
                foreach (float et in ranked)
                    selected.Add(et);
            }

            // Phase 2: Farthest Point Sampling — 남은 예산을 piecewise linear 최대 편차 지점으로 채우기
            while (selected.Count < maxKeys)
            {
                float maxDev = -1f;
                float bestT = -1f;

                float prevSel = -1f;
                foreach (float selT in selected)
                {
                    if (prevSel < 0f) { prevSel = selT; continue; }

                    int iStart = Mathf.CeilToInt(prevSel * hiRes);
                    int iEnd   = Mathf.FloorToInt(selT  * hiRes);
                    if (iEnd - iStart <= 1) { prevSel = selT; continue; }

                    int prevIdx = Mathf.Clamp(iStart, 0, hiRes);
                    int nextIdx = Mathf.Clamp(iEnd,   0, hiRes);
                    float prevV = vs[prevIdx];
                    float nextV = vs[nextIdx];
                    float dt = ts[nextIdx] - ts[prevIdx];
                    if (dt <= 0f) { prevSel = selT; continue; }

                    for (int i = iStart + 1; i < iEnd; i++)
                    {
                        float alpha  = (ts[i] - ts[prevIdx]) / dt;
                        float linVal = Mathf.LerpUnclamped(prevV, nextV, alpha);
                        float dev    = Mathf.Abs(vs[i] - linVal);
                        if (dev > maxDev) { maxDev = dev; bestT = ts[i]; }
                    }
                    prevSel = selT;
                }

                if (bestT < 0f) break;
                selected.Add(bestT);
            }

            return selected;
        }

        // Adaptive 샘플링 + 정확한 미분 탄젠트. Free 탄젠트 모드에서 사용.
        // clampMonotone=true: Fritsch-Carlson 조건으로 Hermite 오버슈트 방지 (Back처럼 [0,1] 이탈이 의도된 커브에는 false).
        private static AnimationCurve AdaptiveSampleD(Func<float, float> fn, Func<float, float> deriv,
            int maxKeys, bool clampMonotone = false)
        {
            var positions = AdaptiveSelectPositions(fn, maxKeys);
            float[] tArr = positions.ToArray();
            int n = tArr.Length;
            float[] vArr = new float[n];
            float[] dArr = new float[n];

            for (int i = 0; i < n; i++)
            {
                vArr[i] = fn(tArr[i]);
                float d = deriv(tArr[i]);
                if (float.IsInfinity(d) || float.IsNaN(d))
                    d = Mathf.Clamp(d, -1000f, 1000f);
                dArr[i] = d;
            }

            if (clampMonotone)
            {
                // Fritsch-Carlson: d[i] ≤ 3 × min(|좌 구간 기울기|, |우 구간 기울기|)
                // → Hermite 보간이 인접 keyframe 값 범위를 벗어나지 않도록 보장
                for (int i = 0; i < n; i++)
                {
                    float limitLeft  = i > 0     ? 3f * Mathf.Abs((vArr[i] - vArr[i-1]) / (tArr[i] - tArr[i-1])) : float.MaxValue;
                    float limitRight = i < n - 1 ? 3f * Mathf.Abs((vArr[i+1] - vArr[i]) / (tArr[i+1] - tArr[i])) : float.MaxValue;
                    float limit = Mathf.Min(limitLeft, limitRight);
                    if (Mathf.Abs(dArr[i]) > limit)
                        dArr[i] = Mathf.Sign(dArr[i]) * limit;
                }
            }

            var keys = new Keyframe[n];
            for (int i = 0; i < n; i++)
                keys[i] = new Keyframe(tArr[i], vArr[i], dArr[i], dArr[i]);
            return new AnimationCurve(keys);
        }

        // 정확한 미분 탄젠트를 사용해 균일 샘플링. Free 탄젠트 모드에서 사용.
        private static AnimationCurve SampleD(Func<float, float> fn, Func<float, float> deriv, int n = 8)
        {
            var keys = new Keyframe[n + 1];
            for (int i = 0; i <= n; i++)
            {
                float t = i / (float)n;
                float d = deriv(t);
                keys[i] = new Keyframe(t, fn(t), d, d);
            }
            return new AnimationCurve(keys);
        }

        // Adaptive 샘플링, 값만 샘플링 (미분 없음). Auto 탄젠트 모드에서 사용.
        private static AnimationCurve AdaptiveSample(Func<float, float> fn, int maxKeys)
        {
            var positions = AdaptiveSelectPositions(fn, maxKeys);
            var keys = positions.Select(t => new Keyframe(t, fn(t))).ToArray();
            return new AnimationCurve(keys);
        }

        // ── Ease 목록 (DOTween 순서 기준) ─────────────────────────────────────

        public static AnimationCurve Linear() => C(K(0f, 0f, 1f), K(1f, 1f, 1f));

        public static AnimationCurve InSine() => AdaptiveSampleD(
            t => 1f - Mathf.Cos(t * Mathf.PI / 2f),
            t => Mathf.PI / 2f * Mathf.Sin(t * Mathf.PI / 2f),
            maxKeys: 4, clampMonotone: true);

        public static AnimationCurve OutSine() => AdaptiveSampleD(
            t => Mathf.Sin(t * Mathf.PI / 2f),
            t => Mathf.PI / 2f * Mathf.Cos(t * Mathf.PI / 2f),
            maxKeys: 4, clampMonotone: true);

        public static AnimationCurve InOutSine() => SampleD(
            t => -(Mathf.Cos(Mathf.PI * t) - 1f) / 2f,
            t => Mathf.PI / 2f * Mathf.Sin(Mathf.PI * t));

        public static AnimationCurve InQuad() => C(K(0f, 0f, 0f), K(1f, 1f, 2f));
        public static AnimationCurve OutQuad() => C(K(0f, 0f, 2f), K(1f, 1f, 0f));
        public static AnimationCurve InOutQuad() => C(K(0f, 0f, 0f), K(0.5f, 0.5f, 2f), K(1f, 1f, 0f));

        public static AnimationCurve InCubic() => C(K(0f, 0f, 0f), K(1f, 1f, 3f));
        public static AnimationCurve OutCubic() => C(K(0f, 0f, 3f), K(1f, 1f, 0f));
        public static AnimationCurve InOutCubic() => C(K(0f, 0f, 0f), K(0.5f, 0.5f, 3f), K(1f, 1f, 0f));

        public static AnimationCurve InQuart() => AdaptiveSampleD(
            t => t * t * t * t,
            t => 4f * t * t * t,
            maxKeys: 5, clampMonotone: true);

        public static AnimationCurve OutQuart() => AdaptiveSampleD(
            t => 1f - Mathf.Pow(1f - t, 4f),
            t => 4f * Mathf.Pow(1f - t, 3f),
            maxKeys: 5, clampMonotone: true);

        public static AnimationCurve InOutQuart() => SampleD(
            t => t < 0.5f ? 8f * t * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 4f) / 2f,
            t => t < 0.5f ? 32f * t * t * t    : 32f * Mathf.Pow(1f - t, 3f));

        public static AnimationCurve InQuint() => AdaptiveSampleD(
            t => t * t * t * t * t,
            t => 5f * t * t * t * t,
            maxKeys: 5, clampMonotone: true);

        public static AnimationCurve OutQuint() => AdaptiveSampleD(
            t => 1f - Mathf.Pow(1f - t, 5f),
            t => 5f * Mathf.Pow(1f - t, 4f),
            maxKeys: 5, clampMonotone: true);

        public static AnimationCurve InOutQuint() => SampleD(
            t => t < 0.5f ? 16f * t * t * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 5f) / 2f,
            t => t < 0.5f ? 80f * t * t * t * t     : 80f * Mathf.Pow(1f - t, 4f));

        public static AnimationCurve InExpo() => AdaptiveSampleD(
            t => t == 0f ? 0f : Mathf.Pow(2f, 10f * t - 10f),
            t => 10f * Mathf.Log(2f) * Mathf.Pow(2f, 10f * t - 10f),
            maxKeys: 5, clampMonotone: true);

        public static AnimationCurve OutExpo() => AdaptiveSampleD(
            t => t == 1f ? 1f : 1f - Mathf.Pow(2f, -10f * t),
            t => 10f * Mathf.Log(2f) * Mathf.Pow(2f, -10f * t),
            maxKeys: 5, clampMonotone: true);

        public static AnimationCurve InOutExpo() => AdaptiveSampleD(
            t => t == 0f ? 0f : t == 1f ? 1f : t < 0.5f
                ? Mathf.Pow(2f, 20f * t - 10f) / 2f
                : (2f - Mathf.Pow(2f, -20f * t + 10f)) / 2f,
            t => t < 0.5f
                ? 10f * Mathf.Log(2f) * Mathf.Pow(2f, 20f * t - 10f)
                : 10f * Mathf.Log(2f) * Mathf.Pow(2f, -20f * t + 10f),
            maxKeys: 12, clampMonotone: true);

        // 끝점에서 미분값이 ∞ 로 발산하므로 Auto 탄젠트 모드로 샘플링
        public static AnimationCurve InCirc() =>
            AdaptiveSample(t => 1f - Mathf.Sqrt(1f - t * t), maxKeys: 8);

        public static AnimationCurve OutCirc() =>
            AdaptiveSample(t => Mathf.Sqrt(1f - (t - 1f) * (t - 1f)), maxKeys: 8);

        public static AnimationCurve InOutCirc() => AdaptiveSample(t => t < 0.5f
            ? (1f - Mathf.Sqrt(1f - 4f * t * t)) / 2f
            : (Mathf.Sqrt(1f - (-2f * t + 2f) * (-2f * t + 2f)) + 1f) / 2f, maxKeys: 12);

        // 진동 곡선이라 많은 키프레임이 필요하므로 Auto 탄젠트 모드로 샘플링
        public static AnimationCurve InElastic() => AdaptiveSample(t =>
        {
            float c4 = 2f * Mathf.PI / 3f;
            return t == 0f ? 0f : t == 1f ? 1f
                : -Mathf.Pow(2f, 10f * t - 10f) * Mathf.Sin((t * 10f - 10.75f) * c4);
        }, maxKeys: 8);

        public static AnimationCurve OutElastic() => AdaptiveSample(t =>
        {
            float c4 = 2f * Mathf.PI / 3f;
            return t == 0f ? 0f : t == 1f ? 1f
                : Mathf.Pow(2f, -10f * t) * Mathf.Sin((t * 10f - 0.75f) * c4) + 1f;
        }, maxKeys: 8);

        public static AnimationCurve InOutElastic() => AdaptiveSample(t =>
        {
            float c5 = 2f * Mathf.PI / 4.5f;
            return t == 0f ? 0f : t == 1f ? 1f
                : t < 0.5f
                    ? -(Mathf.Pow(2f, 20f * t - 10f) * Mathf.Sin((20f * t - 11.125f) * c5)) / 2f
                    : (Mathf.Pow(2f, -20f * t + 10f) * Mathf.Sin((20f * t - 11.125f) * c5)) / 2f + 1f;
        }, maxKeys: 16);

        // 값이 [0,1] 범위를 벗어나는 오버슈트가 있으므로 AdaptiveSampleD 사용.
        private const float BackC1 = 1.70158f;
        private const float BackC2 = BackC1 * 1.525f;
        private const float BackC3 = BackC1 + 1f;

        public static AnimationCurve InBack() => AdaptiveSampleD(
            t => BackC3 * t * t * t - BackC1 * t * t,
            t => 3f * BackC3 * t * t - 2f * BackC1 * t,
            maxKeys: 5);

        public static AnimationCurve OutBack() => AdaptiveSampleD(
            t => 1f + BackC3 * Mathf.Pow(t - 1f, 3f) + BackC1 * Mathf.Pow(t - 1f, 2f),
            t => 3f * BackC3 * (t - 1f) * (t - 1f) + 2f * BackC1 * (t - 1f),
            maxKeys: 5);

        public static AnimationCurve InOutBack() => SampleD(
            t => t < 0.5f
                ? Mathf.Pow(2f * t, 2f) * ((BackC2 + 1f) * 2f * t - BackC2) / 2f
                : (Mathf.Pow(2f * t - 2f, 2f) * ((BackC2 + 1f) * (2f * t - 2f) + BackC2) + 2f) / 2f,
            t => t < 0.5f
                ? 12f * (BackC2 + 1f) * t * t - 4f * BackC2 * t
                : 12f * (BackC2 + 1f) * (t - 1f) * (t - 1f) + 4f * BackC2 * (t - 1f));

        // 구간별 포물선이라 많은 키프레임이 필요하므로 Auto 탄젠트 모드로 샘플링
        private static float BounceOut(float t)
        {
            const float n1 = 7.5625f, d1 = 2.75f;
            if (t < 1f / d1)
                return n1 * t * t;
            if (t < 2f / d1)
            {
                t -= 1.5f / d1;
                return n1 * t * t + 0.75f;
            }
            if (t < 2.5f / d1)
            {
                t -= 2.25f / d1;
                return n1 * t * t + 0.9375f;
            }
            t -= 2.625f / d1;
            return n1 * t * t + 0.984375f;
        }

        public static AnimationCurve InBounce()    => AdaptiveSample(t => 1f - BounceOut(1f - t), maxKeys: 8);
        public static AnimationCurve OutBounce()   => AdaptiveSample(BounceOut, maxKeys: 8);
        public static AnimationCurve InOutBounce() => AdaptiveSample(t => t < 0.5f
            ? (1f - BounceOut(1f - 2f * t)) / 2f
            : (1f + BounceOut(2f * t - 1f)) / 2f, maxKeys: 16);
    }
}
