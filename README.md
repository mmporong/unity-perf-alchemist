# 🧪 Unity Performance Alchemist
> **"Optimize your Unity game for 5,000+ devices while you sleep."**

[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](https://opensource.org/licenses/MIT)
[![Gemini Powered](https://img.shields.io/badge/AI-Gemini%202.5-orange.svg)](https://deepmind.google/technologies/gemini/)
[![Hacker News Target](https://img.shields.io/badge/Target-5000%20Stars-brightgreen.svg)]()

**Unity Performance Alchemist** is an autonomous AI agent designed to bridge the gap between "It works on my machine" and "It runs 60fps on a budget Android." Inspired by Andrej Karpathy's `autoresearch`, it uses a closed-loop evolutionary process to research, refactor, and benchmark C# code across multiple virtual hardware profiles.

---

## 🚀 The Core Vision: Autonomous Performance Engineering

Most performance optimization today is reactive: something is slow, you profile it, you fix it. Alchemist makes it **proactive**.

### 1. The Multi-Device Research Loop
Alchemist doesn't just optimize for your PC. It researchs code variations against **Virtual Hardware Tiers**:
- **🌑 Low-End Tier:** Simulates restricted CPU cores and thermal throttling.
- **🌗 Mid-End Tier:** Standard mobile hardware constraints.
- **☀️ High-End Tier:** Maximum visual fidelity and high-refresh rates.

### 2. Architectural Evolution (Not just Parameter Tuning)
While simple tools change a `float` value, Alchemist's AI Researcher (Gemini 2.5) explores **Structural Hypotheses**:
- *"What if we replace this `Update()` loop with a **Job System** implementation?"*
- *"Can we use **Object Pooling** to eliminate this GC spike?"*
- *"Would a **Struct-based Data Layout** improve cache hits on mobile CPUs?"*

---

## ⚙️ How it Works (The Alchemist Loop)

1.  **Hypothesis:** AI analyzes C# code and identifies a potential bottleneck.
2.  **Experimentation:** AI generates several refactored versions of the code.
3.  **Cross-Device Benchmarking:** Each version is executed via `unity-cli` under different simulated hardware constraints.
4.  **Verification:** High-resolution profiler data (Frame time, Draw calls, Memory) is extracted.
5.  **Commit:** If a version improves performance across ALL tiers, the Agent automatically commits the change to Git.

---

## 🛠️ Quick Start

### 1. Installation
```bash
git clone https://github.com/mmporong/unity-perf-alchemist.git
cd unity-perf-alchemist
npm install
```

### 2. Configure Unity Connector
- Install the **Unity Connector** in your project to enable CLI control.
- Ensure your target C# scripts are accessible by the agent.

### 3. Start the Research
```bash
node core/index.js --target ./Assets/Scripts/MainLogic.cs --goal "Maximize FPS on Low-End Tier"
```

---

## 🗺️ Roadmap
- [ ] **Virtual Hardware Sandbox:** Automated throttling of Unity's main thread.
- [ ] **Hugging Face Visual Judge:** Using Vision models to ensure optimizations don't break visual quality.
- [ ] **Multi-Platform CI/CD:** Direct integration with AWS Device Farm for real-device validation.

---

## 🤝 Contributing
Developed with a focus on **Mechanical Engineering precision** and **Game Engine performance**. We welcome contributions from Technical Artists and Performance Engineers.

Developed by [mmporong](https://github.com/mmporong) 🌠
