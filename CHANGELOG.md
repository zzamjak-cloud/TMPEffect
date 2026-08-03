# Change Log:

## 1.0.0

- 최초 릴리스
- **컴포넌트**: TMPOutlineEffect / TMPOutGlow / TMPAnimation / TMPCurve / TMPLayoutLimiter / TMPMaskFlow
- **자체 Easing 시스템 내장**: 외부 Tweening 패키지 의존성 없이 `ICurve` / `EaseType` / `Easing` 기반 이징 제공
  - Ease 프리셋 30종 (Robert Penner 표준) + AnimationCurve 커스텀 곡선 지원
  - `[SerializeReference, SubclassSelector]` 기반 커브 타입 선택 UI 내장
- **프리셋 시스템**: ScriptableObject 기반 저장/적용 (Animation / Glow / Outline)