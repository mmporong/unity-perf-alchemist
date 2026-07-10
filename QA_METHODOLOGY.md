# QA 방법론 (QA Methodology)

Unity Performance Alchemist v0.2.0의 성능 측정·판정·수용 절차를 기술한다.
외부 수치·주장은 모두 출처 URL을 병기하며, 공개 출처가 없는 것은 "공개 출처 없음"으로,
출처 조합에서의 추론은 [추론]으로 표시한다. 이 프로젝트 자체의 수치는
Artifacts JSON(`Artifacts/research_run_*.json`)에서 나온 것만 유효하다 — 문서에 측정치를
직접 적지 않는다.

---

## 1. 성능 예산 (Performance Budgets)

### 1.1 업계 선례: 예산은 "도출"되는 것

성능 예산은 임의의 목표치가 아니라 감사 가능한 산식에서 나와야 한다.

- **Riot VALORANT 서버**: 128-tick 요구에서 프레임당 7.8125ms가 나오고, 코어당 3게임
  동시 실행으로 나누면 2.6ms, OS/스케줄링용 10%를 예약하면 최종 **2.34ms/frame**이 된다.
  이 예산을 서브시스템(복제, FoW, 네트워크, 애니메이션, 이동 등)별로 커스텀 매크로
  (`ValSubsystemTelemetry`)로 태깅해 내부 분석 플랫폼에서 시각화하고, 테크 리드가
  서브시스템별 예산을 협상했다.
  출처: https://www.riotgames.com/en/news/valorants-128-tick-servers
- **Arm GPU 사이클 예산법**: 대상 GPU의 초당 사이클 수를 목표 FPS로 나눠 프레임당
  사이클 예산을 정의하고, Frame Advisor가 예산 위반(budget violation)을 플래그한다.
  출처: https://developer.arm.com/community/arm-community-blogs/b/mobile-graphics-and-gaming-blog/posts/gpu-processing-budget-approach-to-game-development
- Supercell/King/NCSoft/Krafton 등의 서브시스템별 프레임 예산 수치는 **공개 출처 없음**.
  "16.6ms/33ms에 열 헤드룸 35% 예약" 류의 수치는 서드파티 경험칙이며 벤더 검증 수치가
  아니므로 이 문서는 채용하지 않는다.

### 1.2 이 프로젝트의 티어별 프레임 예산

`DeviceTierProfile.Defaults`(`Core/Experiments/DeviceTierProfile.cs`)에 티어별 예산을
데이터로 선언한다: low = 33.33ms(30fps), mid = 16.67ms(60fps), high = 16.67ms(60fps).
산식은 단순히 1000ms ÷ 목표 FPS이며, 목표 FPS 자체는 프로젝트 정책 값이다(VALORANT처럼
하드웨어 계약에서 도출된 것이 아님을 명시한다).

예산 판정은 `ShippingGate.Evaluate(SampleSet finalRuns, double tierBudgetMs)`가 수행한다.
최종 확인 런의 fresh 표본 중앙값이 티어 예산 이하이면 `BudgetSatisfied = true`로
**라벨링**한다 — 게이트는 채택 여부를 바꾸지 않고 출하 라벨만 붙인다(채택 판정과
분리하는 이유는 §2.5 참조).

## 2. 통계적 채택 기준 (Adoption Criteria)

### 2.1 왜 비모수 검정인가

프레임타임 분포는 GC 스파이크·열 스로틀로 인한 우측 팻테일(right-skewed heavy tail)이
흔하다. Unity 공식 매뉴얼도 GC를 "예측 불가능하고 스터터링을 유발"하는 요인으로 기술한다
(출처: https://docs.unity3d.com/Manual/performance-memory-overview.html). 평균과 t-검정은
이런 비정규·비대칭 분포에서 이상치에 취약하므로, 이 프로젝트는 **Mann-Whitney U(MWU)
비모수 검정 + 중앙값**을 기본으로 한다. 업계 관행도 중앙값에 수렴한다: Unity 성능 테스트의
기본 집계는 Median, Riot은 히스토그램, Chrome은 중앙값 대시보드를 쓴다(§4 출처 참조).

주의: MWU를 "중앙값 차이 검정"으로 해석하는 것은 두 분포의 모양이 같을 때만 엄밀히
유효하다 — 프레임타임은 모양 가정이 깨질 수 있다. [추론 — 일반 통계 문헌 기반, 업계
출처 아님] 이 프로젝트는 MWU를 "한쪽이 확률적으로 우세한가(stochastic dominance)"의
검정으로 사용하고, 크기 판단은 Cliff's delta로 분리한다.

### 2.2 관측 단위: 독립 런당 중앙값 1개

MWU에 넣는 관측 단위는 프레임이 아니라 **독립 런(run)당 중앙값 1개**다
(`SampleSet.RunMediansMs`). 한 런 안의 연속 프레임들은 강한 자기상관을 가지므로 프레임을
표본으로 쓰면 유효 표본 수가 부풀려져 p값이 심하게 낙관적이 된다. 런 단위로 플레이모드를
분리 진입·종료하면 런 간 독립성이 근사적으로 확보된다. 각 런은 워밍업 프레임
(기본 30프레임 또는 1초 중 긴 쪽)을 실제로 폐기한 뒤 중앙값을 계산한다
(`SampleSet.ComputeRunMedian`).

### 2.3 소표본 폴백

두 군 모두 n ≥ 8이면 정규 근사(연속성 보정 + 동점 보정 분산)를 쓰고, 어느 한 군이라도
n < 8이면 **정확 분포로 폴백**한다: 동점이 없으면 정확 U 분포(카운팅 DP), 동점이 있으면
조합 전수 열거 순열 검정. 소표본에서 정규 근사는 p값이 부정확하기 때문이다
(`Core/Statistics/MannWhitneyU.cs`).

### 2.4 효과 크기 게이트: Cliff's delta |d| ≥ 0.147

p < 0.05만으로는 "통계적으로 유의하지만 실무적으로 무의미한" 미세 차이가 채택될 수 있다.
Cliff's delta의 |d| ≥ 0.147(Romano 등의 관례상 'small' 효과 하한)을 함께 요구해
효과 크기가 없는 채택을 차단한다. 이 임계값 0.147은 관례적 구간 경계이지 이 도구에서
실증 도출한 값이 아니다.

`AdoptionGate.Evaluate`의 Adopt 조건: p < 0.05 **AND** |d| ≥ 0.147 **AND** 개선 방향
(후보 중앙값 < 현행 중앙값). 유의하지만 방향이 악화면 Rollback.

### 2.5 이중 게이트: AdoptionGate와 ShippingGate의 분리

- **AdoptionGate**(상대 비교): 후보가 현행 최고보다 통계적으로 나은가.
- **ShippingGate**(절대 비교): 최종 config가 티어 프레임 예산을 만족하는가.

분리 이유: 베이스라인이 이미 예산을 초과한 상태에서도 루프는 상대적으로 더 나은 설정을
향해 등반할 수 있어야 한다. 채택 조건에 예산을 섞으면 "예산 위에서 출발한 프로젝트"는
한 걸음도 못 움직인다. 대신 최종 산출물에 `budgetSatisfied` 라벨을 정직하게 붙여
"개선됐지만 아직 예산 미달"을 구분 가능하게 한다.

### 2.6 Inconclusive 처리

어느 군이든 표본 수가 5 미만이면 판정하지 않고 `Inconclusive`를 반환한다. Inconclusive는
채택도 롤백도 아니며, 루프는 현행 설정을 유지한 채 다음 세대로 진행한다. 표본 부족을
"차이 없음"으로 오독하지 않기 위한 명시적 제3 상태다.

## 3. 루프 무결성 (Loop Integrity)

### 3.1 알려진 한계: 다-비교 누적 false-Adopt

best-vs-candidate 순차 루프는 구조적으로 다중 비교다. 스텝당 α = 0.05를 지켜도 세대가
길어지면 노이즈에 의한 거짓 채택이 누적되어 챔피언 중앙값이 실제 개선 없이 래칫처럼
내려갈 수 있다(회귀-평균 세탁). per-comparison A/A 검증(§6)은 개별 비교의 오탐률만
확인할 뿐 이 누적 드리프트를 잡지 못한다. 이 한계는 코드에도 `ponytail:` 주석으로
명기되어 있다(`Core/Statistics/ChampionConfirmation.cs`).

### 3.2 완화책: 챔피언 최종 확인 런 (fresh-vs-fresh ABAB)

루프 종료 시 챔피언을 그대로 믿지 않고, **베이스라인과 챔피언 설정을 루프 종료 시점에
새로(fresh) 재측정**해 단일 비교로 재검정한다(`ChampionConfirmation.Confirm`).

- **식은(t=0 저장) baseline 재사용 금지**: 루프 시작 시점의 저장 표본과 비교하면 세션
  경과에 따른 열 상태·에디터 상태 드리프트가 "개선"으로 둔갑한다. 확인 런의 두 인자는
  모두 `IsFresh = true`, `IsInterleaved = true`여야 하며, API에 t=0 표본을 받는
  오버로드 자체가 없다.
- **ABAB 인터리브**: baseline·champion 런을 교차 배치해(`MeasureInterleavedAsync`)
  시간 순서 효과(열 누적, 캐시 워밍)가 한쪽에만 유리하게 작용하는 것을 상쇄한다.

### 3.3 잔여 한도의 정직한 기재

확인 런 자체도 단일 비교 α = 0.05 검정이다. 즉 누적 드리프트를 **"단일 비교 false-pass
한도(≤ α)"로 축소**하는 것이지 0으로 만드는 것이 아니다. 다만 ShippingGate가 확인 런의
fresh SampleSet을 읽으므로, 루프가 과대 주장하더라도 최종 보고 수치는 fresh 중앙값으로
정정된다.

### 3.4 confirmationFailed 처리

확인 런에서 채택 조건이 재현되지 않으면 `confirmationFailed = true`이고 신규 config를
도출하지 않는다 — **직전 유효 config를 유지**하며, 콜드 스타트(직전 config 없음)라면
아무것도 출하하지 않는다. confirmationFailed는 루프 신뢰 철회 신호로 취급한다: 해당
세션 결과는 사용 금지, 복원/측정 로직을 수사 대상에 올린다.

루프 중 예산 내였던 챔피언이 확인 런에서 `budgetSatisfied: false`로 뒤집히는 것은
버그가 아니라 **지속 부하에서의 열 스로틀 현실**이다 — 짧은 루프 측정은 열 여유가 있던
시점의 성능이고, 확인 런은 세션 후반의 열 상태를 반영한다. Artifacts JSON의
confirmation.note에 이 사실을 고정 기재한다.

### 3.5 업그레이드 경로 (기록만, 필요 입증 시 구현)

1. 채택 시마다 챔피언 fresh 재측정(후보가 갱신 표본도 이겨야 영구 채택).
2. 그 다음 단계로 α-spending(누적 오류율의 형식적 통제).

현재 미구현이며, 측정된 필요가 입증되기 전에는 추가하지 않는다.

또 하나의 알려진 범위 제한: 게이트 판정은 **p50(런 중앙값) 단일 지표**다. p95/p99 꼬리
악화는 게이트하지 않고 기록만 한다(확인 런 리포트에 p95/p99 델타 의무 표기). p50 개선 +
p99 악화 후보가 통과할 수 있다 — 스터터가 중요해지면 p95 회귀 가드 추가가 업그레이드
경로다.

## 4. 회귀 게이트 (CI Regression Gates)

### 4.1 업계 기준선: Unity Performance Testing Extension

- `Measure.Frames()` 기본값(v3.0 문서): WarmupCount 미지정 시 80ms 또는 3프레임 이상 중
  긴 쪽, MeasurementCount 미지정 시 약 500ms 캡처·최소 7프레임. Edit Mode 테스트 미지원.
  출처: https://docs.unity3d.com/Packages/com.unity.test-framework.performance@3.0/manual/measure-frames.html
- `DynamicMeasurementCount`: 표본 상대오차가 안정될 때까지 측정을 연장 — 기본
  **상대오차 2% @ 99% 신뢰수준**. 엔진 벤더가 공표한 "허용 노이즈" 수치다.
- **버전 의존 주의**: 구버전 `SampleGroupDefinition`은 회귀 게이팅 필드를 내장했고 기본
  임계값이 **베이스라인 대비 15%**였다. v3.0의 `SampleGroup`은 이 필드가 없고 회귀 판정이
  별도 비교 도구로 이동했다. 이 API를 쓰는 도구는 manifest.json에서 패키지 버전을 감지해
  분기해야 한다.
- **PerformanceBenchmarkReporter 패턴**: Unity 공식 비교 도구는 테스트 프레임워크가 방출한
  원시 결과 XML/JSON을 베이스라인과 비교해 리포트를 생성한다 — 즉 **측정 코드와 회귀
  판정을 분리**한다(내부 통계 검정 방식은 미공개).
  출처: https://github.com/Unity-Technologies/PerformanceBenchmarkReporter
- 대조 사례로 Chrome은 고정 통계 임계값을 공표하지 않는다 — 중앙값 대시보드 + 자동
  bisect + 인간 판단.
  출처: https://chromium.googlesource.com/chromium/src/+/refs/heads/main/docs/speed/addressing_performance_regressions.md

이 프로젝트도 같은 분리를 따른다: 측정(`EditorBenchmarkRunner`)은 원시 런 중앙값만
방출하고, 판정(`AdoptionGate`/`ShippingGate`/`ChampionConfirmation`)은 순수 C# Core에서
별도로 수행한다. 통계 검정을 교체해도 측정 코드는 불변이다.

### 4.2 이 리포의 CI가 게이트하는 것 / 못 하는 것

CI(`.github/workflows/core-ci.yml`)는 `dotnet test ci/UPA.Tests.Core.csproj` +
`python tools/validate.py`를 실행한다.

**게이트하는 것**: Core(순수 C#) 통계·게이트·실험·루프 전이 로직의 단위 테스트
(수계산 참조값, A/A 합성 널, 진리표), .meta 존재·GUID 중복·asmdef 그래프·문서 수치 규칙
등 정적 무결성.

**게이트하지 못하는 것**: Unity 전체 컴파일, 플레이모드 실측, 에디터 통합. CI 환경에는
Unity가 없다. 이것들은 오너 Unity 머신에서만 검증 가능하며 §6의 Tier 3에 위임한다.
즉 **CI green ≠ 도구가 동작함**이고, 이 간극을 문서로 은폐하지 않는다.

## 5. 디바이스 티어링 (Device Tiering)

### 5.1 업계 선례

- **Riot PassMark 코호트**: LoL 클라이언트는 매치 종료 시 프레임타임과 CPU 모델·PassMark
  점수를 로깅하고, PassMark 1,000점 밴드로 플레이어를 버킷팅해 "하드웨어 분포가 변한 것"과
  "코드가 느려진 것"을 분리한 뒤 패치 간 히스토그램을 비교한다. 확인된 2ms CPU-stall
  수정이 약 1.93ms의 측정 개선으로 검증된 사례가 공개되어 있다.
  출처: https://www.riotgames.com/en/news/down-rabbit-hole-performance-monitoring
- **Android Media Performance Class(MPC)**: Android 12에서 도입된 CDD 정의·CTS 검증
  티어. 레벨은 30/31/33/34/35가 존재한다 — **32는 없다**. 기기는 OS 업그레이드 후에도
  원래 MPC를 유지한다. Jetpack `core-performance`로 조회한다.
- **Play vitals의 치명적 제약**: slow rendering / frozen frames 지표는 View 기반 UI
  전용이며, 공식 문서가 "Vulkan, Unity, Unreal, OpenGL 앱은 렌더타임 통계가 없다"고
  명시한다. **Unity 앱의 성능 QA는 Play vitals 렌더 지표에 의존할 수 없다.**
  출처: https://developer.android.com/topic/performance/vitals/render
  → 대안 경로: Android Performance Tuner(Unity 플러그인 — 기기 모델 × 씬 × 품질 티어별
  실기기 프레임타임 히스토그램 수집) 또는 커스텀 텔레메트리.
- 실기기 벤치마킹 재현성: Android 11+의 Fixed Performance Mode
  (`adb shell cmd power set-fixed-performance-mode-enabled true`)로 클럭 부스트 분산을
  제거할 수 있다. 단 과열 방지가 아니므로 실행 간 열 베이스라인 복귀·다중 반복이 여전히
  필요하다. 출처: https://developer.android.com/games/optimize/adpf/fixed-performance-mode

### 5.2 이 프로젝트의 티어링과 그 한계

- 런타임 `DeviceTierDetector.DetectTierId()`는 SystemInfo(메모리·GPU 메모리·코어 수)
  휴리스틱으로 low/mid/high를 근사한다. 정확한 매핑은 기기 테이블/MPC 조회가 업그레이드
  경로다(코드에 `ponytail:` 주석).
- 에디터의 `DeviceTierSimulator`는 QualitySettings 프록시로 티어를 **시뮬레이션**한다.
  이는 실기기가 아니다: 데스크톱 GPU/CPU에서의 상대 비교이며, 모바일 SoC의 타일 기반
  렌더링 특성·대역폭 병목·열 스로틀을 재현하지 못한다. 그래서 모든 산출 config에
  `measurementSource: "editor-proxy" | "on-device"`를 강제 기재하고, editor-proxy 산출은
  실기기 검증(§6 Tier 3) 전까지 잠정치로 취급한다.

## 6. 3단 수용 사다리 + 오너 Unity 체크리스트

이 리포는 개발 환경에 Unity/dotnet이 없는 CLI 세션에서 작성될 수 있으므로, 수용 기준을
환경 능력에 따라 3단으로 나눈다. 상위 단계가 통과되기 전까지 하위 단계의 green은
완료 주장이 될 수 없다.

**Tier 1 — 정적 (이 CLI)**: 컴파일 불가 환경. `tools/validate.py`(meta·GUID·asmdef
그래프·문서 수치 규칙·Core의 UnityEngine/Newtonsoft grep 0) + 코드 자체 재독 검토만
가능하다. 문법 오류·API 오용을 원천 배제하지 못한다는 것을 명시한다.

**Tier 2 — GitHub Actions (dotnet + NUnit)**: Core 순수 C#을 netstandard2.1/net8.0으로
컴파일하고 단위 테스트를 실행한다. 통계 참조값, 게이트 진리표, **A/A 합성 널 테스트
(시드 고정 동일 분포 합성쌍 배치에서 false-Adopt율 ≤ α)** 포함. Unity 의존 코드는
컴파일조차 되지 않는다.

**Tier 3 — 오너 Unity (최종 수용)**:
1. Unity 전체 컴파일 0 에러.
2. 플레이모드 스모크(루프 1회 완주 → Artifacts JSON 생성).
3. **P4a 런타임 기준 5종**:
   - N개 독립 런이 실제로 분리 실행되는가(플레이모드 진입/종료 N회).
   - 워밍업 프레임이 실제로 폐기되는가.
   - **실측 A/A**(동일 설정 vs 동일 설정)에서 무채택인가.
   - ABAB 인터리브가 순서 무관하게 재현되는가.
   - `DeviceTierSimulator.VerifyRestored`가 QualitySettings **전 필드** 원복을 확인하는가.
4. fenced(파일 재작성) 모드: 도메인 리로드 후 상태 재개가 해시 가드 하에 동작하는가.
5. **on-device 측정 ≥ 1회** 후에야 해당 티어 config를 validated로 승격.

이 문서와 README의 어떤 성능 주장도 Tier 3 통과 전에는 "editor-proxy 잠정"을 벗을 수 없다.

## 7. 안전성·범위·도구 선택

(구 DEFENSE_STRATEGY.md의 사실 관계를 방법론 서술로 재구성한 절이다.)

### 7.1 안전성 장치

- **컴파일 롤백**: fenced(파일 재작성) 모드에서 LLM 산출 코드가 컴파일 에러를 내면 즉시
  원본으로 롤백한다. 이는 1차 방어일 뿐이며, 컴파일되지만 논리적으로 틀린 코드는 잡지
  못한다 — 완전한 방어는 프로젝트의 단위/플레이모드 테스트 통과를 채택 조건에 연동하는
  것이고, 현재 범위 밖이다.
- **벤치마크 워치독**: 런당 타임아웃(기본 30s) 초과 시 플레이모드를 강제 종료하고 해당
  런을 폐기 기록한다. 무한 루프·행 코드가 에디터 세션을 잠식하는 것을 차단한다.
- **설정-레벨 루프(기본 모드)는 파일을 쓰지 않는다**: QualitySettings 토글만 하므로
  코드 파손 리스크 자체가 없다. 파일 재작성은 명시적 opt-in인 fenced 모드로 격리되며,
  대상 스크립트 경로 + SHA256 해시 가드로 무관한 도메인 리로드에서의 오재개를 방지한다.

### 7.2 로컬 모델 선택 이유

기본 LLM 프로바이더가 로컬(Ollama + 소형 모델)인 이유는 두 가지다.
① **보안**: 상용 프로젝트 소스/설정이 외부 API로 나가지 않는다.
② **비용**: 최적화 루프는 세대당 여러 번 LLM을 호출하며, 로컬 추론은 반복 비용이 0이다.
클라우드 모델(Gemini)은 `ILlmProvider` 뒤의 대체 구현으로 제공한다. 소형 로컬 모델의
가설 품질이 대형 모델보다 낮을 수 있다는 트레이드오프는 인정한다 — 가설이 나빠도
채택 게이트(§2)가 통계적으로 걸러내므로 최악의 경우는 "개선 없음"이지 "개악 채택"이
아니라는 것이 설계 전제다(단, §3.1의 누적 false-Adopt 한도 내에서).

### 7.3 범위 한계: Leaf 컴포넌트

fenced 재작성 모드는 의존성 끝단(leaf) 컴포넌트 최적화에 한정된다. 스크립트 간 의존성이
복잡한 대규모 프로젝트에서 단일 파일 재작성은 인터페이스 파손 위험이 있고, 이를 다루려면
프로젝트 의존성 그래프를 LLM 컨텍스트로 제공하는 기능이 필요하다 — 현재 미구현이며
업그레이드 경로로만 기록한다. `ScriptScorer.IsExcludedPath`가 Editor/Tests/플러그인/
서드파티 경로를 재작성 대상에서 제외하는 것도 같은 이유다.

---

## 부록: 출처 목록

| 주제 | 출처 |
|---|---|
| VALORANT 서버 프레임 예산 도출 | https://www.riotgames.com/en/news/valorants-128-tick-servers |
| LoL PassMark 코호트 회귀 탐지 | https://www.riotgames.com/en/news/down-rabbit-hole-performance-monitoring |
| Arm GPU 사이클 예산법 | https://developer.arm.com/community/arm-community-blogs/b/mobile-graphics-and-gaming-blog/posts/gpu-processing-budget-approach-to-game-development |
| Unity 메모리/GC 공식 매뉴얼 | https://docs.unity3d.com/Manual/performance-memory-overview.html |
| Unity Performance Testing `Measure.Frames` | https://docs.unity3d.com/Packages/com.unity.test-framework.performance@3.0/manual/measure-frames.html |
| Unity PerformanceBenchmarkReporter | https://github.com/Unity-Technologies/PerformanceBenchmarkReporter |
| Chrome 성능 회귀 대응 문서 | https://chromium.googlesource.com/chromium/src/+/refs/heads/main/docs/speed/addressing_performance_regressions.md |
| Play vitals 렌더 지표 제약 | https://developer.android.com/topic/performance/vitals/render |
| Android Fixed Performance Mode | https://developer.android.com/games/optimize/adpf/fixed-performance-mode |
| ADPF 열 헤드룸 | https://developer.android.com/games/optimize/adpf/thermal |
| Play vitals ANR/크래시 하드 임계 | https://developer.android.com/topic/performance/vitals |

리서치 원본: `.omc/research/enterprise-qa-methodology.md` (신뢰도 마커 포함).
