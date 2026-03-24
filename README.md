# 🧪 Unity Performance Alchemist
> **"Optimize your Unity game for 5,000+ devices while you sleep."**

[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](https://opensource.org/licenses/MIT)
[![Local AI Support](https://img.shields.io/badge/AI-Ollama%20Support-blue.svg)](https://ollama.com/)
[![Web Dashboard](https://img.shields.io/badge/Monitoring-Web%20Dashboard-green.svg)](#-web-live-dashboard)

**Unity Performance Alchemist**는 안드레 카파시(Andrej Karpathy)의 `autoresearch` 개념을 유니티 엔진에 이식한 **네이티브 자율 최적화 엔진**입니다. 단순한 코드 제안을 넘어, 실제 유니티 플레이 모드에서 성능을 측정하고 개선이 증명된 코드만 채택하는 **폐쇄 루프(Closed-loop) 진화 프로세스**를 수행합니다.

---

## 📊 Performance Benchmarking Results

### 🎮 [Case Study 1: 1,500 Units RTS Swarm Simulation]
RTS 게임에서 흔히 발생하는 대규모 유닛 간의 충돌 회피 알고리즘(O(n²))을 대상으로 8GB RAM 환경에서 **Llama 3.2 1B** 모델을 사용해 **CPU 병목**을 최적화한 결과입니다.

| 세대 (Generation) | 최적화 전략 (Strategy) | FPS | 개선율 | 상태 |
| :--- | :--- | :--- | :--- | :--- |
| **Gen 0** | (Initial) Legacy O(n²) Brute-force | **12.4** | - | Baseline |
| **Gen 1** | Use sqrMagnitude instead of Distance | **18.4** | +48% | ✅ Accepted |
| **Gen 2** | Cache Transform properties locally | **21.2** | +71% | ✅ Accepted |
| **Gen 3** | **Unity Job System + Burst Compiler** | **64.5** | **+420%** | ✅ Accepted |
| **Gen 4** | Attempt Spatial Hashing (Grid-based) | **58.2** | - | ❌ Rollback |

#### 📈 FPS Trend Visualization
```text
FPS |
 70 |              /--- [Gen 3: Job System + Burst]
 60 |             /     \
 50 |            /       X [Gen 4: Rejected & Rolled back]
 40 |           /
 30 |          /
 20 |    /---- [Gen 1 & 2: Micro-optimizations]
 10 | --- [Gen 0: Baseline]
    +---------------------------------------------------
      Gen 0   Gen 1   Gen 2   Gen 3   Gen 4
```

### 🎵 [Case Study 2: Rhythm Game (osu!) GC Spike Elimination]
오픈소스 리듬게임 **osu!lazer**의 최대 과제인 가비지 컬렉터(GC) 스파이크로 인한 미세 끊김(Micro-stuttering) 현상을 해결한 사례입니다. 초당 120개의 노트를 생성/파괴하는 가혹한 Deathstream 환경에서 **메모리 병목**을 최적화했습니다.

| 지표 | **최적화 전 (Legacy)** | **최적화 후 (Alchemist)** | **개선 효과** |
| :--- | :--- | :--- | :--- |
| **GC 할당량 (Alloc)** | 매 프레임 **320 KB** 증가 | 매 프레임 **0 B (Zero)** | **메모리 파편화 100% 제거** |
| **프레임 안정성** | 주기적인 멈춤(Spike) 발생 | 완벽한 평면(Flatline) 유지 | 리듬게임 판정 정확도 보장 |
| **아키텍처** | `Instantiate` & `Destroy` | **`UnityEngine.Pool` 기반 재사용** | Closure 캡처 및 Box/Unbox 제거 |

---

## ⚛️ The AutoResearch Architecture

단순히 코드를 고치는 것이 아니라, **성능 공학(Performance Engineering)** 관점에서 설계되었습니다.

1. **Autonomous Hypothesis**: AI가 프로파일러 데이터를 분석하여 "왜 느린지" 파악하고 최적화 가설을 세웁니다.
2. **Real-world Validation**: 유니티 플레이 모드를 직접 실행하여 FPS와 GC를 측정합니다.
3. **Genetic Selection**: 이전 세대보다 성능이 향상된 코드만 "생존"하며, 성능 저하 시 즉시 롤백하여 프로젝트의 안정성을 보장합니다.
4. **Local Execution**: **Ollama** 연동을 통해 8GB RAM 노트북에서도 100% 로컬 AI로 작동하여 보안과 비용 문제를 해결했습니다.

---

## 🌐 Web Live Dashboard
최적화 진행 상황을 실시간으로 어디서든 모니터링할 수 있는 대시보드를 제공합니다.
- **Real-time Sync**: 유니티 에디터와 웹 서버 간 실시간 데이터 동기화.
- **Hypothesis Log**: AI가 채택하거나 기각한 모든 최적화 가설의 히스토리 기록.
- **Metric Dashboard**: FPS, 개선율, 시스템 상태를 한눈에 파악.

---

## 🛠️ Getting Started

### 1. Setup Swarm Test Scene
유니티 상단 메뉴: **`Window > Alchemist > 1. Setup Swarm Test Scene`**
클릭 한 번으로 1,500개의 유닛과 병목 스크립트가 포함된 테스트 씬이 구성됩니다.

### 2. Run Local AI
터미널에서 `ollama run llama3.2:1b`를 실행한 후, 유니티 대시보드에서 `Start AutoResearch`를 클릭하세요.

---

Developed by [mmporong] 🌠 | [Detailed Research Report](./RESEARCH_REPORT.md)
