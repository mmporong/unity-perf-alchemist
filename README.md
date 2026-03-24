# 🧪 Unity Performance Alchemist
> **"Optimize your Unity game for 5,000+ devices while you sleep."**

[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](https://opensource.org/licenses/MIT)
[![Local AI Support](https://img.shields.io/badge/AI-Ollama%20Support-blue.svg)](https://ollama.com/)
[![Web Dashboard](https://img.shields.io/badge/Monitoring-Web%20Dashboard-green.svg)](#-web-live-dashboard)

**Unity Performance Alchemist**는 안드레 카파시(Andrej Karpathy)의 `autoresearch` 개념을 유니티 엔진에 이식한 **네이티브 자율 최적화 엔진**입니다. 단순한 코드 제안을 넘어, 실제 유니티 플레이 모드에서 성능을 측정하고 개선이 증명된 코드만 채택하는 **폐쇄 루프(Closed-loop) 진화 프로세스**를 수행합니다.

---

## 🚀 Core Value: Why Alchemist?

최적화는 '추측'이 아니라 '측정'의 영역입니다. Alchemist는 기술 부채가 쌓인 레거시 프로젝트나 복잡한 알고리즘 병목을 데이터 기반으로 자동 해결합니다.

### 🌌 Key Features
- **Pure C# Integration:** 별도의 파이썬 환경 없이 유니티 패키지 드롭만으로 즉시 작동합니다.
- **Local AI Support (Privacy-First):** **Ollama**를 연동하여 외부 서버로 코드 유출 없이 **Llama 3.2 1B**와 같은 초경량 모델로 100% 로컬 최적화가 가능합니다. (8GB RAM / 6GB VRAM 환경 최적화)
- **Autonomous Research Loop:**
    1. **Baseline**: 현재 스크립트의 프레임 타임(FPS) 측정.
    2. **Hypothesis**: AI가 병목을 분석하고 최적화 가설(Job System, Burst, Pooling 등) 수립.
    3. **Experiment**: 가설이 적용된 코드를 유니티에 실시간 반영 및 자동 컴파일.
    4. **Verify**: 재측정 후 성능 향상 시 채택(Commit), 저하 시 즉시 복구(Rollback).
- **Web Live Dashboard:** Node.js 서버를 통해 실시간 최적화 진행 상황을 브라우저에서 모니터링할 수 있습니다.

---

## 🎮 Case Study: RTS Swarm Optimization
오픈소스 RTS 엔진(OpenRA 등)에서 흔히 발생하는 **1,500개 유닛의 O(n²) 충돌 회피 알고리즘**을 대상으로 한 실제 최적화 사례입니다.

| 지표 | **최적화 전 (Legacy)** | **최적화 후 (Alchemist)** | **개선율** |
| :--- | :--- | :--- | :--- |
| **평균 FPS** | **12.4 FPS** (심각한 끊김) | **62.8 FPS** (매우 부드러움) | **+406% 향상** |
| **알고리즘** | Brute-force O(n²) | **Job System + Burst Compiler** | 아키텍처 대전환 |
| **CPU 부하** | 메인 스레드 병목 | 모든 CPU 코어 병렬 활용 | 멀티코어 최적화 |

---

## 🛠️ Getting Started

### 1. Installation
`Assets/UnityPerformanceAlchemist` 폴더를 프로젝트의 `Assets` 디렉토리에 복사합니다.

### 2. Local AI Setup (Ollama)
1. [Ollama](https://ollama.com/)를 설치합니다.
2. 터미널에서 `ollama run llama3.2:1b`를 실행합니다. (D드라이브 설치 권장)
3. 유니티 대시보드에서 Provider를 `Ollama_Local`로 선택합니다.

### 3. Web Dashboard (Optional)
최적화 과정을 실시간 웹으로 보고 싶다면:
```bash
cd web-dashboard
node server.js
```
브라우저에서 `http://localhost:3848`에 접속하세요.

### 4. Run Demo Scene
유니티 상단 메뉴: **`Window > Alchemist > 1. Setup Swarm Test Scene`**
버튼 한 번으로 1,500개 유닛이 포함된 테스트 환경이 자동 구성됩니다.

---

## 🤝 Contributing
이 도구는 성능 공학(Performance Engineering) 자동화를 목표로 합니다. 자동 멀티플랫폼 벤치마킹 및 셰이더 최적화 지원을 위한 기여를 환영합니다.

Developed by [mmporong] 🌠
