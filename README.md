# 🧪 Unity Performance Alchemist

> **설정-레벨 AutoResearch: 기기 티어별 최적화 세팅을 통계 게이트로 도출하는 Unity 에디터 툴**

[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](https://opensource.org/licenses/MIT)
[![Local AI Support](https://img.shields.io/badge/AI-Ollama%20Support-blue.svg)](https://ollama.com/)
[![Core CI](https://img.shields.io/badge/CI-dotnet%20%2B%20NUnit-green.svg)](.github/workflows/core-ci.yml)

**Unity Performance Alchemist**는 안드레 카파시(Andrej Karpathy)의 `autoresearch` 개념을 유니티에 이식한 **폐쇄 루프(closed-loop) 자율 최적화 엔진**입니다. v0.2부터 루프의 액션 스페이스는 **품질 세팅/최적화 토글**입니다:

> LLM이 "다음에 뒤집을 세팅"을 가설로 제안 → 플레이모드에서 **N회 독립 측정** → **비모수 통계 게이트**(Mann-Whitney U + Cliff's delta)로 채택/롤백 → 반복 → **챔피언 최종 확인 런**(fresh ABAB 재측정) → 기기 티어별 최적 세팅 도출.

코드 전체를 재작성하는 이전 방식과 달리, 세팅 변경은 **컴파일이 필요 없어** 루프가 한 세션 안에서 안전하게 돌고, 롤백은 값 하나를 되돌리는 것으로 끝납니다.

---

## 📖 프로젝트 소개

### 영감을 받은 것

이 프로젝트는 안드레 카파시(Andrej Karpathy)의 [`autoresearch`](https://github.com/karpathy/autoresearch)에서 영감을 받았습니다. 사람이 코드를 조금씩 수정하고 벤치마크를 반복하는 작업을, **가설을 세우고 측정한 뒤 결과에 따라 다음 행동을 결정하는 실험 루프**로 바꾸고 싶었습니다.

특히 Unity 성능 최적화는 "이 옵션을 끄면 빨라질 것 같다"는 경험에 의존하기 쉽습니다. 하지만 프레임타임은 GC 스파이크, 열 스로틀링, 실행 순서와 같은 요인의 영향을 받으므로 단 한 번의 실행만으로 개선을 단정하기 어렵습니다. 그래서 이 프로젝트는 LLM을 최적화의 최종 판단자가 아니라 **다음 실험의 가설을 제안하는 역할**로 제한하고, 실제 채택 여부는 반복 측정과 통계 게이트가 결정하도록 설계했습니다.

### 프로젝트를 구축하며 배운 것

1. **성능 최적화는 코드보다 측정 설계가 먼저다.**
   프레임을 그대로 표본으로 사용하면 연속 프레임 사이의 자기상관 때문에 표본 수가 부풀려질 수 있습니다. 이 도구는 워밍업 프레임을 버리고 각 독립 런의 중앙값 하나만 사용해 조건을 비교합니다. 후보를 채택하려면 다음 조건을 동시에 만족해야 합니다.

   \[
   p < \alpha = 0.05
   \quad\land\quad
   |d| \ge 0.147
   \quad\land\quad
   \tilde{x}_{\mathrm{candidate}} < \tilde{x}_{\mathrm{current}}
   \]

   여기서 \(p\)는 Mann–Whitney U 검정의 유의확률, \(d\)는 Cliff's delta, \(\tilde{x}\)는 런 중앙값입니다. 통계적 유의성과 실질적인 효과 크기를 함께 요구하면서, 우연한 미세 개선을 최적화 결과로 오인하는 일을 줄였습니다.

2. **자동화에는 독립적인 안전장치가 필요하다.**
   LLM이 제안한 변경을 그대로 신뢰하는 대신, 설정 수준 루프에서는 QualitySettings 토글만 변경하고 매 런 후 원래 값을 복원·검증합니다. 파일 재작성 방식은 실험 모드로 격리하고, 컴파일 오류 롤백·워치독·대상 해시 가드를 둬 기본 경로와 위험도를 분리했습니다.

3. **기기별 최적화는 하나의 정답을 찾는 문제가 아니다.**
   목표 프레임 레이트 \(F\)에 따른 프레임 예산은 다음처럼 계산됩니다.

   \[
   T_{\mathrm{frame}} = \frac{1000\ \mathrm{ms}}{F\ \mathrm{fps}}
   \]

   따라서 \(30\ \mathrm{fps}\)는 약 \(33.33\ \mathrm{ms}\), \(60\ \mathrm{fps}\)는 약 \(16.67\ \mathrm{ms}\)의 예산을 가집니다. 이 프로젝트는 low/mid/high 티어별로 예산과 품질 프록시를 분리하고, 결과에 빌드 버전·패치 ID를 `derivedAgainst`로 남깁니다. 최적값은 절대적인 상수가 아니라 **특정 빌드와 특정 티어에서 도출된 설정**이라는 점을 배웠습니다.

4. **측정하지 않은 수치는 주장하지 않아야 한다.**
   현재 v0.2의 측정 경로는 `editor-proxy`이며, 실기기의 절대 프레임타임이나 SoC별 열 특성을 검증하지 않습니다. 따라서 문서에서는 실제 Artifacts에 기록된 수치만 `Measured`로 다루고, 문헌 기반 예상은 `Projected`, 아직 실행하지 않은 항목은 `미측정`으로 구분합니다. 성능 도구에서 신뢰도는 화려한 개선 수치보다 이런 경계 표시에서 시작된다는 것을 확인했습니다.

### 프로젝트를 구축한 과정

1. **문제 정의**: Unity의 CPU, 메모리/GC, UI 리빌드, GPU 오버드로 병목을 재현할 수 있는 네 가지 샘플 시나리오를 만들고, 최적화 후보를 품질 설정과 토글의 조합으로 모델링했습니다.
2. **안전한 실행 표면으로 전환**: 초기의 파일 재작성 중심 접근을 기본 경로에서 분리하고, 컴파일 없이 적용·복원할 수 있는 설정 수준 `SettingsResearchRunner`를 중심으로 재설계했습니다.
3. **핵심 로직 분리**: 통계, 실험 계획, 게이트, 설정 도출을 UnityEngine에 의존하지 않는 `UPA.Core`로 분리했습니다. `UPA.Runtime`은 출하 설정과 기기 티어 적용을 담당하고, `UPA.Editor`는 벤치마크·LLM·대시보드를 연결합니다.
4. **탐색 루프 구현**: LLM이 가설을 제안하는 모드와 LLM 없이 실행할 수 있는 DOE 모드를 함께 구현했습니다. DOE는 상황에 따라 OFAT의 \(1+k\)개 런 또는 Full Factorial의 \(2^k\)개 조합을 사용합니다.
5. **판정과 재현성 강화**: Mann–Whitney U와 Cliff's delta 기반 `AdoptionGate`, 티어 예산을 확인하는 `ShippingGate`, 종료 시점의 fresh ABAB 챔피언 확인 런을 추가했습니다. 모든 연구 결과는 JSON 아티팩트와 HTML 리포트로 남기고, NUnit·dotnet CI·정적 검증으로 핵심 계약을 고정했습니다.

### 직면했던 어려움과 해결 방법

- **성능 측정값의 노이즈**: 한두 번의 측정으로는 작은 차이를 신뢰하기 어려웠습니다. 독립 런별 중앙값, 실행 순서 랜덤화, 워밍업 폐기, baseline과 champion을 교차 측정하는 fresh ABAB 확인 런을 도입해 열 드리프트와 우연한 변동의 영향을 줄였습니다.
- **LLM 변경의 안전성**: 컴파일되는 코드가 항상 올바른 코드는 아니며, 파일 전체를 다시 쓰는 방식은 도메인 리로드와 롤백이 복잡했습니다. 기본 루프를 설정 변경으로 제한하고, 파일 재작성은 명시적 opt-in 실험 모드로 낮췄습니다. LLM은 제안자이고 통계 게이트가 승인자라는 역할 분리가 핵심이었습니다.
- **Unity와 자동화 테스트의 경계**: 에디터와 런타임 API를 직접 참조하면 핵심 로직을 빠르게 검증하기 어렵습니다. 그래서 `UPA.Core`를 순수 C#으로 유지해 일반 .NET 테스트에서 통계·게이트·실험 계획을 검증하고, Unity 의존 코드는 Editor/Runtime 계층에 모았습니다.
- **기기 차이를 에디터에서 완전히 재현하기 어려움**: 에디터 프록시는 상대적인 비용 순위를 탐색하는 데는 유용하지만 실제 기기의 열·드라이버·SoC 특성까지 대신할 수 없습니다. 이 한계를 숨기지 않고 결과에 `measurementSource: "editor-proxy"`를 기록하며, 실기기 측정은 별도의 후속 검증 단계로 남겼습니다.

이 과정을 통해 Unity Performance Alchemist는 단순히 "더 빠른 코드를 자동으로 만드는 도구"가 아니라, **성능 가설을 반복 가능하게 실험하고 근거와 한계를 함께 기록하는 연구 도구**로 발전했습니다.

---

## 🎯 왜 이 도구인가

| 문제 | 이 도구의 접근 |
| :--- | :--- |
| 단일 실행 벤치마크는 노이즈에 속는다 | 조건당 N회(기본 10) 독립 런, 런당 중앙값만 통계에 투입 (자기상관 배제) |
| 미세한 상승만 보고 채택하는 판정 | MWU 유의성(α=0.05) + Cliff's delta 효과 크기(\|d\|≥0.147) 동시 충족 시에만 채택 |
| 긴 루프는 노이즈로도 등반한다 | 종료 시 챔피언·베이스라인을 **둘 다 fresh로 ABAB 인터리브 재측정**해 최종 확인 |
| 기기마다 최적 세팅이 다르다 | 티어(low/mid/high)별 프레임 예산 선언 → 티어별 config 도출 |
| 패치마다 최적값이 변한다 | 모든 도출 결과에 `derivedAgainst`(빌드 버전·패치 ID) 태깅 → 패치 간 diff 리포트 |

측정·판정·도출의 방법론 전체는 **[QA_METHODOLOGY.md](./QA_METHODOLOGY.md)** 와 **[DEVICE_TIER_OPTIMIZATION.md](./DEVICE_TIER_OPTIMIZATION.md)** 에 문서화되어 있습니다.

---

## 📊 병목 시나리오 카탈로그 (Case Studies)

아래 4개 샘플 씬은 유니티의 4대 병목을 의도적으로 재현하는 **재현 가능한 실험 픽스처**입니다.
각 시나리오의 병목 메커니즘과 최적화 이론은 [RESEARCH_REPORT.md](./RESEARCH_REPORT.md)에 정리되어 있습니다.

> **수치 정직성 원칙**: 이 리포의 모든 성능 수치는 도구가 직접 기록한 `Artifacts/research_run_*.json`에서만 인용됩니다.
> 아직 실측되지 않은 항목은 "미측정"으로 표기하며, 문헌 기반 추정은 "projected"로 구분합니다.
> 측정 실행 방법은 아래 [Getting Started](#%EF%B8%8F-getting-started)를 참고하세요.

| # | 시나리오 | 병목 축 | 측정 상태 |
| :--- | :--- | :--- | :--- |
| 1 | RTS Swarm (1,500 유닛 O(n²) 회피) | CPU | 미측정 — 실행 시 Artifacts/에 자동 기록 |
| 2 | 리듬게임 노트 폭주 (Instantiate/Destroy·클로저) | 메모리/GC | 미측정 — 실행 시 Artifacts/에 자동 기록 |
| 3 | UGUI 500셀 그리드 연쇄 리빌드 | UI/렌더링 | 미측정 — 실행 시 Artifacts/에 자동 기록 |
| 4 | 파티클 50시스템 고유 머티리얼 오버드로 | GPU | 미측정 — 실행 시 Artifacts/에 자동 기록 |

---

## 🛠️ Getting Started

### 1. 테스트 씬 셋업
유니티 상단 메뉴에서 병목 씬을 생성합니다:
- `Window > Alchemist > 1. Setup Swarm Test Scene`
- `Window > Alchemist > 2. Setup Osu GC Spike Scene`
- `Window > Alchemist > 3. Setup UGUI Rebuild Spike Scene`
- `Window > Alchemist > 4. Setup Particle Overdraw Scene`

### 2. 로컬 AI 실행 (선택)
```bash
ollama run llama3.2:1b
```
LLM 없이도 **DOE 모드**(OFAT / Full Factorial)로 순수 실험 계획 기반 탐색이 가능합니다.

### 3. 연구 실행
`Window > Alchemist > Performance Researcher` 대시보드에서:
- **티어 선택** (low 33.33ms / mid·high 16.67ms 프레임 예산 — 선언값, 근거는 QA_METHODOLOGY.md 문헌 절 참조)
- **START SETTINGS RESEARCH**: LLM 가설 루프
- **RUN DOE EXPERIMENT**: OFAT(1+k런) 또는 Factorial(2^k런) 자동 탐색

### 4. 결과 확인
- `Artifacts/research_run_<타임스탬프>.json` — 원시 샘플·통계·게이트 판정·도출 config 전체 기록
- `Artifacts/report.html` — 정적 HTML 리포트 (히스토리, p95/p99 델타, 패치 간 diff)

---

## 🔬 신뢰할 수 있는 결과를 위한 장치

- **이중 게이트**: 스텝 채택(현재 최고 대비 유의미한 개선인가)과 출하 판정(티어 예산을 만족하는가)을 분리 — 예산 위에서 시작해도 루프가 개선을 누적할 수 있습니다.
- **챔피언 최종 확인 런**: 루프가 노이즈로 등반했을 가능성을 종료 시점 fresh 재측정으로 검증. 실패 시 `confirmationFailed`로 기록되고 해당 결과는 출하·문서 인용이 금지됩니다.
- **A/A 널 테스트**: 동일 조건 두 측정에서 게이트가 채택을 내지 않는지가 CI 테스트로 고정되어 있습니다.
- **알려진 한계**(정직 고지): 게이트는 p50 단일 지표(꼬리 악화는 기록만), 확인 런도 단일 비교 ≤α 잔여 한도 존재, 에디터 측정은 **editor-proxy**이며 실기기 검증 전 "validated"를 주장하지 않습니다.

## 🧩 아키텍처

```
Core/        순수 C# (UnityEngine 의존 0) — 통계·게이트·실험 계획·config 도출 = dotnet CI에서 컴파일+테스트
Runtime/     출하 표면 — DeviceTierConfig(SO), 런타임 티어 감지, 세팅 적용
Editor/      글루 — N회 벤치마크 러너, 티어 시뮬레이터, LLM 프로바이더(Ollama/Gemini), 대시보드
Samples/     병목 시나리오 4종 (실험 픽스처)
Tests/       NUnit — 동일 소스가 Unity Test Runner와 dotnet 양쪽에서 실행
```

> **⚠ Experimental — File Rewrite Mode**: 이전 버전의 "LLM이 소스 파일 전체를 재작성"하는 루프는 실험 모드로 격리되어 기본 비활성입니다. 컴파일 롤백·도메인 리로드 복구·대상 해시 가드가 있지만, 이 모드의 결과는 헤드라인 수치로 사용하지 않습니다.

---

Developed by [mmporong](https://github.com/mmporong) 🌠 | [Research Report](./RESEARCH_REPORT.md) · [QA Methodology](./QA_METHODOLOGY.md) · [Device Tier Optimization](./DEVICE_TIER_OPTIMIZATION.md)
