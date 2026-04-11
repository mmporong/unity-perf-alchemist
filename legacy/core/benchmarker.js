import { execFile } from 'child_process';
import util from 'util';

const execFilePromise = util.promisify(execFile);
const UNITY_CLI_EXE = 'C:\\Users\\LIMMM\\AppData\\Local\\unity-cli\\unity-cli.exe';

export class Benchmarker {
  constructor(projectPath) {
    this.projectPath = projectPath;
  }

  async runCommand(args) {
    return await execFilePromise(UNITY_CLI_EXE, ['--project', this.projectPath, ...args]);
  }

  /**
   * Runs a benchmark under a specific hardware profile.
   * profile: 'low', 'mid', 'high'
   */
  async runBenchmark(profile = 'mid') {
    console.log(`[Benchmarker] 🚀 Running benchmark with [${profile}] profile...`);
    
    try {
      // 1. Setup Profile (Simulated via Unity Quality settings or custom script)
      const setupCode = `
        QualitySettings.SetQualityLevel(${profile === 'low' ? 0 : profile === 'high' ? 5 : 2});
        Application.targetFrameRate = ${profile === 'low' ? 30 : 60};
        return "Profile Set";
      `;
      await this.runCommand(['exec', setupCode.replace(/\n/g, '')]);

      // 2. Play and Record
      await this.runCommand(['editor', 'play']);
      await new Promise(r => setTimeout(r, 5000)); // Observe for 5 seconds

      // 3. Extract Profiler Stats
      const extractCode = `
        float avgFrameTime = 0;
        // In a real implementation, we'd average over the last 100 frames
        avgFrameTime = Time.smoothDeltaTime * 1000.0f; 
        long memory = UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong() / 1024 / 1024;
        return avgFrameTime.ToString("F2") + "|" + memory.ToString();
      `;
      const { stdout } = await this.runCommand(['exec', extractCode.replace(/\n/g, '')]);
      
      await this.runCommand(['editor', 'stop']);

      const [fps, mem] = stdout.trim().split('|').map(parseFloat);
      return { fps: 1000 / fps, frameTime: fps, memory: mem };

    } catch (e) {
      console.error(`[Benchmarker] ❌ Benchmark failed: ${e.message}`);
      return { error: e.message };
    }
  }
}
