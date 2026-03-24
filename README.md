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

#### 🔍 Case 1: Core Code Evolution
| **Legacy (Gen 0: Brute-force)** | **Alchemist (Gen 3: Job System)** |
| :--- | :--- |
| ```csharp void Update() { foreach(var a in units) { foreach(var b in units) { float d = Vector3.Distance(a.pos, b.pos); } } } ``` | ```csharp [BurstCompile] struct SwarmJob : IJobParallelFor { public void Execute(int i) { // SIMD + Multi-threading // sqrMagnitude Optimization } } ``` |

---

### 🎵 [Case Study 2: Rhythm Game (osu!) GC Spike Elimination]
오픈소스 리듬게임 **osu!lazer**의 최대 과제인 가비지 컬렉터(GC) 스파이크를 해결하기 위해, AI가 메모리 파편화의 모든 요인을 단계적으로 제거한 사례입니다. (120 FPS Deathstream 환경)

| 세대 (Generation) | 최적화 전략 (Strategy) | GC Alloc | FPS | 상태 |
| :--- | :--- | :--- | :--- | :--- |
| **Gen 0** | (Initial) `Instantiate`/`Destroy` & Closures | **320 KB** | 14.5 | Baseline |
| **Gen 1** | String caching & StringBuilder for judgement | 280 KB | 18.2 | ✅ Accepted |
| **Gen 2** | Static delegates (Remove Closure allocation) | 190 KB | 22.5 | ✅ Accepted |
| **Gen 3** | **`UnityEngine.Pool` (Object Pooling)** | 12 KB | 58.4 | ✅ Accepted |
| **Gen 4** | Fixed-size arrays (Zero-Allocation architecture) | **0 B** | **62.1** | ✅ Accepted |

#### 🔍 Case 2: Core Code Evolution
| **Legacy (Gen 0: High-Alloc)** | **Alchemist (Gen 4: Zero-Alloc)** |
| :--- | :--- |
| ```csharp void Spawn() { Instantiate(note); // 힙 할당 Action a = () => { Log(lane); }; // 클로저 할당 } ``` | ```csharp void Spawn() { var n = pool.Get(); // 풀링 재사용 StaticApply(n, lane); // 정적 메서드로 가비지 0 } ``` |

---

## ⚛️ The AutoResearch Architecture

1. **Autonomous Hypothesis**: AI가 프로파일러 데이터를 분석하여 최적화 가설 수립.
2. **Real-world Validation**: 유니티 플레이 모드를 직접 실행하여 성능 측정.
3. **Genetic Selection**: 성능 향상 시 채택, 저하 시 즉시 롤백하여 안정성 보장.
4. **Local Execution**: **Ollama** 연동으로 8GB RAM 노트북에서 100% 로컬 작동.

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

### 2. Run Local AI
터미널에서 `ollama run llama3.2:1b`를 실행한 후, 유니티 대시보드에서 `Start AutoResearch`를 클릭하세요.

---

Developed by [mmporong] 🌠 | [Detailed Research Report](./RESEARCH_REPORT.md)
