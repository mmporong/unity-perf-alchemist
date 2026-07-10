using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using Newtonsoft.Json.Linq;

namespace UnityPerformanceAlchemist.Editor
{
    /// <summary>
    /// Artifacts/research_run_*.json 전부 → 자기완결 정적 HTML(Artifacts/report.html).
    /// 외부 리소스 0 (인라인 CSS만). 런 히스토리, 게이트 판정 배지, p95/p99 델타,
    /// derivedAgainst 표기, 패치 간 diff 섹션(configDiff가 저장돼 있으면 표시).
    /// </summary>
    public static class ReportExporter
    {
        public static string Export(string artifactsDir = "Artifacts")
        {
            var runs = new List<(string fileName, JObject root)>();
            if (Directory.Exists(artifactsDir))
            {
                foreach (string file in Directory.GetFiles(artifactsDir, "research_run_*.json")
                                                 .OrderBy(f => f, StringComparer.Ordinal))
                {
                    try
                    {
                        runs.Add((Path.GetFileName(file), JObject.Parse(File.ReadAllText(file))));
                    }
                    catch (Exception)
                    {
                        // 손상 파일은 리포트에서 조용히 제외
                    }
                }
            }

            var html = new StringBuilder();
            html.Append("<!DOCTYPE html><html lang=\"ko\"><head><meta charset=\"utf-8\">");
            html.Append("<title>Performance Alchemist Report</title>");
            html.Append("<style>");
            html.Append("body{background:#14171c;color:#dfe3e8;font-family:'Segoe UI',Helvetica,Arial,sans-serif;");
            html.Append("margin:0;padding:32px;line-height:1.5}");
            html.Append("h1{font-size:22px;border-bottom:1px solid #2c333d;padding-bottom:12px}");
            html.Append("h2{font-size:17px;margin-top:36px;color:#aeb8c4}");
            html.Append("h3{font-size:14px;color:#8b96a3;margin-bottom:6px}");
            html.Append("table{border-collapse:collapse;width:100%;margin:8px 0 20px;font-size:13px}");
            html.Append("th,td{border:1px solid #2c333d;padding:6px 10px;text-align:left}");
            html.Append("th{background:#1c2128;color:#9aa5b1}");
            html.Append("tr:nth-child(even){background:#181c22}");
            html.Append(".badge{display:inline-block;padding:2px 10px;border-radius:10px;font-size:12px;font-weight:600}");
            html.Append(".adopt,.ok{background:#1d5c34;color:#c9f2d8}");
            html.Append(".rollback,.fail{background:#7a2e2e;color:#f6d3d3}");
            html.Append(".inconclusive{background:#6b5b1e;color:#f2e6b8}");
            html.Append(".meta{color:#7d8896;font-size:12px}");
            html.Append(".num{font-variant-numeric:tabular-nums}");
            html.Append(".delta-good{color:#7ddba0}.delta-bad{color:#e89a9a}");
            html.Append("</style></head><body>");
            html.Append("<h1>Performance Alchemist Report</h1>");
            html.Append($"<p class=\"meta\">generated {Enc(DateTime.UtcNow.ToString("o"))} · runs: {runs.Count}</p>");

            if (runs.Count == 0)
            {
                html.Append("<p>Artifacts에 research_run_*.json이 없습니다.</p>");
            }
            else
            {
                AppendSummaryTable(html, runs);
                foreach ((string fileName, JObject root) in runs)
                {
                    AppendRunSection(html, fileName, root);
                }
            }

            html.Append("</body></html>");

            Directory.CreateDirectory(artifactsDir);
            string reportPath = Path.Combine(artifactsDir, "report.html");
            File.WriteAllText(reportPath, html.ToString());
            return reportPath;
        }

        // ── 런 히스토리 요약 테이블 ──────────────────────────────────────────

        private static void AppendSummaryTable(StringBuilder html, List<(string fileName, JObject root)> runs)
        {
            html.Append("<h2>Run History</h2><table><tr>");
            html.Append("<th>file</th><th>tier</th><th>derivedAtUtc</th><th>patch</th>");
            html.Append("<th>median (ms)</th><th>budget</th><th>confirmation</th></tr>");
            foreach ((string fileName, JObject root) in runs)
            {
                JObject derived = root["derivedConfig"] as JObject;
                JObject against = root["derivedAgainst"] as JObject;
                double? median = derived == null ? null : (double?)derived["medianMs"];
                bool budget = derived != null && ((bool?)derived["budgetSatisfied"] ?? false);
                bool confirmationFailed = derived != null && ((bool?)derived["confirmationFailed"] ?? false);

                html.Append("<tr>");
                html.Append($"<td>{Enc(fileName)}</td>");
                html.Append($"<td>{Enc((string)root["tierId"] ?? "-")}</td>");
                html.Append($"<td class=\"meta\">{Enc(against == null ? "-" : (string)against["derivedAtUtc"] ?? "-")}</td>");
                html.Append($"<td>{Enc(against == null ? "-" : Blank((string)against["patchId"]))}</td>");
                html.Append($"<td class=\"num\">{(median.HasValue ? median.Value.ToString("F2") : "-")}</td>");
                html.Append($"<td>{Badge(budget ? "ok" : "fail", budget ? "budget ok" : "over budget")}</td>");
                html.Append($"<td>{Badge(confirmationFailed ? "fail" : "ok", confirmationFailed ? "failed" : "ok")}</td>");
                html.Append("</tr>");
            }
            html.Append("</table>");
        }

        // ── 개별 런 상세 ────────────────────────────────────────────────────

        private static void AppendRunSection(StringBuilder html, string fileName, JObject root)
        {
            html.Append($"<h2>{Enc(fileName)}</h2>");

            if (root["derivedAgainst"] is JObject against)
            {
                html.Append("<p class=\"meta\">derivedAgainst — ");
                html.Append($"build: {Enc(Blank((string)against["gameBuildVersion"]))} · ");
                html.Append($"schema: {Enc(Blank((string)against["upaSchemaVersion"]))} · ");
                html.Append($"patch: {Enc(Blank((string)against["patchId"]))} · ");
                html.Append($"at: {Enc(Blank((string)against["derivedAtUtc"]))}</p>");
            }
            html.Append($"<p class=\"meta\">tier: {Enc((string)root["tierId"] ?? "-")} · ");
            html.Append($"source: {Enc((string)root["measurementSource"] ?? "-")}</p>");

            AppendStepsTable(html, root);
            AppendConfirmation(html, root);
            AppendDerivedConfig(html, root);
            AppendConfigDiff(html, root);
        }

        private static void AppendStepsTable(StringBuilder html, JObject root)
        {
            double? baselineMedian = MedianOf(root["baseline"]?["runMediansMs"] as JArray);
            html.Append("<h3>Steps</h3><table><tr>");
            html.Append("<th>gen</th><th>strategy</th><th>median (ms)</th><th>gate</th>");
            html.Append("<th>p</th><th>&delta;</th><th>method</th></tr>");

            html.Append("<tr><td class=\"num\">0</td><td>baseline</td>");
            html.Append($"<td class=\"num\">{FormatMs(baselineMedian)}</td>");
            html.Append("<td>-</td><td>-</td><td>-</td><td>-</td></tr>");

            if (root["steps"] is JArray steps)
            {
                foreach (JToken step in steps)
                {
                    JObject gate = step["gate"] as JObject;
                    string outcome = gate == null ? "-" : (string)gate["outcome"] ?? "-";
                    html.Append("<tr>");
                    html.Append($"<td class=\"num\">{Enc(((int?)step["generation"])?.ToString() ?? "-")}</td>");
                    html.Append($"<td>{Enc((string)step["strategy"] ?? "-")}</td>");
                    html.Append($"<td class=\"num\">{FormatMs(MedianOf(step["runMediansMs"] as JArray))}</td>");
                    html.Append($"<td>{GateBadge(outcome)}</td>");
                    html.Append($"<td class=\"num\">{FormatNum(gate == null ? null : (double?)gate["pValue"], "F4")}</td>");
                    html.Append($"<td class=\"num\">{FormatNum(gate == null ? null : (double?)gate["cliffsDelta"], "F3")}</td>");
                    html.Append($"<td class=\"meta\">{Enc(gate == null ? "-" : (string)gate["method"] ?? "-")}</td>");
                    html.Append("</tr>");
                }
            }
            html.Append("</table>");
        }

        private static void AppendConfirmation(StringBuilder html, JObject root)
        {
            if (!(root["confirmation"] is JObject confirmation))
            {
                return;
            }

            bool confirmed = (bool?)confirmation["confirmed"] ?? false;
            html.Append("<h3>Confirmation (fresh ABAB)</h3><p>");
            html.Append(Badge(confirmed ? "adopt" : "rollback", confirmed ? "confirmed" : "not confirmed"));
            html.Append(" · p95 &Delta;: ");
            html.Append(DeltaSpan((double?)confirmation["p95DeltaMs"]));
            html.Append(" · p99 &Delta;: ");
            html.Append(DeltaSpan((double?)confirmation["p99DeltaMs"]));
            string note = (string)confirmation["note"];
            if (!string.IsNullOrEmpty(note))
            {
                html.Append($"</p><p class=\"meta\">{Enc(note)}");
            }
            html.Append("</p>");
        }

        private static void AppendDerivedConfig(StringBuilder html, JObject root)
        {
            if (!(root["derivedConfig"] is JObject derived))
            {
                return;
            }

            bool budget = (bool?)derived["budgetSatisfied"] ?? false;
            bool confirmationFailed = (bool?)derived["confirmationFailed"] ?? false;
            html.Append("<h3>Derived Config</h3><p>");
            html.Append($"median: <span class=\"num\">{FormatNum((double?)derived["medianMs"], "F2")}ms</span> · ");
            html.Append(Badge(budget ? "ok" : "fail", budget ? "budget ok" : "over budget"));
            html.Append(" · ");
            html.Append(Badge(confirmationFailed ? "fail" : "ok",
                              confirmationFailed ? "confirmation failed" : "confirmation ok"));
            html.Append("</p>");

            if (derived["settings"] is JObject settings)
            {
                html.Append("<table><tr><th>setting</th><th>value</th></tr>");
                foreach (JProperty property in settings.Properties().OrderBy(p => p.Name, StringComparer.Ordinal))
                {
                    html.Append($"<tr><td>{Enc(property.Name)}</td><td>{Enc((string)property.Value ?? "-")}</td></tr>");
                }
                html.Append("</table>");
            }
        }

        private static void AppendConfigDiff(StringBuilder html, JObject root)
        {
            if (!(root["configDiff"] is JObject diff))
            {
                return;
            }

            html.Append("<h3>Diff vs Previous Patch</h3>");
            html.Append($"<p class=\"meta\">median: <span class=\"num\">{FormatNum((double?)diff["prevMedianMs"], "F2")}ms → " +
                        $"{FormatNum((double?)diff["nextMedianMs"], "F2")}ms</span> · budget: " +
                        $"{((bool?)diff["prevBudgetSatisfied"] ?? false ? "ok" : "fail")} → " +
                        $"{((bool?)diff["nextBudgetSatisfied"] ?? false ? "ok" : "fail")}</p>");

            if (diff["changes"] is JArray changes && changes.Count > 0)
            {
                html.Append("<table><tr><th>key</th><th>prev</th><th>next</th></tr>");
                foreach (JToken change in changes)
                {
                    html.Append("<tr>");
                    html.Append($"<td>{Enc((string)change["key"] ?? "-")}</td>");
                    html.Append($"<td>{Enc((string)change["prevValue"] ?? "-")}</td>");
                    html.Append($"<td>{Enc((string)change["nextValue"] ?? "-")}</td>");
                    html.Append("</tr>");
                }
                html.Append("</table>");
            }
            else
            {
                html.Append("<p class=\"meta\">변경된 세팅 없음</p>");
            }
        }

        // ── 헬퍼 ────────────────────────────────────────────────────────────

        private static double? MedianOf(JArray values)
        {
            if (values == null || values.Count == 0)
            {
                return null;
            }
            double[] sorted = values.Select(v => (double)v).OrderBy(v => v).ToArray();
            int mid = sorted.Length / 2;
            return sorted.Length % 2 == 1 ? sorted[mid] : (sorted[mid - 1] + sorted[mid]) / 2.0;
        }

        private static string GateBadge(string outcome)
        {
            switch (outcome)
            {
                case "Adopt": return Badge("adopt", "Adopt");
                case "Rollback": return Badge("rollback", "Rollback");
                case "Inconclusive": return Badge("inconclusive", "Inconclusive");
                default: return Enc(outcome);
            }
        }

        private static string Badge(string cssClass, string text)
        {
            return $"<span class=\"badge {cssClass}\">{Enc(text)}</span>";
        }

        private static string DeltaSpan(double? value)
        {
            if (!value.HasValue)
            {
                return "-";
            }
            // 음수 = 개선 (champion − baseline)
            string cssClass = value.Value <= 0.0 ? "delta-good" : "delta-bad";
            return $"<span class=\"num {cssClass}\">{value.Value:+0.00;-0.00;0.00}ms</span>";
        }

        private static string FormatMs(double? value)
        {
            return value.HasValue ? value.Value.ToString("F2") : "-";
        }

        private static string FormatNum(double? value, string format)
        {
            return value.HasValue ? value.Value.ToString(format) : "-";
        }

        private static string Blank(string value)
        {
            return string.IsNullOrEmpty(value) ? "-" : value;
        }

        private static string Enc(string value)
        {
            return WebUtility.HtmlEncode(value ?? "");
        }
    }
}
