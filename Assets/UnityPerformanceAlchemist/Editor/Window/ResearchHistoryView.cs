using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace UnityPerformanceAlchemist.Editor
{
    // 기존 PerformanceAlchemistWindow.DrawResearchHistory 이식 —
    // fps 대신 medianMs, isAccepted 2값 대신 Adopt/Rollback/Inconclusive 3색.
    public sealed class ResearchHistoryView
    {
        private Vector2 scrollPos;

        public void Draw(IReadOnlyList<ResearchProgress> history)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Label("📜 Research Change Log & Hypotheses", EditorStyles.boldLabel);

            scrollPos = EditorGUILayout.BeginScrollView(scrollPos);
            if (history != null)
            {
                foreach (var entry in history)
                {
                    DrawRow(entry);
                }
            }
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private static void DrawRow(ResearchProgress entry)
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.textArea);

            GUILayout.Label($"Gen {entry.Generation}", GUILayout.Width(50));
            GUILayout.Label(OutcomeIcon(entry.Outcome), GUILayout.Width(20));
            GUILayout.Label(
                $"<color={OutcomeColorName(entry.Outcome)}>{entry.MedianMs:F2} ms</color>",
                new GUIStyle(EditorStyles.label) { richText = true, fontStyle = FontStyle.Bold },
                GUILayout.Width(80));

            string text = string.IsNullOrEmpty(entry.Message)
                ? entry.Strategy
                : $"{entry.Strategy} — {entry.Message}";
            GUILayout.Label(text, EditorStyles.wordWrappedLabel);

            EditorGUILayout.EndHorizontal();
        }

        private static string OutcomeIcon(string outcome)
        {
            switch (outcome)
            {
                case "Adopt": return "✅";
                case "Rollback": return "❌";
                case "Inconclusive": return "❓";
                default: return "•";
            }
        }

        private static string OutcomeColorName(string outcome)
        {
            switch (outcome)
            {
                case "Adopt": return "green";
                case "Rollback": return "red";
                case "Inconclusive": return "grey";
                default: return "white";
            }
        }
    }
}
