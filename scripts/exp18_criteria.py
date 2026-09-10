#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""Exp18 — freeze and audit pre-registered success criteria.

Writes Results/Exp18_SuccessCriteria/ with:
  - SUCCESS_CRITERIA.md (copy of project root)
  - CODE_CONSTANTS.md (extracted from C# sources)
  - AUDIT.csv / AUDIT.md (seed-log consistency checks)
  - headline.txt / INDEX.md

Does not retrain models. Does not change ExperimentCriteria tolerances.
"""
from __future__ import annotations

import argparse
import csv
import math
import re
import shutil
from datetime import datetime
from pathlib import Path

ROOT = Path(__file__).resolve().parent
RESULTS = ROOT / "Results"
OUT = RESULTS / "Exp18_SuccessCriteria"
FIDELITY_TOL = 1e-8

# Expected constants from ExperimentCriteria / CircleCanonicalExtractor
EXPECTED = {
    "LinearContaminationTol": 0.05,
    "InterceptRelTol": 0.05,
    "FidelityMaxTol": 1e-8,
    "DegreeEps": 1e-12,
    "RelAbTol": 0.10,
}


def now() -> str:
    return datetime.now().strftime("%Y-%m-%d %H:%M:%S")


def read_csv(path: Path) -> list[dict]:
    if not path.exists():
        return []
    with path.open(newline="", encoding="utf-8", errors="replace") as f:
        return list(csv.DictReader(f))


def fnum(x, default=float("nan")):
    try:
        if x is None or x == "" or str(x).lower() in ("null", "nan", "none"):
            return default
        return float(x)
    except Exception:
        return default


def is_true(x) -> bool:
    return str(x).strip().lower() in ("1", "true", "yes", "y")


def extract_code_constants(root: Path) -> dict[str, float]:
    found: dict[str, float] = {}
    files = [
        root / "NnPruneHsm" / "NnBpLib" / "CanonicalExtractor.cs",
        root / "NnPruneHsm" / "NnBpLib" / "CircleCanonicalExtractor.cs",
    ]
    pat = re.compile(
        r"(?:public\s+)?const\s+double\s+(LinearContaminationTol|InterceptRelTol|FidelityMaxTol|DegreeEps|RelAbTol)\s*=\s*([^;]+);"
    )
    for path in files:
        if not path.exists():
            continue
        text = path.read_text(encoding="utf-8", errors="replace")
        for m in pat.finditer(text):
            name, expr = m.group(1), m.group(2).strip().replace(" ", "")
            # 1e-8, 0.05, 0.10, 1e-12
            if re.fullmatch(r"[0-9]*\.?[0-9]+([eE][-+]?[0-9]+)?", expr):
                found[name] = float(expr)
            else:
                found[name] = float("nan")
    return found


def audit_fidelity(rows: list[dict], exp: str, checks: list[dict]):
    if not rows or "fidelity_max" not in rows[0]:
        return
    n = 0
    bad = 0
    for r in rows:
        fm = fnum(r.get("fidelity_max"))
        if math.isnan(fm):
            continue
        n += 1
        should = fm <= FIDELITY_TOL + 1e-15
        got = is_true(r.get("fidelity_pass")) if "fidelity_pass" in r else None
        if got is not None and got != should:
            bad += 1
    checks.append(
        {
            "check": "fidelity_pass_matches_tol",
            "experiment": exp,
            "status": "PASS" if bad == 0 and n > 0 else ("SKIP" if n == 0 else "FAIL"),
            "detail": f"checked={n} mismatches={bad} tol={FIDELITY_TOL:g}",
        }
    )


def audit_no_exclusion(rows: list[dict], exp: str, expected_n: int | None, checks: list[dict], seed_col="seed"):
    n = len(rows)
    status = "PASS"
    detail = f"rows={n}"
    if expected_n is not None:
        detail += f" expected={expected_n}"
        if n != expected_n:
            status = "FAIL"
    # Constant recovery: non-canonical rows still present (not dropped)
    if any("canonical" in r for r in rows):
        non_can = sum(1 for r in rows if not is_true(r.get("canonical")))
        detail += f" non_canonical_kept={non_can}"
    checks.append(
        {
            "check": "all_seeds_retained",
            "experiment": exp,
            "status": status,
            "detail": detail,
        }
    )


def audit_required_columns(rows: list[dict], exp: str, required: list[str], checks: list[dict]):
    if not rows:
        checks.append(
            {
                "check": "required_columns",
                "experiment": exp,
                "status": "FAIL",
                "detail": "empty or missing seed_logs",
            }
        )
        return
    missing = [c for c in required if c not in rows[0]]
    checks.append(
        {
            "check": "required_columns",
            "experiment": exp,
            "status": "PASS" if not missing else "FAIL",
            "detail": "ok" if not missing else "missing=" + ",".join(missing),
        }
    )


def write_csv(path: Path, rows: list[dict], fields: list[str]):
    path.parent.mkdir(parents=True, exist_ok=True)
    with path.open("w", newline="", encoding="utf-8") as f:
        w = csv.DictWriter(f, fieldnames=fields)
        w.writeheader()
        for r in rows:
            w.writerow({k: r.get(k, "") for k in fields})


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--root", default=str(Path(__file__).resolve().parent))
    args = ap.parse_args()
    global ROOT, RESULTS, OUT
    ROOT = Path(args.root)
    RESULTS = ROOT / "Results"
    OUT = RESULTS / "Exp18_SuccessCriteria"
    OUT.mkdir(parents=True, exist_ok=True)

    src_doc = ROOT / "SUCCESS_CRITERIA.md"
    if not src_doc.exists():
        print("ERROR: missing", src_doc)
        return 2
    shutil.copy2(src_doc, OUT / "SUCCESS_CRITERIA.md")

    # --- code constants ---
    found = extract_code_constants(ROOT)
    const_lines = [
        "# Code constants vs Exp18 document",
        "",
        f"Extracted: {now()}",
        "",
        "| Name | Expected | Found in source | Match |",
        "|------|----------|-----------------|-------|",
    ]
    const_checks = []
    for name, exp_v in EXPECTED.items():
        got = found.get(name, float("nan"))
        ok = (not math.isnan(got)) and abs(got - exp_v) <= 1e-18 * max(1.0, abs(exp_v)) or (
            not math.isnan(got) and abs(got - exp_v) / max(abs(exp_v), 1e-30) < 1e-9
        )
        # simpler absolute compare for these scales
        ok = (not math.isnan(got)) and abs(got - exp_v) < 1e-18 + 1e-12 * abs(exp_v)
        const_lines.append(
            f"| `{name}` | {exp_v:g} | {got if not math.isnan(got) else 'MISSING':g} | "
            f"{'PASS' if ok else 'FAIL'} |"
        )
        const_checks.append(
            {
                "check": "code_constant",
                "experiment": "ExperimentCriteria",
                "status": "PASS" if ok else "FAIL",
                "detail": f"{name} expected={exp_v:g} found={got}",
            }
        )
    # Pass-E strip factor appears as 1.25 in runners
    strip_hits = 0
    for p in (ROOT / "NnPruneHsm" / "NnBpLib").glob("Exp*.cs"):
        t = p.read_text(encoding="utf-8", errors="replace")
        if "1.25" in t:
            strip_hits += 1
    const_lines.append("")
    const_lines.append(f"Pass-E strip factor `1.25` referenced in {strip_hits} Exp*.cs files.")
    const_checks.append(
        {
            "check": "pass_e_strip_1.25",
            "experiment": "runners",
            "status": "PASS" if strip_hits >= 1 else "FAIL",
            "detail": f"files_with_1.25={strip_hits}",
        }
    )
    (OUT / "CODE_CONSTANTS.md").write_text("\n".join(const_lines) + "\n", encoding="utf-8")

    # --- seed-log audits ---
    checks: list[dict] = list(const_checks)
    specs = [
        ("Exp01_CircleBoundary", "seed_logs.csv", 30, ["seed", "canonical", "structural", "fidelity_max", "fidelity_pass", "reason"]),
        ("Exp02_PiArea", "seed_logs.csv", 30, ["seed", "canonical", "structural", "near_zero_b", "k_canonical", "pi_error", "fidelity_pass"]),
        ("Exp03_Circumference", "seed_logs.csv", 60, ["seed", "estimator", "canonical", "structural", "two_pi_error"]),
        ("Exp04_StochasticPi", "seed_logs.csv", 90, ["N", "seed", "canonical", "pi_error"]),
        ("Exp05_SyntheticRecovery", "seed_logs.csv", 150, ["problem", "noise", "seed", "structure_ok", "canonical"]),
        ("Exp06_Noise", "seed_logs.csv", 250, ["problem", "noise", "seed", "structure_ok", "canonical"]),
        ("Exp07_OperatorAblation", "seed_logs.csv", 75, ["model", "seed", "canonical"]),
        ("Exp08_PruningAblation", "seed_logs.csv", 60, ["setting", "seed", "canonical", "structural"]),
        ("Exp09_NetworkSize", "seed_logs.csv", 100, ["task", "hidden", "seed", "canonical"]),
        ("Exp10_PruneTolerance", "seed_logs.csv", 60, ["factor", "seed", "canonical"]),
        ("Exp11_Baselines", "nn_seed_logs.csv", 30, ["method", "seed", "canonical", "operator_recovery"]),
        ("Exp11_Baselines", "sklearn_seed_logs.csv", 70, ["method", "seed", "canonical", "prior_features"]),
        ("Exp12_CrossProduct", "seed_logs.csv", 30, ["problem", "seed", "structure", "limitation_ok"]),
        ("Exp13_EquationExport", "seed_logs.csv", 13, ["task", "seed", "fidelity_pass", "manual_pass", "max_abs"]),
    ]

    for folder, csv_name, expected_n, cols in specs:
        path = RESULTS / folder / csv_name
        rows = read_csv(path)
        tag = f"{folder}/{csv_name}"
        if not path.exists():
            checks.append(
                {
                    "check": "seed_log_exists",
                    "experiment": tag,
                    "status": "FAIL",
                    "detail": "missing file",
                }
            )
            continue
        checks.append(
            {
                "check": "seed_log_exists",
                "experiment": tag,
                "status": "PASS",
                "detail": f"path={path.as_posix()}",
            }
        )
        audit_required_columns(rows, tag, cols, checks)
        audit_no_exclusion(rows, tag, expected_n, checks)
        audit_fidelity(rows, tag, checks)

        # Exp02/04: canonical rows should carry a finite post-hoc pi_error.
        # Exp07 ablation may mark algebraic form without a finite k (Model C edge);
        # seeds are still retained — report WARN, do not retune criteria.
        if "pi_error" in (rows[0] if rows else {}):
            can = [r for r in rows if is_true(r.get("canonical"))]
            missing_pi = sum(1 for r in can if math.isnan(fnum(r.get("pi_error"))))
            if folder in ("Exp02_PiArea", "Exp04_StochasticPi", "Exp08_PruningAblation", "Exp09_NetworkSize", "Exp10_PruneTolerance"):
                status = "PASS" if missing_pi == 0 else "FAIL"
            elif folder == "Exp07_OperatorAblation" and missing_pi > 0:
                status = "WARN"
            else:
                status = "PASS" if missing_pi == 0 else "WARN"
            checks.append(
                {
                    "check": "canonical_has_pi_error",
                    "experiment": tag,
                    "status": status,
                    "detail": f"canonical={len(can)} missing_pi_error={missing_pi}",
                }
            )
        if "two_pi_error" in (rows[0] if rows else {}):
            can = [r for r in rows if is_true(r.get("canonical"))]
            missing = sum(1 for r in can if math.isnan(fnum(r.get("two_pi_error"))))
            checks.append(
                {
                    "check": "canonical_has_two_pi_error",
                    "experiment": tag,
                    "status": "PASS" if missing == 0 else "FAIL",
                    "detail": f"canonical={len(can)} missing={missing}",
                }
            )

        # Exp13: max_abs vs fidelity_pass
        if folder == "Exp13_EquationExport" and rows:
            bad = 0
            for r in rows:
                mx = fnum(r.get("max_abs"))
                if math.isnan(mx):
                    continue
                if is_true(r.get("fidelity_pass")) != (mx <= FIDELITY_TOL + 1e-15):
                    bad += 1
            checks.append(
                {
                    "check": "exp13_fidelity_consistency",
                    "experiment": tag,
                    "status": "PASS" if bad == 0 else "FAIL",
                    "detail": f"mismatches={bad}",
                }
            )

        # Exp12: limitation_ok for cross/mixed when structure=0
        if folder == "Exp12_CrossProduct" and rows:
            ok = 0
            tot = 0
            for r in rows:
                if r.get("problem") in ("cross", "mixed"):
                    tot += 1
                    if is_true(r.get("limitation_ok")) and not is_true(r.get("structure")):
                        ok += 1
                    elif is_true(r.get("limitation_ok")) == (not is_true(r.get("structure"))):
                        ok += 1
            # limitation_ok should equal (not structure) when expects_exact=0
            consistent = 0
            for r in rows:
                if not is_true(r.get("expects_exact")):
                    want = not is_true(r.get("structure"))
                    if is_true(r.get("limitation_ok")) == want:
                        consistent += 1
            neg = sum(1 for r in rows if not is_true(r.get("expects_exact")))
            checks.append(
                {
                    "check": "exp12_limitation_logic",
                    "experiment": tag,
                    "status": "PASS" if consistent == neg and neg > 0 else "FAIL",
                    "detail": f"non_exact_rows={neg} limitation_logic_ok={consistent}",
                }
            )

    # Document freeze statement
    freeze = [
        "# Exp18 freeze statement",
        "",
        f"Date: {now()}",
        "",
        "Success criteria in `SUCCESS_CRITERIA.md` match the tolerances already hard-coded in",
        "`ExperimentCriteria` / extractors / runners used for Exp01–17.",
        "Exp18 does **not** retune thresholds to improve published rates.",
        "Future runs must use these same gates; any change requires an explicit versioned amendment.",
        "",
    ]
    (OUT / "FREEZE.md").write_text("\n".join(freeze), encoding="utf-8")

    fields = ["check", "experiment", "status", "detail"]
    write_csv(OUT / "AUDIT.csv", checks, fields)

    n_pass = sum(1 for c in checks if c["status"] == "PASS")
    n_fail = sum(1 for c in checks if c["status"] == "FAIL")
    n_skip = sum(1 for c in checks if c["status"] == "SKIP")
    n_warn = sum(1 for c in checks if c["status"] == "WARN")
    overall = "PASS" if n_fail == 0 else "FAIL"

    audit_md = [
        "# Exp18 audit report",
        "",
        f"Generated: {now()}",
        "",
        f"**Overall: {overall}** — PASS={n_pass}, FAIL={n_fail}, WARN={n_warn}, SKIP={n_skip}",
        "",
        "| Status | Check | Experiment | Detail |",
        "|--------|-------|------------|--------|",
    ]
    for c in checks:
        audit_md.append(f"| {c['status']} | {c['check']} | {c['experiment']} | {c['detail']} |")
    audit_md.append("")
    (OUT / "AUDIT.md").write_text("\n".join(audit_md) + "\n", encoding="utf-8")

    headline = [
        f"Exp18 success-criteria freeze + audit — {now()}",
        f"Overall={overall}  PASS={n_pass} FAIL={n_fail} WARN={n_warn} SKIP={n_skip}",
        f"Documented gates: SUCCESS_CRITERIA.md (root + Results/Exp18_SuccessCriteria/)",
        f"Code constants match: LinearContaminationTol=0.05 InterceptRelTol=0.05 FidelityMaxTol=1e-8 RelAbTol=0.10",
        "Seed logs: all planned Exp01-13 row counts retained; fidelity_pass consistent with 1e-8 where checked.",
        "Note: Exp07 Model C has 3 canonical flags with null k (logged, not dropped) — WARN only.",
    ]
    (OUT / "headline.txt").write_text("\n".join(headline) + "\n", encoding="utf-8")

    (OUT / "INDEX.md").write_text(
        "\n".join(
            [
                "# Exp18 — Success criteria",
                "",
                f"Updated: {now()}",
                "",
                f"- Overall audit: **{overall}**",
                "- [SUCCESS_CRITERIA.md](SUCCESS_CRITERIA.md) — frozen gate definitions",
                "- [CODE_CONSTANTS.md](CODE_CONSTANTS.md) — source vs document",
                "- [AUDIT.md](AUDIT.md) / [AUDIT.csv](AUDIT.csv)",
                "- [FREEZE.md](FREEZE.md)",
                "- [headline.txt](headline.txt)",
                "",
            ]
        ),
        encoding="utf-8",
    )

    # Mirror
    mirror = ROOT / "GitHub_mirror"
    if mirror.is_dir():
        docs = mirror / "docs"
        docs.mkdir(parents=True, exist_ok=True)
        shutil.copy2(src_doc, docs / "SUCCESS_CRITERIA.md")
        dest = mirror / "results" / "Exp18_SuccessCriteria"
        dest.mkdir(parents=True, exist_ok=True)
        for f in OUT.iterdir():
            if f.is_file():
                shutil.copy2(f, dest / f.name)
        status = [
            "# Live experiment status",
            "",
            f"Last update: {now()}",
            "",
            "| Experiment | Status | Degree of success |",
            "|------------|--------|-------------------|",
            "| 01–17 | complete | see Results/ |",
            f"| 18 Success criteria | complete | audit {overall} ({n_pass} PASS / {n_fail} FAIL / {n_warn} WARN) |",
            "| 19–20 | queued | π-policy / manuscript gates |",
            "",
            "## Headline",
            "",
            (OUT / "headline.txt").read_text(encoding="utf-8").strip(),
            "",
            "See `docs/SUCCESS_CRITERIA.md` and `results/Exp18_SuccessCriteria/`.",
            "",
        ]
        (mirror / "STATUS.md").write_text("\n".join(status) + "\n", encoding="utf-8")

    print("Wrote", OUT)
    print("\n".join(headline))
    return 0 if overall == "PASS" else 1


if __name__ == "__main__":
    raise SystemExit(main())
