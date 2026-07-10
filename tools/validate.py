#!/usr/bin/env python3
"""정적 밸리데이터 (계약 §7) — stdlib only. exit 0=통과, 1=실패."""
import glob
import json
import os
import re
import sys
import xml.etree.ElementTree as ET

# Windows 콘솔(cp949 등)에서도 UTF-8 출력 보장
if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8")

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
ASSETS = os.path.join(ROOT, "Assets")
CORE_DIR = os.path.join(ASSETS, "UnityPerformanceAlchemist", "Core")

failures = []


def fail(msg):
    failures.append(msg)


def rel(path):
    return os.path.relpath(path, ROOT).replace("\\", "/")


# ① Assets/** 모든 파일·폴더에 .meta 존재
def check_meta_presence():
    for dirpath, dirnames, filenames in os.walk(ASSETS):
        for name in dirnames + [f for f in filenames if not f.endswith(".meta")]:
            p = os.path.join(dirpath, name)
            if not os.path.exists(p + ".meta"):
                fail(f"[meta] .meta 누락: {rel(p)}")
        for f in filenames:
            if f.endswith(".meta"):
                target = os.path.join(dirpath, f[:-5])
                if not os.path.exists(target):
                    fail(f"[meta] 고아 .meta (대상 없음): {rel(os.path.join(dirpath, f))}")


# ② GUID 중복 0
def check_guid_duplicates():
    guid_re = re.compile(r"^guid: ([0-9a-f]{32})\s*$", re.M)
    seen = {}
    for meta in glob.glob(os.path.join(ASSETS, "**", "*.meta"), recursive=True):
        text = open(meta, encoding="utf-8").read()
        m = guid_re.search(text)
        if not m:
            fail(f"[guid] guid 없음: {rel(meta)}")
            continue
        g = m.group(1)
        if g in seen:
            fail(f"[guid] GUID 중복: {g} — {rel(seen[g])} / {rel(meta)}")
        else:
            seen[g] = meta


# ③ asmdef 그래프 == 기대 그래프 (계약 하드코딩)
EXPECTED_GRAPH = {
    "UPA.Core": set(),
    "UPA.Samples": set(),
    "UPA.Runtime": {"UPA.Core"},
    "UPA.Editor": {"UPA.Core", "UPA.Runtime", "UPA.Samples"},
    "UPA.Tests.Core": {"UPA.Core"},
    "UPA.Tests.Editor": {"UPA.Editor", "UPA.Core", "UPA.Runtime"},
}
TEST_RUNNER_REFS = {"UnityEngine.TestRunner", "UnityEditor.TestRunner"}


def check_asmdef_graph():
    found = {}
    for path in glob.glob(os.path.join(ASSETS, "**", "*.asmdef"), recursive=True):
        try:
            data = json.load(open(path, encoding="utf-8"))
        except ValueError as e:
            fail(f"[asmdef] JSON 파싱 실패: {rel(path)} — {e}")
            continue
        found[data.get("name", "?")] = (path, data)
    for name, refs in EXPECTED_GRAPH.items():
        if name not in found:
            fail(f"[asmdef] 누락된 어셈블리: {name}")
            continue
        path, data = found[name]
        actual = set(data.get("references", [])) - TEST_RUNNER_REFS
        if actual != refs:
            fail(f"[asmdef] {name} 참조 불일치: 실제 {sorted(actual)} != 기대 {sorted(refs)} ({rel(path)})")
    if "UPA.Core" in found and not found["UPA.Core"][1].get("noEngineReferences", False):
        fail("[asmdef] UPA.Core는 noEngineReferences: true 필수")
    extra = set(found) - set(EXPECTED_GRAPH)
    if extra:
        fail(f"[asmdef] 기대 그래프에 없는 어셈블리: {sorted(extra)}")


# ④ 문서 허구 리터럴 grep + 표 수치 라벨 규칙
FICTION_RE = re.compile(
    r"12\.45|0\.42\s*ms|2,?864|38\.5|0\.8\s*ms|(?<!\d)214(?!\d)|42\.3|2\.1\s*ms|320\s*KB|12\.4\s*KB|0\.1\s*KB"
)
UNIT_NUM_RE = re.compile(r"\d+(?:[.,]\d+)?\s*(?:ms|KB|MB|fps|FPS)\b")
LABELS = ("Artifacts/", "(실측", "projected", "미측정", "문헌", "설계 상수")


def doc_files():
    files = [
        os.path.join(ROOT, "README.md"),
        os.path.join(ROOT, "RESEARCH_REPORT.md"),
        os.path.join(ROOT, "QA_METHODOLOGY.md"),
        os.path.join(ROOT, "DEVICE_TIER_OPTIMIZATION.md"),
    ]
    files += glob.glob(os.path.join(ROOT, "docs", "**", "*.md"), recursive=True)
    return [f for f in files if os.path.isfile(f)]


def check_docs():
    for path in doc_files():
        for i, line in enumerate(open(path, encoding="utf-8"), 1):
            labeled = any(lbl in line for lbl in LABELS)
            if FICTION_RE.search(line) and not labeled:
                fail(f"[docs] 허구 측정 수치 의심 리터럴: {rel(path)}:{i}: {line.strip()[:80]}")
            # 마크다운 표 행: 단위 붙은 수치 → 라벨/Artifacts 경로 필수
            if line.lstrip().startswith("|") and UNIT_NUM_RE.search(line) and not labeled:
                fail(f"[docs] 표 수치에 출처 라벨 없음(실측/projected/미측정/문헌/Artifacts): {rel(path)}:{i}: {line.strip()[:80]}")


# ⑤ ci csproj·asmdef·package.json 유효성
def check_syntax():
    for path in glob.glob(os.path.join(ROOT, "ci", "*.csproj")):
        try:
            ET.parse(path)
        except ET.ParseError as e:
            fail(f"[syntax] XML 파싱 실패: {rel(path)} — {e}")
    json_targets = glob.glob(os.path.join(ASSETS, "**", "*.asmdef"), recursive=True)
    json_targets.append(os.path.join(ASSETS, "UnityPerformanceAlchemist", "package.json"))
    for path in json_targets:
        if not os.path.isfile(path):
            fail(f"[syntax] 파일 없음: {rel(path)}")
            continue
        try:
            json.load(open(path, encoding="utf-8"))
        except ValueError as e:
            fail(f"[syntax] JSON 파싱 실패: {rel(path)} — {e}")


# ⑥ Core 순수성: using UnityEngine / Newtonsoft 금지
def check_core_purity():
    for path in glob.glob(os.path.join(CORE_DIR, "**", "*.cs"), recursive=True):
        text = open(path, encoding="utf-8").read()
        if "using UnityEngine" in text:
            fail(f"[core] Core에 using UnityEngine 발견: {rel(path)}")
        if "Newtonsoft" in text:
            fail(f"[core] Core에 Newtonsoft 발견: {rel(path)}")


def main():
    check_meta_presence()
    check_guid_duplicates()
    check_asmdef_graph()
    check_docs()
    check_syntax()
    check_core_purity()
    if failures:
        print(f"FAIL — {len(failures)}건:")
        for f in failures:
            print("  " + f)
        return 1
    print("OK — 모든 검사 통과 (meta/guid/asmdef/docs/syntax/core-purity)")
    return 0


if __name__ == "__main__":
    sys.exit(main())
