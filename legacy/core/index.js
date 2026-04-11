import fs from 'fs/promises';
import path from 'path';
import 'dotenv/config';
import { Benchmarker } from './benchmarker.js';
import { Researcher } from './researcher.js';
import { simpleGit } from 'simple-git';

async function main() {
  const targetFile = process.argv[2] || './Assets/Scripts/HeavyLogic.cs';
  const projectPath = process.argv[3] || 'Gomyammi';
  const goal = "Minimize Frame Time on Low-End Hardware";

  console.log(`🌌 Unity Performance Alchemist 1.0`);
  console.log(`🎯 Goal: ${goal}`);
  console.log(`📄 Target: ${targetFile}`);

  const benchmarker = new Benchmarker(projectPath);
  const researcher = new Researcher(process.env.GEMINI_API_KEY);
  const git = simpleGit();

  // 1. Initial Baseline Benchmark
  console.log("\n--- [1/5] Establishing Baseline ---");
  const baseline = await benchmarker.runBenchmark('low');
  console.log(`📊 Baseline Frame Time: ${baseline.frameTime}ms (${(1000/baseline.frameTime).toFixed(1)} FPS)`);

  let bestScore = baseline.frameTime;
  let bestCode = await fs.readFile(targetFile, 'utf-8');

  // 2. The Research Loop
  for (let gen = 1; gen <= 5; gen++) {
    console.log(`\n--- Generation ${gen} ---`);

    // Hypothesis Generation
    const proposal = await researcher.proposeRefactor(bestCode, { frameTime: bestScore, memory: baseline.memory }, goal);
    
    // Apply changes (Surgical Change)
    await fs.writeFile(targetFile, proposal);
    console.log(`📝 Applied AI refactor to ${targetFile}`);

    // Mult-Device Benchmarking
    const results = await benchmarker.runBenchmark('low');
    
    // 3. Evaluation & Decision
    if (results.frameTime < bestScore && !results.error) {
      const improvement = ((bestScore - results.frameTime) / bestScore * 100).toFixed(1);
      console.log(`✨ SUCCESS! Performance improved by ${improvement}%`);
      
      bestScore = results.frameTime;
      bestCode = proposal;

      // Commit to Git (The Star Magnet Feature)
      try {
        await git.add(targetFile);
        await git.commit(`perf: optimized ${path.basename(targetFile)} (FrameTime: ${bestScore.toFixed(2)}ms)`);
        console.log(`✅ Progress committed to Git.`);
      } catch (e) {
        console.warn(`⚠️ Git commit failed (maybe no changes or repo not init).`);
      }
    } else {
      console.log(`📉 REGRESSION: Performance dropped to ${results.frameTime}ms. Rolling back.`);
      await fs.writeFile(targetFile, bestCode);
    }
  }

  console.log(`\n🏁 Research Finished. Best Frame Time: ${bestScore.toFixed(2)}ms`);
}

main().catch(err => {
  console.error("❌ Alchemist Loop Crashed:", err);
});
