# 🧪 Unity Performance Alchemist
> **"Optimize your Unity game for 5,000+ devices while you sleep."**

[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](https://opensource.org/licenses/MIT)
[![Local AI Support](https://img.shields.io/badge/AI-Ollama%20Support-blue.svg)](https://ollama.com/)
[![Web Dashboard](https://img.shields.io/badge/Monitoring-Web%20Dashboard-green.svg)](#-web-live-dashboard)

**Unity Performance Alchemist**는 안드레 카파시(Andrej Karpathy)의 `autoresearch` 개념을 유니티 엔진에 이식한 **네이티브 자율 최적화 엔진**입니다. 단순한 코드 제안을 넘어, 실제 유니티 플레이 모드에서 성능을 측정하고 개선이 증명된 코드만 채택하는 **폐쇄 루프(Closed-loop) 진화 프로세스**를 수행합니다.

---

## 📊 Performance Benchmarking Results

> **⚠️ 실측 예정**: 아래 수치는 실제 벤치마크 실행 후 `Artifacts/benchmark_results.json`으로 자동 기록됩니다.
> 현재는 샘플 케이스 스터디 구조만 표시합니다.

### 🎮 [Case Study 1: 1,500 Units RTS Swarm Simulation]
RTS 게임의 대규모 유닛 충돌 회피 알고리즘(O(n²))을 대상으로 한 **CPU 병목** 최적화 사례입니다.

| 세대 (Generation) | 최적화 전략 (Strategy) | FPS | 개선율 | 상태 |
| :--- | :--- | :--- | :--- | :--- |
| **Gen 0** | (Initial) Legacy O(n²) Brute-force | (실측 예정) | - | Baseline |
| **Gen 1** | Use sqrMagnitude instead of Distance | (실측 예정) | (실측 예정) | (실측 예정) |
| **Gen 2** | Cache Transform properties locally | (실측 예정) | (실측 예정) | (실측 예정) |
| **Gen 3** | Unity Job System + Burst Compiler | (실측 예정) | (실측 예정) | (실측 예정) |

---

### 🎵 [Case Study 2: Rhythm Game GC Spike Elimination]
리듬게임의 고부하 상황에서 발생하는 **메모리 병목 및 GC 스파이크** 최적화 사례입니다.

| 세대 (Generation) | 최적화 전략 (Strategy) | GC Alloc | FPS | 상태 |
| :--- | :--- | :--- | :--- | :--- |
| **Gen 0** | (Initial) `Instantiate`/`Destroy` & Closures | (실측 예정) | (실측 예정) | Baseline |
| **Gen 1** | String caching & StringBuilder | (실측 예정) | (실측 예정) | (실측 예정) |
| **Gen 2** | Static delegates (Remove Closure allocation) | (실측 예정) | (실측 예정) | (실측 예정) |
| **Gen 3** | `UnityEngine.Pool` (Object Pooling) | (실측 예정) | (실측 예정) | (실측 예정) |

---

### 🌶️ [Case Study 3: UGUI Canvas Rebuild Spike]
대규모 UI 렌더링 병목(Canvas.SendWillRenderCanvases) 해결 사례입니다.

| 세대 (Generation) | 최적화 전략 (Strategy) | Rebuild Time | Draw Calls | 상태 |
| :--- | :--- | :--- | :--- | :--- |
| **Gen 0** | (Initial) Nested `GridLayoutGroup` + `ContentSizeFitter` | (실측 예정) | (실측 예정) | Baseline |
| **Gen 1** | Disable `raycastTarget` on non-interactive elements | (실측 예정) | (실측 예정) | (실측 예정) |
| **Gen 2** | Canvas Partitioning (Sub-Canvas) | (실측 예정) | (실측 예정) | (실측 예정) |
| **Gen 3** | Remove LayoutGroups + Virtualization | (실측 예정) | (실측 예정) | (실측 예정) |

---

### 🎆 [Case Study 4: 5K Particle GPU Overdraw]
ParticleSystem Material 중복으로 인한 **GPU 병목** 최적화 사례입니다.

| 세대 (Generation) | 최적화 전략 (Strategy) | Draw Calls | GPU Time | 상태 |
| :--- | :--- | :--- | :--- | :--- |
| **Gen 0** | (Initial) 50 Systems × Unique Materials | (실측 예정) | (실측 예정) | Baseline |
| **Gen 1** | Share single Material across all systems | (실측 예정) | (실측 예정) | (실측 예정) |
| **Gen 2** | Enable GPU Instancing on shared Material | (실측 예정) | (실측 예정) | (실측 예정) |
| **Gen 3** | Merge into single system + LOD | (실측 예정) | (실측 예정) | (실측 예정) |

---

1. **Autonomous Hypothesis**: AI가 프로파일러 데이터를 분석하여 최적화 가설 수립.
2. **Real-world Validation**: 유니티 플레이 모드를 직접 실행하여 성능 측정.
3. **Genetic Selection**: 성능 향상 시 채택, 저하 시 즉시 롤백하여 안정성 보장.
4. **Local Execution**: **Ollama** 연동으로 8GB RAM 노트북에서도 100% 로컬 작동.

---

## 🌐 Web Live Dashboard
최적화 진행 상황을 실시간으로 모니터링하고 **진화된 코드 전문을 브라우저에서 즉시 확인**할 수 있습니다.
- **Real-time Sync**: 유니티 에디터와 웹 서버 간 실시간 데이터 동기화.
- **Code Viewer**: 각 세대별 리팩토링된 코드를 클릭 한 번으로 비교 확인.

---

## 🛠️ Getting Started

### 1. Setup Demo Scenes
유니티 상단 메뉴: 
- **`Window > Alchemist > 1. Setup Swarm Test Scene`** (RTS 최적화 테스트)
- **`Window > Alchemist > 2. Setup Osu GC Spike Scene`** (리듬게임 최적화 테스트)
- **`Window > Alchemist > 4. Setup Particle Overdraw Scene`** (GPU 파티클 오버드로 테스트)

### 2. Run Local AI
터미널에서 `ollama run llama3.2:1b`를 실행한 후, 유니티 대시보드에서 `Start AutoResearch`를 클릭하세요.

---

Developed by [mmporong] 🌠 | [Detailed Research Report](./RESEARCH_REPORT.md)
