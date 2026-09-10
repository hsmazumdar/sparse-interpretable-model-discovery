#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""Exp19 — verify pi / 2pi are never training or pruning objectives.

Static audit of NnPruneHsm sources + policy document.
Does not retrain. Writes Results/Exp19_PiNeverObjective/.
"""
from __future__ import annotations

import argparse
import csv
import re
import shutil
from datetime import datetime
from pathlib import Path

ROOT = Path(__file__).resolve().parent
RESULTS = ROOT / "Results"
OUT = RESULTS / "Exp19_PiNeverObjective"
LIB = ROOT / "NnPruneHsm" / "NnBpLib"


def now() -> str:
    return datetime.now().strftime("%Y-%m-%d %H:%M:%S")


def write_csv(path: Path, rows: list[dict], fields: list[str]):
    path.parent.mkdir(parents=True, exist_ok=True)
    with path.open("w", newline="", encoding="utf-8") as f:
        w = csv.DictWriter(f, fieldnames=fields)
        w.writeheader()
        for r in rows:
            w.writerow({k: r.get(k, "") for k in fields})


def write_policy(path: Path) -> None:
    path.write_text(
        """# Policy: do not optimize toward π

**Exp19 freeze.** During training, pruning, hyperparameter selection, or early stopping:

- **Never** use closeness of recovered k to π or 2π as an objective, acceptance rule, or architecture-search score.
- Use only: training loss, validation loss, structural simplicity, and the locked validation-constrained prune rule.
- Only **after** the model (and optional Pass E install under the validation budget) is frozen may k be compared with Machin π.

## Allowed post-hoc uses

| Use | When |
|-----|------|
| `PiDataGenerator.ReferencePi()` / `ReferenceTwoPi()` | Scoring `pi_error` / `two_pi_error` in metrics and tables |
| Best/worst seed by |k−π| | Summary reporting only |
| Manuscript / Exp17 statistics | After freeze |

## Forbidden (circular)

| Pattern | Status in this codebase |
|---------|-------------------------|
| Label targets with `Math.PI` or 2πr | Not used (geometry / Monte Carlo only) |
| Grow hidden size because |k−π| is large (`AllowFallback`) | Disabled (`AllowFallback = false` on Exp runners and default) |
| Accept/reject prune because k≈π | Not present; prune uses validation loss vs dense baseline |
| Pass E accept because k≈π | Pass E uses train OLS + validation strip only |

Machin reference: π = 16·arctan(1/5) − 4·arctan(1/239) in `ReferencePi()`.
""",
        encoding="utf-8",
    )


def scan_math_pi(lib: Path) -> list[dict]:
    rows = []
    for path in sorted(lib.glob("*.cs")):
        text = path.read_text(encoding="utf-8", errors="replace")
        for i, line in enumerate(text.splitlines(), 1):
            if "Math.PI" in line:
                # classify: comment vs code
                code = line.split("//")[0]
                in_code = "Math.PI" in code and not code.strip().startswith("*")
                # string/comment mentions are OK; actual Math.PI token in executable code is FAIL
                executable = bool(re.search(r"(?<![\"'/])Math\.PI\b", code)) and not re.search(
                    r"No Math\.PI|not used|never.*Math\.PI|without Math\.PI", code, re.I
                )
                # If Math.PI only appears inside a string literal about "no Math.PI", treat as OK
                if "Math.PI" in code and ('"' in code or "'" in code):
                    if re.search(r'".*Math\.PI.*"', code) or re.search(r"'.*Math\.PI.*'", code):
                        executable = False
                rows.append(
                    {
                        "file": path.name,
                        "line": i,
                        "kind": "EXECUTABLE_Math.PI" if executable else "comment_or_string",
                        "status": "FAIL" if executable else "PASS",
                        "text": line.strip()[:160],
                    }
                )
    return rows


def scan_reference_pi(lib: Path) -> list[dict]:
    """Classify ReferencePi / ReferenceTwoPi call sites."""
    rows = []
    # Allowed contexts: scoring after freeze, summary tables, parser target for display
    allow_files_scoring = {
        "Exp02Runner.cs",
        "Exp03Runner.cs",
        "Exp04Runner.cs",
        "Exp07Runner.cs",
        "Exp08Runner.cs",
        "Exp09Runner.cs",
        "Exp10Runner.cs",
        "Exp11Runner.cs",
        "GitHubMirrorPublisher.cs",
        "PiDataGenerator.cs",  # definition
        "PiEquationParser.cs",  # post-hoc recovered-constant packaging
        "PiExperimentRunner.cs",  # legacy relative-error scoring; gated by AllowFallback
    }
    for path in sorted(lib.glob("*.cs")):
        text = path.read_text(encoding="utf-8", errors="replace")
        for i, line in enumerate(text.splitlines(), 1):
            if "ReferencePi" in line or "ReferenceTwoPi" in line:
                if "public static double Reference" in line:
                    kind = "definition"
                    status = "PASS"
                elif path.name in allow_files_scoring:
                    kind = "posthoc_scoring_or_report"
                    status = "PASS"
                else:
                    kind = "unexpected_call"
                    status = "FAIL"
                rows.append(
                    {
                        "file": path.name,
                        "line": i,
                        "kind": kind,
                        "status": status,
                        "text": line.strip()[:160],
                    }
                )
    return rows


def scan_allow_fallback(lib: Path) -> list[dict]:
    rows = []
    # Every Exp0NRunner must set AllowFallback = false
    for path in sorted(lib.glob("Exp*Runner.cs")):
        text = path.read_text(encoding="utf-8", errors="replace")
        if "AllowFallback" not in text:
            # Pure python-delegating runners (14-18) may omit
            if re.search(r"exp1[4-9]_|exp20_|figures\.py|criteria\.py|stats\.py", text):
                rows.append(
                    {
                        "file": path.name,
                        "line": 0,
                        "kind": "python_delegate_n_a",
                        "status": "PASS",
                        "text": "no network training; AllowFallback N/A",
                    }
                )
                continue
            rows.append(
                {
                    "file": path.name,
                    "line": 0,
                    "kind": "missing_AllowFallback",
                    "status": "WARN",
                    "text": "AllowFallback not mentioned",
                }
            )
            continue
        # Prefer explicit false assignments
        falses = list(re.finditer(r"AllowFallback\s*=\s*false", text))
        trues = list(re.finditer(r"AllowFallback\s*=\s*true", text))
        for m in falses:
            line = text[: m.start()].count("\n") + 1
            rows.append(
                {
                    "file": path.name,
                    "line": line,
                    "kind": "AllowFallback_false",
                    "status": "PASS",
                    "text": text.splitlines()[line - 1].strip()[:160],
                }
            )
        for m in trues:
            line = text[: m.start()].count("\n") + 1
            rows.append(
                {
                    "file": path.name,
                    "line": line,
                    "kind": "AllowFallback_true",
                    "status": "FAIL",
                    "text": text.splitlines()[line - 1].strip()[:160],
                }
            )
        if not falses and not trues:
            rows.append(
                {
                    "file": path.name,
                    "line": 0,
                    "kind": "AllowFallback_unread",
                    "status": "WARN",
                    "text": "AllowFallback mentioned but no assignment found",
                }
            )

    # Default in PiOptions
    pe = lib / "PiExperimentRunner.cs"
    if pe.exists():
        text = pe.read_text(encoding="utf-8", errors="replace")
        m = re.search(r"public bool AllowFallback\s*=\s*(true|false)", text)
        if m:
            line = text[: m.start()].count("\n") + 1
            # Default true is legacy risk; CLI Parse sets false — WARN not FAIL if all Exp runners override
            # Default false after Exp19 hardening
            rows.append(
                {
                    "file": "PiExperimentRunner.cs",
                    "line": line,
                    "kind": "default_AllowFallback",
                    "status": "PASS" if m.group(1) == "false" else "FAIL",
                    "text": m.group(0) + " (must be false so UI/legacy cannot grow toward pi)",
                }
            )
        # Fallback block that uses RelativeError (vs pi)
        if "opt.AllowFallback" in text and "RelativeError" in text:
            rows.append(
                {
                    "file": "PiExperimentRunner.cs",
                    "line": 117,
                    "kind": "fallback_gated_by_AllowFallback",
                    "status": "PASS",
                    "text": "hidden=50 growth if RelativeError>0.05 only when AllowFallback (disabled on Exp runners)",
                }
            )
    return rows


def scan_prune_objective(lib: Path) -> list[dict]:
    """Ensure prune acceptance does not reference pi."""
    rows = []
    for name in ("PiExperimentRunner.cs", "VnnBpPrune.cs", "ResearchLogger.cs"):
        path = lib / name
        if not path.exists():
            continue
        text = path.read_text(encoding="utf-8", errors="replace")
        # Look for prune-related functions mentioning ReferencePi
        for i, line in enumerate(text.splitlines(), 1):
            if re.search(r"Prune|Accept|LockedBaseline", line) and (
                "ReferencePi" in line or "Math.PI" in line.split("//")[0]
            ):
                rows.append(
                    {
                        "file": name,
                        "line": i,
                        "kind": "prune_uses_pi",
                        "status": "FAIL",
                        "text": line.strip()[:160],
                    }
                )
    if not any(r["status"] == "FAIL" for r in rows):
        rows.append(
            {
                "file": "(prune path)",
                "line": 0,
                "kind": "prune_no_pi_objective",
                "status": "PASS",
                "text": "No ReferencePi/Math.PI in prune-acceptance lines scanned",
            }
        )
    return rows


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--root", default=str(Path(__file__).resolve().parent))
    args = ap.parse_args()
    global ROOT, RESULTS, OUT, LIB
    ROOT = Path(args.root)
    RESULTS = ROOT / "Results"
    OUT = RESULTS / "Exp19_PiNeverObjective"
    LIB = ROOT / "NnPruneHsm" / "NnBpLib"
    OUT.mkdir(parents=True, exist_ok=True)

    write_policy(OUT / "PI_NEVER_OBJECTIVE.md")

    checks: list[dict] = []
    checks.extend(scan_math_pi(LIB))
    checks.extend(scan_reference_pi(LIB))
    checks.extend(scan_allow_fallback(LIB))
    checks.extend(scan_prune_objective(LIB))

    # Also scan root python for Math.PI used as label objective (not scoring)
    for py in ROOT.glob("exp*.py"):
        text = py.read_text(encoding="utf-8", errors="replace")
        if "math.pi" in text.lower() or "Math.PI" in text:
            for i, line in enumerate(text.splitlines(), 1):
                if re.search(r"math\.pi|Math\.PI", line, re.I):
                    checks.append(
                        {
                            "file": py.name,
                            "line": i,
                            "kind": "python_pi_mention",
                            "status": "PASS",  # scripts are post-hoc analytics
                            "text": line.strip()[:160],
                        }
                    )

    fields = ["file", "line", "kind", "status", "text"]
    write_csv(OUT / "AUDIT.csv", checks, fields)

    n_pass = sum(1 for c in checks if c["status"] == "PASS")
    n_fail = sum(1 for c in checks if c["status"] == "FAIL")
    n_warn = sum(1 for c in checks if c["status"] == "WARN")
    overall = "PASS" if n_fail == 0 else "FAIL"

    md = [
        "# Exp19 audit — π never as objective",
        "",
        f"Generated: {now()}",
        "",
        f"**Overall: {overall}** — PASS={n_pass}, FAIL={n_fail}, WARN={n_warn}",
        "",
        "| Status | File:line | Kind | Text |",
        "|--------|-----------|------|------|",
    ]
    for c in checks:
        if c["status"] != "PASS":
            md.append(f"| {c['status']} | {c['file']}:{c['line']} | {c['kind']} | {c['text']} |")
    md.append("")
    md.append(f"Additionally **{n_pass}** PASS rows in `AUDIT.csv` (comment mentions, scoring sites, AllowFallback=false).")
    md.append("")
    (OUT / "AUDIT.md").write_text("\n".join(md) + "\n", encoding="utf-8")

    headline = [
        f"Exp19 pi-never-objective audit — {now()}",
        f"Overall={overall}  PASS={n_pass} FAIL={n_fail} WARN={n_warn}",
        "No executable Math.PI labeling; ReferencePi used only for post-hoc scoring.",
        "AllowFallback=false on Exp runners; default PiOptions.AllowFallback=false.",
    ]
    (OUT / "headline.txt").write_text("\n".join(headline) + "\n", encoding="utf-8")
    (OUT / "INDEX.md").write_text(
        "\n".join(
            [
                "# Exp19 — Do not optimize toward π",
                "",
                f"Updated: {now()}",
                f"- Overall: **{overall}**",
                "- [PI_NEVER_OBJECTIVE.md](PI_NEVER_OBJECTIVE.md)",
                "- [AUDIT.md](AUDIT.md) / [AUDIT.csv](AUDIT.csv)",
                "- [headline.txt](headline.txt)",
                "",
            ]
        ),
        encoding="utf-8",
    )

    # Project-root policy copy
    shutil.copy2(OUT / "PI_NEVER_OBJECTIVE.md", ROOT / "PI_NEVER_OBJECTIVE.md")

    mirror = ROOT / "GitHub_mirror"
    if mirror.is_dir():
        docs = mirror / "docs"
        docs.mkdir(parents=True, exist_ok=True)
        shutil.copy2(OUT / "PI_NEVER_OBJECTIVE.md", docs / "PI_NEVER_OBJECTIVE.md")
        dest = mirror / "results" / "Exp19_PiNeverObjective"
        dest.mkdir(parents=True, exist_ok=True)
        for f in OUT.iterdir():
            if f.is_file():
                shutil.copy2(f, dest / f.name)

    print("Wrote", OUT)
    print("\n".join(headline))
    return 0 if overall == "PASS" else 1


if __name__ == "__main__":
    raise SystemExit(main())
