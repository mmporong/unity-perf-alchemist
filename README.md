# 🧪 Unity Performance Alchemist
> **"Optimize your Unity game for 5,000+ devices while you sleep."**

[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](https://opensource.org/licenses/MIT)
[![Gemini Powered](https://img.shields.io/badge/AI-Gemini%202.5-orange.svg)](https://deepmind.google/technologies/gemini/)

**Unity Performance Alchemist** is a native Unity Editor Extension designed to autonomously optimize your C# scripts. Inspired by Andrej Karpathy's `autoresearch`, it uses a closed-loop evolutionary process: **[Measure -> Refactor -> Verify -> Commit]**.

---

## 🚀 The Core Advantage: Native & Autonomous

Unlike other AI tools that just suggest code, Alchemist **benchmarks** the suggestions in your actual Unity project to prove they are faster.

### 🌌 Key Features
- **Pure C# Integration:** No Node.js or Python required. Just drop the script into your Unity project.
- **Autonomous Evolution:** Gemini 2.5 analyzes your profiler data and refactors your code (Object Pooling, Job System, etc.).
- **Visual Validation:** Automatically toggles Play Mode to measure real-world Frame Time and Memory Allocation.
- **Auto-Rollback:** If AI-suggested code is slower, Alchemist automatically reverts to the previous best version.

---

## ⚙️ How it Works (The Alchemist Loop)

1.  **Baseline:** Measures the current script's frame time in Play Mode.
2.  **Hypothesis:** Gemini proposes a refactored version of the script.
3.  **Experiment:** Unity applies the code and re-runs the benchmark.
4.  **Decision:** If FPS improves, the code is saved as the new baseline. Otherwise, it rolls back.

---

## 🛠️ Quick Start

### 1. Installation
Simply copy the `Assets/UnityPerformanceAlchemist` folder into your Unity project's `Assets` directory.

### 2. Usage
1.  Open the Alchemist window: **Window > Alchemist > Performance Researcher**.
2.  Enter your **Gemini API Key**.
3.  Drag & Drop the **Target C# Script** you want to optimize.
4.  Set your goal (e.g., "Reduce GC allocations") and click **🚀 Start Autonomous Research**.

---

## 🤝 Contributing
This tool is built for high-performance engineering. We welcome contributions that add support for **Automatic multi-platform benchmarking** and **Shader optimization**.

Developed by [mmporong](https://github.com/mmporong) 🌠
