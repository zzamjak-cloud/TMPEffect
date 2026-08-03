using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CAT.UI
{
    /// <summary>
    /// TMP 텍스트를 RectMask2D 영역 안에서 반복 이동시키는 전광판 흐름 효과.
    /// </summary>
    [ExecuteAlways]
    [DefaultExecutionOrder(40)]
    [RequireComponent(typeof(TextMeshProUGUI))]
    [RequireComponent(typeof(RectMask2D))]
    [AddComponentMenu("CAT/UI/TMP Mask Flow")]
    public class TMPMaskFlow : MonoBehaviour
    {
        private const string CONTENT_OBJECT_NAME = "[TMPMaskFlow] Content";
        private const string REPEAT_CONTENT_OBJECT_NAME = "[TMPMaskFlow] Repeat Content";
        private const string CONTENT_OBJECT_PREFIX = "[TMPMaskFlow] Content ";
        public const int StaticAlignmentStart = -1;
        public const int StaticAlignmentCenter = 0;
        public const int StaticAlignmentEnd = 1;
        private static readonly Vector2 CENTER_PIVOT = new Vector2(0.5f, 0.5f);
        private static readonly Vector2 UninitializedContentPosition = new Vector2(float.NaN, float.NaN);

        /// <summary>
        /// 텍스트가 흘러가는 방향.
        /// </summary>
        public enum FlowDirection
        {
            Top,
            Bottom,
            Left,
            Right
        }

        /// <summary>
        /// localization key와 Editor preview용 표시 텍스트를 함께 보관한다.
        /// </summary>
        [Serializable]
        public class TextEntry
        {
            [Tooltip("localization 시스템에 전달할 key")]
            [SerializeField]
            private string _key = string.Empty;

            [Tooltip("resolver가 없을 때 Editor preview에서 표시할 텍스트")]
            [SerializeField, TextArea]
            private string _previewText = string.Empty;

            public TextEntry()
            {
            }

            public TextEntry(string key, string previewText = "")
            {
                _key = key ?? string.Empty;
                _previewText = previewText ?? string.Empty;
            }

            public string Key { get => _key; set => _key = value ?? string.Empty; }
            public string PreviewText { get => _previewText; set => _previewText = value ?? string.Empty; }
        }

        [Header("Flow")]
        [Tooltip("컴포넌트 활성화 시 자동 재생")]
        [SerializeField]
        private bool _playOnEnable = true;

        [Tooltip("Time.unscaledDeltaTime으로 재생")]
        [SerializeField]
        private bool _useUnscaledTime = false;

        [Tooltip("텍스트가 마스크 영역을 넘어갈 때만 Flow를 실행하고, 맞으면 원본 TMP 정렬을 유지합니다.")]
        [SerializeField]
        private bool _static = false;

        [Tooltip("Play 또는 Restart 후 첫 이동을 시작하기 전 대기 시간")]
        [SerializeField, Min(0f)]
        private float _delay = 0f;

        [Tooltip("텍스트가 흘러가는 방향")]
        [SerializeField]
        private FlowDirection _direction = FlowDirection.Left;

        [Tooltip("텍스트가 이동하는 속도")]
        [SerializeField, Min(0.01f)]
        private float _velocity = 80f;

        [Tooltip("텍스트가 한 칸 이동한 뒤 다음 이동을 시작하기 전 대기 시간")]
        [SerializeField, Min(0f)]
        private float _interval = 0f;

        [Tooltip("반복 텍스트 사이의 빈 거리")]
        [SerializeField, Min(0f)]
        private float _gap = 80f;

        [Tooltip("Left/Right 시퀀스 플로우에서 생성할 최대 복사본 세트 수. 0이면 제한 없음")]
        [SerializeField, Min(0)]
        private int _maxSequenceCopyCount = 12;

        [Header("Performance")]
        [Tooltip("부모 Canvas와 렌더링 업데이트 범위를 분리하기 위해 중첩 Canvas를 자동 추가합니다.")]
        [SerializeField]
        private bool _isolateRenderCanvas = true;

        [SerializeField, HideInInspector]
        private bool _isolatedCanvasAddedByThis;

        [Tooltip("턴마다 순서대로 표시할 localization key와 preview 텍스트 목록")]
        [SerializeField]
        private List<TextEntry> _textEntries = new List<TextEntry>();

        [SerializeField, HideInInspector]
        private List<string> _textKeys = new List<string>();

        private TextMeshProUGUI _sourceText;
        private RectTransform _sourceRect;
        private RectTransform _contentRect;
        private TextMeshProUGUI _contentText;
        private RectTransform _repeatContentRect;
        private TextMeshProUGUI _repeatContentText;
        private readonly List<RectTransform> _contentRects = new List<RectTransform>();
        private readonly List<TextMeshProUGUI> _contentTexts = new List<TextMeshProUGUI>();
        private readonly List<Vector2> _sequenceContentSizes = new List<Vector2>();
        private readonly List<float> _sequenceItemOffsets = new List<float>();
        private readonly List<string> _resolvedDisplayTexts = new List<string>();
        private readonly List<string> _contentAppliedTexts = new List<string>();
        private readonly List<Vector2> _contentAppliedSizes = new List<Vector2>();
        private readonly List<bool> _contentSizeInitialized = new List<bool>();
        private readonly List<int> _contentAppliedVersions = new List<int>();
        private readonly List<Vector2> _contentAppliedPositions = new List<Vector2>();
        private readonly List<bool> _contentAppliedVisible = new List<bool>();
        private readonly StringBuilder _signatureBuilder = new StringBuilder();
        private string _cachedTextKeySignature = string.Empty;
        private bool _textKeySignatureDirty = true;
        private int _lastTextEntryCount = -1;
        private int _lastTextEntryFingerprint;
        private int _contentVersion;
        private float _cachedSequenceDistance = 1f;
        private int _sequenceItemCount = 1;
        private bool _sourceWasEnabled;
        private bool _isFlowActive = true;
        private Vector2 _flowOriginOffset = Vector2.zero;
        private bool _isPlaying;
        private float _elapsedTime;
        private float _turnElapsedTime;
        private float _delayRemaining;
        private int _turnIndex;
        private Func<string, string> _textResolver;
        private string _lastText;
        private string _lastTextKeySignature;
        private Vector2 _lastSourceSize;
        private TMP_FontAsset _lastFont;
        private Material _lastFontMaterial;
        private float _lastFontSize;
        private Color _lastColor;
        private bool _lastStatic;
        private TextAlignmentOptions _lastAlignment;
        private FlowDirection _lastDirection;
        private float _lastGap = -1f;
        private Canvas _isolatedCanvas;
#if UNITY_EDITOR
        private bool _editorRefreshQueued;
        private bool _editorRemovalQueued;
#endif

        public bool IsPlaying => _isPlaying;
        public bool Static { get => _static; set => _static = value; }
        public float Delay { get => _delay; set => _delay = Mathf.Max(0f, value); }
        public FlowDirection Direction { get => _direction; set => _direction = value; }
        public float Velocity { get => _velocity; set => _velocity = Mathf.Max(0.01f, value); }
        public float Interval { get => _interval; set => _interval = Mathf.Max(0f, value); }
        public float Gap { get => _gap; set => _gap = Mathf.Max(0f, value); }
        public IReadOnlyList<TextEntry> TextEntries => _textEntries;
        public IReadOnlyList<string> TextKeys
        {
            get
            {
                SyncTextKeyCache();
                return _textKeys;
            }
        }
        public string CurrentTextKey => GetTextKey(_turnIndex);

        private void Awake()
        {
            if (!EnsureComponentCompatibility())
            {
                return;
            }

            CacheComponents();
        }

        private void OnEnable()
        {
            if (!EnsureComponentCompatibility())
            {
                return;
            }

            CacheComponents();
            _sourceWasEnabled = _sourceText != null && _sourceText.enabled;
            EnsureContent();
            SyncContent();

            if (_playOnEnable)
            {
                Play();
            }
            else
            {
                _turnIndex = 0;
                _turnElapsedTime = 0f;
                _delayRemaining = _delay;
                ApplyState();
            }
        }

        private void OnDisable()
        {
            _isPlaying = false;

            if (_sourceText != null)
            {
                _sourceText.enabled = _sourceWasEnabled;
            }

            DestroyContent();
        }

        private void OnDestroy()
        {
            CleanupIsolatedCanvas();
        }

        private void Update()
        {
            float deltaTime = _useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            if (!Application.isPlaying)
            {
                deltaTime = 0f;
            }

            AdvanceFlow(deltaTime);
        }

        /// <summary>
        /// Editor preview와 런타임 Update에서 공통으로 사용하는 흐름 진행 처리.
        /// </summary>
        public void AdvanceFlow(float deltaTime)
        {
            if (_sourceText == null)
            {
                CacheComponents();
            }

            if (_contentTexts.Count == 0 || _contentRects.Count == 0 || _contentTexts[0] == null || _contentRects[0] == null)
            {
                EnsureContent();
            }

            if (CheckContentDirty())
            {
                SyncContent();
            }

            if (!_isPlaying)
            {
                return;
            }

            if (!_isFlowActive)
            {
                return;
            }

            deltaTime = Mathf.Max(0f, deltaTime);
            AdvancePlayback(deltaTime);
            ApplyState();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (!EnsureComponentCompatibility())
            {
                return;
            }

            EnsureTextEntries();
            if (_textKeys == null)
            {
                _textKeys = new List<string>();
            }

            _delay = Mathf.Max(0f, _delay);
            _velocity = Mathf.Max(0.01f, _velocity);
            _interval = Mathf.Max(0f, _interval);
            _gap = Mathf.Max(0f, _gap);
            _maxSequenceCopyCount = Mathf.Max(0, _maxSequenceCopyCount);

            CacheComponents();
            if (RefreshTextEntryFingerprint())
            {
                MarkContentDirty();
            }

            if (isActiveAndEnabled)
            {
                QueueEditorRefresh();
            }

            if (_isolateRenderCanvas)
            {
                EnsureIsolatedCanvas();
            }
            else
            {
                CleanupIsolatedCanvas();
            }
        }

        private void QueueEditorRefresh()
        {
            if (_editorRefreshQueued)
            {
                return;
            }

            _editorRefreshQueued = true;
            UnityEditor.EditorApplication.delayCall += ApplyQueuedEditorRefresh;
        }

        private void ApplyQueuedEditorRefresh()
        {
            _editorRefreshQueued = false;
            if (this == null || !isActiveAndEnabled)
            {
                return;
            }

            CacheComponents();
            EnsureContent();
            SyncContent();
            ApplyState();
            UnityEditor.EditorApplication.QueuePlayerLoopUpdate();
            UnityEditor.SceneView.RepaintAll();
        }
#endif

        /// <summary>
        /// 흐름 효과를 처음부터 재생한다.
        /// </summary>
        public void Play()
        {
            if (!EnsureComponentCompatibility())
            {
                return;
            }

            CacheComponents();
            EnsureContent();

            _elapsedTime = 0f;
            _turnElapsedTime = 0f;
            _delayRemaining = _delay;
            _turnIndex = 0;
            _isPlaying = true;
            SyncContent();
            ApplyState();
        }

        /// <summary>
        /// 현재 위치에서 흐름 효과를 정지한다.
        /// </summary>
        public void Stop()
        {
            _isPlaying = false;
        }

        /// <summary>
        /// 흐름 효과를 처음부터 다시 재생한다.
        /// </summary>
        public void Restart()
        {
            Play();
        }

        /// <summary>
        /// 런타임에서 localization key 목록을 교체한다.
        /// </summary>
        public void SetTextKeys(IEnumerable<string> textKeys, bool restart = false)
        {
            EnsureTextEntries();
            _textEntries.Clear();
            if (textKeys != null)
            {
                foreach (string textKey in textKeys)
                {
                    _textEntries.Add(new TextEntry(textKey));
                }
            }

            SyncTextKeyCache();
            MarkTextEntriesDirty();
            MarkContentDirty();
            if (restart)
            {
                Restart();
                return;
            }

            SyncContent();
            ApplyState();
        }

        /// <summary>
        /// 런타임에서 localization key와 preview 텍스트 목록을 교체한다.
        /// </summary>
        public void SetTextEntries(IEnumerable<TextEntry> textEntries, bool restart = false)
        {
            EnsureTextEntries();
            _textEntries.Clear();
            if (textEntries != null)
            {
                foreach (TextEntry textEntry in textEntries)
                {
                    if (textEntry == null)
                    {
                        _textEntries.Add(new TextEntry());
                        continue;
                    }

                    _textEntries.Add(new TextEntry(textEntry.Key, textEntry.PreviewText));
                }
            }

            SyncTextKeyCache();
            MarkTextEntriesDirty();
            MarkContentDirty();
            if (restart)
            {
                Restart();
                return;
            }

            SyncContent();
            ApplyState();
        }

        /// <summary>
        /// localization key를 실제 표시 텍스트로 변환하는 함수를 주입한다.
        /// </summary>
        public void SetTextResolver(Func<string, string> textResolver, bool refreshImmediately = true)
        {
            _textResolver = textResolver;
            MarkContentDirty();
            if (!refreshImmediately)
            {
                return;
            }

            SyncContent();
            ApplyState();
        }

        /// <summary>
        /// 텍스트 복사본과 크기 계산을 강제로 갱신한다.
        /// </summary>
        public void Refresh()
        {
            if (!EnsureComponentCompatibility())
            {
                return;
            }

            CacheComponents();
            EnsureContent();
            SyncContent();
            ApplyState();
        }

        public static bool CanAddTo(GameObject target)
        {
            return target == null || target.GetComponent<TMPAnimation>() == null;
        }

        private bool EnsureComponentCompatibility()
        {
            TMPAnimation animation = GetComponent<TMPAnimation>();
            if (animation == null || !animation.enabled)
            {
                return true;
            }

            Debug.LogWarning("TMPMaskFlow와 TMPAnimation은 같은 TMP 오브젝트에 함께 사용할 수 없습니다.", this);
            enabled = false;
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                QueueEditorRemoval();
                return false;
            }
#endif
            Destroy(this);
            return false;
        }

#if UNITY_EDITOR
        private void Reset()
        {
            EnsureComponentCompatibility();
        }

        private void QueueEditorRemoval()
        {
            if (_editorRemovalQueued)
            {
                return;
            }

            _editorRemovalQueued = true;
            UnityEditor.EditorApplication.delayCall += RemoveInEditor;
        }

        private void RemoveInEditor()
        {
            _editorRemovalQueued = false;
            if (this == null || Application.isPlaying || CanAddTo(gameObject))
            {
                return;
            }

            DestroyImmediate(this);
        }
#endif

        /// <summary>
        /// 방향 enum을 실제 이동 벡터로 변환한다.
        /// </summary>
        public static Vector2 GetDirectionVector(FlowDirection direction)
        {
            switch (direction)
            {
                case FlowDirection.Top:
                    return Vector2.up;
                case FlowDirection.Bottom:
                    return Vector2.down;
                case FlowDirection.Right:
                    return Vector2.right;
                case FlowDirection.Left:
                default:
                    return Vector2.left;
            }
        }

        /// <summary>
        /// 흐름 방향 축에서 텍스트가 차지하는 길이를 계산한다.
        /// </summary>
        public static float EvaluateTextExtent(Vector2 contentSize, FlowDirection direction)
        {
            switch (direction)
            {
                case FlowDirection.Top:
                case FlowDirection.Bottom:
                    return Mathf.Max(1f, contentSize.y);
                case FlowDirection.Left:
                case FlowDirection.Right:
                default:
                    return Mathf.Max(1f, contentSize.x);
            }
        }

        /// <summary>
        /// 텍스트 앞뒤에 gap 절반씩을 더한 한 칸 이동 거리를 계산한다.
        /// </summary>
        public static float EvaluateTravelDistance(Vector2 contentSize, FlowDirection direction, float gap)
        {
            float halfGap = Mathf.Max(0f, gap) * 0.5f;
            return EvaluateTextExtent(contentSize, direction) + halfGap + halfGap;
        }

        /// <summary>
        /// 등록된 모든 텍스트가 한 번씩 이어진 전체 순환 거리를 계산한다.
        /// </summary>
        /// <summary>
        /// 방향별로 interval 옵션을 사용하는지 확인한다.
        /// </summary>
        public static bool UsesInterval(FlowDirection direction)
        {
            return direction == FlowDirection.Top || direction == FlowDirection.Bottom;
        }

        public static bool UsesSequenceFlow(FlowDirection direction)
        {
            return direction == FlowDirection.Left || direction == FlowDirection.Right;
        }

        /// <summary>
        /// Static 모드에서 텍스트 크기가 마스크 영역을 벗어나 Flow가 필요한지 확인한다.
        /// </summary>
        public static bool ShouldUseStaticFlow(Vector2 contentSize, Vector2 maskSize, FlowDirection direction)
        {
            switch (direction)
            {
                case FlowDirection.Top:
                case FlowDirection.Bottom:
                    return Mathf.Max(0f, contentSize.y) > Mathf.Max(0f, maskSize.y);
                case FlowDirection.Left:
                case FlowDirection.Right:
                default:
                    return Mathf.Max(0f, contentSize.x) > Mathf.Max(0f, maskSize.x);
            }
        }

        /// <summary>
        /// 원본 TMP 정렬을 기준으로 Static Flow 시작 중심점을 계산한다.
        /// </summary>
        public static Vector2 EvaluateStaticStartPosition(
            Vector2 contentSize,
            Vector2 maskSize,
            FlowDirection direction,
            int staticAlignment)
        {
            int alignment = Mathf.Clamp(staticAlignment, StaticAlignmentStart, StaticAlignmentEnd);
            if (alignment == StaticAlignmentCenter)
            {
                return Vector2.zero;
            }

            if (UsesSequenceFlow(direction))
            {
                float alignedX = (Mathf.Max(0f, contentSize.x) - Mathf.Max(0f, maskSize.x)) * 0.5f;
                return new Vector2(alignment == StaticAlignmentStart ? alignedX : -alignedX, 0f);
            }

            float alignedY = (Mathf.Max(0f, maskSize.y) - Mathf.Max(0f, contentSize.y)) * 0.5f;
            return new Vector2(0f, alignment == StaticAlignmentStart ? alignedY : -alignedY);
        }

        public static float EvaluateSequenceDistance(IReadOnlyList<Vector2> contentSizes, FlowDirection direction, float gap)
        {
            if (contentSizes == null || contentSizes.Count == 0)
            {
                return EvaluateTravelDistance(Vector2.one, direction, gap);
            }

            float safeGap = Mathf.Max(0f, gap);
            float distance = 0f;
            for (int i = 0; i < contentSizes.Count; i++)
            {
                distance += EvaluateTextExtent(contentSizes[i], direction) + safeGap;
            }

            return Mathf.Max(1f, distance);
        }

        /// <summary>
        /// 순환 시퀀스 안에서 특정 텍스트의 중앙 기준 offset을 계산한다.
        /// </summary>
        public static float EvaluateSequenceItemOffset(
            IReadOnlyList<Vector2> contentSizes,
            int itemIndex,
            int sequenceIndex,
            FlowDirection direction,
            float gap)
        {
            if (contentSizes == null || contentSizes.Count == 0)
            {
                return -EvaluateSequenceDistance(contentSizes, direction, gap) * Mathf.Max(0, sequenceIndex);
            }

            int count = contentSizes.Count;
            int wrappedItemIndex = ((itemIndex % count) + count) % count;
            float safeGap = Mathf.Max(0f, gap);
            float offset = -EvaluateSequenceDistance(contentSizes, direction, gap) * Mathf.Max(0, sequenceIndex);

            for (int i = 0; i < wrappedItemIndex; i++)
            {
                float currentExtent = EvaluateTextExtent(contentSizes[i], direction);
                float nextExtent = EvaluateTextExtent(contentSizes[i + 1], direction);
                offset -= currentExtent * 0.5f + safeGap + nextExtent * 0.5f;
            }

            return offset;
        }

        /// <summary>
        /// 흐른 거리를 반영해 특정 텍스트 사본의 현재 위치를 계산한다.
        /// </summary>
        public static Vector2 EvaluateSequenceItemPosition(
            float flowDistance,
            int itemIndex,
            int sequenceIndex,
            IReadOnlyList<Vector2> contentSizes,
            FlowDirection direction,
            float gap)
        {
            float sequenceDistance = EvaluateSequenceDistance(contentSizes, direction, gap);
            float phase = Mathf.Repeat(Mathf.Max(0f, flowDistance), sequenceDistance);
            float offset = EvaluateSequenceItemOffset(contentSizes, itemIndex, sequenceIndex, direction, gap) + phase;

            return GetDirectionVector(direction) * offset;
        }

        /// <summary>
        /// delay와 interval을 반영해 실제로 진행된 이동 거리를 계산한다.
        /// </summary>
        public static float EvaluateFlowDistance(
            float elapsedTime,
            Vector2 contentSize,
            FlowDirection direction,
            float velocity,
            float delay,
            float interval,
            float gap)
        {
            float travelDistance = EvaluateTravelDistance(contentSize, direction, gap);
            float safeVelocity = Mathf.Max(0.01f, velocity);
            float moveDuration = travelDistance / safeVelocity;
            float activeTime = Mathf.Max(0f, elapsedTime - Mathf.Max(0f, delay));
            float effectiveInterval = UsesInterval(direction) ? Mathf.Max(0f, interval) : 0f;
            float cycleDuration = moveDuration + effectiveInterval;
            int completedCycles = cycleDuration > 0f ? Mathf.FloorToInt(activeTime / cycleDuration) : 0;
            float cycleTime = cycleDuration > 0f ? activeTime - completedCycles * cycleDuration : 0f;
            float movingTime = Mathf.Min(cycleTime, moveDuration);

            return completedCycles * travelDistance + movingTime * safeVelocity;
        }

        /// <summary>
        /// 두 텍스트 복사본 중 하나의 현재 중앙 기준 위치를 계산한다.
        /// </summary>
        public static Vector2 EvaluateFlowPosition(
            float elapsedTime,
            int copyIndex,
            Vector2 contentSize,
            FlowDirection direction,
            float velocity,
            float delay,
            float interval,
            float gap)
        {
            float travelDistance = EvaluateTravelDistance(contentSize, direction, gap);
            float flowDistance = EvaluateFlowDistance(elapsedTime, contentSize, direction, velocity, delay, interval, gap);
            float copyOffset = EvaluateCopyOffset(flowDistance, copyIndex, travelDistance);

            return GetDirectionVector(direction) * copyOffset;
        }

        /// <summary>
        /// turn index에 해당하는 localization key index를 계산한다.
        /// </summary>
        public static int EvaluateTextKeyIndex(int turnIndex, int textKeyCount)
        {
            if (textKeyCount <= 0)
            {
                return 0;
            }

            return Mathf.Abs(turnIndex) % textKeyCount;
        }

        /// <summary>
        /// localization key를 실제 표시 텍스트로 변환한다.
        /// </summary>
        public static string ResolveTextKey(string textKey, Func<string, string> textResolver)
        {
            string safeKey = textKey ?? string.Empty;
            if (textResolver == null)
            {
                return safeKey;
            }

            string resolvedText = textResolver.Invoke(safeKey);
            return resolvedText ?? safeKey;
        }

        /// <summary>
        /// localization key와 preview 텍스트를 실제 표시 텍스트로 변환한다.
        /// </summary>
        public static string ResolveTextEntry(string textKey, string previewText, Func<string, string> textResolver)
        {
            string safeKey = textKey ?? string.Empty;
            string safePreviewText = previewText ?? string.Empty;
            if (textResolver != null)
            {
                string resolvedText = textResolver.Invoke(safeKey);
                if (resolvedText != null)
                {
                    return resolvedText;
                }
            }

            return string.IsNullOrEmpty(safePreviewText) ? safeKey : safePreviewText;
        }

        private static float EvaluateCopyOffset(float flowDistance, int copyIndex, float travelDistance)
        {
            float safeTravelDistance = Mathf.Max(1f, travelDistance);
            float loopDistance = safeTravelDistance * 2f;
            float firstOffset = Mathf.Repeat(Mathf.Max(0f, flowDistance), loopDistance);
            if (firstOffset > safeTravelDistance)
            {
                firstOffset -= loopDistance;
            }

            if (Mathf.Abs(copyIndex) % 2 == 0)
            {
                return firstOffset;
            }

            return firstOffset >= 0f ? firstOffset - safeTravelDistance : firstOffset + safeTravelDistance;
        }

        private void AdvancePlayback(float deltaTime)
        {
            _elapsedTime += deltaTime;
            float remainingTime = deltaTime;

            if (_delayRemaining > 0f)
            {
                float consumedDelay = Mathf.Min(_delayRemaining, remainingTime);
                _delayRemaining -= consumedDelay;
                remainingTime -= consumedDelay;

                if (remainingTime <= Mathf.Epsilon)
                {
                    return;
                }
            }

            int guard = 0;
            while (remainingTime > Mathf.Epsilon && guard < 128)
            {
                float turnDuration = EvaluateCurrentTurnDuration();
                float remainingTurnTime = Mathf.Max(0f, turnDuration - _turnElapsedTime);

                if (remainingTime < remainingTurnTime)
                {
                    _turnElapsedTime += remainingTime;
                    return;
                }

                _turnElapsedTime = turnDuration;
                remainingTime -= remainingTurnTime;
                AdvanceTurn();
                guard++;
            }
        }

        private void AdvanceTurn()
        {
            _turnIndex++;
            _turnElapsedTime = 0f;
            SyncContent();
        }

        private float EvaluateCurrentTurnDuration()
        {
            float moveDuration = EvaluateCurrentMoveDuration();
            float effectiveInterval = UsesInterval(_direction) ? Mathf.Max(0f, _interval) : 0f;
            return moveDuration + effectiveInterval;
        }

        private float EvaluateCurrentMoveDuration()
        {
            float travelDistance = EvaluateCurrentTravelDistance();
            return travelDistance / Mathf.Max(0.01f, _velocity);
        }

        private float EvaluateCurrentTravelDistance()
        {
            if (_sequenceContentSizes.Count == 0)
            {
                return EvaluateTravelDistance(Vector2.one, _direction, _gap);
            }

            if (UsesSequenceFlow(_direction))
            {
                return _cachedSequenceDistance;
            }

            return EvaluateTravelDistance(_sequenceContentSizes[0], _direction, _gap);
        }

        private void CacheComponents()
        {
            if (_sourceText == null)
            {
                _sourceText = GetComponent<TextMeshProUGUI>();
            }

            if (_sourceRect == null)
            {
                _sourceRect = GetComponent<RectTransform>();
            }

            EnsureIsolatedCanvas();
        }

        private void EnsureIsolatedCanvas()
        {
            if (!_isolateRenderCanvas)
            {
                return;
            }

            if (!HasAncestorCanvas())
            {
#if UNITY_EDITOR
                if (!Application.isPlaying)
                {
                    Debug.LogWarning(
                        "TMPMaskFlow: 상위 Canvas가 없어 중첩 Canvas 분리를 적용할 수 없습니다. UI Canvas 하위에 배치해주세요.",
                        this);
                }
#endif
                return;
            }

            _isolatedCanvas = GetComponent<Canvas>();
            if (_isolatedCanvas == null)
            {
                _isolatedCanvas = gameObject.AddComponent<Canvas>();
                _isolatedCanvasAddedByThis = true;
            }

            _isolatedCanvas.overrideSorting = false;
            _isolatedCanvas.pixelPerfect = false;
            _isolatedCanvas.enabled = true;
        }

        private void CleanupIsolatedCanvas()
        {
            if (!_isolatedCanvasAddedByThis)
            {
                _isolatedCanvas = null;
                return;
            }

            if (_isolatedCanvas == null)
            {
                _isolatedCanvas = GetComponent<Canvas>();
            }

            if (_isolatedCanvas == null)
            {
                _isolatedCanvasAddedByThis = false;
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(_isolatedCanvas);
            }
            else
            {
                DestroyImmediate(_isolatedCanvas);
            }

            _isolatedCanvas = null;
            _isolatedCanvasAddedByThis = false;
        }

        private bool HasAncestorCanvas()
        {
            Transform parent = transform.parent;
            while (parent != null)
            {
                if (parent.GetComponent<Canvas>() != null)
                {
                    return true;
                }

                parent = parent.parent;
            }

            return false;
        }

        private void EnsureContent()
        {
            if (_sourceRect == null)
            {
                return;
            }

            EnsureContentCount(2);
        }

        private void EnsureContentCount(int contentCount)
        {
            int safeContentCount = Mathf.Max(2, contentCount);
            while (_contentRects.Count < safeContentCount)
            {
                _contentRects.Add(null);
            }

            while (_contentTexts.Count < safeContentCount)
            {
                _contentTexts.Add(null);
            }

            while (_contentAppliedTexts.Count < safeContentCount)
            {
                _contentAppliedTexts.Add(null);
            }

            while (_contentAppliedSizes.Count < safeContentCount)
            {
                _contentAppliedSizes.Add(Vector2.zero);
            }

            while (_contentSizeInitialized.Count < safeContentCount)
            {
                _contentSizeInitialized.Add(false);
            }

            while (_contentAppliedVersions.Count < safeContentCount)
            {
                _contentAppliedVersions.Add(-1);
            }

            while (_contentAppliedPositions.Count < safeContentCount)
            {
                _contentAppliedPositions.Add(UninitializedContentPosition);
            }

            while (_contentAppliedVisible.Count < safeContentCount)
            {
                _contentAppliedVisible.Add(false);
            }

            for (int i = 0; i < safeContentCount; i++)
            {
                RectTransform contentRect = _contentRects[i];
                TextMeshProUGUI contentText = _contentTexts[i];

                EnsureContentObject(GetContentObjectName(i), ref contentRect, ref contentText);

                _contentRects[i] = contentRect;
                _contentTexts[i] = contentText;
            }

            RefreshLegacyContentReferences();
        }

        private string GetContentObjectName(int contentIndex)
        {
            if (contentIndex == 0)
            {
                return CONTENT_OBJECT_NAME;
            }

            if (contentIndex == 1)
            {
                return REPEAT_CONTENT_OBJECT_NAME;
            }

            return CONTENT_OBJECT_PREFIX + contentIndex;
        }

        private void RefreshLegacyContentReferences()
        {
            _contentRect = _contentRects.Count > 0 ? _contentRects[0] : null;
            _contentText = _contentTexts.Count > 0 ? _contentTexts[0] : null;
            _repeatContentRect = _contentRects.Count > 1 ? _contentRects[1] : null;
            _repeatContentText = _contentTexts.Count > 1 ? _contentTexts[1] : null;
        }

        private void EnsureContentObject(
            string objectName,
            ref RectTransform contentRect,
            ref TextMeshProUGUI contentText)
        {
            if (contentText != null && contentRect != null)
            {
                return;
            }

            Transform existing = transform.Find(objectName);
            GameObject contentObject = existing != null ? existing.gameObject : new GameObject(objectName);
            contentObject.hideFlags = HideFlags.HideAndDontSave;

            if (existing == null)
            {
                contentObject.transform.SetParent(transform, false);
            }

            contentRect = contentObject.GetComponent<RectTransform>();
            if (contentRect == null)
            {
                contentRect = contentObject.AddComponent<RectTransform>();
            }

            contentText = contentObject.GetComponent<TextMeshProUGUI>();
            if (contentText == null)
            {
                contentText = contentObject.AddComponent<TextMeshProUGUI>();
            }

            TMPAnimation legacyAnimation = contentObject.GetComponent<TMPAnimation>();
            if (legacyAnimation != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(legacyAnimation);
                }
                else
                {
                    DestroyImmediate(legacyAnimation);
                }
            }

            contentText.raycastTarget = false;
            contentText.maskable = true;
            contentObject.SetActive(true);
        }

        private void DestroyContent()
        {
            for (int i = _contentTexts.Count - 1; i >= 0; i--)
            {
                TextMeshProUGUI contentText = _contentTexts[i];
                if (contentText == null)
                {
                    continue;
                }

                GameObject contentObject = contentText.gameObject;
                if (Application.isPlaying)
                {
                    Destroy(contentObject);
                }
                else
                {
                    DestroyImmediate(contentObject);
                }
            }

            _contentRects.Clear();
            _contentTexts.Clear();
            _sequenceContentSizes.Clear();
            _sequenceItemOffsets.Clear();
            _resolvedDisplayTexts.Clear();
            _contentAppliedTexts.Clear();
            _contentAppliedSizes.Clear();
            _contentSizeInitialized.Clear();
            _contentAppliedVersions.Clear();
            _contentAppliedPositions.Clear();
            _contentAppliedVisible.Clear();
            _contentText = null;
            _contentRect = null;
            _repeatContentText = null;
            _repeatContentRect = null;
        }

        private void EnsureTextEntries()
        {
            if (_textEntries == null)
            {
                _textEntries = new List<TextEntry>();
            }

            if (_textKeys != null && _textKeys.Count > 0 && _textEntries.Count == 0)
            {
                foreach (string textKey in _textKeys)
                {
                    _textEntries.Add(new TextEntry(textKey));
                }

                MarkTextEntriesDirty();
            }

            for (int i = 0; i < _textEntries.Count; i++)
            {
                if (_textEntries[i] == null)
                {
                    _textEntries[i] = new TextEntry();
                    MarkTextEntriesDirty();
                }
            }

            SyncTextKeyCache();
        }

        private void SyncTextKeyCache()
        {
            if (_textKeys == null)
            {
                _textKeys = new List<string>();
            }

            _textKeys.Clear();
            if (_textEntries == null)
            {
                return;
            }

            foreach (TextEntry textEntry in _textEntries)
            {
                _textKeys.Add(textEntry != null ? textEntry.Key : string.Empty);
            }
        }

        private string GetTextKey(int turnIndex)
        {
            if (_textEntries == null || _textEntries.Count == 0)
            {
                return _sourceText != null ? _sourceText.text : string.Empty;
            }

            return GetTextEntry(turnIndex).Key;
        }

        private string GetDisplayText(int turnIndex)
        {
            if (_textEntries == null || _textEntries.Count == 0)
            {
                return _sourceText != null ? _sourceText.text : string.Empty;
            }

            TextEntry textEntry = GetTextEntry(turnIndex);
            return ResolveTextEntry(textEntry.Key, textEntry.PreviewText, _textResolver);
        }

        private void CacheDisplayTexts(int startTurnIndex, int count)
        {
            _resolvedDisplayTexts.Clear();
            for (int i = 0; i < count; i++)
            {
                _resolvedDisplayTexts.Add(GetDisplayText(startTurnIndex + i));
            }
        }

        private string GetCachedDisplayText(int index)
        {
            if (_resolvedDisplayTexts.Count == 0)
            {
                return GetDisplayText(index);
            }

            int wrappedIndex = ((index % _resolvedDisplayTexts.Count) + _resolvedDisplayTexts.Count) % _resolvedDisplayTexts.Count;
            return _resolvedDisplayTexts[wrappedIndex];
        }

        private TextEntry GetTextEntry(int turnIndex)
        {
            EnsureTextEntries();
            if (_textEntries.Count == 0)
            {
                return new TextEntry(_sourceText != null ? _sourceText.text : string.Empty);
            }

            return _textEntries[EvaluateTextKeyIndex(turnIndex, _textEntries.Count)];
        }

        private int GetSequenceItemCount()
        {
            EnsureTextEntries();
            return Mathf.Max(1, _textEntries.Count);
        }

        private float GetViewportExtent()
        {
            if (_sourceRect == null)
            {
                return 1f;
            }

            return Mathf.Max(1f, EvaluateTextExtent(_sourceRect.rect.size, _direction));
        }

        private int EvaluateRequiredSequenceCopyCount(float sequenceDistance)
        {
            float safeSequenceDistance = Mathf.Max(1f, sequenceDistance);
            int viewportCopyCount = Mathf.CeilToInt(GetViewportExtent() / safeSequenceDistance);
            int copyCount = Mathf.Max(2, viewportCopyCount + 2);
            if (_maxSequenceCopyCount > 0)
            {
                copyCount = Mathf.Min(copyCount, _maxSequenceCopyCount);
            }

            return copyCount;
        }

        private void RebuildSequenceMetrics()
        {
            _sequenceItemOffsets.Clear();
            if (_sequenceContentSizes.Count == 0)
            {
                _cachedSequenceDistance = EvaluateTravelDistance(Vector2.one, _direction, _gap);
                _sequenceItemOffsets.Add(0f);
                return;
            }

            float safeGap = Mathf.Max(0f, _gap);
            float distance = 0f;
            for (int i = 0; i < _sequenceContentSizes.Count; i++)
            {
                distance += EvaluateTextExtent(_sequenceContentSizes[i], _direction) + safeGap;
            }

            _cachedSequenceDistance = Mathf.Max(1f, distance);
            float offset = 0f;
            for (int i = 0; i < _sequenceContentSizes.Count; i++)
            {
                if (i == 0)
                {
                    _sequenceItemOffsets.Add(0f);
                    continue;
                }

                float previousExtent = EvaluateTextExtent(_sequenceContentSizes[i - 1], _direction);
                float currentExtent = EvaluateTextExtent(_sequenceContentSizes[i], _direction);
                offset -= previousExtent * 0.5f + safeGap + currentExtent * 0.5f;
                _sequenceItemOffsets.Add(offset);
            }
        }

        private string BuildTextKeySignature(bool refreshFingerprint = true)
        {
            if (refreshFingerprint)
            {
                RefreshTextEntryFingerprint();
            }

            if (_textEntries == null || _textEntries.Count == 0)
            {
                _cachedTextKeySignature = string.Empty;
                _textKeySignatureDirty = false;
                return string.Empty;
            }

            if (!_textKeySignatureDirty)
            {
                return _cachedTextKeySignature;
            }

            _signatureBuilder.Clear();
            for (int i = 0; i < _textEntries.Count; i++)
            {
                if (i > 0)
                {
                    _signatureBuilder.Append('\u001F');
                }

                TextEntry textEntry = _textEntries[i];
                _signatureBuilder.Append(textEntry?.Key);
                _signatureBuilder.Append('\u001E');
                _signatureBuilder.Append(textEntry?.PreviewText);
            }

            _cachedTextKeySignature = _signatureBuilder.ToString();
            _textKeySignatureDirty = false;
            return _cachedTextKeySignature;
        }

        private void MarkTextEntriesDirty()
        {
            _textKeySignatureDirty = true;
            _lastTextEntryCount = -1;
        }

        private bool RefreshTextEntryFingerprint()
        {
            int count = _textEntries != null ? _textEntries.Count : 0;
            int fingerprint = CalculateTextEntryFingerprint();
            bool dirty = count != _lastTextEntryCount || fingerprint != _lastTextEntryFingerprint;
            if (dirty)
            {
                _lastTextEntryCount = count;
                _lastTextEntryFingerprint = fingerprint;
                _textKeySignatureDirty = true;
            }

            return dirty;
        }

        private int CalculateTextEntryFingerprint()
        {
            unchecked
            {
                int hash = 17;
                if (_textEntries == null)
                {
                    return hash;
                }

                for (int i = 0; i < _textEntries.Count; i++)
                {
                    TextEntry textEntry = _textEntries[i];
                    hash = hash * 31 + (textEntry?.Key != null ? textEntry.Key.GetHashCode() : 0);
                    hash = hash * 31 + (textEntry?.PreviewText != null ? textEntry.PreviewText.GetHashCode() : 0);
                }

                return hash;
            }
        }

        private void MarkContentDirty()
        {
            _contentVersion++;
            InvalidateAllContentStates();
        }

        private void InvalidateAllContentStates()
        {
            for (int i = 0; i < _contentAppliedPositions.Count; i++)
            {
                _contentAppliedPositions[i] = UninitializedContentPosition;
            }
        }

        private void InvalidateContentState(int contentIndex)
        {
            if (contentIndex < 0 || contentIndex >= _contentAppliedPositions.Count)
            {
                return;
            }

            _contentAppliedPositions[contentIndex] = UninitializedContentPosition;
        }

        private bool CheckContentDirty()
        {
            if (_sourceText == null || _sourceRect == null)
            {
                return false;
            }

            Vector2 size = _sourceRect.rect.size;
            TextAlignmentOptions alignment = _sourceText.alignment;
            bool textEntriesDirty = RefreshTextEntryFingerprint();
            string textKeySignature = BuildTextKeySignature(refreshFingerprint: false);
            bool staticDirty = _lastStatic != _static;
            bool alignmentDirty = _lastAlignment != alignment;
            bool contentDirty = _lastText != _sourceText.text ||
                                _lastTextKeySignature != textKeySignature ||
                                textEntriesDirty ||
                                _lastFont != _sourceText.font ||
                                _lastFontMaterial != _sourceText.fontSharedMaterial ||
                                !Mathf.Approximately(_lastFontSize, _sourceText.fontSize) ||
                                _lastColor != _sourceText.color ||
                                alignmentDirty;
            bool dirty = contentDirty ||
                         staticDirty ||
                         _lastSourceSize != size ||
                         _lastDirection != _direction ||
                         !Mathf.Approximately(_lastGap, _gap);

            if (dirty)
            {
                if (contentDirty)
                {
                    MarkContentDirty();
                }

                _lastText = _sourceText.text;
                _lastTextKeySignature = textKeySignature;
                _lastSourceSize = size;
                _lastFont = _sourceText.font;
                _lastFontMaterial = _sourceText.fontSharedMaterial;
                _lastFontSize = _sourceText.fontSize;
                _lastColor = _sourceText.color;
                _lastStatic = _static;
                _lastAlignment = alignment;
                _lastDirection = _direction;
                _lastGap = _gap;
            }

            return dirty;
        }

        private void SyncContent()
        {
            EnsureTextEntries();
            if (_sourceText == null ||
                _sourceRect == null)
            {
                return;
            }

            if (!SyncStaticFlowMode())
            {
                return;
            }

            if (UsesSequenceFlow(_direction))
            {
                SyncSequenceContent();
                return;
            }

            SyncTurnContent();
        }

        private bool SyncStaticFlowMode()
        {
            if (!_static)
            {
                _isFlowActive = true;
                _flowOriginOffset = Vector2.zero;
                ApplySourceTextMode(false, null);
                return true;
            }

            string displayText = GetDisplayText(_turnIndex);
            Vector2 contentSize = EvaluateSourcePreferredSize(displayText);
            Vector2 maskSize = _sourceRect.rect.size;
            if (!ShouldUseStaticFlow(contentSize, maskSize, _direction))
            {
                _isFlowActive = false;
                _flowOriginOffset = Vector2.zero;
                _sequenceContentSizes.Clear();
                _sequenceContentSizes.Add(contentSize);
                ApplySourceTextMode(true, displayText);
                HideAllContent();
                RefreshLegacyContentReferences();
                return false;
            }

            _isFlowActive = true;
            _flowOriginOffset = EvaluateStaticStartPosition(
                contentSize,
                maskSize,
                _direction,
                GetSourceStaticAlignment());
            ApplySourceTextMode(false, null);
            return true;
        }

        private Vector2 EvaluateSourcePreferredSize(string displayText)
        {
            Vector2 preferredSize = _sourceText.GetPreferredValues(displayText ?? string.Empty, Mathf.Infinity, Mathf.Infinity);
            return new Vector2(
                Mathf.Max(1f, preferredSize.x),
                Mathf.Max(1f, preferredSize.y));
        }

        private void ApplySourceTextMode(bool visible, string displayText)
        {
            if (_sourceText == null)
            {
                return;
            }

            if (visible && displayText != null && _sourceText.text != displayText)
            {
                _sourceText.text = displayText;
                _lastText = displayText;
            }

            _sourceText.enabled = visible && (_sourceWasEnabled || !Application.isPlaying);
        }

        private void HideAllContent()
        {
            for (int i = 0; i < _contentTexts.Count; i++)
            {
                if (_contentTexts[i] == null || _contentRects[i] == null)
                {
                    continue;
                }

                ApplyContentState(i, _contentTexts[i], _contentRects[i], Vector2.zero, false);
                _contentTexts[i].gameObject.SetActive(false);
            }
        }

        private int GetSourceStaticAlignment()
        {
            if (_sourceText == null)
            {
                return StaticAlignmentStart;
            }

            return UsesSequenceFlow(_direction)
                ? GetHorizontalStaticAlignment(_sourceText.alignment)
                : GetVerticalStaticAlignment(_sourceText.alignment);
        }

        private static int GetHorizontalStaticAlignment(TextAlignmentOptions alignment)
        {
            string alignmentName = alignment.ToString();
            if (ContainsAlignmentToken(alignmentName, "Right"))
            {
                return StaticAlignmentEnd;
            }

            if (ContainsAlignmentToken(alignmentName, "Center"))
            {
                return StaticAlignmentCenter;
            }

            return StaticAlignmentStart;
        }

        private static int GetVerticalStaticAlignment(TextAlignmentOptions alignment)
        {
            string alignmentName = alignment.ToString();
            if (ContainsAlignmentToken(alignmentName, "Bottom"))
            {
                return StaticAlignmentEnd;
            }

            if (ContainsAlignmentToken(alignmentName, "Center") ||
                ContainsAlignmentToken(alignmentName, "Middle") ||
                ContainsAlignmentToken(alignmentName, "Midline"))
            {
                return StaticAlignmentCenter;
            }

            return StaticAlignmentStart;
        }

        private static bool ContainsAlignmentToken(string alignmentName, string token)
        {
            return !string.IsNullOrEmpty(alignmentName) &&
                   alignmentName.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void SyncSequenceContent()
        {
            _sequenceItemCount = GetSequenceItemCount();
            CacheDisplayTexts(0, _sequenceItemCount);
            EnsureContentCount(_sequenceItemCount);
            float viewportHeight = Mathf.Max(1f, _sourceRect.rect.height);

            _sequenceContentSizes.Clear();
            for (int i = 0; i < _sequenceItemCount; i++)
            {
                if (TrySyncContent(i, GetCachedDisplayText(i), viewportHeight, true, Vector2.one, out Vector2 contentSize))
                {
                    _sequenceContentSizes.Add(contentSize);
                }
            }

            if (_sequenceContentSizes.Count == 0)
            {
                _sequenceContentSizes.Add(Vector2.one);
            }

            RebuildSequenceMetrics();
            float sequenceDistance = _cachedSequenceDistance;
            int sequenceCopyCount = EvaluateRequiredSequenceCopyCount(sequenceDistance);
            int requiredContentCount = Mathf.Max(2, _sequenceItemCount * sequenceCopyCount);
            EnsureContentCount(requiredContentCount);

            for (int i = 0; i < requiredContentCount; i++)
            {
                int entryIndex = i % _sequenceItemCount;
                Vector2 contentSize = _sequenceContentSizes[Mathf.Min(entryIndex, _sequenceContentSizes.Count - 1)];
                TrySyncContent(i, GetCachedDisplayText(entryIndex), viewportHeight, false, contentSize, out _);
            }

            for (int i = requiredContentCount; i < _contentTexts.Count; i++)
            {
                if (_contentTexts[i] == null || _contentRects[i] == null)
                {
                    continue;
                }

                ApplyContentState(i, _contentTexts[i], _contentRects[i], Vector2.zero, false);
                _contentTexts[i].gameObject.SetActive(false);
            }

            RefreshLegacyContentReferences();
        }

        private void SyncTurnContent()
        {
            _sequenceItemCount = 1;
            CacheDisplayTexts(_turnIndex, 2);
            EnsureContentCount(2);
            float viewportHeight = Mathf.Max(1f, _sourceRect.rect.height);

            _sequenceContentSizes.Clear();
            if (TrySyncContent(0, GetCachedDisplayText(0), viewportHeight, true, Vector2.one, out Vector2 currentSize))
            {
                _sequenceContentSizes.Add(currentSize);
            }

            if (TrySyncContent(1, GetCachedDisplayText(1), viewportHeight, true, Vector2.one, out Vector2 nextSize) &&
                _sequenceContentSizes.Count == 0)
            {
                _sequenceContentSizes.Add(nextSize);
            }

            if (_sequenceContentSizes.Count == 0)
            {
                _sequenceContentSizes.Add(Vector2.one);
            }

            RebuildSequenceMetrics();

            for (int i = 2; i < _contentTexts.Count; i++)
            {
                if (_contentTexts[i] == null || _contentRects[i] == null)
                {
                    continue;
                }

                ApplyContentState(i, _contentTexts[i], _contentRects[i], Vector2.zero, false);
                _contentTexts[i].gameObject.SetActive(false);
            }

            RefreshLegacyContentReferences();
        }

        private bool TrySyncContent(
            int contentIndex,
            string displayText,
            float viewportHeight,
            bool evaluatePreferredSize,
            Vector2 requestedSize,
            out Vector2 contentSize)
        {
            contentSize = requestedSize;
            if (contentIndex < 0 ||
                contentIndex >= _contentTexts.Count ||
                contentIndex >= _contentRects.Count)
            {
                return false;
            }

            TextMeshProUGUI contentText = _contentTexts[contentIndex];
            RectTransform contentRect = _contentRects[contentIndex];
            if (contentText == null || contentRect == null)
            {
                return false;
            }

            bool contentVersionChanged = _contentAppliedVersions[contentIndex] != _contentVersion;
            bool textChanged = _contentAppliedTexts[contentIndex] != displayText;
            if (contentVersionChanged)
            {
                CopyTextSettings(_sourceText, contentText);
            }

            if (contentVersionChanged || textChanged)
            {
                contentText.text = displayText;
            }

            if (evaluatePreferredSize &&
                _contentSizeInitialized[contentIndex] &&
                !contentVersionChanged &&
                !textChanged)
            {
                contentSize = _contentAppliedSizes[contentIndex];
            }
            else
            {
                contentSize = evaluatePreferredSize
                    ? EvaluatePreferredSize(contentText, viewportHeight)
                    : new Vector2(Mathf.Max(1f, requestedSize.x), Mathf.Max(1f, requestedSize.y));
            }

            bool sizeChanged = !_contentSizeInitialized[contentIndex] || _contentAppliedSizes[contentIndex] != contentSize;
            if (sizeChanged)
            {
                ApplyContentLayout(contentRect, contentSize);
            }

            if (!contentText.gameObject.activeSelf)
            {
                contentText.gameObject.SetActive(true);
            }

            if (contentVersionChanged || textChanged || sizeChanged)
            {
                contentText.ForceMeshUpdate();
                InvalidateContentState(contentIndex);
            }

            _contentAppliedTexts[contentIndex] = displayText;
            _contentAppliedSizes[contentIndex] = contentSize;
            _contentSizeInitialized[contentIndex] = true;
            _contentAppliedVersions[contentIndex] = _contentVersion;
            return true;
        }

        private Vector2 EvaluatePreferredSize(TextMeshProUGUI contentText, float viewportHeight)
        {
            Vector2 preferredSize = contentText.GetPreferredValues(contentText.text, Mathf.Infinity, viewportHeight);
            return new Vector2(
                Mathf.Max(1f, preferredSize.x),
                Mathf.Max(viewportHeight, preferredSize.y));
        }

        private void ApplyContentLayout(RectTransform contentRect, Vector2 contentSize)
        {
            contentRect.anchorMin = new Vector2(0.5f, 0.5f);
            contentRect.anchorMax = new Vector2(0.5f, 0.5f);
            contentRect.pivot = CENTER_PIVOT;
            contentRect.sizeDelta = contentSize;
        }

        private void CopyTextSettings(TextMeshProUGUI source, TextMeshProUGUI target)
        {
            target.font = source.font;
            target.fontSharedMaterial = source.fontSharedMaterial;
            target.color = source.color;
            target.fontSize = source.fontSize;
            target.fontStyle = source.fontStyle;
            target.alignment = source.alignment;
            target.richText = source.richText;
            target.enableAutoSizing = source.enableAutoSizing;
            target.fontSizeMin = source.fontSizeMin;
            target.fontSizeMax = source.fontSizeMax;
            target.characterSpacing = source.characterSpacing;
            target.wordSpacing = source.wordSpacing;
            target.lineSpacing = source.lineSpacing;
            target.paragraphSpacing = source.paragraphSpacing;
            target.margin = Vector4.zero;
            target.textWrappingMode = TextWrappingModes.NoWrap;
            target.overflowMode = TextOverflowModes.Overflow;
            target.spriteAsset = source.spriteAsset;
            target.styleSheet = source.styleSheet;
            target.isRightToLeftText = source.isRightToLeftText;
        }

        private void ApplyState()
        {
            if (!_isFlowActive)
            {
                return;
            }

            if (_contentTexts.Count == 0 ||
                _contentRects.Count == 0 ||
                _sequenceContentSizes.Count == 0)
            {
                return;
            }

            if (UsesSequenceFlow(_direction))
            {
                ApplySequenceState();
                return;
            }

            ApplyTurnState();
        }

        private void ApplySequenceState()
        {
            float travelDistance = _cachedSequenceDistance;
            float moveDuration = travelDistance / Mathf.Max(0.01f, _velocity);
            float progress = moveDuration > 0f ? Mathf.Clamp01(_turnElapsedTime / moveDuration) : 1f;
            float flowDistance = travelDistance * progress;
            float phase = Mathf.Repeat(Mathf.Max(0f, flowDistance), travelDistance);
            Vector2 directionVector = GetDirectionVector(_direction);
            int sequenceItemCount = Mathf.Max(1, _sequenceItemCount);

            for (int i = 0; i < _contentTexts.Count; i++)
            {
                TextMeshProUGUI contentText = _contentTexts[i];
                RectTransform contentRect = _contentRects[i];
                if (contentText == null || contentRect == null || !contentText.gameObject.activeSelf)
                {
                    continue;
                }

                int itemIndex = i % sequenceItemCount;
                int sequenceIndex = i / sequenceItemCount;
                float itemOffset = itemIndex < _sequenceItemOffsets.Count ? _sequenceItemOffsets[itemIndex] : 0f;
                float offset = itemOffset - travelDistance * sequenceIndex + phase;
                Vector2 position = _flowOriginOffset + directionVector * offset;

                ApplyContentState(i, contentText, contentRect, position, true);
            }
        }

        private void ApplyTurnState()
        {
            Vector2 currentSize = _sequenceContentSizes.Count > 0 ? _sequenceContentSizes[0] : Vector2.one;
            float travelDistance = EvaluateTravelDistance(currentSize, _direction, _gap);
            float moveDuration = travelDistance / Mathf.Max(0.01f, _velocity);
            float progress = moveDuration > 0f ? Mathf.Clamp01(_turnElapsedTime / moveDuration) : 1f;
            float flowDistance = travelDistance * progress;
            Vector2 directionVector = GetDirectionVector(_direction);
            int visibleContentCount = Mathf.Min(2, Mathf.Min(_contentTexts.Count, _contentRects.Count));

            for (int i = 0; i < visibleContentCount; i++)
            {
                TextMeshProUGUI contentText = _contentTexts[i];
                RectTransform contentRect = _contentRects[i];
                if (contentText == null || contentRect == null)
                {
                    continue;
                }

                float offset = i == 0 ? flowDistance : flowDistance - travelDistance;
                ApplyContentState(i, contentText, contentRect, _flowOriginOffset + directionVector * offset, true);
                contentText.gameObject.SetActive(true);
            }

            for (int i = visibleContentCount; i < _contentTexts.Count; i++)
            {
                if (_contentTexts[i] == null || _contentRects[i] == null)
                {
                    continue;
                }

                ApplyContentState(i, _contentTexts[i], _contentRects[i], Vector2.zero, false);
                _contentTexts[i].gameObject.SetActive(false);
            }
        }

        private void ApplyContentState(
            int contentIndex,
            TextMeshProUGUI contentText,
            RectTransform contentRect,
            Vector2 anchoredPosition,
            bool visible)
        {
            if (contentIndex < 0 ||
                contentIndex >= _contentAppliedPositions.Count ||
                contentIndex >= _contentAppliedVisible.Count)
            {
                contentText.enabled = visible;
                if (visible)
                {
                    contentRect.pivot = CENTER_PIVOT;
                    contentRect.anchoredPosition = anchoredPosition;
                    contentRect.localScale = Vector3.one;
                }

                return;
            }

            bool visibilityChanged = _contentAppliedVisible[contentIndex] != visible;
            if (visibilityChanged)
            {
                contentText.enabled = visible;
                _contentAppliedVisible[contentIndex] = visible;
            }

            if (!visible)
            {
                return;
            }

            Vector2 lastPosition = _contentAppliedPositions[contentIndex];
            bool positionChanged = float.IsNaN(lastPosition.x) ||
                                     !Mathf.Approximately(lastPosition.x, anchoredPosition.x) ||
                                     !Mathf.Approximately(lastPosition.y, anchoredPosition.y);
            if (positionChanged)
            {
                contentRect.anchoredPosition = anchoredPosition;
                _contentAppliedPositions[contentIndex] = anchoredPosition;
            }

            if (visibilityChanged || positionChanged)
            {
                contentRect.pivot = CENTER_PIVOT;
                contentRect.localScale = Vector3.one;
            }
        }
    }
}
