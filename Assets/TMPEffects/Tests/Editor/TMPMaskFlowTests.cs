using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CAT.UI.Tests
{
    public class TMPMaskFlowTests
    {
        [Test]
        public void Type_ExposesFlowConfiguration()
        {
            System.Type type = typeof(TMPMaskFlow);

            Assert.That(type.GetProperty("Delay", BindingFlags.Public | BindingFlags.Instance), Is.Not.Null);
            Assert.That(type.GetProperty("Direction", BindingFlags.Public | BindingFlags.Instance), Is.Not.Null);
            Assert.That(type.GetProperty("Velocity", BindingFlags.Public | BindingFlags.Instance), Is.Not.Null);
            Assert.That(type.GetProperty("Interval", BindingFlags.Public | BindingFlags.Instance), Is.Not.Null);
            Assert.That(type.GetProperty("Gap", BindingFlags.Public | BindingFlags.Instance), Is.Not.Null);
            Assert.That(type.GetProperty("Static", BindingFlags.Public | BindingFlags.Instance), Is.Not.Null);
            Assert.That(type.GetMethod("UsesInterval", BindingFlags.Public | BindingFlags.Static), Is.Not.Null);
            Assert.That(type.GetMethod("UsesSequenceFlow", BindingFlags.Public | BindingFlags.Static), Is.Not.Null);
            Assert.That(type.GetMethod("CanAddTo", BindingFlags.Public | BindingFlags.Static), Is.Not.Null);
            Assert.That(type.GetMethod("ShouldUseStaticFlow", BindingFlags.Public | BindingFlags.Static), Is.Not.Null);
        }

        [Test]
        public void Type_ExposesTextKeyConfiguration()
        {
            System.Type type = typeof(TMPMaskFlow);

            Assert.That(type.GetNestedType("TextEntry", BindingFlags.Public), Is.Not.Null);
            Assert.That(type.GetProperty("TextEntries", BindingFlags.Public | BindingFlags.Instance), Is.Not.Null);
            Assert.That(type.GetProperty("TextKeys", BindingFlags.Public | BindingFlags.Instance), Is.Not.Null);
            Assert.That(type.GetProperty("CurrentTextKey", BindingFlags.Public | BindingFlags.Instance), Is.Not.Null);
            Assert.That(type.GetMethod("SetTextKeys", BindingFlags.Public | BindingFlags.Instance), Is.Not.Null);
            Assert.That(type.GetMethod("SetTextEntries", BindingFlags.Public | BindingFlags.Instance), Is.Not.Null);
            Assert.That(type.GetMethod("SetTextResolver", BindingFlags.Public | BindingFlags.Instance), Is.Not.Null);
            Assert.That(type.GetField("_textEntries", BindingFlags.NonPublic | BindingFlags.Instance), Is.Not.Null);
        }

        [Test]
        public void Type_DoesNotExposeRemovedConfiguration()
        {
            System.Type type = typeof(TMPMaskFlow);

            Assert.That(type.GetMethod("EvaluateScale", BindingFlags.Public | BindingFlags.Static), Is.Null);
            Assert.That(type.GetProperty("StartScale", BindingFlags.Public | BindingFlags.Instance), Is.Null);
            Assert.That(type.GetProperty("CenterScale", BindingFlags.Public | BindingFlags.Instance), Is.Null);
            Assert.That(type.GetProperty("EndScale", BindingFlags.Public | BindingFlags.Instance), Is.Null);
            Assert.That(type.GetProperty("StartPosition", BindingFlags.Public | BindingFlags.Instance), Is.Null);
            Assert.That(type.GetProperty("EndPosition", BindingFlags.Public | BindingFlags.Instance), Is.Null);
            Assert.That(type.GetProperty("Duration", BindingFlags.Public | BindingFlags.Instance), Is.Null);
            Assert.That(type.GetProperty("Pivot", BindingFlags.Public | BindingFlags.Instance), Is.Null);
            Assert.That(type.GetMethod("EvaluatePairTravelDistance", BindingFlags.Public | BindingFlags.Static), Is.Null);
            Assert.That(type.GetField("_sourceAnimation", BindingFlags.NonPublic | BindingFlags.Instance), Is.Null);
        }

        [Test]
        public void EvaluateTravelDistance_AddsHalfGapBeforeAndAfterText()
        {
            float distance = TMPMaskFlow.EvaluateTravelDistance(
                new Vector2(120f, 30f),
                TMPMaskFlow.FlowDirection.Left,
                40f);

            Assert.That(distance, Is.EqualTo(160f).Within(0.0001f));
        }

        [Test]
        public void EvaluateTravelDistance_UsesDirectionalTextExtent()
        {
            float distance = TMPMaskFlow.EvaluateTravelDistance(
                new Vector2(120f, 30f),
                TMPMaskFlow.FlowDirection.Top,
                40f);

            Assert.That(distance, Is.EqualTo(70f).Within(0.0001f));
        }

        [Test]
        public void EvaluateTravelDistance_DoesNotDependOnNextTextWidth()
        {
            float shortTextDistance = TMPMaskFlow.EvaluateTravelDistance(new Vector2(120f, 30f), TMPMaskFlow.FlowDirection.Left, 40f);
            float longTextDistance = TMPMaskFlow.EvaluateTravelDistance(new Vector2(120f, 30f), TMPMaskFlow.FlowDirection.Left, 40f);

            Assert.That(shortTextDistance, Is.EqualTo(160f).Within(0.0001f));
            Assert.That(longTextDistance, Is.EqualTo(shortTextDistance).Within(0.0001f));
        }

        [Test]
        public void EvaluateSequenceDistance_SumsEveryTextWidthAndGap()
        {
            Vector2[] contentSizes =
            {
                new Vector2(120f, 30f),
                new Vector2(240f, 30f),
                new Vector2(60f, 30f)
            };

            float distance = TMPMaskFlow.EvaluateSequenceDistance(
                contentSizes,
                TMPMaskFlow.FlowDirection.Left,
                40f);

            Assert.That(distance, Is.EqualTo(540f).Within(0.0001f));
        }

        [Test]
        public void EvaluateSequenceItemOffset_ConnectsVariableWidthTextsByGap()
        {
            Vector2[] contentSizes =
            {
                new Vector2(120f, 30f),
                new Vector2(240f, 30f),
                new Vector2(60f, 30f)
            };

            float firstOffset = TMPMaskFlow.EvaluateSequenceItemOffset(contentSizes, 0, 0, TMPMaskFlow.FlowDirection.Left, 40f);
            float secondOffset = TMPMaskFlow.EvaluateSequenceItemOffset(contentSizes, 1, 0, TMPMaskFlow.FlowDirection.Left, 40f);
            float thirdOffset = TMPMaskFlow.EvaluateSequenceItemOffset(contentSizes, 2, 0, TMPMaskFlow.FlowDirection.Left, 40f);
            float nextFirstOffset = TMPMaskFlow.EvaluateSequenceItemOffset(contentSizes, 0, 1, TMPMaskFlow.FlowDirection.Left, 40f);

            Assert.That(firstOffset, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(secondOffset, Is.EqualTo(-220f).Within(0.0001f));
            Assert.That(thirdOffset, Is.EqualTo(-410f).Within(0.0001f));
            Assert.That(nextFirstOffset, Is.EqualTo(-540f).Within(0.0001f));
        }

        [Test]
        public void EvaluateSequenceItemPosition_WrapsFirstTextAfterLastText()
        {
            Vector2[] contentSizes =
            {
                new Vector2(120f, 30f),
                new Vector2(240f, 30f),
                new Vector2(60f, 30f)
            };

            Vector2 firstSequenceFirst = TMPMaskFlow.EvaluateSequenceItemPosition(
                520f,
                0,
                0,
                contentSizes,
                TMPMaskFlow.FlowDirection.Left,
                40f);
            Vector2 nextSequenceFirst = TMPMaskFlow.EvaluateSequenceItemPosition(
                520f,
                0,
                1,
                contentSizes,
                TMPMaskFlow.FlowDirection.Left,
                40f);

            Assert.That(firstSequenceFirst.x, Is.EqualTo(-520f).Within(0.0001f));
            Assert.That(nextSequenceFirst.x, Is.EqualTo(20f).Within(0.0001f));
        }

        [Test]
        public void EvaluateFlowPosition_AppliesDelayBeforeFirstMove()
        {
            Vector2 first = TMPMaskFlow.EvaluateFlowPosition(
                0.75f,
                0,
                new Vector2(120f, 30f),
                TMPMaskFlow.FlowDirection.Left,
                80f,
                1f,
                0f,
                40f);
            Vector2 second = TMPMaskFlow.EvaluateFlowPosition(
                0.75f,
                1,
                new Vector2(120f, 30f),
                TMPMaskFlow.FlowDirection.Left,
                80f,
                1f,
                0f,
                40f);

            Assert.That(first.x, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(second.x, Is.EqualTo(160f).Within(0.0001f));
        }

        [Test]
        public void EvaluateFlowPosition_PausesDuringInterval()
        {
            Vector2 duringInterval = TMPMaskFlow.EvaluateFlowPosition(
                1f,
                1,
                new Vector2(120f, 30f),
                TMPMaskFlow.FlowDirection.Top,
                80f,
                0f,
                1.5f,
                40f);

            Assert.That(duringInterval.y, Is.EqualTo(0f).Within(0.0001f));
        }

        [Test]
        public void EvaluateFlowDistance_IgnoresIntervalForHorizontalDirections()
        {
            Vector2 contentSize = new Vector2(120f, 30f);

            float withoutInterval = TMPMaskFlow.EvaluateFlowDistance(
                2.25f,
                contentSize,
                TMPMaskFlow.FlowDirection.Left,
                80f,
                0f,
                0f,
                40f);
            float withInterval = TMPMaskFlow.EvaluateFlowDistance(
                2.25f,
                contentSize,
                TMPMaskFlow.FlowDirection.Left,
                80f,
                0f,
                1.5f,
                40f);

            Assert.That(withInterval, Is.EqualTo(withoutInterval).Within(0.0001f));
            Assert.That(withInterval, Is.EqualTo(180f).Within(0.0001f));
        }

        [Test]
        public void UsesInterval_ReturnsTrueOnlyForVerticalDirections()
        {
            Assert.That(TMPMaskFlow.UsesInterval(TMPMaskFlow.FlowDirection.Top), Is.True);
            Assert.That(TMPMaskFlow.UsesInterval(TMPMaskFlow.FlowDirection.Bottom), Is.True);
            Assert.That(TMPMaskFlow.UsesInterval(TMPMaskFlow.FlowDirection.Left), Is.False);
            Assert.That(TMPMaskFlow.UsesInterval(TMPMaskFlow.FlowDirection.Right), Is.False);
        }

        [Test]
        public void UsesSequenceFlow_ReturnsTrueOnlyForHorizontalDirections()
        {
            Assert.That(TMPMaskFlow.UsesSequenceFlow(TMPMaskFlow.FlowDirection.Left), Is.True);
            Assert.That(TMPMaskFlow.UsesSequenceFlow(TMPMaskFlow.FlowDirection.Right), Is.True);
            Assert.That(TMPMaskFlow.UsesSequenceFlow(TMPMaskFlow.FlowDirection.Top), Is.False);
            Assert.That(TMPMaskFlow.UsesSequenceFlow(TMPMaskFlow.FlowDirection.Bottom), Is.False);
        }

        [Test]
        public void Components_CannotBeAddedTogetherOnSameTMPObject()
        {
            GameObject gameObject = new GameObject("TMPMaskFlow Compatibility Test");

            try
            {
                System.Type tmpTextType = System.Type.GetType("TMPro.TextMeshProUGUI, Unity.TextMeshPro");
                Assert.That(tmpTextType, Is.Not.Null);
                gameObject.AddComponent(tmpTextType);

                TMPAnimation animation = gameObject.AddComponent<TMPAnimation>();
                Assert.That(TMPMaskFlow.CanAddTo(gameObject), Is.False);

                UnityEngine.Object.DestroyImmediate(animation);

                TMPMaskFlow flow = gameObject.AddComponent<TMPMaskFlow>();
                Assert.That(TMPAnimation.CanAddTo(gameObject), Is.False);

                UnityEngine.Object.DestroyImmediate(flow);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void BuildTextKeySignature_ReusesCachedValueUntilEntriesChange()
        {
            GameObject gameObject = new GameObject("TMPMaskFlow Signature Cache Test");
            TMPMaskFlow flow = gameObject.AddComponent<TMPMaskFlow>();

            try
            {
                flow.SetTextEntries(new[]
                {
                    new TMPMaskFlow.TextEntry("ui.notice.ready", "Ready"),
                    new TMPMaskFlow.TextEntry("ui.notice.go", "Go")
                });

                MethodInfo buildSignature = typeof(TMPMaskFlow)
                    .GetMethod("BuildTextKeySignature", BindingFlags.NonPublic | BindingFlags.Instance);
                string first = (string)buildSignature.Invoke(flow, new object[] { true });
                string second = (string)buildSignature.Invoke(flow, new object[] { true });

                Assert.That(second, Is.SameAs(first));

                flow.SetTextEntries(new[]
                {
                    new TMPMaskFlow.TextEntry("ui.notice.ready", "Ready"),
                    new TMPMaskFlow.TextEntry("ui.notice.stop", "Stop")
                });

                string changed = (string)buildSignature.Invoke(flow, new object[] { true });
                Assert.That(changed, Is.Not.SameAs(first));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void RebuildSequenceMetrics_CachesDistanceAndItemOffsets()
        {
            GameObject gameObject = new GameObject("TMPMaskFlow Sequence Cache Test");
            TMPMaskFlow flow = gameObject.AddComponent<TMPMaskFlow>();

            try
            {
                System.Type type = typeof(TMPMaskFlow);
                type.GetField("_direction", BindingFlags.NonPublic | BindingFlags.Instance)
                    .SetValue(flow, TMPMaskFlow.FlowDirection.Left);
                type.GetField("_gap", BindingFlags.NonPublic | BindingFlags.Instance)
                    .SetValue(flow, 40f);

                List<Vector2> contentSizes = (List<Vector2>)type
                    .GetField("_sequenceContentSizes", BindingFlags.NonPublic | BindingFlags.Instance)
                    .GetValue(flow);
                contentSizes.Clear();
                contentSizes.Add(new Vector2(120f, 30f));
                contentSizes.Add(new Vector2(240f, 30f));
                contentSizes.Add(new Vector2(60f, 30f));

                MethodInfo rebuildMetrics = type
                    .GetMethod("RebuildSequenceMetrics", BindingFlags.NonPublic | BindingFlags.Instance);
                rebuildMetrics.Invoke(flow, null);

                float cachedDistance = (float)type
                    .GetField("_cachedSequenceDistance", BindingFlags.NonPublic | BindingFlags.Instance)
                    .GetValue(flow);
                List<float> offsets = (List<float>)type
                    .GetField("_sequenceItemOffsets", BindingFlags.NonPublic | BindingFlags.Instance)
                    .GetValue(flow);

                Assert.That(cachedDistance, Is.EqualTo(540f).Within(0.0001f));
                Assert.That(offsets, Has.Count.EqualTo(3));
                Assert.That(offsets[0], Is.EqualTo(0f).Within(0.0001f));
                Assert.That(offsets[1], Is.EqualTo(-220f).Within(0.0001f));
                Assert.That(offsets[2], Is.EqualTo(-410f).Within(0.0001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void VerticalTurnDuration_UsesCurrentTextDistanceBeforeInterval()
        {
            GameObject gameObject = new GameObject("TMPMaskFlow Duration Test");
            TMPMaskFlow flow = gameObject.AddComponent<TMPMaskFlow>();

            try
            {
                System.Type type = typeof(TMPMaskFlow);
                type.GetField("_direction", BindingFlags.NonPublic | BindingFlags.Instance)
                    .SetValue(flow, TMPMaskFlow.FlowDirection.Top);
                type.GetField("_gap", BindingFlags.NonPublic | BindingFlags.Instance)
                    .SetValue(flow, 40f);
                type.GetField("_velocity", BindingFlags.NonPublic | BindingFlags.Instance)
                    .SetValue(flow, 70f);
                type.GetField("_interval", BindingFlags.NonPublic | BindingFlags.Instance)
                    .SetValue(flow, 1.5f);

                List<Vector2> contentSizes = (List<Vector2>)type
                    .GetField("_sequenceContentSizes", BindingFlags.NonPublic | BindingFlags.Instance)
                    .GetValue(flow);
                contentSizes.Clear();
                contentSizes.Add(new Vector2(120f, 30f));
                contentSizes.Add(new Vector2(240f, 30f));

                float turnDuration = (float)type
                    .GetMethod("EvaluateCurrentTurnDuration", BindingFlags.NonPublic | BindingFlags.Instance)
                    .Invoke(flow, null);

                Assert.That(turnDuration, Is.EqualTo(2.5f).Within(0.0001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void EvaluateFlowPosition_ReusesTwoCopiesAlternately()
        {
            Vector2 contentSize = new Vector2(120f, 30f);

            Vector2 afterFirstMove = TMPMaskFlow.EvaluateFlowPosition(
                2f,
                1,
                contentSize,
                TMPMaskFlow.FlowDirection.Left,
                80f,
                0f,
                1.5f,
                40f);
            // Left/Right 방향은 interval을 사용하지 않으므로 (v1.2.x 사양)
            // 두 번째 이동 완료 시점은 이동 시간(2s) × 2 = 4s
            Vector2 afterSecondMove = TMPMaskFlow.EvaluateFlowPosition(
                4f,
                0,
                contentSize,
                TMPMaskFlow.FlowDirection.Left,
                80f,
                0f,
                1.5f,
                40f);

            Assert.That(afterFirstMove.x, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(afterSecondMove.x, Is.EqualTo(0f).Within(0.0001f));
        }

        [Test]
        public void EvaluateFlowPosition_UsesDirectionVector()
        {
            Vector2 result = TMPMaskFlow.EvaluateFlowPosition(
                0.5f,
                0,
                new Vector2(120f, 30f),
                TMPMaskFlow.FlowDirection.Top,
                40f,
                0f,
                0f,
                40f);

            Assert.That(result.x, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(result.y, Is.EqualTo(20f).Within(0.0001f));
        }

        [Test]
        public void ShouldUseStaticFlow_DependsOnDirectionalOverflow()
        {
            Assert.That(TMPMaskFlow.ShouldUseStaticFlow(
                new Vector2(101f, 20f),
                new Vector2(100f, 40f),
                TMPMaskFlow.FlowDirection.Left), Is.True);
            Assert.That(TMPMaskFlow.ShouldUseStaticFlow(
                new Vector2(99f, 50f),
                new Vector2(100f, 40f),
                TMPMaskFlow.FlowDirection.Left), Is.False);
            Assert.That(TMPMaskFlow.ShouldUseStaticFlow(
                new Vector2(99f, 41f),
                new Vector2(100f, 40f),
                TMPMaskFlow.FlowDirection.Top), Is.True);
            Assert.That(TMPMaskFlow.ShouldUseStaticFlow(
                new Vector2(120f, 39f),
                new Vector2(100f, 40f),
                TMPMaskFlow.FlowDirection.Top), Is.False);
        }

        [Test]
        public void EvaluateStaticStartPosition_UsesAlignmentWithinMask()
        {
            Vector2 contentSize = new Vector2(200f, 40f);
            Vector2 maskSize = new Vector2(100f, 80f);

            Assert.That(TMPMaskFlow.EvaluateStaticStartPosition(
                contentSize,
                maskSize,
                TMPMaskFlow.FlowDirection.Left,
                TMPMaskFlow.StaticAlignmentStart), Is.EqualTo(new Vector2(50f, 0f)));
            Assert.That(TMPMaskFlow.EvaluateStaticStartPosition(
                contentSize,
                maskSize,
                TMPMaskFlow.FlowDirection.Left,
                TMPMaskFlow.StaticAlignmentEnd), Is.EqualTo(new Vector2(-50f, 0f)));
            Assert.That(TMPMaskFlow.EvaluateStaticStartPosition(
                contentSize,
                maskSize,
                TMPMaskFlow.FlowDirection.Top,
                TMPMaskFlow.StaticAlignmentStart), Is.EqualTo(new Vector2(0f, 20f)));
        }

        [Test]
        public void StaticMode_KeepsSourceTextWhenContentFits()
        {
            GameObject gameObject = CreateTMPMaskFlowObject("TMPMaskFlow Static Fits Test", out Behaviour sourceText);
            TMPMaskFlow flow = gameObject.AddComponent<TMPMaskFlow>();

            try
            {
                RectTransform rectTransform = gameObject.GetComponent<RectTransform>();
                rectTransform.sizeDelta = new Vector2(10000f, 1000f);
                SetTMPText(sourceText, "Short");

                flow.Static = true;
                flow.Direction = TMPMaskFlow.FlowDirection.Left;
                flow.Refresh();

                Assert.That(sourceText.enabled, Is.True);
                Assert.That(GetContentObject(gameObject, "[TMPMaskFlow] Content")?.activeSelf, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void StaticMode_UsesFlowWhenContentOverflows()
        {
            GameObject gameObject = CreateTMPMaskFlowObject("TMPMaskFlow Static Overflow Test", out Behaviour sourceText);
            TMPMaskFlow flow = gameObject.AddComponent<TMPMaskFlow>();

            try
            {
                RectTransform rectTransform = gameObject.GetComponent<RectTransform>();
                rectTransform.sizeDelta = new Vector2(1f, 1000f);
                SetTMPText(sourceText, "This localized text is intentionally long.");

                flow.Static = true;
                flow.Direction = TMPMaskFlow.FlowDirection.Left;
                flow.Refresh();

                Assert.That(sourceText.enabled, Is.False);
                Assert.That(GetContentObject(gameObject, "[TMPMaskFlow] Content")?.activeSelf, Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void EditorPreviewText_AppliesToSourceTextWhenEntriesAreEmpty()
        {
            GameObject gameObject = CreateTMPMaskFlowObject("TMPMaskFlow Editor Preview Source Test", out Behaviour sourceText);
            TMPMaskFlow flow = gameObject.AddComponent<TMPMaskFlow>();

            try
            {
                using SerializedObject serializedFlow = new SerializedObject(flow);

                TMPMaskFlowEditor.ApplyPreviewTextForEditor(flow, serializedFlow, "Editor preview source text", 0);

                Assert.That(GetTMPText(sourceText), Is.EqualTo("Editor preview source text"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void EditorPreviewText_AppliesToSelectedTextEntryPreview()
        {
            GameObject gameObject = CreateTMPMaskFlowObject("TMPMaskFlow Editor Preview Entry Test", out _);
            TMPMaskFlow flow = gameObject.AddComponent<TMPMaskFlow>();

            try
            {
                flow.SetTextEntries(new[]
                {
                    new TMPMaskFlow.TextEntry("ui.notice.first", "First"),
                    new TMPMaskFlow.TextEntry("ui.notice.second", "Second")
                });
                using SerializedObject serializedFlow = new SerializedObject(flow);

                TMPMaskFlowEditor.ApplyPreviewTextForEditor(flow, serializedFlow, "Second Preview Changed", 1);

                Assert.That(flow.TextEntries[0].PreviewText, Is.EqualTo("First"));
                Assert.That(flow.TextEntries[1].PreviewText, Is.EqualTo("Second Preview Changed"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void SingleTextKey_UsesOneRepeatedSequenceItem()
        {
            GameObject gameObject = new GameObject("TMPMaskFlow Test");
            TMPMaskFlow flow = gameObject.AddComponent<TMPMaskFlow>();

            flow.SetTextKeys(new[] { "ui.notice.single" });

            int sequenceItemCount = (int)typeof(TMPMaskFlow)
                .GetMethod("GetSequenceItemCount", BindingFlags.NonPublic | BindingFlags.Instance)
                .Invoke(flow, null);
            string firstDisplayText = (string)typeof(TMPMaskFlow)
                .GetMethod("GetDisplayText", BindingFlags.NonPublic | BindingFlags.Instance)
                .Invoke(flow, new object[] { 0 });
            string repeatedDisplayText = (string)typeof(TMPMaskFlow)
                .GetMethod("GetDisplayText", BindingFlags.NonPublic | BindingFlags.Instance)
                .Invoke(flow, new object[] { 1 });

            Assert.That(sequenceItemCount, Is.EqualTo(1));
            Assert.That(firstDisplayText, Is.EqualTo("ui.notice.single"));
            Assert.That(repeatedDisplayText, Is.EqualTo("ui.notice.single"));

            UnityEngine.Object.DestroyImmediate(gameObject);
        }

        [Test]
        public void EvaluateTextKeyIndex_WrapsByTurn()
        {
            Assert.That(TMPMaskFlow.EvaluateTextKeyIndex(0, 3), Is.EqualTo(0));
            Assert.That(TMPMaskFlow.EvaluateTextKeyIndex(1, 3), Is.EqualTo(1));
            Assert.That(TMPMaskFlow.EvaluateTextKeyIndex(3, 3), Is.EqualTo(0));
            Assert.That(TMPMaskFlow.EvaluateTextKeyIndex(5, 3), Is.EqualTo(2));
        }

        [Test]
        public void EvaluateTextKeyIndex_ReturnsZeroWhenListIsEmpty()
        {
            Assert.That(TMPMaskFlow.EvaluateTextKeyIndex(10, 0), Is.EqualTo(0));
        }

        [Test]
        public void ResolveTextKey_UsesInjectedResolver()
        {
            string result = TMPMaskFlow.ResolveTextKey("ui.notice.ready", key => $"localized:{key}");

            Assert.That(result, Is.EqualTo("localized:ui.notice.ready"));
        }

        [Test]
        public void ResolveTextKey_FallsBackToKeyWhenResolverReturnsNull()
        {
            string result = TMPMaskFlow.ResolveTextKey("ui.notice.ready", _ => null);

            Assert.That(result, Is.EqualTo("ui.notice.ready"));
        }

        [Test]
        public void ResolveTextEntry_UsesPreviewTextWithoutResolver()
        {
            string result = TMPMaskFlow.ResolveTextEntry("ui.notice.ready", "Ready to launch", null);

            Assert.That(result, Is.EqualTo("Ready to launch"));
        }

        [Test]
        public void ResolveTextEntry_ResolverOverridesPreviewText()
        {
            string result = TMPMaskFlow.ResolveTextEntry(
                "ui.notice.ready",
                "Ready to launch",
                key => $"localized:{key}");

            Assert.That(result, Is.EqualTo("localized:ui.notice.ready"));
        }

        [Test]
        public void ResolveTextEntry_FallsBackToPreviewTextWhenResolverReturnsNull()
        {
            string result = TMPMaskFlow.ResolveTextEntry("ui.notice.ready", "Ready to launch", _ => null);

            Assert.That(result, Is.EqualTo("Ready to launch"));
        }

        private static GameObject CreateTMPMaskFlowObject(string objectName, out Behaviour sourceText)
        {
            GameObject gameObject = new GameObject(objectName);
            System.Type tmpTextType = System.Type.GetType("TMPro.TextMeshProUGUI, Unity.TextMeshPro");
            Assert.That(tmpTextType, Is.Not.Null);
            sourceText = (Behaviour)gameObject.AddComponent(tmpTextType);
            return gameObject;
        }

        private static void SetTMPText(Behaviour sourceText, string text)
        {
            sourceText.GetType().GetProperty("text").SetValue(sourceText, text);
        }

        private static string GetTMPText(Behaviour sourceText)
        {
            return (string)sourceText.GetType().GetProperty("text").GetValue(sourceText);
        }

        private static GameObject GetContentObject(GameObject gameObject, string objectName)
        {
            Transform child = gameObject.transform.Find(objectName);
            return child != null ? child.gameObject : null;
        }
    }
}
