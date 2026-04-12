using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace UnityPerformanceAlchemist.Editor
{
    public class AlchemistFsmState
    {
        public enum Phase { Idle, Baseline, Hypothesis, WriteFile_Committed, Benchmark, Decide, Done }

        public Phase phase = Phase.Idle;
        public int generation = 1;
        public int maxGenerations = 10;
        public float initialFPS;
        public float bestFPS;
        public float currentTestFPS;
        public string bestCode = "";
        public string pendingCode = "";
        public string pendingStrategy = "";
        public string targetScriptPath = "";
        public string optimizationGoal = "";
        public string llmProvider = "Ollama_Local";
        [JsonIgnore] public string apiKey = ""; // EditorPrefs에서 직접 로드 — 파일에 평문 저장 금지
        public string localEndpoint = "http://localhost:11434/v1/chat/completions";
        public string localModel = "llama3.2:1b";
        public List<GenDataDto> history = new List<GenDataDto>();

        public class GenDataDto
        {
            public int generation;
            public float fps;
            public string strategy;
            public bool isAccepted;
        }

        private const string StateDir = "ProjectSettings/Alchemist";
        private const string StatePath = "ProjectSettings/Alchemist/fsm_state.json";

        public static AlchemistFsmState Load()
        {
            if (!File.Exists(StatePath)) return null;
            try { return JsonConvert.DeserializeObject<AlchemistFsmState>(File.ReadAllText(StatePath)); }
            catch { return null; }
        }

        public void Save()
        {
            Directory.CreateDirectory(StateDir);
            File.WriteAllText(StatePath, JsonConvert.SerializeObject(this, Formatting.Indented));
        }

        public static void Delete()
        {
            if (File.Exists(StatePath)) File.Delete(StatePath);
        }
    }
}
