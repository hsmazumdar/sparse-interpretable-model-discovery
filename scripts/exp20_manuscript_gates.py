#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""Exp20 — manuscript rewrite gates checklist.

Verifies minimum gates from RemainingToDoExperiments against Results/.
Does not rewrite the manuscript. Writes Results/Exp20_ManuscriptGates/.
"""
from __future__ import annotations

import argparse
import csv
import math
import shutil
from datetime import datetime
from pathlib import Path

ROOT = Path(__file__).resolve().parent
RESULTS = ROOT / "Results"
OUT = RESULTS / "Exp20_ManuscriptGates"


def now() -> str:
    return datetime.now().strftime("%Y-%m-%d %H:%M:%S")


def read_csv(path: Path) -> list[dict]:
    if not path.exists():
        return []
    with path.open(newline="", encoding="utf-8", errors="replace") as f:
        return list(csv.DictReader(f))


def is_true(x) -> bool:
    return str(x).strip().lower() in ("1", "true", "yes", "y")


def rel_evidence(p) -> str:
    """Portable path for GATES.csv (no drive-letter leak). Leave already-relative notes alone."""
    s = str(p).strip()
    if not s:
        return s
    if ":" not in s[:3] and "\\" not in s:
        return s.replace("\\", "/")
    path = Path(s)
    try:
        return path.relative_to(ROOT).as_posix()
    except ValueError:
        marker = "MathModelNnPaper/"
        t = s.replace("\\", "/")
        i = t.lower().find(marker.lower())
        if i >= 0:
            return t[i + len(marker) :].lstrip("/")
        return s.replace("\\", "/")


def write_csv(path: Path, rows: list[dict], fields: list[str]):
    path.parent.mkdir(parents=True, exist_ok=True)
    with path.open("w", newline="", encoding="utf-8") as f:
        w = csv.DictWriter(f, fieldnames=fields)
        w.writeheader()
        for r in rows:
            w.writerow({k: r.get(k, "") for k in fields})


def gate(checks: list[dict], name: str, status: str, detail: str, evidence: str = ""):
    checks.append(
        {
            "gate": name,
            "status": status,
            "detail": detail,
            "evidence": evidence,
        }
    )


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--root", default=str(Path(__file__).resolve().parent))
    args = ap.parse_args()
    global ROOT, RESULTS, OUT
    ROOT = Path(args.root)
    RESULTS = ROOT / "Results"
    OUT = RESULTS / "Exp20_ManuscriptGates"
    OUT.mkdir(parents=True, exist_ok=True)

    checks: list[dict] = []

    # --- Gate 1: 30-seed circle ---
    p = RESULTS / "Exp01_CircleBoundary" / "seed_logs.csv"
    rows = read_csv(p)
    n = len(rows)
    can = sum(1 for r in rows if is_true(r.get("canonical")))
    ok = n >= 30 and can >= 30
    gate(
        checks,
        "30-seed circle experiment complete",
        "PASS" if ok else "FAIL",
        f"n={n} canonical={can}/30",
        str(p),
    )

    # --- Gate 2: 30-seed canonical area ---
    p = RESULTS / "Exp02_PiArea" / "seed_logs.csv"
    rows = read_csv(p)
    n = len(rows)
    can = sum(1 for r in rows if is_true(r.get("canonical")))
    ok = n >= 30 and can >= 30
    gate(
        checks,
        "30-seed canonical area experiment complete",
        "PASS" if ok else "FAIL",
        f"n={n} canonical={can}/30",
        str(p),
    )

    # --- Gate 3: equation export verified ---
    p = RESULTS / "Exp13_EquationExport" / "seed_logs.csv"
    rows = read_csv(p)
    n = len(rows)
    fid = sum(1 for r in rows if is_true(r.get("fidelity_pass")))
    man = sum(1 for r in rows if is_true(r.get("manual_pass")))
    ok = n >= 1 and fid == n and man == n
    gate(
        checks,
        "equation export verified",
        "PASS" if ok else "FAIL",
        f"n={n} fidelity_pass={fid} manual_pass={man}",
        str(p),
    )

    # --- Gate 4: stochastic convergence ---
    p = RESULTS / "Exp04_StochasticPi" / "seed_logs.csv"
    rows = read_csv(p)
    depths = sorted({r.get("N") for r in rows})
    can = sum(1 for r in rows if is_true(r.get("canonical")))
    ok = len(rows) >= 30 and len(depths) >= 3
    gate(
        checks,
        "stochastic convergence experiment complete",
        "PASS" if ok else "FAIL",
        f"rows={len(rows)} depths={len(depths)} ({','.join(depths)}) canonical={can}",
        str(p),
    )

    # --- Gate 5: operator ablation ---
    p = RESULTS / "Exp07_OperatorAblation" / "seed_logs.csv"
    rows = read_csv(p)
    models = sorted({r.get("model") for r in rows})
    ok = len(rows) >= 50 and len(models) >= 4
    gate(
        checks,
        "operator ablation complete",
        "PASS" if ok else "FAIL",
        f"rows={len(rows)} models={models}",
        str(p),
    )

    # --- Gate 6: pruning ablation ---
    p = RESULTS / "Exp08_PruningAblation" / "seed_logs.csv"
    rows = read_csv(p)
    settings = sorted({r.get("setting") for r in rows})
    ok = len(rows) >= 40 and len(settings) >= 4
    gate(
        checks,
        "pruning ablation complete",
        "PASS" if ok else "FAIL",
        f"rows={len(rows)} settings={settings}",
        str(p),
    )

    # --- Gate 7: at least four synthetic equations ---
    p = RESULTS / "Exp05_SyntheticRecovery" / "seed_logs.csv"
    rows = read_csv(p)
    problems = sorted({r.get("problem") for r in rows})
    ok = len(problems) >= 4
    gate(
        checks,
        "at least four synthetic equations complete",
        "PASS" if ok else "FAIL",
        f"problems={problems} rows={len(rows)}",
        str(p),
    )

    # --- Gate 8: negative / limitation experiment ---
    p = RESULTS / "Exp12_CrossProduct" / "seed_logs.csv"
    rows = read_csv(p)
    lim = sum(1 for r in rows if is_true(r.get("limitation_ok")))
    cross = [r for r in rows if r.get("problem") in ("cross", "mixed")]
    ok = len(cross) >= 1 and lim == len(rows)
    gate(
        checks,
        "at least one negative/limitation experiment complete",
        "PASS" if ok else "FAIL",
        f"rows={len(rows)} cross_mixed={len(cross)} limitation_ok={lim}/{len(rows)}",
        str(p),
    )

    # --- Gate 9: baseline comparison ---
    nn = read_csv(RESULTS / "Exp11_Baselines" / "nn_seed_logs.csv")
    sk = read_csv(RESULTS / "Exp11_Baselines" / "sklearn_seed_logs.csv")
    methods = sorted({r.get("method") for r in nn + sk})
    ok = len(nn) >= 10 and len(sk) >= 20 and "hsm" in methods
    gate(
        checks,
        "baseline comparison complete",
        "PASS" if ok else "FAIL",
        f"nn={len(nn)} sklearn={len(sk)} methods={methods}",
        str(RESULTS / "Exp11_Baselines"),
    )

    # --- Gate 10: raw data and scripts archived ---
    needed = [
        RESULTS / "Exp01_CircleBoundary" / "seed_logs.csv",
        RESULTS / "Exp02_PiArea" / "seed_logs.csv",
        RESULTS / "Exp14_Reproducibility",
        ROOT / "GitHub_mirror" / "STATUS.md",
        ROOT / "NnPruneHsm" / "NnPruneHsm.csproj",
        ROOT / "SUCCESS_CRITERIA.md",
        ROOT / "PI_NEVER_OBJECTIVE.md",
        ROOT / "exp17_stats.py",
        ROOT / "exp18_criteria.py",
        ROOT / "exp19_pi_policy.py",
        ROOT / "RemainingToDoExperiments.docx",
    ]
    missing = [str(x) for x in needed if not x.exists()]
    # Also count seed_* dirs under Exp02 as raw archive signal
    seed_dirs = list((RESULTS / "Exp02_PiArea").rglob("seed_*")) if (RESULTS / "Exp02_PiArea").is_dir() else []
    ok = len(missing) == 0 and len(seed_dirs) >= 20
    gate(
        checks,
        "all raw data and scripts archived",
        "PASS" if ok else "FAIL",
        f"missing={len(missing)} exp02_seed_dirs={len(seed_dirs)}"
        + (f" first_missing={missing[0]}" if missing else ""),
        "Results/ + GitHub_mirror/ + scripts",
    )

    # --- Bonus completed (not minimum, but listed for manuscript readiness) ---
    bonus = [
        ("Exp03 circumference", RESULTS / "Exp03_Circumference" / "seed_logs.csv", 60),
        ("Exp06 noise", RESULTS / "Exp06_Noise" / "seed_logs.csv", 100),
        ("Exp09 network size", RESULTS / "Exp09_NetworkSize" / "seed_logs.csv", 50),
        ("Exp10 prune tolerance", RESULTS / "Exp10_PruneTolerance" / "seed_logs.csv", 30),
        ("Exp15 tables", RESULTS / "Exp15_ManuscriptTables" / "ALL_TABLES.md", None),
        ("Exp16 figures", RESULTS / "Exp16_Figures" / "INDEX.md", None),
        ("Exp17 statistics", RESULTS / "Exp17_Statistics" / "ALL_STATS.md", None),
        ("Exp18 criteria", RESULTS / "Exp18_SuccessCriteria" / "AUDIT.md", None),
        ("Exp19 pi policy", RESULTS / "Exp19_PiNeverObjective" / "AUDIT.md", None),
    ]
    bonus_rows = []
    for name, path, min_n in bonus:
        if path.suffix == ".csv":
            rows = read_csv(path)
            st = "PASS" if path.exists() and (min_n is None or len(rows) >= min_n) else "FAIL"
            detail = f"rows={len(rows)}" if path.exists() else "missing"
        else:
            st = "PASS" if path.exists() else "PENDING"
            detail = "present" if path.exists() else "missing"
        bonus_rows.append({"item": name, "status": st, "detail": detail, "path": str(path)})

    for c in checks:
        if c.get("evidence"):
            c["evidence"] = rel_evidence(c["evidence"])
    for b in bonus_rows:
        if b.get("path"):
            b["path"] = rel_evidence(b["path"])

    fields = ["gate", "status", "detail", "evidence"]
    write_csv(OUT / "GATES.csv", checks, fields)
    write_csv(OUT / "BONUS.csv", bonus_rows, ["item", "status", "detail", "path"])

    n_pass = sum(1 for c in checks if c["status"] == "PASS")
    n_fail = sum(1 for c in checks if c["status"] == "FAIL")
    overall = "PASS" if n_fail == 0 else "FAIL"
    rewrite_ok = overall == "PASS"

    md = [
        "# Exp20 — Manuscript rewrite gates",
        "",
        f"Generated: {now()}",
        "",
        f"**Minimum gates: {overall}** ({n_pass}/10 PASS).",
        "",
        f"**Manuscript Results/Discussion rewrite: "
        f"{'ALLOWED' if rewrite_ok else 'BLOCKED — finish failing gates first'}.**",
        "",
        "## Minimum gates (from RemainingToDoExperiments)",
        "",
        "| # | Gate | Status | Detail |",
        "|---|------|--------|--------|",
    ]
    for i, c in enumerate(checks, 1):
        md.append(f"| {i} | {c['gate']} | **{c['status']}** | {c['detail']} |")
    md.extend(
        [
            "",
            "## Additional completed work (beyond minimum)",
            "",
            "| Item | Status | Detail |",
            "|------|--------|--------|",
        ]
    )
    for b in bonus_rows:
        md.append(f"| {b['item']} | {b['status']} | {b['detail']} |")
    md.extend(
        [
            "",
            "## Next step",
            "",
            "If all minimum gates PASS, update the manuscript Results/Discussion from",
            "`Results/Exp15_ManuscriptTables`, `Exp16_Figures`, `Exp17_Statistics`,",
            "`SUCCESS_CRITERIA.md`, and `PI_NEVER_OBJECTIVE.md`. Do not invent numbers;",
            "cite seed logs and summary tables only.",
            "",
        ]
    )
    (OUT / "MANUSCRIPT_GATES.md").write_text("\n".join(md) + "\n", encoding="utf-8")

    headline = [
        f"Exp20 manuscript rewrite gates — {now()}",
        f"Minimum gates={overall} ({n_pass}/10 PASS, {n_fail} FAIL)",
        f"Rewrite Results/Discussion: {'ALLOWED' if rewrite_ok else 'BLOCKED'}",
    ]
    for c in checks:
        headline.append(f"  [{c['status']}] {c['gate']}: {c['detail']}")
    (OUT / "headline.txt").write_text("\n".join(headline) + "\n", encoding="utf-8")
    (OUT / "INDEX.md").write_text(
        "\n".join(
            [
                "# Exp20 — Manuscript gates",
                "",
                f"Updated: {now()}",
                f"- Minimum gates: **{overall}**",
                f"- Rewrite: **{'ALLOWED' if rewrite_ok else 'BLOCKED'}**",
                "- [MANUSCRIPT_GATES.md](MANUSCRIPT_GATES.md)",
                "- [GATES.csv](GATES.csv)",
                "- [BONUS.csv](BONUS.csv)",
                "- [headline.txt](headline.txt)",
                "",
            ]
        ),
        encoding="utf-8",
    )

    # Root pointer
    (ROOT / "MANUSCRIPT_GATES.md").write_text(
        f"# Manuscript rewrite gates\n\nSee `Results/Exp20_ManuscriptGates/MANUSCRIPT_GATES.md`.\n\n"
        f"Last check: {now()} — **{overall}** — rewrite "
        f"{'ALLOWED' if rewrite_ok else 'BLOCKED'}.\n",
        encoding="utf-8",
    )

    mirror = ROOT / "GitHub_mirror"
    if mirror.is_dir():
        dest = mirror / "results" / "Exp20_ManuscriptGates"
        dest.mkdir(parents=True, exist_ok=True)
        for f in OUT.iterdir():
            if f.is_file():
                shutil.copy2(f, dest / f.name)
        docs = mirror / "docs"
        docs.mkdir(parents=True, exist_ok=True)
        shutil.copy2(OUT / "MANUSCRIPT_GATES.md", docs / "MANUSCRIPT_GATES.md")
        status = [
            "# Live experiment status",
            "",
            f"Last update: {now()}",
            "",
            "| Experiment | Status | Degree of success |",
            "|------------|--------|-------------------|",
            "| 01–18 | complete | see Results/ |",
            f"| 19 π-never-objective | complete | see Exp19 (run before/with this) |",
            f"| 20 Manuscript gates | complete | minimum {overall}; rewrite "
            f"{'ALLOWED' if rewrite_ok else 'BLOCKED'} |",
            "| Appendix A item 10 prior-art | frozen | docs/PRIOR_ART.md; joint-pipeline novelty only |",
            "",
            "## Headline (Exp20)",
            "",
            (OUT / "headline.txt").read_text(encoding="utf-8").strip(),
            "",
            "Experimental programme minimum gates satisfied for manuscript rewrite."
            if rewrite_ok
            else "Fix failing gates before Results/Discussion rewrite.",
            "",
        ]
        # Prefer merging Exp19 headline if present
        e19 = RESULTS / "Exp19_PiNeverObjective" / "headline.txt"
        if e19.exists():
            status.insert(
                -3,
                "## Headline (Exp19)\n\n" + e19.read_text(encoding="utf-8").strip() + "\n",
            )
        (mirror / "STATUS.md").write_text("\n".join(status) + "\n", encoding="utf-8")

    print("Wrote", OUT)
    print("\n".join(headline[:5]))
    return 0 if rewrite_ok else 1


if __name__ == "__main__":
    raise SystemExit(main())
