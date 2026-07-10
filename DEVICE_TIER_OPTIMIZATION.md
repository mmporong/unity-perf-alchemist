# DEVICE_TIER_OPTIMIZATION — 기기 티어별 최적 세팅 도출 방법론

> Unity Performance Alchemist v0.2 설계 문서.
> 주제: **"모바일 기기별로 주요 최적화 방법을 하나씩 켜고 끄면서, 유의미한 개선이 확인됐을 때 각 버전(패치)마다 최적화된 세팅 값을 도출하는 방법"** — 일반 방법론 분석 + 이 리포의 구현 설계.
> 근거 리서치: `.omc/research/device-tier-toggle-methodology.md` (2026-07-10 수집, 출처 URL 포함). 통계 판정 상세는 QA_METHODOLOGY.md 상호참조.
> 외부 수치·주장에는 출처 URL을 달았고, 출처가 없는 판단은 **(추론)** 으로 표시했다.

---

## 1. 문제 정의: 왜 수동 튜닝이 실패하는가

세 개의 축이 곱해진다.

1. **기기 스펙트럼**: 저가 보급형(30fps도 버거움)부터 플래그십(120Hz 여유)까지. 같은 세팅이 한쪽에선 과잉 품질, 다른 쪽에선 스로틀링 유발.
2. **토글 조합 공간**: 최적화 기법이 k개면 on/off 조합만 2^k. 이 리포의 TechniqueRegistry 기준 9종이면 512 조합. 기기 티어 3종을 곱하면 1,536 셀.
3. **패치마다 변하는 코드**: 새 셰이더, 새 씬, 새 파티클이 들어오면 지난 패치에서 도출한 최적 세팅의 전제가 무너진다. "한 번 튜닝하고 끝"이 아니라 **패치마다 재도출**해야 하는 반복 작업.

사람이 손으로 하면: (a) 조합 공간을 다 못 돈다 → 감으로 몇 개만 찍는다, (b) 열 스로틀링·측정 노이즈 속에서 2ms 개선이 진짜인지 못 가린다, (c) 패치마다 같은 노동을 반복할 인력이 없다. 그래서 이 문제는 **실험 설계(2장) + 통계 판정(3장) + 티어링(4장) + 패치별 파이프라인(5장)** 으로 분해해 자동화해야 한다.

---

## 2. 실험 설계 분석 (핵심 장): 무엇을 언제 쓰나

"하나씩 켜고 끄는" 방식에도 급이 있다. 4가지 설계를 비교한다.

### 2.1 OFAT (One-Factor-At-a-Time) — 오너 요구의 문자 그대로

baseline 1회 + 토글 k개를 각각 단독으로 뒤집어 k회 = **1+k 런**. 직관적이고 각 런의 해석이 명확하다("그림자 끄니 3ms 줌").

- **한계**: (1) **상호작용 효과를 못 본다** — 두 토글이 함께 켜졌을 때의 시너지/간섭이 안 보임. (2) 동등한 추정 정밀도를 얻는 데 factorial 대비 총 런 수가 더 든다. (3) 순차 적용 시 국소 최적에 갇힐 수 있다. 출처: https://en.wikipedia.org/wiki/One-factor-at-a-time_method
- **상호작용 예시**: **해상도 스케일 × 포스트프로세싱**. 포스트프로세싱은 화면 해상도에 비례하는 픽셀 비용이므로, 해상도를 0.75로 내리면 포스트프로세싱 끄기의 절감 폭 자체가 줄어든다. OFAT은 "포스트 끄면 4ms 절감"(풀해상도 기준)이라 보고하지만 최종 config(0.75 해상도)에서는 2ms일 수 있다. (추론 — 픽셀 비용 비례 관계에서 유도)
- **언제 쓰나**: 토글이 적고(≤5), 빠른 1차 감 잡기, 또는 상호작용이 물리적으로 없다고 확신할 때.

### 2.2 Full Factorial — 상호작용까지 전부

2^k 런으로 주효과 + 모든 상호작용을 추정. k=3이면 8런으로 완전, k=7이면 128런 — 기기당 1런에 수 분(측정 N회 반복 포함)이면 비현실적으로 폭발한다. 출처: https://www.jmp.com/en/statistics-knowledge-portal/design-of-experiments/screening-designs/plackett-burman-designs (factorial 대비 스크리닝 맥락)

- **언제 쓰나**: 팩터가 소수(k ≤ 4~5)로 좁혀진 뒤 상호작용을 확정하고 싶을 때.

### 2.3 Plackett-Burman 스크리닝 — 많은 토글을 싸게 거르기

N회 실행으로 최대 **N−1개 팩터의 주효과**를 스크리닝(예: 12런으로 11팩터). 대가: 2팩터 상호작용이 주효과와 교락(confound)되어 상호작용은 못 본다. 출처: https://www.jmp.com/en/statistics-knowledge-portal/design-of-experiments/screening-designs/plackett-burman-designs

- **언제 쓰나**: 토글 후보가 많을 때(8개 이상) 1차 필터로. "효과 없는 다수"를 빨리 버리고 "critical few"만 남긴다.

### 2.4 Bayesian Optimization — 연속 파라미터 정밀 탐색

on/off가 아닌 연속값(해상도 스케일 0.5~1.0, 그림자 거리 10~60 등)의 최적점을 순차적으로 찾는다. 고비용 평가 환경(GPU 커널 오토튜닝, AlphaGo 하이퍼파라미터)에서 검증된 접근. 출처: https://arxiv.org/abs/2111.14991. 단, **모바일 그래픽 세팅에 BO를 직접 적용한 발표 사례는 리서치에서 미발견 — 인접 분야 선례로부터의 전이 (추론)**.

- **언제 쓰나**: 토글 스크리닝이 끝난 뒤, 살아남은 연속 파라미터의 정확한 값을 그리드 없이 찾을 때.

### 2.5 권장 워크플로우: 2단계 (PB 스크리닝 → factorial)

```
[토글 후보 전체 (Adaptive Performance 스케일러 카탈로그 기반, 9~15개)]
        │  1단계: Plackett-Burman 스크리닝 (12~16런)
        ▼
[critical few (효과 큰 3~5개)]
        │  2단계: full factorial (8~32런) — 상호작용 해소
        ▼
[티어별 최적 토글 조합]  ──(선택)──▶ 연속 파라미터는 BO로 정밀화
```

이 2단계가 표준 DOE 관행이다: 스크리닝 설계로 팩터를 줄인 뒤 남은 것만 factorial/response-surface로 상호작용과 최적점을 확정. 출처: https://www.jmp.com/en/statistics-knowledge-portal/design-of-experiments/screening-designs/plackett-burman-designs

**이 리포 v0.2의 위치**: ExperimentPlan은 OFAT과 Full Factorial(k≤8 가드)만 구현한다. PB 설계 행렬과 BO는 계약 외 — 토글 수가 TechniqueRegistry 9종 수준이라 "OFAT으로 감 잡기 → 상위 3~4개만 factorial"이 수동 2단계로 충분히 돌아가기 때문 (추론/ponytail 판단). PB·BO는 토글 카탈로그가 커질 때의 업그레이드 경로.

---

## 3. "유의미한 개선"의 판정

### 3.1 평균 금지, 퍼센타일 사용

잭(jank)은 꼬리 현상이다 — 평균 FPS는 간헐 스터터를 숨긴다. Android Macrobenchmark의 FrameTimingMetric도 p50/p90/p95/p99를 표준 출력으로 쓴다(API 31+에서는 frameOverrunMs 추가). 출처: https://developer.android.com/topic/performance/benchmarking/macrobenchmark-metrics

### 3.2 이 리포의 통계 게이트 (요약 — 상세는 QA_METHODOLOGY.md)

- **관측 단위**: 프레임이 아닌 **런 중앙값** (런당 프레임 시계열 → 워밍업 폐기 → 중앙값 1개). 프레임 간 자기상관으로 인한 유효표본 부풀리기 방지.
- **이중 게이트** (`AdoptionGate`, `Core/Statistics/AdoptionGate.cs`): 채택 = **Mann-Whitney U p < 0.05 (양측)** AND **|Cliff's delta| ≥ 0.147** AND 개선 방향 일치. 통계적 유의성(우연이 아님) + 실질적 효과 크기(체감 가능) 둘 다 요구.
- **확인 런** (`ChampionConfirmation`): 루프 종료 시점에 baseline과 champion을 **fresh ABAB 인터리브**로 재측정해 재검정 — 루프 누적 드리프트를 단일 비교 false-pass 한도(≤α)로 축소. p95/p99 델타 의무 보고.

### 3.3 열 드리프트 통제 — 모바일 측정의 최대 교락 변수

열 스로틀링은 "먼저 측정한 조건이 유리"하게 만드는 계통 오차다. 통제 수단:

| 수단 | 근거 |
|---|---|
| 워밍업 폐기 (기록 전 고정 워밍업 구간 버림) | Play Console Slow Sessions가 **세션 첫 1분을 제외**하는 공식 선례. https://developer.android.com/games/optimize/vitals/slow-session |
| 실행 순서 랜덤화 (Fisher-Yates, 시드 주입) | 첫 실행 = 가장 시원한 상태 교락 방지 (DOE 표준 관행) |
| ABAB 인터리브 (baseline/champion 교차 측정) | 두 조건이 같은 열 곡선을 공유하게 함 |
| 런 간 쿨다운 (옵션) | 시작 온도/클럭 조건 표준화 |

스로틀 온셋 타이밍: 플래그십은 피크 클럭 약 2~5분 유지, 보급형은 60~90초 만에 스로틀 시작 가능 — **근사치, SoC·섀시 의존 (추론)**.

**Fixed Performance Mode**: on-device 측정 시 `adb shell cmd power set-fixed-performance-mode-enabled true`로 클럭을 고정하면 열 변동성 자체를 제거해 재현성을 높일 수 있다(지원 기기 한정). 출처: https://developer.android.com/topic/performance/benchmarking/macrobenchmark-metrics (Macrobenchmark 문서 계열의 벤치마크 안정화 권고)

---

## 4. 기기 티어링: 어느 기기에 어느 세팅을

### 4.1 업계 티어링 신호

- **Android — Media Performance Class (MPC)**: 표준화된 기기 역량 티어. 레벨 **30/31/33/34/35 (32는 없음)**. Jetpack `androidx.core:core-performance` + `PlayServicesDevicePerformance`로 조회. 실시간 열 신호가 아닌 1회성 역량 버킷 → "이 기기가 지속 가능한 비주얼 티어" 결정에 적합. 출처: https://developer.android.com/topic/performance/performance-class
- **Apple**: `ProcessInfo.thermalState`(nominal/fair/serious/critical)가 유일한 1st-party 실시간 열 신호이고 performance class API는 없다 → 기기 식별자/칩 세대 → 티어 커스텀 테이블이 필요. 대형 스튜디오도 이렇게 한다: Fortnite Mobile은 기기 allowlist/config를 수작업 큐레이션. 출처: https://dev.epicgames.com/documentation/fortnite/mobile-optimization-in-fortnite?lang=en-US
- **Unity Adaptive Performance 스케일러 카탈로그**: AdaptiveLOD, AdaptiveResolution, AdaptiveFramerate, AdaptiveViewDistance, AdaptivePhysics, AdaptiveLayerCulling + URP 전용(AdaptiveMSAA, AdaptiveShadowCascade/Distance/Quality 등). 출처: https://docs.unity3d.com/Packages/com.unity.adaptiveperformance@5.0/manual/user-guide.html — **이 카탈로그가 이 리포 TechniqueRegistry(9종 토글)의 원전**이다: 업계가 "런타임에 조절할 가치가 있다"고 검증한 노브 목록을 실험 팩터 목록으로 재사용.

### 4.2 이 리포의 DeviceTierProfile

`Core/Experiments/DeviceTierProfile.cs` — low/mid/high 3종 기본 제공. 아래 예산은 **측정치가 아닌 설계 상수**(api-contract.md 정의, fps 목표의 역수):

| TierId | 프레임 예산 (설계 상수, 미측정) | 의미 |
|---|---|---|
| low | 33.33ms (30fps 목표, 설계 상수) | 보급형 — 예산 자체를 완화 |
| mid | 16.67ms (60fps 목표, 설계 상수) | 중급형 |
| high | 16.67ms (60fps 목표, 설계 상수) | 플래그십 — 같은 예산을 더 높은 QualityProxy에서 달성 |

각 티어는 `QualityProxy`(티어 근사용 기본 세팅 dict)를 갖고, `ShippingGate`가 최종 중앙값을 `FrameBudgetMs`와 비교해 `budgetSatisfied` 라벨을 붙인다. 티어 확장(예: "ultra-low")은 리스트 항목 추가로 가능.

---

## 5. 버전/패치별 세팅 도출 파이프라인 — "각 버전의 패치에서" 차원

패치마다 최적 세팅의 전제가 바뀌므로, 도출 결과를 **어떤 빌드에서 도출했는지 태깅**하고 패치마다 재도출·비교·배포·라이브 검증하는 파이프라인이 필요하다.

### 5.1 전체 흐름

```
[패치 vN 빌드]
     │
     ▼
① 랩 실험 (티어별): ToggleExperimentRunner(DOE) / SettingsResearchRunner(LLM 루프)
     │   → 결과에 derivedAgainst { gameBuildVersion, upaSchemaVersion, patchId, derivedAtUtc } 태깅
     ▼
② ConfigDerivation.Derive → 티어별 DerivedConfig (settings + medianMs + budgetSatisfied)
     │
     ▼
③ ConfigDerivation.Diff(prev vN-1, next vN) → "패치 간 무엇이 왜 바뀌었나" 리포트
     │   (ReportExporter가 HTML diff 섹션으로 출력)
     ▼
④ 배포: Remote Config (Unity Game Overrides / Firebase 조건)
     │   단계 램프: 2% → 10% → 30% → 100%
     ▼
⑤ 라이브 검증 (각 램프 단계마다):
     ├─ Firebase A/B: 프레임타임 커스텀 지표를 goal로, unequal-variance t-test
     ├─ APT 텔레메트리: quality level × 기기 모델별 프레임타임 분해
     └─ Slow Sessions 회귀 게이트: 세션 프레임 >25% slow → 홀드/롤백
     ▼
[패치 vN 티어별 확정 세팅] ──(다음 패치에서 ①로 회귀)
```

### 5.2 단계별 근거

- **① 태깅**: `DerivedAgainst`(`Core/Experiments/DerivedAgainst.cs`)의 4필드 — gameBuildVersion(게임 빌드), upaSchemaVersion(도구 스키마), patchId(패치 식별자), derivedAtUtc(ISO-8601, Editor 경계 주입). 이 태그가 있어야 "이 세팅은 어느 코드 기준의 최적인가"에 답할 수 있고, 패치가 바뀌면 stale 여부를 기계적으로 판정한다.
- **③ Diff**: `ConfigDerivation.Diff(prev, next)` — 키별 변경(PrevValue→NextValue) + 중앙값·예산 충족 변화. 패치 리뷰에서 "왜 그림자가 다시 켜졌지?"를 추적하는 근거.
- **④ Remote Config 배포**: Unity Remote Config Game Overrides(기기/앱/유저 속성 규칙) 또는 Firebase Remote Config staged rollout(랜덤 % 조건 + 자동 컨트롤 그룹, 앱 버전 스코프). 출처: https://firebase.google.com/docs/remote-config/rollouts
- **⑤ Firebase A/B**: 연속 지표는 unequal-variance t-test, p<0.05, 최대 8 variants, 배정은 experiment ID+install ID 해시. Firebase Performance Monitoring 커스텀 지표(프레임타임)를 A/B goal로 직접 연결 가능. 출처: https://firebase.google.com/docs/ab-testing/ab-concepts
- **⑤ Slow Sessions**: Play Console Android vitals(게임 전용) — 세션의 >25% 프레임이 slow(이전 present 후 50ms 내 미표시 ≈20fps, 보조 34ms/30fps)면 slow session. 구글은 20fps 미달 게임의 노출 저하 의사를 밝혔다 → 이걸 이진 회귀 게이트로 쓴다. 출처: https://developer.android.com/games/optimize/vitals/slow-session
- **⑤ APT**: Android Performance Tuner — fidelity parameters를 named quality levels로 그룹화하고 quality level × annotation × 기기 모델별 프레임타임 텔레메트리 집계. 우리가 만드는 것과 가장 가까운 공식 유사물. 출처: https://developer.android.com/games/sdk/performance-tuner
- **세그먼트별 표본 수**: MDE ∝ 1/√n (표준 검정력 분석 — 검출 가능 효과를 절반으로 줄이려면 표본 4배). 롱테일 기기 모델은 라이브에서 유의성에 도달 못 함 → **해당 티어의 랩 결과를 그대로 채택 근거로 사용** (라이브는 회귀 감시만).

---

## 6. 이 리포 구현 매핑

| 컴포넌트 | 경로 | 역할 |
|---|---|---|
| `TechniqueRegistry` | `Assets/UnityPerformanceAlchemist/Core/Experiments/OptimizationTechnique.cs` | 토글 카탈로그 9종 (resolutionScale, shadows, shadowDistance, msaa, pixelLightCount, textureQuality, lodBias, softParticles, vsync) — Adaptive Performance 스케일러 카탈로그가 원전 |
| `ExperimentPlan` | `Assets/UnityPerformanceAlchemist/Core/Experiments/ExperimentPlan.cs` | OFAT(1+k런) / Full Factorial(2^k, k≤8 가드) 계획 생성 + 시드 주입 Fisher-Yates 순서 랜덤화 |
| `ToggleExperimentRunner` | `Assets/UnityPerformanceAlchemist/Editor/Experiments/ToggleExperimentRunner.cs` | **DOE 모드**: LLM 없이 계획된 런 전부 실행 → ExperimentResult 수집 → ConfigDerivation → Artifacts JSON + 확인 런 |
| `SettingsResearchRunner` | `Assets/UnityPerformanceAlchemist/Editor/Loop/SettingsResearchRunner.cs` | **LLM 가설 모드**: 티어 선택 → baseline → (LLM 가설→적용→측정→AdoptionGate)×N세대 → 확인 런 → ShippingGate → 도출 |
| `DeviceTierSimulator` | `Assets/UnityPerformanceAlchemist/Editor/Experiments/DeviceTierSimulator.cs` | **editor-proxy**: QualitySettings+해상도 스냅샷/적용/복원/복원검증 — 에디터에서 티어 QualityProxy 근사 |
| `DeviceTierProfile` | `Assets/UnityPerformanceAlchemist/Core/Experiments/DeviceTierProfile.cs` | low/mid/high + 프레임 예산 + QualityProxy |
| `DeviceTierConfig` | `Assets/UnityPerformanceAlchemist/Runtime/DeviceTierConfig.cs` | ScriptableObject — **출하 표면**: 티어별 도출 세팅 + derivedAgainst + budgetSatisfied를 빌드에 실어 배포 |
| `DeviceTierDetector` | `Assets/UnityPerformanceAlchemist/Runtime/DeviceTierDetector.cs` | 런타임 티어 감지 — SystemInfo 휴리스틱 (정확한 매핑은 기기 테이블/MPC 조회가 업그레이드 경로) |
| `ConfigDerivation` | `Assets/UnityPerformanceAlchemist/Core/Experiments/ConfigDerivation.cs` | Derive(최저 중앙값 선택) + Diff(패치 간 비교) |
| `ReportExporter` | `Assets/UnityPerformanceAlchemist/Editor/Experiments/ReportExporter.cs` | Artifacts JSON → 자기완결 HTML (게이트 판정, p95/p99 델타, 패치 간 diff) |

### 6.1 editor-proxy vs on-device 경계 (중요)

**v0.2는 editor-proxy only.** 모든 측정은 에디터 플레이모드에서 DeviceTierSimulator로 티어 QualityProxy를 근사한 것이며, Artifacts에 `measurementSource: "editor-proxy"`로 정직하게 기록된다. editor-proxy가 답할 수 있는 것: "이 세팅 조합의 **상대적** 비용 순위와 상호작용 구조". 답할 수 없는 것: 실기기 절대 프레임타임, 열 스로틀 곡선, 드라이버/SoC 특이성.

**on-device 확장 시 권고 (별도 작업)**:
- `measurementSource: "on-device"`로 구분 기록.
- **런당 반복 수 N을 editor-proxy보다 늘리거나 열 통제 비용을 예산에 반영** — 실기기는 워밍업/쿨다운/Fixed Performance Mode 등 열 통제 절차로 런당 소요 시간이 크게 늘어나므로, 계획 단계(2장)의 런 수 절약(PB 스크리닝)이 실기기에서 훨씬 절실해진다.
- on-device 결과로 검증된 config만 `validated` 상태로 승격하고, editor-proxy 단독 도출은 "후보" 지위로 유지 (추론 — 계약의 measurementSource 필드 취지에서 유도).

---

## 부록: 출처 목록

- OFAT 한계: https://en.wikipedia.org/wiki/One-factor-at-a-time_method
- Plackett-Burman / 스크리닝 설계: https://www.jmp.com/en/statistics-knowledge-portal/design-of-experiments/screening-designs/plackett-burman-designs
- Bayesian optimization (GPU 오토튜닝): https://arxiv.org/abs/2111.14991
- Android Performance Class (MPC): https://developer.android.com/topic/performance/performance-class
- Android Performance Tuner: https://developer.android.com/games/sdk/performance-tuner
- Play Console Slow Sessions: https://developer.android.com/games/optimize/vitals/slow-session
- Macrobenchmark FrameTimingMetric (퍼센타일): https://developer.android.com/topic/performance/benchmarking/macrobenchmark-metrics
- Unity Adaptive Performance: https://docs.unity3d.com/Packages/com.unity.adaptiveperformance@5.0/manual/user-guide.html
- Firebase A/B Testing: https://firebase.google.com/docs/ab-testing/ab-concepts
- Firebase Remote Config rollouts: https://firebase.google.com/docs/remote-config/rollouts
- Fortnite Mobile 기기 티어링: https://dev.epicgames.com/documentation/fortnite/mobile-optimization-in-fortnite?lang=en-US
