using UnityEngine;
using TMPro;

namespace CAT.UI
{
    /// <summary>
    /// TMP 텍스트 곡선 효과
    /// - AnimationCurve를 따라 텍스트 정점을 변형
    /// - TMP 이벤트 기반으로 텍스트 변경 즉시 곡선 적용 (깜빡임 방지)
    /// - LateUpdate에서 설정 변경 감지 및 추가 업데이트
    /// - 모바일 최적화: 불필요한 업데이트 스킵
    /// </summary>
    [ExecuteAlways]
    [DefaultExecutionOrder(10)]
    [RequireComponent(typeof(TMP_Text))]
    [AddComponentMenu("CAT/UI/TMP Curve")]
    public class TMPCurve : MonoBehaviour
    {
        // ─────────────────────────────────────────────
        // Inspector 설정
        // ─────────────────────────────────────────────

        [Header("Curve Settings")]
        [Tooltip("텍스트가 따라갈 곡선. X축은 텍스트 위치(0~1), Y축은 높이 오프셋")]
        [SerializeField]
        private AnimationCurve _curve = new AnimationCurve(
            new Keyframe(0f, 0f),
            new Keyframe(0.5f, 1f),
            new Keyframe(1f, 0f)
        );

        [Tooltip("곡선의 수직 스케일 (픽셀 단위). 동적 보정이 켜지면 이 값은 '기준 조건에서의' 강도로 사용됩니다.")]
        [SerializeField]
        private float _curveScale = 50f;

        [Tooltip("곡선·회전 적용 후에도 가시 글자 정점의 Y 평균이 변형 전과 같도록 보정해, 텍스트가 길어져도 수직으로 밀리지 않게 합니다.")]
        [SerializeField]
        private bool _preserveVerticalCenter = true;

        [Header("Dynamic Curve Scale")]
        [Tooltip("가시 글자 수·실제 폰트 크기·(선택) 레이아웃 크기를 참조값 대비로 보정해, 로컬라이징·Auto Size에서도 곡선 체감이 비슷하게 유지되도록 합니다.")]
        [SerializeField]
        private bool _enableDynamicCurveScale = true;

        [Tooltip("곡선을 맞춰 둔 기준 가시 글자 수(스페이스·태그 제외한 실제 글리프). 예: 영어 10글자로 튜닝했다면 10.")]
        [SerializeField]
        private float _referenceVisibleCharacterCount = 10f;

        [Tooltip("곡선을 맞춰 둔 기준 폰트 크기(동일 TMP의 Font Size / Auto Size 적용 후 기대 크기).")]
        [SerializeField]
        private float _referenceFontSize = 36f;

        [Tooltip("추가 보정: 기준일 때의 TMP RectTransform 크기. (0,0)이면 사용하지 않음.")]
        [SerializeField]
        private Vector2 _referenceRectSize = Vector2.zero;

        [Header("Rotation Settings")]
        [Tooltip("글자가 곡선의 접선 방향을 따라 회전할지 여부")]
        [SerializeField]
        private bool _rotateAlongCurve = true;

        [Tooltip("회전 강도 (0 = 회전 없음, 1 = 완전 회전)")]
        [SerializeField, Range(0f, 1f)]
        private float _rotationStrength = 1f;

        // ─────────────────────────────────────────────
        // 캐싱
        // ─────────────────────────────────────────────

        private TMP_Text _tmpText;
        private RectTransform _rectTransform;
        private bool _isDirty = true;
        private bool _forceUpdateNextFrame = false;
        private float _previousCurveScale;
        private bool _previousRotateAlongCurve;
        private float _previousRotationStrength;

        // RectTransform 크기 변경 감지용
        private Vector2 _previousRectSize;

        // 동적 Curve Scale 기준 변경 감지용
        private int _previousVisibleCharCount = -1;
        private float _previousTmpFontSize = -1f;
        private float _previousTextBoundsWidth = -1f;

        private bool _previousPreserveVerticalCenter;
        private bool _previousEnableDynamicCurveScale;
        private float _previousReferenceVisibleCharacterCount = -1f;
        private float _previousReferenceFontSize = -1f;
        private Vector2 _previousReferenceRectSize = new Vector2(-1f, -1f);

        // Curve 키프레임 해시 (변경 감지용)
        private int _previousCurveHash;

        // ─────────────────────────────────────────────
        // TMPAnimation 연동 — 글자별 커브 오프셋 캐시
        // - TMPAnimation이 함께 붙어 있을 때 정점을 직접 변조하지 않고
        //   글자별 (yOffset, 회전 각도)만 계산해 캐싱하여 TMPAnimation이 합성하도록 함
        // ─────────────────────────────────────────────

        // 글자별 누적 Y 오프셋 (preserveVerticalCenter 보정 포함, 픽셀 단위)
        private float[] _charCurveY;
        // 글자별 접선 회전 각도 (도 단위)
        private float[] _charCurveAngle;
        // 유효 엔트리 수 (= 현재 characterCount)
        private int _charOffsetCount;
        // TMPAnimation이 Play 중일 때 true → 정점 변조 스킵, 오프셋만 캐싱
        private bool _suppressVertexModification;
        // 오프셋 버전 (외부에서 캐시 무효화 감지용)
        private int _offsetsVersion;

        // ─────────────────────────────────────────────
        // Public Properties
        // ─────────────────────────────────────────────

        /// <summary>
        /// 텍스트가 따라갈 곡선
        /// </summary>
        public AnimationCurve Curve
        {
            get => _curve;
            set
            {
                _curve = value;
                SetDirty();
            }
        }

        /// <summary>
        /// 곡선의 수직 스케일 (픽셀 단위)
        /// </summary>
        public float CurveScale
        {
            get => _curveScale;
            set
            {
                _curveScale = value;
                SetDirty();
            }
        }

        /// <summary>
        /// 글자가 곡선의 접선 방향을 따라 회전할지 여부
        /// </summary>
        public bool RotateAlongCurve
        {
            get => _rotateAlongCurve;
            set
            {
                _rotateAlongCurve = value;
                SetDirty();
            }
        }

        /// <summary>
        /// 회전 강도 (0~1)
        /// </summary>
        public float RotationStrength
        {
            get => _rotationStrength;
            set
            {
                _rotationStrength = Mathf.Clamp01(value);
                SetDirty();
            }
        }

        // ─────────────────────────────────────────────
        // 라이프사이클
        // ─────────────────────────────────────────────

        private void Awake()
        {
            CacheComponents();
        }

        private void OnEnable()
        {
            CacheComponents();

            // TMP 텍스트 변경 이벤트 구독 (깜빡임 방지의 핵심)
            TMPro_EventManager.TEXT_CHANGED_EVENT.Add(OnTextChanged);

            _forceUpdateNextFrame = true;
            SetDirty();
        }

        private void OnDisable()
        {
            // 이벤트 구독 해제
            TMPro_EventManager.TEXT_CHANGED_EVENT.Remove(OnTextChanged);

            // 비활성화 시 원본 메시로 복원
            if (_tmpText != null)
            {
                _tmpText.ForceMeshUpdate();
            }
        }

        /// <summary>
        /// TMP 텍스트 변경 이벤트 핸들러
        /// - TMP가 메시를 업데이트한 직후 호출됨
        /// - 이 시점에 곡선을 적용해야 깜빡임이 없음
        /// </summary>
        private void OnTextChanged(Object obj)
        {
            // 이 컴포넌트의 TMP인지 확인
            if (obj == _tmpText)
            {
                ApplyCurveToMesh();
            }
        }

        private void LateUpdate()
        {
            if (_tmpText == null) return;

            // 강제 업데이트 플래그 (OnEnable 직후)
            if (_forceUpdateNextFrame)
            {
                _forceUpdateNextFrame = false;
                if (_suppressVertexModification)
                {
                    // TMPAnimation이 주도하는 상태 — 정점 재생성 대신 오프셋만 갱신
                    ApplyCurveToMesh();
                }
                else
                {
                    _tmpText.ForceMeshUpdate();
                    // OnTextChanged가 호출되어 곡선 적용됨
                }
                _isDirty = false;
                return;
            }

            // 설정 변경 감지 (Inspector 변경 등)
            CheckSettingsDirty();

            // RectTransform 크기 변경 감지
            CheckRectSizeDirty();

            // 동적 스케일 기준(글자 수·폰트·bounds) 변경 감지 — 문자열변경 없이 Auto Size만 바뀌는 경우
            CheckDynamicCurveBasisDirty();

            if (_isDirty)
            {
                if (_suppressVertexModification)
                {
                    // 억제 상태에서는 ForceMeshUpdate가 TMPAnimation의 프레임 출력을 덮어쓰므로
                    // 오프셋만 재계산하여 TMPAnimation이 다음 프레임에 소비하도록 한다
                    ApplyCurveToMesh();
                }
                else
                {
                    _tmpText.ForceMeshUpdate();
                    // OnTextChanged가 호출되어 곡선 적용됨
                }
                _isDirty = false;
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            CacheComponents();
            SetDirty();
        }
#endif

        // ─────────────────────────────────────────────
        // Private Methods
        // ─────────────────────────────────────────────

        private void CacheComponents()
        {
            if (_tmpText == null)
            {
                _tmpText = GetComponent<TMP_Text>();
            }
            if (_rectTransform == null)
            {
                _rectTransform = GetComponent<RectTransform>();
            }
        }

        private void SetDirty()
        {
            _isDirty = true;
        }

        /// <summary>
        /// 설정 변경 감지 (Inspector 값 변경 등)
        /// </summary>
        private void CheckSettingsDirty()
        {
            // 스케일 변경 확인
            if (!Mathf.Approximately(_curveScale, _previousCurveScale))
            {
                _previousCurveScale = _curveScale;
                _isDirty = true;
            }

            // 회전 설정 변경 확인
            if (_rotateAlongCurve != _previousRotateAlongCurve)
            {
                _previousRotateAlongCurve = _rotateAlongCurve;
                _isDirty = true;
            }

            if (!Mathf.Approximately(_rotationStrength, _previousRotationStrength))
            {
                _previousRotationStrength = _rotationStrength;
                _isDirty = true;
            }

            // Curve 변경 확인 (간단한 해시 비교)
            int curveHash = GetCurveHash();
            if (curveHash != _previousCurveHash)
            {
                _previousCurveHash = curveHash;
                _isDirty = true;
            }

            if (_enableDynamicCurveScale != _previousEnableDynamicCurveScale)
            {
                _previousEnableDynamicCurveScale = _enableDynamicCurveScale;
                _isDirty = true;
            }

            if (!Mathf.Approximately(_referenceVisibleCharacterCount, _previousReferenceVisibleCharacterCount))
            {
                _previousReferenceVisibleCharacterCount = _referenceVisibleCharacterCount;
                _isDirty = true;
            }

            if (!Mathf.Approximately(_referenceFontSize, _previousReferenceFontSize))
            {
                _previousReferenceFontSize = _referenceFontSize;
                _isDirty = true;
            }

            if (_referenceRectSize != _previousReferenceRectSize)
            {
                _previousReferenceRectSize = _referenceRectSize;
                _isDirty = true;
            }

            if (_preserveVerticalCenter != _previousPreserveVerticalCenter)
            {
                _previousPreserveVerticalCenter = _preserveVerticalCenter;
                _isDirty = true;
            }
        }

        /// <summary>
        /// 가시 글자 수·실제 폰트 크기·렌더 bounds 너비 변경 감지 (문자열은 같고 Auto Size만 바뀌는 경우 등)
        /// </summary>
        private void CheckDynamicCurveBasisDirty()
        {
            if (!_enableDynamicCurveScale || _tmpText == null) return;

            TMP_TextInfo ti = _tmpText.textInfo;
            if (ti == null) return;

            int visible = CountVisibleCharacters(ti);
            float fontSz = _tmpText.fontSize;
            float bw = _tmpText.bounds.size.x;

            if (visible != _previousVisibleCharCount ||
                !Mathf.Approximately(fontSz, _previousTmpFontSize) ||
                !Mathf.Approximately(bw, _previousTextBoundsWidth))
            {
                _previousVisibleCharCount = visible;
                _previousTmpFontSize = fontSz;
                _previousTextBoundsWidth = bw;
                _isDirty = true;
            }
        }

        /// <summary>
        /// RectTransform 크기 변경 감지 (LayoutElement에 의한 크기 변경)
        /// </summary>
        private void CheckRectSizeDirty()
        {
            if (_rectTransform == null) return;

            Vector2 currentSize = _rectTransform.rect.size;
            if (!Mathf.Approximately(currentSize.x, _previousRectSize.x) ||
                !Mathf.Approximately(currentSize.y, _previousRectSize.y))
            {
                _previousRectSize = currentSize;
                _isDirty = true;
            }
        }

        /// <summary>
        /// AnimationCurve의 간단한 해시 계산
        /// </summary>
        private int GetCurveHash()
        {
            if (_curve == null || _curve.length == 0) return 0;

            unchecked
            {
                int hash = 17;
                for (int i = 0; i < _curve.length; i++)
                {
                    var key = _curve[i];
                    hash = hash * 31 + key.time.GetHashCode();
                    hash = hash * 31 + key.value.GetHashCode();
                }
                return hash;
            }
        }

        private static int CountVisibleCharacters(TMP_TextInfo textInfo)
        {
            int n = 0;
            for (int i = 0; i < textInfo.characterCount; i++)
            {
                if (textInfo.characterInfo[i].isVisible) n++;
            }
            return n;
        }

        /// <summary>
        /// Inspector의 Curve Scale을 기준값으로 두고, 가시 글자 수·폰트·(선택) Rect로 보정한 실제 스케일.
        /// </summary>
        private float GetEffectiveCurveScale(TMP_TextInfo textInfo)
        {
            if (!_enableDynamicCurveScale)
                return _curveScale;

            int visible = CountVisibleCharacters(textInfo);
            if (visible <= 0)
                return _curveScale;

            float refChars = Mathf.Max(1f, _referenceVisibleCharacterCount);
            float refFont = Mathf.Max(0.01f, _referenceFontSize);

            float charFactor = Mathf.Max(1f, visible) / refChars;
            float fontFactor = _tmpText.fontSize / refFont;

            float scale = _curveScale * charFactor * fontFactor;

            if (_rectTransform != null &&
                _referenceRectSize.x > 0.01f &&
                _referenceRectSize.y > 0.01f)
            {
                Vector2 cur = _rectTransform.rect.size;
                float refArea = Mathf.Max(0.01f, _referenceRectSize.x * _referenceRectSize.y);
                float curArea = Mathf.Max(0.01f, Mathf.Abs(cur.x * cur.y));
                scale *= Mathf.Sqrt(curArea / refArea);
            }
            else if (_rectTransform != null && _referenceRectSize.x > 0.01f && _referenceRectSize.y <= 0.01f)
            {
                float rw = Mathf.Max(0.01f, _referenceRectSize.x);
                scale *= _rectTransform.rect.width / rw;
            }
            else if (_rectTransform != null && _referenceRectSize.y > 0.01f && _referenceRectSize.x <= 0.01f)
            {
                float rh = Mathf.Max(0.01f, _referenceRectSize.y);
                scale *= _rectTransform.rect.height / rh;
            }

            return scale;
        }

        /// <summary>
        /// 곡선 효과를 현재 메시에 적용 (ForceMeshUpdate 없이)
        /// - TEXT_CHANGED_EVENT에서 호출됨
        /// - 항상 글자별 오프셋을 계산해 캐시에 저장 (TMPAnimation 연동용)
        /// - _suppressVertexModification이 false일 때만 정점에도 베이크
        /// </summary>
        private void ApplyCurveToMesh()
        {
            if (_curve == null || _tmpText == null) return;
            if (!isActiveAndEnabled) return;

            // Curve 래핑 모드 설정 — 평가는 언클램프드 값을 유지
            _curve.preWrapMode = WrapMode.Clamp;
            _curve.postWrapMode = WrapMode.Clamp;

            TMP_TextInfo textInfo = _tmpText.textInfo;
            if (textInfo == null) return;
            int characterCount = textInfo.characterCount;

            if (characterCount == 0)
            {
                _charOffsetCount = 0;
                return;
            }

            float effectiveCurveScale = GetEffectiveCurveScale(textInfo);

            // 텍스트 경계 계산
            float boundsMinX = _tmpText.bounds.min.x;
            float boundsMaxX = _tmpText.bounds.max.x;
            float boundsWidth = boundsMaxX - boundsMinX;

            if (boundsWidth <= 0) return;

            // 1) 글자별 오프셋 계산 — 정점과 무관하게 항상 수행 (TMPAnimation이 소비)
            ComputeCharacterCurveOffsets(textInfo, characterCount, effectiveCurveScale, boundsMinX, boundsWidth);

            // 2) 정점 변조 — TMPAnimation 연동 시에는 스킵 (TMPAnimation이 후처리로 합성)
            if (_suppressVertexModification) return;

            ApplyCachedOffsetsToVertices(textInfo, characterCount);

            // 메시 업데이트 (정점 데이터만)
            _tmpText.UpdateVertexData();
        }

        /// <summary>
        /// 각 글자의 커브 Y 오프셋과 접선 회전 각도를 계산해 캐시에 저장.
        /// preserveVerticalCenter가 켜진 경우, 보이는 글자들의 평균 Y 이동량을 빼서 수직 중심을 유지.
        /// (회전으로 인한 Y 기여는 작은 각도에서 미미하여 근사로 처리)
        /// </summary>
        private void ComputeCharacterCurveOffsets(
            TMP_TextInfo textInfo,
            int characterCount,
            float effectiveCurveScale,
            float boundsMinX,
            float boundsWidth)
        {
            EnsureOffsetCapacity(characterCount);
            _charOffsetCount = characterCount;

            for (int i = 0; i < characterCount; i++)
            {
                _charCurveY[i] = 0f;
                _charCurveAngle[i] = 0f;
            }

            double sumCurveY = 0.0;
            int visibleCount = 0;

            for (int i = 0; i < characterCount; i++)
            {
                TMP_CharacterInfo charInfo = textInfo.characterInfo[i];
                if (!charInfo.isVisible) continue;

                int vertexIndex = charInfo.vertexIndex;
                int materialIndex = charInfo.materialReferenceIndex;
                Vector3[] vertices = textInfo.meshInfo[materialIndex].vertices;

                float charCenterX = (vertices[vertexIndex].x + vertices[vertexIndex + 2].x) / 2f;
                float normalizedX = (charCenterX - boundsMinX) / boundsWidth;

                // 언클램프드 평가 — 키프레임 값이 0~1을 벗어나도 그대로 사용
                float curveY = _curve.Evaluate(normalizedX) * effectiveCurveScale;

                float angle = 0f;
                if (_rotateAlongCurve && _rotationStrength > 0f)
                {
                    // 미분을 통한 접선 계산 (작은 델타 사용)
                    float delta = 0.001f;
                    float x0 = Mathf.Clamp01(normalizedX - delta);
                    float x1 = Mathf.Clamp01(normalizedX + delta);
                    float y0 = _curve.Evaluate(x0) * effectiveCurveScale;
                    float y1 = _curve.Evaluate(x1) * effectiveCurveScale;

                    Vector2 tangent = new Vector2((x1 - x0) * boundsWidth, y1 - y0);
                    angle = Mathf.Atan2(tangent.y, tangent.x) * Mathf.Rad2Deg * _rotationStrength;
                }

                _charCurveY[i] = curveY;
                _charCurveAngle[i] = angle;
                sumCurveY += curveY;
                visibleCount++;
            }

            // 수직 중심 보정: 평균 curveY만큼 빼서 전체가 수직으로 밀리지 않게 함 (근사)
            if (_preserveVerticalCenter && visibleCount > 0)
            {
                float dy = (float)(-sumCurveY / visibleCount);
                if (Mathf.Abs(dy) > 1e-5f)
                {
                    for (int i = 0; i < characterCount; i++)
                    {
                        if (textInfo.characterInfo[i].isVisible)
                        {
                            _charCurveY[i] += dy;
                        }
                    }
                }
            }

            _offsetsVersion++;
        }

        /// <summary>
        /// 캐시된 오프셋을 실제 메시 정점에 베이크 (standalone 동작).
        /// </summary>
        private void ApplyCachedOffsetsToVertices(TMP_TextInfo textInfo, int characterCount)
        {
            for (int i = 0; i < characterCount; i++)
            {
                TMP_CharacterInfo charInfo = textInfo.characterInfo[i];
                if (!charInfo.isVisible) continue;

                int vertexIndex = charInfo.vertexIndex;
                int materialIndex = charInfo.materialReferenceIndex;
                Vector3[] vertices = textInfo.meshInfo[materialIndex].vertices;

                Vector3 charCenter = new Vector3(
                    (vertices[vertexIndex].x + vertices[vertexIndex + 2].x) / 2f,
                    charInfo.baseLine,
                    0f
                );

                ApplyCurveOffsetToQuad(vertices, vertexIndex, charCenter, _charCurveY[i], _charCurveAngle[i]);
            }
        }

        /// <summary>
        /// 주어진 쿼드(4 정점)에 커브 오프셋(중심 기준 Z축 회전 + Y 이동)을 합성.
        /// TMPAnimation이 자체 변환을 적용한 후 후처리로 호출할 수 있도록 static 제공.
        /// </summary>
        internal static void ApplyCurveOffsetToQuad(
            Vector3[] vertices,
            int vertexIndex,
            Vector3 center,
            float yOffset,
            float angleDegrees)
        {
            bool hasRotation = Mathf.Abs(angleDegrees) > 1e-6f;
            float cos = 1f, sin = 0f;
            if (hasRotation)
            {
                float rad = angleDegrees * Mathf.Deg2Rad;
                cos = Mathf.Cos(rad);
                sin = Mathf.Sin(rad);
            }

            for (int i = 0; i < 4; i++)
            {
                int idx = vertexIndex + i;
                Vector3 v = vertices[idx];
                float rx = v.x - center.x;
                float ry = v.y - center.y;
                if (hasRotation)
                {
                    float nx = rx * cos - ry * sin;
                    float ny = rx * sin + ry * cos;
                    rx = nx;
                    ry = ny;
                }
                v.x = rx + center.x;
                v.y = ry + center.y + yOffset;
                vertices[idx] = v;
            }
        }

        private void EnsureOffsetCapacity(int required)
        {
            if (_charCurveY == null || _charCurveY.Length < required)
            {
                int cap = Mathf.Max(required, 16);
                _charCurveY = new float[cap];
                _charCurveAngle = new float[cap];
            }
        }

        // ─────────────────────────────────────────────
        // Public Methods
        // ─────────────────────────────────────────────

        /// <summary>
        /// 강제로 곡선 효과 다시 적용
        /// </summary>
        public void Refresh()
        {
            SetDirty();
        }

        // ─────────────────────────────────────────────
        // TMPAnimation 연동 API
        // ─────────────────────────────────────────────

        /// <summary>
        /// 글자별 커브 오프셋 캐시가 유효한지 여부
        /// </summary>
        public bool HasCurveOffsets => _charCurveY != null && _charOffsetCount > 0;

        /// <summary>
        /// 오프셋 버전 — 설정 변경 등으로 오프셋이 재계산되면 증가
        /// </summary>
        public int OffsetsVersion => _offsetsVersion;

        /// <summary>
        /// 특정 글자 인덱스의 커브 Y 오프셋과 접선 회전 각도를 조회.
        /// </summary>
        /// <param name="charIndex">TMP 캐릭터 인덱스</param>
        /// <param name="yOffset">커브 Y 오프셋 (픽셀, preserveVerticalCenter 반영)</param>
        /// <param name="angleDegrees">접선 기반 회전 각도 (도 단위)</param>
        /// <returns>캐시된 오프셋이 존재하면 true</returns>
        public bool TryGetCurveOffset(int charIndex, out float yOffset, out float angleDegrees)
        {
            if (_charCurveY == null || charIndex < 0 || charIndex >= _charOffsetCount)
            {
                yOffset = 0f;
                angleDegrees = 0f;
                return false;
            }
            yOffset = _charCurveY[charIndex];
            angleDegrees = _charCurveAngle[charIndex];
            return true;
        }

        /// <summary>
        /// TMPAnimation 연동 시 정점 베이크를 억제할지 설정.
        /// - true: 오프셋만 캐싱, 정점은 pristine 유지 (TMPAnimation이 후처리로 합성)
        /// - false: 기존처럼 정점에 직접 베이크 (standalone 동작)
        /// </summary>
        public void SetSuppressVertexModification(bool suppressed)
        {
            if (_suppressVertexModification == suppressed) return;
            _suppressVertexModification = suppressed;
            // 억제 해제 시 다음 프레임에 정점에 다시 베이크
            if (!suppressed)
            {
                SetDirty();
                _forceUpdateNextFrame = true;
            }
        }

        /// <summary>
        /// 곡선을 아치 형태로 설정
        /// </summary>
        /// <param name="height">아치 높이 (픽셀)</param>
        public void SetArchCurve(float height)
        {
            _curve = new AnimationCurve(
                new Keyframe(0f, 0f, 0f, 2f),
                new Keyframe(0.5f, 1f, 0f, 0f),
                new Keyframe(1f, 0f, -2f, 0f)
            );
            _curveScale = height;
            SetDirty();
        }

        /// <summary>
        /// 곡선을 웨이브 형태로 설정
        /// </summary>
        /// <param name="amplitude">웨이브 진폭 (픽셀)</param>
        /// <param name="frequency">웨이브 주기 (1 = 한 주기)</param>
        public void SetWaveCurve(float amplitude, float frequency = 1f)
        {
            int keyCount = Mathf.Max(5, Mathf.RoundToInt(frequency * 4) + 1);
            Keyframe[] keys = new Keyframe[keyCount];

            for (int i = 0; i < keyCount; i++)
            {
                float t = (float)i / (keyCount - 1);
                float value = Mathf.Sin(t * frequency * Mathf.PI * 2f);
                keys[i] = new Keyframe(t, value);
            }

            _curve = new AnimationCurve(keys);
            _curveScale = amplitude;
            SetDirty();
        }

        /// <summary>
        /// 곡선을 직선으로 리셋
        /// </summary>
        public void ResetCurve()
        {
            _curve = new AnimationCurve(
                new Keyframe(0f, 0f),
                new Keyframe(1f, 0f)
            );
            _curveScale = 0f;
            SetDirty();
        }
    }
}
