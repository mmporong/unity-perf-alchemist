using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;

namespace UnityPerformanceAlchemist.Editor
{
    /// <summary>
    /// fenced 파일 재작성 루프 전용 FSM 상태 영속화 — 기존 AlchemistFsmState 이식
    /// + targetScriptSha256(재개 해시 가드) 추가. 설정-레벨 루프(SettingsResearchRunner)는 사용하지 않는다.
    /// </summary>
    public class FsmStatePersistence
    {
        public enum Phase { Idle, ScanProject, Baseline, Hypothesis, WriteFile_Committed, Benchmark, Decide, Done }

        public Phase phase = Phase.Idle;
        public int generation = 1;
        public int maxGenerations = 5;
        public List<double> bestRunMediansMs = new List<double>();        // 현재 best 코드의 N런 중앙값들
        public List<double> candidateRunMediansMs = new List<double>();   // 판정 대기 후보의 N런 중앙값들
        public string bestCode = "";
        public string pendingCode = "";
        public string pendingStrategy = "";
        public string targetScriptPath = "";
        public string targetScriptSha256 = "";   // 대상 파일의 '기대 내용' SHA256 — 재개 전 불일치면 상태 정리
        public string scanRoot = "Assets";
        public List<string> pendingScripts = new List<string>();
        public int currentScriptIndex = 0;
        public string optimizationGoal = "";
        public string llmProvider = "Ollama";
        [JsonIgnore] public string apiKey = ""; // EditorPrefs에서 직접 로드 — 파일에 평문 저장 금지
        public string localEndpoint = "http://localhost:11434/v1/chat/completions";
        public string localModel = "llama3.2:1b";
        // 리로드 재개 시 측정 조건 일관성 유지 — best/candidate 표본 수가 달라지면 판정이 오염된다
        public int runsPerCondition = 10;
        public int framesPerRun = 120;
        public int warmupFrames = 30;
        public double warmupSeconds = 1.0;
        public List<GenDataDto> history = new List<GenDataDto>();

        public class GenDataDto
        {
            public int generation;
            public double medianMs;
            public string strategy;
            public bool isAccepted;
            public string scriptPath;
        }

        private const string StateDir = "ProjectSettings/Alchemist";
        private const string StatePath = "ProjectSettings/Alchemist/fsm_state.json";

        public static FsmStatePersistence Load()
        {
            if (!File.Exists(StatePath))
            {
                return null;
            }
            try
            {
                return JsonConvert.DeserializeObject<FsmStatePersistence>(File.ReadAllText(StatePath));
            }
            catch
            {
                return null;
            }
        }

        public void Save()
        {
            Directory.CreateDirectory(StateDir);
            File.WriteAllText(StatePath, JsonConvert.SerializeObject(this, Formatting.Indented));
        }

        public static void Delete()
        {
            if (File.Exists(StatePath))
            {
                File.Delete(StatePath);
            }
        }

        /// <summary>UTF-8 바이트 기준 SHA256 소문자 hex — File.WriteAllText(UTF-8 no BOM) 결과와 일치.</summary>
        public static string ComputeSha256(string text)
        {
            using (var sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(text ?? ""));
                var sb = new StringBuilder(hash.Length * 2);
                foreach (byte b in hash)
                {
                    sb.Append(b.ToString("x2"));
                }
                return sb.ToString();
            }
        }
    }
}
