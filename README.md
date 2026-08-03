# TMP Effects - 모바일 최적화 TextMeshPro 효과 시스템

모바일 게임에 특화된 TMP 텍스트 효과 시스템입니다.  
Material 자동 공유, GC 제거, 더티 체크 최적화로 Galaxy S10 / iPhone 11 @ 60 FPS 타겟.

## 설치 (UPM)

Unity Package Manager → **Add package from git URL...** 에 아래 주소를 입력합니다.

```
https://github.com/zzamjak-cloud/TMPEffect.git?path=Assets/TMPEffects
```

특정 버전 설치:

```
https://github.com/zzamjak-cloud/TMPEffect.git?path=Assets/TMPEffects#v1.0.0
```

> **Private 레포 안내**: 이 저장소가 private인 동안 git URL 설치는 GitHub 인증이 설정된 환경에서만 동작합니다.
> (Git Credential Manager 로그인 또는 `https://<PAT>@github.com/zzamjak-cloud/TMPEffect.git?path=Assets/TMPEffects` 형태의 Personal Access Token 사용)

## 컴포넌트 요약

| 컴포넌트 | 역할 | 핵심 |
|----------|------|------|
| **TMPOutlineEffect** | Outline / Shadow / Second Face | Underlay(GPU) + Mesh Shadow(CPU) |
| **TMPOutGlow** | 방사형 Glow + Inner Glow | Underlay(GPU) + 자식 오브젝트 |
| **TMPAnimation** | 글자별 애니메이션 | Appear → Loop → Disappear + 이벤트 기반 Impact, ICurve 이징 |
| **TMPCurve** | 텍스트 곡선 변형 | AnimationCurve 정점 수정 + 동적 스케일·수직 중심 보정 (v1.1.1+) · TMPAnimation 호환 (v1.1.2+) |
| **TMPLayoutLimiter** | 너비/높이 제한 | LayoutElement 조작 |
| **TMPMaskFlow** | 마스크 영역 전광판 흐름 | RectMask2D + 복제 TMP 이동, Static overflow 자동 전환 (v1.2.1+) |

## 빠른 시작

### Inspector (권장)

```
1. TMP 오브젝트 선택
2. Add Component → CAT/UI → 원하는 컴포넌트 추가
3. 인스펙터에서 설정 조정
4. "💾 새 프리셋 저장" 으로 프리셋화
```

### 코드

```csharp
using CAT.UI;

// Outline
var effect = gameObject.AddComponent<TMPOutlineEffect>();
effect.SetOutline(Color.black, 0.2f);

// Shadow 추가
effect.SetOutlineWithShadow(Color.black, 0.2f, 0.5f, new Vector2(0.1f, -0.1f));

// Glow
var glow = gameObject.AddComponent<TMPOutGlow>();
glow.SetGlow(new Color(1f, 0.8f, 0f, 0.5f), 0.3f);

// Animation
var anim = gameObject.AddComponent<TMPAnimation>();
anim.Play();

// 프리셋 적용
var preset = Resources.Load<TMPEffectPreset>("Presets/TitleOutline");
effect.ApplyPreset(preset);
```

## TMPOutlineEffect 상세

### 효과 종류
- **Outline** (GPU): Underlay Dilate로 외곽선 — `effect.UnderlayDilate = 0.2f`
- **Drop Shadow** (GPU): Underlay Offset으로 그림자 — `effect.UnderlayOffsetX/Y`
- **Shadow** (CPU, 선택적): 정점 복제 기반, `effect.EnableShadow = true`
- **Second Face** (자식 오브젝트): 안쪽 축소 텍스트 레이어, `effect.EnableSecondFace = true`
- **Face Dilate**: 텍스트 본체 두께 조절 (-1 ~ 1)

### 주요 속성

```csharp
// Outline (GPU)
effect.UnderlayColor / UnderlayDilate / UnderlayOffsetX / UnderlayOffsetY / UnderlaySoftness

// Shadow (CPU)
effect.EnableShadow / ShadowOffset / ShadowAlpha

// Second Face (자식 오브젝트, v2.3.0+)
effect.EnableSecondFace / SecondFaceColor / SecondFaceDilate / SecondFaceOffsetX / SecondFaceOffsetY

// Face
effect.EnableFace / FaceDilate
```

## TMPOutGlow 상세

```csharp
glow.GlowColor = Color.yellow;      // Glow 색상 (RGB + Alpha)
glow.GlowRange = 0.3f;              // 빛 번짐 범위 (0~1)
glow.InnerGlowAlpha = 1f;           // Inner Glow 강도 (0~1)
glow.FaceDilate = 0f;               // 텍스트 굵기 (-1~1)
```

- Glow Color RGB → TMP Tint Color에 자동 반영
- Inner Glow는 `[Inner Glow]` 자식 오브젝트로 자동 생성

## TMPAnimation 상세

### 기본 구조: Appear → Loop → Disappear (시간 기반)

각 단계별 Position / Scale / Rotation / Alpha 독립 제어.

```csharp
anim.CharacterDelay = 0.05f;        // 글자 간 딜레이
anim.Pivot = new Vector2(0.5f, 0.5f); // 스케일/회전 피벗 (정규화 0~1, 기본 (0.5, 0) = 가로 중앙 + 베이스라인)

// Appear
anim.EnableAppear = true;
anim.AppearPosition = new Vector3(0, 50, 0);
anim.AppearAlpha = 0f;
anim.AppearDuration = 0.5f;
anim.AppearCurve = ICurve.Ease(EaseType.OutBack);

// Loop
anim.EnableLoop = true;
anim.LoopCount = -1;                // -1 = 무한
anim.LoopType = TMPLoopMode.Yoyo;

// Disappear
anim.EnableDisappear = true;
anim.DisappearDuration = 0.5f;

// 제어
anim.Play() / Pause() / Resume() / Stop() / Restart()
```

### Impact (이벤트 기반 단발성 연출) — v1.1.1+

Appear/Loop/Disappear와 **독립 레이어로 병렬 합성**되는 단발성 애니메이션. Loop가 돌아가는 중에도 그 위에 오프셋으로 겹쳐서 재생된다.

| 합성 방식 | 속성 |
|---|---|
| **곱셈** | Scale (기존 Scale × Impact Scale) |
| **덧셈** | Position / Rotation |
| **미지원** | Alpha |

```csharp
// 설정
anim.EnableImpact = true;           // Inspector에서도 체크 가능
// Peak 값은 Inspector의 Impact Animation 섹션에서 지정 (Scale / Position / Rotation / Duration / Curve / Character Delay)

// 런타임 트리거 — 예: 동전이 숫자에 꽂힐 때마다 호출
anim.TriggerImpact();

// 상태 조회
if (anim.IsImpactPlaying)
{
    // 재생 중인 동안 추가 TriggerImpact() 호출은 자동 무시(Ignore)
    // 전체 글자 완료 시점에 다시 이벤트를 받을 수 있다
}
```

**동작 특성**
- **Ignore 정책**: 재생 중 추가 `TriggerImpact()` 호출은 무시됨 → 짧은 간격으로 연속 트리거해도 파르르 떨림이 리셋되지 않고 자연스럽게 유지
- **파도타기**: `_impactCharacterDelay > 0` 이면 좌→우 글자별 순차 재생, `= 0`이면 전체 동시 반응
- **커브**: 일반 0→1 ICurve를 지정하면 내부에서 미러링되어 0 → peak → 0 왕복으로 자동 적용
- **메인 상태 보존**: Impact 종료 시 자동으로 원래 위치(Loop/최종 상태)로 복귀, 블렌딩 캡처도 Impact 잔향에 오염되지 않음

**사용 시나리오**
- 동전이 숫자에 꽂힐 때 숫자 파르르 떨림
- 데미지 텍스트에 연속 타격 이펙트
- 카운트다운 숫자의 강조 펄스
- 텍스트 UI에 외부 충격/이벤트를 동기화한 반응

### Pivot 설정 — v1.1.1+

스케일/회전의 피벗 점을 정규화 좌표(0~1)로 지정.

```csharp
anim.Pivot = new Vector2(0.5f, 0f);   // 기본: 가로 중앙 + 베이스라인 (기존 동작)
anim.Pivot = new Vector2(0.5f, 0.5f); // 시각 중심 — Impact 떨림에 자연스러움
anim.Pivot = new Vector2(0.5f, 1f);   // 상단 고정 (매달린 듯한 효과)
```

Inspector의 `Timing` 섹션에 `Baseline / Center / Top` 프리셋 버튼 제공.

## TMPCurve 상세

`AnimationCurve`의 X(0~1)를 **렌더 bounds 너비**에 맞춰 샘플링하고, `Curve Scale`만큼 Y로 밀어 올려 곡선을 만든다. 글자는 곡선 접선 방향으로 회전할 수 있다(`RotateAlongCurve`, `RotationStrength`).

### 코드 예시

```csharp
var curve = GetComponent<TMPCurve>();
curve.SetArchCurve(50f);             // 아치 (높이 50px)
curve.SetWaveCurve(30f, 2f);         // 웨이브 (진폭 30, 2주기)
curve.RotateAlongCurve = true;
curve.CurveScale = 50f;
curve.Refresh();                     // 설정 후 강제 재적용
```

### Dynamic Curve Scale — v1.1.1+

로컬라이징으로 **가시 글자 수**가 바뀌거나 **Auto Size**로 실제 `fontSize`가 달라져도, 튜닝해 둔 곡선 **느낌**을 맞추기 위한 옵션이다.

| 항목 | 설명 |
|------|------|
| **Enable Dynamic Curve Scale** | 켜면 `Curve Scale`을 “기준 조건에서의 강도”로 두고, 아래 참조값 대비로 유효 스케일을 보정한다. |
| **Reference Visible Character Count** | 곡선을 맞춰 둔 때의 **가시 글자 수** (스페이스·비표시 문자 제외). |
| **Reference Font Size** | 그때의 **실제 표시 폰트 크기** (Auto Size 적용 후 값 기준). |
| **Reference Rect Size** | (선택) 기준일 때 `RectTransform` 크기. x·y 모두 0이면 Rect 보정은 쓰지 않는다. |

인스펙터에서 각 참조 필드 오른쪽 **Get** 버튼으로 **현재 TMP·Rect 상태**를 한 번에 채울 수 있다.

- 텍스트가 바뀌지 않아도 **폰트 크기·bounds 너비**만 바뀌는 경우(레이아웃·Auto Size)를 잡기 위해 `LateUpdate`에서 변화를 감지한다. 타이틀처럼 **문구가 고정**이면 부하는 매우 작다.
- 동적 보정이 **불필요**한 화면(단일 언어·고정 문자열)에서는 **Enable Dynamic Curve Scale**을 끄면 매 프레임 가시 글자 스캔도 생략된다.

### Preserve Vertical Center — v1.1.1+

곡선·회전 적용 후 **가시 글자 정점의 Y 평균**이 변형 전과 같아지도록 한 번 더 평행 이동한다. 문장이 길어져도 블록 전체가 **위로 밀려 보이는 현상**을 줄인다. 끄면 기존처럼 보정 없이 곡선만 적용된다.

### TMPAnimation 호환 — v1.1.2+

같은 GameObject에 `TMPCurve`와 `TMPAnimation`을 함께 붙여도 정상 동작한다. TMPCurve는 글자별 `(Y 오프셋, 접선 회전 각도)`를 캐싱하고, TMPAnimation은 자체 스케일/회전/이동을 적용한 뒤 이 오프셋을 **후처리로 합성**한다. 이를 통해 로컬라이징·커브 스케일 보정·수직 중심 보정이 애니메이션 중에도 그대로 유지된다.

- TMPAnimation 재생 중에는 TMPCurve의 정점 베이크가 자동으로 억제됨 (`SetSuppressVertexModification`)
- Stop / OnDisable 시 standalone 모드로 자동 복귀 — 커브가 다시 정점에 직접 베이크됨
- 외부 연동용 공개 API: `TMPCurve.TryGetCurveOffset(charIndex, out yOffset, out angleDegrees)`, `HasCurveOffsets`, `OffsetsVersion`
- SecondFace / InnerGlow에도 동일한 커브 오프셋이 동기 적용됨

### 언클램프드 커브 편집 — v1.1.2+

인스펙터의 커브 그래프 필드 Y 시야 범위가 -2 ~ 2로 확장되어 **1을 초과하는 키프레임**도 자유롭게 편집·확인할 수 있다. 런타임 `AnimationCurve.Evaluate`는 기존과 동일하게 키프레임 값을 그대로 사용한다(클램핑 없음).

### 성능·사용 시나리오

- **타이틀·버튼 라벨** 등 짧은 문자열·소수 인스턴스에 적합하다.
- 비용은 주로 TMP의 `ForceMeshUpdate` 쪽이며, 곡선은 가시 글자 수에 비례한 정점 루프다.
- **TMPCurve + Shadow**: IMeshModifier 충돌 가능 — 아래 주의사항 참고.

## TMPLayoutLimiter

```csharp
var limiter = GetComponent<TMPLayoutLimiter>();
limiter.MaxWidth = 300f;             // 0 = 제한 없음
limiter.MaxHeight = 100f;
```

## TMPMaskFlow 상세 — v1.2.0+

`RectMask2D`로 잘린 영역 안에서 TMP 텍스트가 흘러가는 **전광판(마퀴)** 연출입니다. 상점 공지, 뉴스 티커처럼 화면에 오래 머무르며 반복 재생되는 UI에 적합합니다. (static 옵션 추가로, 로컬라이징에 따라서 너무 긴 글자는 flow 연출을 통해서 보여주는 방식으로 활용 가능)

### 추가 방법

```
Add Component → CAT/UI → TMP Mask Flow
```

`TextMeshProUGUI`와 `RectMask2D`가 같은 GameObject에 필요합니다. 기본 Flow 모드에서는 소스 TMP가 비활성화되고, 복제된 Content TMP가 실제로 그려집니다. `Static` 모드에서는 텍스트가 마스크 영역 안에 들어올 때 소스 TMP를 그대로 표시해 정렬을 유지합니다.

### Static 모드 — v1.2.1+

`Static`을 켜면 TMP `RectTransform`의 Width / Height를 마스크 기준으로 사용합니다.

- 텍스트 preferred size가 마스크 안에 들어오면 Flow를 진행하지 않고 원본 `TextMeshProUGUI`를 그대로 표시합니다.
- 텍스트가 마스크 영역을 넘으면 원본 TMP를 비활성화하고 복제 TMP로 Flow를 시작합니다.
- Left / Right는 width overflow, Top / Bottom은 height overflow를 기준으로 판단합니다.
- 런타임에서 TMP text나 `SetTextEntries(...)` 값이 바뀌면 다음 갱신 시 fit / overflow가 다시 계산됩니다.
- 같은 localization key의 resolver 결과만 바뀐 경우에는 `Refresh()`를 호출해 preferred size를 다시 계산하세요.

### 방향별 동작

| 방향 | 동작 | Interval |
|------|------|----------|
| **Left / Right** | 등록된 텍스트가 이어진 **시퀀스**로 무한 흐름 | 미사용 |
| **Top / Bottom** | 텍스트를 **한 턴씩** 순환 표시 | 턴 사이 대기 가능 |

### 코드 예시

```csharp
var flow = GetComponent<TMPMaskFlow>();

// localization key 목록
flow.SetTextEntries(new[]
{
    new TMPMaskFlow.TextEntry("ui.shop.notice1", "첫 번째 공지"),
    new TMPMaskFlow.TextEntry("ui.shop.notice2", "두 번째 공지"),
});

// 런타임 localization 연동
flow.SetTextResolver(key => Localization.Get(key));

flow.Static = true; // 텍스트가 마스크를 넘을 때만 Flow
flow.Direction = TMPMaskFlow.FlowDirection.Left;
flow.Velocity = 80f;
flow.Gap = 40f;
flow.Play();
```

### 주요 속성

```csharp
flow.Delay          // 재생 시작 전 대기
flow.Static         // 텍스트가 마스크 영역을 넘을 때만 Flow
flow.Velocity       // 이동 속도 (px/s)
flow.Gap            // 텍스트 사이 빈 거리
flow.Interval       // Top/Bottom 턴 사이 대기
flow.IsPlaying
flow.CurrentTextKey

flow.Play() / Stop() / Restart() / Refresh()
flow.SetTextKeys(...) / SetTextEntries(...) / SetTextResolver(...)
```

### 모바일 성능 — v1.2.0+

- **핫패스**: 재생 중에는 `anchoredPosition` 갱신만 매 프레임 수행. `ForceMeshUpdate`·`GetPreferredValues`는 텍스트·폰트·크기가 바뀔 때만 호출.
- **시퀀스 복사본 상한**: Left/Right에서 뷰포트를 채우기 위해 생성하는 TMP 복제 수를 `_maxSequenceCopyCount`(기본 12)로 제한. `0`이면 제한 없음.
- **Canvas 분리** (`Isolate Render Canvas`, 기본 ON): 상위 UI Canvas 하위에 배치되면 **중첩 Canvas**를 자동 추가합니다. 매 프레임 위치 갱신으로 인한 리빌드가 **부모 Canvas 전체**로 퍼지지 않고, 전광판 오브젝트 범위로 격리됩니다. (리렌더 자체는 피할 수 없음.)

### TMPAnimation과의 관계

같은 GameObject에 `TMPAnimation`과 `TMPMaskFlow`를 **동시에 붙일 수 없습니다**. 전광판 연출과 글자별 애니메이션은 서로 다른 오브젝트로 분리하세요.

### 사용 시나리오

- 상점·로비 **공지 티커** (화면당 1~2개 권장)
- 짧은 문구의 **좌우 무한 스크롤** (Left/Right)
- 여러 공지를 **순서대로 위·아래로 교체** (Top/Bottom + Interval)

### TMPMaskFlowEditor

- **Playback Control**: Play / Stop / Restart / Refresh
- 에디터 모드: **Play (직접)** / **Restart (직접)** 로 런타임과 동일 API 프리뷰
- **Preview Text**: `Text Entries`가 있으면 선택한 entry의 `PreviewText`, 없으면 원본 TMP text를 직접 바꿔가며 Static fit / overflow와 Flow 동작을 테스트

## 프리셋 시스템

- ScriptableObject 기반, 인스펙터에서 저장/적용/갱신
- Outline과 Glow 프리셋은 타입별 자동 분리
- 같은 프리셋 = Material 1개만 생성 (100개 텍스트, 5개 프리셋 = 5개 Material)

## 사용 전략

| Tier | 대상 | 권장 |
|------|------|------|
| **1** | 타이틀, 버튼 (10~20개) | 모든 효과 활용 가능 |
| **2** | 라벨, 수치 | Underlay만 (Shadow OFF) |
| **2** | 상점 공지 티커 | TMPMaskFlow (화면당 1~2개, Isolate Render Canvas ON) |
| **3** | 대화, 설명문 | 효과 없음 또는 Font 베이크 |

## 주의사항

- **Shadow**: 정점 2배 → 중요 UI에만 사용, Underlay Offset으로 대체 가능
- **Second Face / Inner Glow**: 자식 오브젝트 생성 → 화면당 5~10개 권장
- **Font Padding**: Underlay Dilate가 크면 Font Asset Padding을 충분히 설정 (10 이상)
- **TMPCurve + Shadow**: IMeshModifier 충돌 가능, Underlay 효과만 사용 권장
- **TMPMaskFlow**: 매 프레임 위치 갱신 → 화면당 소수 인스턴스 권장. 상위 Canvas와 분리하려면 `Isolate Render Canvas` 유지. `TMPAnimation`과 동일 오브젝트 불가

## 폴더 구조

```
TMPEffects/
├── Script/          # 컴포넌트 및 유틸리티
├── Editor/          # 커스텀 인스펙터
├── Tests/Editor/    # Editor 테스트 (TMPMaskFlow 등)
├── Presets/         # 프리셋 저장 (Animation / Glow / Outline)
└── README.md
```

## 요구사항

- Unity 6 (6000.0.x) 이상
- TextMeshPro (Unity 6의 `com.unity.ugui`에 포함)
- URP 17.2.0 이상

## 라이선스

MIT License
