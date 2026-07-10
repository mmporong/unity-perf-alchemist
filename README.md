# 🧪 Unity Performance Alchemist

> **설정-레벨 AutoResearch: 기기 티어별 최적화 세팅을 통계 게이트로 도출하는 Unity 에디터 툴**

[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](https://opensource.org/licenses/MIT)
[![Local AI Support](https://img.shields.io/badge/AI-Ollama%20Support-blue.svg)](https://ollama.com/)
[![Core CI](https://img.shields.io/badge/CI-dotnet%20%2B%20NUnit-green.svg)](.github/workflows/core-ci.yml)

**Unity Performance Alchemist**는 안드레 카파시(Andrej Karpathy)의 `autoresearch` 개념을 유니티에 이식한 **폐쇄 루프(closed-loop) 자율 최적화 엔진**입니다. v0.2부터 루프의 액션 스페이스는 **품질 세팅/최적화 토글**입니다:

> LLM이 "다음에 뒤집을 세팅"을 가설로 제안 → 플레이모드에서 **N회 독립 측정** → **비모수 통계 게이트**(Mann-Whitney U + Cliff's delta)로 채택/롤백 → 반복 → **챔피언 최종 확인 런**(fresh ABAB 재측정) → 기기 티어별 최적 세팅 도출.

코드 전체를 재작성하는 이전 방식과 달리, 세팅 변경은 **컴파일이 필요 없어** 루프가 한 세션 안에서 안전하게 돌고, 롤백은 값 하나를 되돌리는 것으로 끝납니다.

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
