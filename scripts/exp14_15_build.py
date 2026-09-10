#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""Exp14 reproducibility audit + Exp15 manuscript summary tables.

Reads authoritative Results/Exp0N_* folders (never overwrites them).
Writes:
  Results/Exp14_Reproducibility/
  Results/Exp15_ManuscriptTables/
"""
from __future__ import annotations

import argparse
import csv
import math
import os
import re
import shutil
import statistics
from collections import defaultdict
from datetime import datetime
from pathlib import Path

ROOT = Path(__file__).resolve().parent
RESULTS = ROOT / "Results"

# Authoritative experiment folders (smoke/partial archives excluded).
AUTHORITATIVE = [
    "Exp01_CircleBoundary",
    "Exp02_PiArea",
    "Exp03_Circumference",
    "Exp04_StochasticPi",
    "Exp05_SyntheticRecovery",
    "Exp06_Noise",
    "Exp07_OperatorAblation",
    "Exp08_PruningAblation",
    "Exp09_NetworkSize",
    "Exp10_PruneTolerance",
    "Exp11_Baselines",
    "Exp12_CrossProduct",
    "Exp13_EquationExport",
]

REQUIRED_RUN_FILES = [
    "config.json",
    "seed.txt",
    "train.csv",
    "validation.csv",
    "test.csv",
    "dense_network.txt",
    "pruned_network.txt",
    "exported_equation.txt",
    "metrics.json",
    "pruning_log.csv",
    "predictions.csv",
    "runtime.txt",
]

# Accept aliases for some required names.
ALIASES = {
    "pruning_log.csv": ["pruning_log.csv", "pruning_history.csv"],
    "pruned_network.txt": ["pruned_network.txt", "final_network_equation.txt"],
    "exported_equation.txt": ["exported_equation.txt", "final_network_equation.txt"],
    "predictions.csv": ["predictions.csv"],
    "runtime.txt": ["runtime.txt"],
    "metrics.json": ["metrics.json"],
    "config.json": ["config.json"],
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
        if x is None or x == "" or x == "null":
            return default
        return float(x)
    except Exception:
        return default


def find_seed_dirs(exp_dir: Path) -> list[Path]:
    """Find leaf run directories that look like seed_XX (any depth)."""
    if not exp_dir.is_dir():
        return []
    found = []
    for p in exp_dir.rglob("seed_*"):
        if p.is_dir() and re.fullmatch(r"seed_\d+", p.name):
            found.append(p)
    return sorted(found, key=lambda q: str(q).lower())


def has_required(seed_dir: Path, name: str) -> bool:
    names = ALIASES.get(name, [name])
    for n in names:
        if (seed_dir / n).is_file():
            return True
    return False


# ---------------------------------------------------------------------------
# Exp14
# ---------------------------------------------------------------------------

def run_exp14(out: Path) -> dict:
    out.mkdir(parents=True, exist_ok=True)
    rows = []
    index_lines = [
        "# Exp14 — Reproducibility index",
        "",
        f"Generated: {now()}",
        "",
        "Authoritative result folders (do not overwrite prior runs):",
        "",
        "| Experiment | Path | Seed runs found | Mean required-file coverage |",
        "|------------|------|-----------------|-----------------------------|",
    ]
    gap_lines = [
        "# Exp14 — Reproducibility gap report",
        "",
        f"Generated: {now()}",
        "",
        "Required per-run files (from RemainingToDoExperiments Exp14):",
        "",
    ]
    for f in REQUIRED_RUN_FILES:
        gap_lines.append(f"- `{f}`")
    gap_lines += ["", "---", ""]

    for exp in AUTHORITATIVE:
        exp_dir = RESULTS / exp
        seeds = find_seed_dirs(exp_dir) if exp_dir.is_dir() else []
        coverages = []
        missing_examples = defaultdict(int)
        for sd in seeds:
            present = sum(1 for req in REQUIRED_RUN_FILES if has_required(sd, req))
            cov = present / len(REQUIRED_RUN_FILES)
            coverages.append(cov)
            for req in REQUIRED_RUN_FILES:
                if not has_required(sd, req):
                    missing_examples[req] += 1
            rows.append(
                {
                    "experiment": exp,
                    "seed_dir": str(sd.relative_to(RESULTS)).replace("\\", "/"),
                    "coverage": f"{cov:.3f}",
                    "present": present,
                    "required": len(REQUIRED_RUN_FILES),
                }
            )
        mean_cov = statistics.mean(coverages) if coverages else 0.0
        exists = exp_dir.is_dir()
        index_lines.append(
            f"| {exp} | `Results/{exp}/` | {len(seeds)} | "
            f"{'n/a (no seed_* dirs)' if not seeds and exists else f'{100*mean_cov:.1f}%'} |"
            if exists
            else f"| {exp} | MISSING | 0 | — |"
        )
        gap_lines.append(f"## {exp}")
        gap_lines.append("")
        if not exists:
            gap_lines.append("Folder missing.")
        elif not seeds:
            # Exp04-style may nest under N_* without always matching; still report
            gap_lines.append(
                f"No `seed_*` directories found under `{exp}` "
                f"(may use a nested layout; check folder manually)."
            )
            # list top-level
            tops = sorted([p.name for p in exp_dir.iterdir()])[:20]
            gap_lines.append("Top-level: " + ", ".join(tops))
        else:
            gap_lines.append(f"Seed runs: **{len(seeds)}**. Mean coverage: **{100*mean_cov:.1f}%**.")
            if exp == "Exp11_Baselines":
                gap_lines.append(
                    "Note: shared train/val/test CSVs live under `Exp11_Baselines/seed_XX/`; "
                    "method runs under `Method_*/seed_XX/` inherit those splits."
                )
            if missing_examples:
                gap_lines.append("")
                gap_lines.append("| Missing file | # seed dirs lacking it |")
                gap_lines.append("|--------------|------------------------|")
                for k, v in sorted(missing_examples.items(), key=lambda kv: -kv[1]):
                    gap_lines.append(f"| `{k}` | {v} |")
            else:
                gap_lines.append("All required files present in every seed dir.")
        gap_lines.append("")

    # Write outputs
    (out / "INDEX.md").write_text("\n".join(index_lines) + "\n", encoding="utf-8")
    (out / "GAP_REPORT.md").write_text("\n".join(gap_lines) + "\n", encoding="utf-8")
    with (out / "coverage.csv").open("w", newline="", encoding="utf-8") as f:
        w = csv.DictWriter(
            f, fieldnames=["experiment", "seed_dir", "coverage", "present", "required"]
        )
        w.writeheader()
        w.writerows(rows)

    # Manifest of authoritative folders
    man = ["# Results manifest", "", f"Generated: {now()}", ""]
    for exp in AUTHORITATIVE:
        p = RESULTS / exp
        man.append(f"- `{exp}/` — {'present' if p.is_dir() else 'MISSING'}")
    (out / "MANIFEST.md").write_text("\n".join(man) + "\n", encoding="utf-8")

    headline = (
        f"Exp14: audited {len(AUTHORITATIVE)} experiment folders; "
        f"{sum(1 for e in AUTHORITATIVE if (RESULTS/e).is_dir())} present; "
        f"{len(rows)} seed runs scored.\n"
    )
    (out / "headline.txt").write_text(
        headline + "\nSee INDEX.md and GAP_REPORT.md.\n", encoding="utf-8"
    )
    return {"seed_runs": len(rows), "headline": headline}


# ---------------------------------------------------------------------------
# Exp15 tables
# ---------------------------------------------------------------------------

def md_table(headers: list[str], rows: list[list]) -> str:
    lines = [
        "| " + " | ".join(headers) + " |",
        "|" + "|".join(["---"] * len(headers)) + "|",
    ]
    for r in rows:
        lines.append("| " + " | ".join(str(c) for c in r) + " |")
    return "\n".join(lines) + "\n"


def fmt(x, prec=".5g"):
    if x is None or (isinstance(x, float) and (math.isnan(x) or math.isinf(x))):
        return "—"
    if isinstance(x, float):
        return format(x, prec)
    return str(x)


def table_a(out: Path) -> str:
    rows = read_csv(RESULTS / "Exp01_CircleBoundary" / "seed_logs.csv")
    body = []
    for r in rows:
        body.append(
            [
                r.get("seed", ""),
                r.get("dense_n", ""),
                r.get("dense_e", ""),
                r.get("pruned_n", ""),
                r.get("pruned_e", ""),
                r.get("quad", ""),
                fmt(100 * fnum(r.get("test_acc")), ".2f") + "%",
                fmt(fnum(r.get("a"))),
                fmt(fnum(r.get("b"))),
                fmt(100 * fnum(r.get("rel_ab")), ".2f"),
                "PASS" if r.get("fidelity_pass") in ("1", "true", "True") else "FAIL",
            ]
        )
    md = "# Table A — Circle recovery\n\n" + md_table(
        [
            "Seed",
            "Dense n",
            "Dense e",
            "Final n",
            "Final e",
            "Quad retained",
            "Test acc",
            "a",
            "b",
            "|a-b| rel %",
            "Fidelity",
        ],
        body,
    )
    # CSV
    with (out / "TableA_Circle.csv").open("w", newline="", encoding="utf-8") as f:
        w = csv.writer(f)
        w.writerow(
            [
                "seed",
                "dense_n",
                "dense_e",
                "final_n",
                "final_e",
                "quad",
                "test_acc",
                "a",
                "b",
                "rel_ab_pct",
                "fidelity_pass",
            ]
        )
        for r in rows:
            w.writerow(
                [
                    r.get("seed"),
                    r.get("dense_n"),
                    r.get("dense_e"),
                    r.get("pruned_n"),
                    r.get("pruned_e"),
                    r.get("quad"),
                    r.get("test_acc"),
                    r.get("a"),
                    r.get("b"),
                    100 * fnum(r.get("rel_ab")),
                    r.get("fidelity_pass"),
                ]
            )
    (out / "TableA_Circle.md").write_text(md, encoding="utf-8")
    return md


def table_b(out: Path) -> str:
    rows = read_csv(RESULTS / "Exp02_PiArea" / "seed_logs.csv")
    body = []
    for r in rows:
        topo = f"{r.get('pruned_neurons','?')}/{r.get('pruned_edges','?')}"
        body.append(
            [
                r.get("seed", ""),
                topo,
                "yes" if r.get("canonical") in ("1", "true") else "no",
                fmt(fnum(r.get("b"))),
                fmt(fnum(r.get("k_canonical"))),
                fmt(100 * fnum(r.get("pi_error")), ".4f"),
                "yes" if int(fnum(r.get("sq_linear"), 0)) > 0 else "no",
                fmt(fnum(r.get("test_rmse"))),
            ]
        )
    md = "# Table B — Pi area recovery\n\n" + md_table(
        [
            "Seed",
            "Final n/e",
            "Canonical kr^2?",
            "Bias b",
            "Recovered k",
            "Pi error %",
            "Quad retained?",
            "Test RMSE",
        ],
        body,
    )
    with (out / "TableB_PiArea.csv").open("w", newline="", encoding="utf-8") as f:
        w = csv.writer(f)
        w.writerow(
            [
                "seed",
                "final_n",
                "final_e",
                "canonical",
                "b",
                "k",
                "pi_error_pct",
                "sq_linear",
                "test_rmse",
            ]
        )
        for r in rows:
            w.writerow(
                [
                    r.get("seed"),
                    r.get("pruned_neurons"),
                    r.get("pruned_edges"),
                    r.get("canonical"),
                    r.get("b"),
                    r.get("k_canonical"),
                    100 * fnum(r.get("pi_error")),
                    r.get("sq_linear"),
                    r.get("test_rmse"),
                ]
            )
    (out / "TableB_PiArea.md").write_text(md, encoding="utf-8")
    return md


def table_c(out: Path) -> str:
    rows = read_csv(RESULTS / "Exp04_StochasticPi" / "depth_summary.csv")
    body = []
    for r in rows:
        body.append(
            [
                r.get("N", ""),
                fmt(fnum(r.get("mean_k"))),
                fmt(fnum(r.get("std_k"))),
                f"[{fmt(fnum(r.get('ci95_lo')))}, {fmt(fnum(r.get('ci95_hi')))}]",
                fmt(100 * fnum(r.get("mean_pi_error")), ".4f"),
                fmt(100 * fnum(r.get("canonical_pct")), ".1f"),
            ]
        )
    md = "# Table C — Stochastic convergence\n\n" + md_table(
        ["Samples N", "Mean k", "Std k", "95% CI", "Pi error %", "Canonical recovery %"],
        body,
    )
    shutil.copy2(
        RESULTS / "Exp04_StochasticPi" / "depth_summary.csv",
        out / "TableC_Stochastic.csv",
    )
    (out / "TableC_Stochastic.md").write_text(md, encoding="utf-8")
    return md


def table_d(out: Path) -> str:
    # Prefer existing TableD markdown; also rebuild from seed_logs
    rows = read_csv(RESULTS / "Exp07_OperatorAblation" / "seed_logs.csv")
    by = defaultdict(list)
    for r in rows:
        by[r.get("model", "?")].append(r)
    desc = {
        "A": "lin/sig only + prune",
        "B": "mixed + prune",
        "C": "square-linear only + prune",
        "D": "mixed, no prune",
        "E": "mixed + prune + Pass E",
    }
    body = []
    csv_rows = []
    for mid in ["A", "B", "C", "D", "E"]:
        xs = by.get(mid, [])
        if not xs:
            continue
        n = len(xs)
        can = sum(1 for r in xs if r.get("canonical") in ("1", "true"))
        rms = [fnum(r.get("test_rmse")) for r in xs]
        rms_f = [v for v in rms if not math.isnan(v)]
        neu = statistics.mean(fnum(r.get("final_n")) for r in xs)
        edg = statistics.mean(fnum(r.get("final_e")) for r in xs)
        terms = statistics.mean(fnum(r.get("terms")) for r in xs)
        quad = sum(1 for r in xs if fnum(r.get("explicit_r2"), 0) > 0 or fnum(r.get("sqlin"), 0) > 0)
        body.append(
            [
                mid,
                desc.get(mid, ""),
                fmt(statistics.mean(rms_f) if rms_f else float("nan")),
                fmt(neu, ".1f"),
                fmt(edg, ".1f"),
                fmt(terms, ".1f"),
                f"{100*quad/n:.0f}%",
                f"{100*can/n:.0f}%",
            ]
        )
        csv_rows.append(
            [
                mid,
                desc.get(mid, ""),
                statistics.mean(rms_f) if rms_f else "",
                neu,
                edg,
                terms,
                quad / n,
                can / n,
                n,
            ]
        )
    md = "# Table D — Operator ablation\n\n" + md_table(
        [
            "Model",
            "Description",
            "Test error",
            "Neurons",
            "Edges",
            "Equation terms",
            "Quadratic recovered?",
            "Canonical equation?",
        ],
        body,
    )
    with (out / "TableD_Ablation.csv").open("w", newline="", encoding="utf-8") as f:
        w = csv.writer(f)
        w.writerow(
            [
                "model",
                "description",
                "mean_test_rmse",
                "mean_neurons",
                "mean_edges",
                "mean_terms",
                "quad_frac",
                "canon_frac",
                "n",
            ]
        )
        w.writerows(csv_rows)
    (out / "TableD_Ablation.md").write_text(md, encoding="utf-8")
    return md


def copy_extra_summaries(out: Path) -> list[str]:
    """Pull headline/summary tables from later ablations into the manuscript pack."""
    extras = []
    mapping = [
        ("Exp08_PruningAblation", "TableE_PruningAblation"),
        ("Exp09_NetworkSize", "TableF_NetworkSize"),
        ("Exp10_PruneTolerance", "TableG_PruneTolerance"),
        ("Exp11_Baselines", "TableH_Baselines"),
        ("Exp12_CrossProduct", "TableI_CrossProduct"),
        ("Exp13_EquationExport", "TableJ_EquationExport"),
    ]
    for exp, tag in mapping:
        src_dir = RESULTS / exp
        for name in ("headline.txt", "summary.md", "Table_Baselines.md", "pareto_points.csv"):
            src = src_dir / name
            if src.is_file():
                dst = out / f"{tag}_{name}"
                shutil.copy2(src, dst)
                extras.append(dst.name)
        # seed logs
        sl = src_dir / "seed_logs.csv"
        if sl.is_file():
            shutil.copy2(sl, out / f"{tag}_seed_logs.csv")
            extras.append(f"{tag}_seed_logs.csv")
        nn = src_dir / "nn_seed_logs.csv"
        if nn.is_file():
            shutil.copy2(nn, out / f"{tag}_nn_seed_logs.csv")
            extras.append(f"{tag}_nn_seed_logs.csv")
        sk = src_dir / "sklearn_seed_logs.csv"
        if sk.is_file():
            shutil.copy2(sk, out / f"{tag}_sklearn_seed_logs.csv")
            extras.append(f"{tag}_sklearn_seed_logs.csv")
    return extras


def run_exp15(out: Path) -> dict:
    out.mkdir(parents=True, exist_ok=True)
    parts = []
    parts.append(table_a(out))
    parts.append(table_b(out))
    parts.append(table_c(out))
    parts.append(table_d(out))
    extras = copy_extra_summaries(out)

    master = [
        "# Manuscript summary tables (Exp15)",
        "",
        f"Generated: {now()}",
        "",
        "Primary tables A–D built from authoritative `Results/Exp0N_*` CSVs.",
        "Extended ablation/baseline headlines copied as TableE–J companions.",
        "",
        "## Contents",
        "",
        "- `TableA_Circle.md` / `.csv`",
        "- `TableB_PiArea.md` / `.csv`",
        "- `TableC_Stochastic.md` / `.csv`",
        "- `TableD_Ablation.md` / `.csv`",
    ]
    for e in extras:
        master.append(f"- `{e}`")
    master.append("")
    master.append("---")
    master.append("")
    for p in parts:
        master.append(p)
        master.append("")
    text = "\n".join(master)
    (out / "ALL_TABLES.md").write_text(text, encoding="utf-8")
    headline = (
        "Exp15: Tables A–D generated; "
        f"extended companions={len(extras)} files.\n"
    )
    (out / "headline.txt").write_text(headline + "See ALL_TABLES.md.\n", encoding="utf-8")
    return {"extras": len(extras), "headline": headline}


def update_mirror(exp14: Path, exp15: Path):
    mirror = ROOT / "GitHub_mirror"
    if not mirror.is_dir():
        return
    status = [
        "# Live experiment status",
        "",
        f"Last update: {now()}",
        "",
        "| Experiment | Status | Degree of success |",
        "|------------|--------|-------------------|",
        "| 01–13 | complete | see Results/ |",
        "| 14 Reproducibility | complete | INDEX + GAP_REPORT |",
        "| 15 Manuscript tables | complete | Tables A–D + E–J companions |",
        "| 16–20 | queued | figures / stats / manuscript |",
        "",
        "## Headline",
        "",
        (exp14 / "headline.txt").read_text(encoding="utf-8").strip(),
        "",
        (exp15 / "headline.txt").read_text(encoding="utf-8").strip(),
        "",
    ]
    (mirror / "STATUS.md").write_text("\n".join(status) + "\n", encoding="utf-8")
    for src, name in ((exp14, "Exp14_Reproducibility"), (exp15, "Exp15_ManuscriptTables")):
        dest = mirror / "results" / name
        dest.mkdir(parents=True, exist_ok=True)
        for f in src.iterdir():
            if f.is_file():
                shutil.copy2(f, dest / f.name)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--root", default=str(Path(__file__).resolve().parent))
    ap.add_argument("--skip14", action="store_true")
    ap.add_argument("--skip15", action="store_true")
    args = ap.parse_args()
    root = Path(args.root)
    results = root / "Results"

    # Rebind module-level paths used by helpers
    global ROOT, RESULTS
    ROOT = root
    RESULTS = results

    out14 = RESULTS / "Exp14_Reproducibility"
    out15 = RESULTS / "Exp15_ManuscriptTables"
    r14 = r15 = {}
    if not args.skip14:
        print("Running Exp14 reproducibility audit...")
        r14 = run_exp14(out14)
        print(r14["headline"].strip())
    if not args.skip15:
        print("Running Exp15 manuscript tables...")
        r15 = run_exp15(out15)
        print(r15["headline"].strip())
    update_mirror(out14, out15)
    print("Wrote", out14)
    print("Wrote", out15)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
