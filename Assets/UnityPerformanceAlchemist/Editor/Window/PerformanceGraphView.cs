using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace UnityPerformanceAlchemist.Editor
{
    // 기존 PerformanceAlchemistWindow.DrawPerformanceGraph 이식 —
    // 세로축 fps 대신 medianMs(낮을수록 좋음), 세그먼트 색은 Adopt/Rollback/Inconclusive 3색.
    public sealed class PerformanceGraphView
    {
        public void Draw(IReadOnlyList<ResearchProgress> history)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Label("📈 Performance Trend (median ms — lower is better)", EditorStyles.boldLabel);

            Rect graphRect = GUILayoutUtility.GetRect(200, 150);
            EditorGUI.DrawRect(graphRect, new Color(0.15f, 0.15f, 0.15f));

            if (history != null && history.Count > 1)
            {
                double maxMs = 0.0;
                foreach (var entry in history)
                {
                    if (entry.MedianMs > maxMs)
                    {
                        maxMs = entry.MedianMs;
                    }
                }
                maxMs *= 1.2;
                if (maxMs < 16.67)
                {
                    maxMs = 16.67;
                }

                for (int i = 0; i < history.Count - 1; i++)
                {
                    Vector2 start = PointFor(graphRect, history, i, maxMs);
                    Vector2 end = PointFor(graphRect, history, i + 1, maxMs);

                    Handles.color = OutcomeColor(history[i + 1].Outcome);
                    Handles.DrawLine(start, end);

                    EditorGUI.DrawRect(new Rect(start.x - 2, start.y - 2, 4, 4), Color.white);
                }

                Vector2 lastPoint = PointFor(graphRect, history, history.Count - 1, maxMs);
                EditorGUI.DrawRect(new Rect(lastPoint.x - 2, lastPoint.y - 2, 4, 4), Color.white);
            }
            else
            {
                GUI.Label(graphRect, "Not enough data to draw graph...",
                    new GUIStyle { alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.gray } });
            }

            EditorGUILayout.EndVertical();
        }

        private static Vector2 PointFor(Rect graphRect, IReadOnlyList<ResearchProgress> history, int index, double maxMs)
        {
            float x = graphRect.x + (index * (graphRect.width / (history.Count - 1)));
            float normalized = (float)(history[index].MedianMs / maxMs);
            float y = graphRect.y + graphRect.height - (normalized * graphRect.height);
            return new Vector2(x, y);
        }

        private static Color OutcomeColor(string outcome)
        {
            switch (outcome)
            {
                case "Adopt": return Color.green;
                case "Rollback": return Color.red;
                case "Inconclusive": return Color.gray;
                default: return Color.white;
            }
        }
    }
}
