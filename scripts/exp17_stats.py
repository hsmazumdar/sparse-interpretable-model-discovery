#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""Exp17 — statistical reporting from saved Results CSV files only.

For each repeated experiment:
  - mean, SD, median, min, max, 95% CI (Student-t) for continuous metrics
  - structure / canonical recovery rates with Wilson 95% CI
  - paired comparisons on matched seeds (Wilcoxon + paired t + Cohen dz;
    McNemar for binary outcomes); Holm correction within comparison families

Does not hard-code experimental outcomes; all values come from disk.
"""
from __future__ import annotations

import argparse
import csv
import math
import shutil
from collections import defaultdict
from datetime import datetime
from pathlib import Path
from typing import Iterable, Optional, Sequence

import numpy as np
from scipy import stats

ROOT = Path(__file__).resolve().parent
RESULTS = ROOT / "Results"
OUT = RESULTS / "Exp17_Statistics"


def now() -> str:
    return datetime.now().strftime("%Y-%m-%d %H:%M:%S")


def read_csv(path: Path) -> list[dict]:
    if not path.exists():
        raise FileNotFoundError(path)
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


def write_csv(path: Path, rows: list[dict], fieldnames: Optional[Sequence[str]] = None):
    path.parent.mkdir(parents=True, exist_ok=True)
    if not rows:
        path.write_text("", encoding="utf-8")
        return
    if fieldnames is None:
        keys: list[str] = []
        seen = set()
        for r in rows:
            for k in r:
                if k not in seen:
                    seen.add(k)
                    keys.append(k)
        fieldnames = keys
    with path.open("w", newline="", encoding="utf-8") as f:
        w = csv.DictWriter(f, fieldnames=list(fieldnames), extrasaction="ignore")
        w.writeheader()
        for r in rows:
            w.writerow({k: r.get(k, "") for k in fieldnames})


def fmt(x, digits=6):
    if x is None or (isinstance(x, float) and (math.isnan(x) or math.isinf(x))):
        return ""
    if isinstance(x, (int, np.integer)):
        return str(int(x))
    try:
        xf = float(x)
    except Exception:
        return str(x)
    if abs(xf) >= 1e4 or (abs(xf) > 0 and abs(xf) < 1e-3):
        return f"{xf:.6g}"
    return f"{xf:.{digits}g}"


def fmt_pct(x, digits=1) -> str:
    if x is None or (isinstance(x, float) and (math.isnan(x) or math.isinf(x))):
        return ""
    return f"{float(x):.{digits}f}"


def continuous_summary(xs: Sequence[float], label: str = "") -> dict:
    arr = np.asarray([x for x in xs if x is not None and not (isinstance(x, float) and math.isnan(x))], dtype=float)
    n = int(arr.size)
    out = {
        "metric": label,
        "n": n,
        "mean": float("nan"),
        "sd": float("nan"),
        "median": float("nan"),
        "min": float("nan"),
        "max": float("nan"),
        "ci95_lo": float("nan"),
        "ci95_hi": float("nan"),
    }
    if n == 0:
        return out
    mean = float(np.mean(arr))
    sd = float(np.std(arr, ddof=1)) if n > 1 else 0.0
    out.update(
        {
            "mean": mean,
            "sd": sd,
            "median": float(np.median(arr)),
            "min": float(np.min(arr)),
            "max": float(np.max(arr)),
        }
    )
    if n == 1:
        out["ci95_lo"] = mean
        out["ci95_hi"] = mean
    else:
        se = sd / math.sqrt(n)
        tcrit = float(stats.t.ppf(0.975, n - 1))
        out["ci95_lo"] = mean - tcrit * se
        out["ci95_hi"] = mean + tcrit * se
    return out


def wilson_ci(k: int, n: int, z: float = 1.959963984540054) -> tuple[float, float]:
    if n <= 0:
        return float("nan"), float("nan")
    p = k / n
    denom = 1.0 + z * z / n
    centre = p + z * z / (2.0 * n)
    half = z * math.sqrt(p * (1.0 - p) / n + z * z / (4.0 * n * n))
    return (centre - half) / denom, (centre + half) / denom


def rate_summary(flags: Sequence[bool], label: str = "") -> dict:
    n = len(flags)
    k = int(sum(1 for f in flags if f))
    lo, hi = wilson_ci(k, n)
    return {
        "metric": label,
        "n": n,
        "k": k,
        "rate": (k / n) if n else float("nan"),
        "rate_pct": (100.0 * k / n) if n else float("nan"),
        "ci95_lo": lo,
        "ci95_hi": hi,
        "ci95_lo_pct": 100.0 * lo if n else float("nan"),
        "ci95_hi_pct": 100.0 * hi if n else float("nan"),
    }


def paired_continuous(a: dict[int, float], b: dict[int, float], name_a: str, name_b: str, metric: str) -> dict:
    seeds = sorted(set(a) & set(b))
    xa = np.array([a[s] for s in seeds], dtype=float)
    xb = np.array([b[s] for s in seeds], dtype=float)
    mask = np.isfinite(xa) & np.isfinite(xb)
    xa, xb = xa[mask], xb[mask]
    n = int(xa.size)
    d = xa - xb
    row = {
        "comparison": f"{name_a} vs {name_b}",
        "metric": metric,
        "n_pairs": n,
        "mean_a": float(np.mean(xa)) if n else float("nan"),
        "mean_b": float(np.mean(xb)) if n else float("nan"),
        "mean_diff_a_minus_b": float(np.mean(d)) if n else float("nan"),
        "sd_diff": float(np.std(d, ddof=1)) if n > 1 else float("nan"),
        "cohen_dz": float("nan"),
        "paired_t_p": float("nan"),
        "wilcoxon_p": float("nan"),
        "ci95_diff_lo": float("nan"),
        "ci95_diff_hi": float("nan"),
        "holm_adj_p": "",
        "test_used_for_holm": "wilcoxon",
    }
    if n < 2:
        return row
    sd = float(np.std(d, ddof=1))
    if sd > 0:
        row["cohen_dz"] = float(np.mean(d) / sd)
    se = sd / math.sqrt(n) if sd == sd else float("nan")
    tcrit = float(stats.t.ppf(0.975, n - 1))
    row["ci95_diff_lo"] = float(np.mean(d) - tcrit * se)
    row["ci95_diff_hi"] = float(np.mean(d) + tcrit * se)
    try:
        row["paired_t_p"] = float(stats.ttest_rel(xa, xb, nan_policy="omit").pvalue)
    except Exception:
        pass
    # Wilcoxon needs non-zero differences
    if np.any(d != 0):
        try:
            row["wilcoxon_p"] = float(stats.wilcoxon(d, zero_method="wilcox", alternative="two-sided").pvalue)
        except Exception:
            row["wilcoxon_p"] = row["paired_t_p"]
    else:
        row["wilcoxon_p"] = 1.0
    return row


def mcnemar_exact(a: dict[int, bool], b: dict[int, bool], name_a: str, name_b: str, metric: str) -> dict:
    seeds = sorted(set(a) & set(b))
    n01 = n10 = n11 = n00 = 0
    for s in seeds:
        xa, xb = bool(a[s]), bool(b[s])
        if xa and xb:
            n11 += 1
        elif (not xa) and (not xb):
            n00 += 1
        elif xa and (not xb):
            n10 += 1
        else:
            n01 += 1
    discord = n01 + n10
    if discord == 0:
        p = 1.0
    else:
        # Exact binomial McNemar (two-sided)
        p = float(stats.binomtest(n10, discord, 0.5, alternative="two-sided").pvalue)
    return {
        "comparison": f"{name_a} vs {name_b}",
        "metric": metric,
        "n_pairs": len(seeds),
        "both_true": n11,
        "both_false": n00,
        "a_only": n10,
        "b_only": n01,
        "rate_a": (n11 + n10) / len(seeds) if seeds else float("nan"),
        "rate_b": (n11 + n01) / len(seeds) if seeds else float("nan"),
        "mcnemar_p": p,
        "holm_adj_p": "",
    }


def holm_adjust(pvals: Sequence[float]) -> list[float]:
    m = len(pvals)
    if m == 0:
        return []
    order = sorted(range(m), key=lambda i: (float("inf") if math.isnan(pvals[i]) else pvals[i]))
    adj = [float("nan")] * m
    running = 0.0
    for rank, i in enumerate(order):
        p = pvals[i]
        if math.isnan(p):
            continue
        val = min(1.0, (m - rank) * p)
        running = max(running, val)
        adj[i] = running
    return adj


def apply_holm(rows: list[dict], pkey: str = "wilcoxon_p"):
    ps = []
    for r in rows:
        p = r.get(pkey)
        if p == "" or p is None:
            ps.append(float("nan"))
        else:
            ps.append(float(p))
    adj = holm_adjust(ps)
    for r, a in zip(rows, adj):
        r["holm_adj_p"] = a if not math.isnan(a) else ""
        r["test_used_for_holm"] = pkey


def group_by(rows: Iterable[dict], *keys: str) -> dict[tuple, list[dict]]:
    g: dict[tuple, list[dict]] = defaultdict(list)
    for r in rows:
        g[tuple(str(r.get(k, "")) for k in keys)].append(r)
    return g


# ---------------------------------------------------------------------------
# Per-experiment builders
# ---------------------------------------------------------------------------

cont_rows: list[dict] = []
rate_rows: list[dict] = []
paired_cont_rows: list[dict] = []
paired_bin_rows: list[dict] = []
section_md: list[str] = []


def add_cont(experiment: str, group: str, metric: str, values: Sequence[float]):
    s = continuous_summary(values, metric)
    row = {"experiment": experiment, "group": group, **s}
    cont_rows.append(row)
    return row


def add_rate(experiment: str, group: str, metric: str, flags: Sequence[bool]):
    s = rate_summary(flags, metric)
    row = {"experiment": experiment, "group": group, **s}
    rate_rows.append(row)
    return row


def md_cont_table(title: str, rows: list[dict]) -> str:
    lines = [f"### {title}", "", "| Group | Metric | n | Mean | SD | Median | Min | Max | 95% CI |",
             "|-------|--------|---|------|----|--------|-----|-----|--------|"]
    for r in rows:
        ci = f"[{fmt(r['ci95_lo'])}, {fmt(r['ci95_hi'])}]"
        lines.append(
            f"| {r['group']} | {r['metric']} | {r['n']} | {fmt(r['mean'])} | {fmt(r['sd'])} | "
            f"{fmt(r['median'])} | {fmt(r['min'])} | {fmt(r['max'])} | {ci} |"
        )
    lines.append("")
    return "\n".join(lines)


def md_rate_table(title: str, rows: list[dict]) -> str:
    lines = [f"### {title}", "",
             "| Group | Metric | n | k | Rate % | Wilson 95% CI % |",
             "|-------|--------|---|---|--------|-----------------|"]
    for r in rows:
        ci = f"[{fmt_pct(r['ci95_lo_pct'], 1)}, {fmt_pct(r['ci95_hi_pct'], 1)}]"
        lines.append(
            f"| {r['group']} | {r['metric']} | {r['n']} | {r['k']} | {fmt_pct(r['rate_pct'], 1)} | {ci} |"
        )
    lines.append("")
    return "\n".join(lines)


def exp01():
    rows = read_csv(RESULTS / "Exp01_CircleBoundary" / "seed_logs.csv")
    exp = "Exp01_CircleBoundary"
    local_c, local_r = [], []
    for metric, col in [
        ("test_acc", "test_acc"),
        ("baseline_acc_no_quad", "baseline_acc"),
        ("R_hat", "R_hat"),
        ("R_err", "R_err"),
        ("rel_ab", "rel_ab"),
        ("fidelity_max", "fidelity_max"),
    ]:
        vals = [fnum(r[col]) for r in rows]
        local_c.append(add_cont(exp, "all", metric, vals))
    for metric, col in [("canonical", "canonical"), ("structural", "structural"), ("fidelity_pass", "fidelity_pass")]:
        local_r.append(add_rate(exp, "all", metric, [is_true(r[col]) for r in rows]))

    # Paired: quadratic bank vs no-quad baseline accuracy (matched seeds)
    a = {int(r["seed"]): fnum(r["test_acc"]) for r in rows}
    b = {int(r["seed"]): fnum(r["baseline_acc"]) for r in rows}
    pc = paired_continuous(a, b, "quad_bank", "no_quad_baseline", "test_acc")
    pc["experiment"] = exp
    paired_cont_rows.append(pc)
    apply_holm([pc], "wilcoxon_p")

    section_md.append(f"## {exp}\n")
    section_md.append(md_rate_table("Structure / success rates", local_r))
    section_md.append(md_cont_table("Continuous metrics", local_c))
    section_md.append(
        f"**Paired (matched seeds):** quad vs no-quad accuracy — "
        f"meanDiff={fmt(pc['mean_diff_a_minus_b'])}, "
        f"Cohen dz={fmt(pc['cohen_dz'])}, "
        f"Wilcoxon p={fmt(pc['wilcoxon_p'])}, Holm p={fmt(pc['holm_adj_p'])}.\n"
    )


def exp02():
    rows = read_csv(RESULTS / "Exp02_PiArea" / "seed_logs.csv")
    exp = "Exp02_PiArea"
    local_c, local_r = [], []
    for metric, col in [
        ("k_canonical", "k_canonical"),
        ("pi_error", "pi_error"),
        ("test_rmse", "test_rmse"),
        ("val_loss", "val_loss"),
        ("b", "b"),
        ("fidelity_max", "fidelity_max"),
        ("pruned_neurons", "pruned_neurons"),
        ("pruned_edges", "pruned_edges"),
    ]:
        local_c.append(add_cont(exp, "all", metric, [fnum(r[col]) for r in rows]))
    for metric, col in [
        ("canonical", "canonical"),
        ("structural", "structural"),
        ("near_zero_b", "near_zero_b"),
        ("fidelity_pass", "fidelity_pass"),
    ]:
        local_r.append(add_rate(exp, "all", metric, [is_true(r[col]) for r in rows]))
    section_md.append(f"## {exp}\n")
    section_md.append(md_rate_table("Structure / success rates", local_r))
    section_md.append(md_cont_table("Continuous metrics", local_c))


def exp03():
    rows = read_csv(RESULTS / "Exp03_Circumference" / "seed_logs.csv")
    exp = "Exp03_Circumference"
    local_c, local_r = [], []
    by_est = group_by(rows, "estimator")
    for (est,), xs in sorted(by_est.items()):
        for metric, col in [
            ("k_canonical", "k_canonical"),
            ("two_pi_error", "two_pi_error"),
            ("test_rmse", "test_rmse"),
            ("val_loss", "val_loss"),
        ]:
            local_c.append(add_cont(exp, est, metric, [fnum(r[col]) for r in xs]))
        for metric, col in [("canonical", "canonical"), ("structural", "structural"), ("near_zero_b", "near_zero_b")]:
            local_r.append(add_rate(exp, est, metric, [is_true(r[col]) for r in xs]))

    # Paired raw vs improved on |k - 2π| error and test_rmse
    raw = {int(r["seed"]): r for r in rows if r["estimator"] == "raw"}
    imp = {int(r["seed"]): r for r in rows if r["estimator"] == "improved"}
    fam = []
    for metric, col in [("two_pi_error", "two_pi_error"), ("test_rmse", "test_rmse"), ("k_canonical", "k_canonical")]:
        a = {s: fnum(raw[s][col]) for s in raw if s in imp}
        b = {s: fnum(imp[s][col]) for s in imp if s in raw}
        pc = paired_continuous(a, b, "raw", "improved", metric)
        pc["experiment"] = exp
        paired_cont_rows.append(pc)
        fam.append(pc)
    apply_holm(fam, "wilcoxon_p")
    pb = mcnemar_exact(
        {s: is_true(raw[s]["canonical"]) for s in raw if s in imp},
        {s: is_true(imp[s]["canonical"]) for s in imp if s in raw},
        "raw",
        "improved",
        "canonical",
    )
    pb["experiment"] = exp
    paired_bin_rows.append(pb)

    section_md.append(f"## {exp}\n")
    section_md.append(md_rate_table("Structure / success rates by estimator", local_r))
    section_md.append(md_cont_table("Continuous metrics by estimator", local_c))
    section_md.append(
        "**Paired raw vs improved (matched seeds):** see `paired_continuous.csv` / `paired_binary.csv`.\n"
    )


def exp04():
    rows = read_csv(RESULTS / "Exp04_StochasticPi" / "seed_logs.csv")
    exp = "Exp04_StochasticPi"
    local_c, local_r = [], []
    by_n = group_by(rows, "N")
    for (nlab,), xs in sorted(by_n.items(), key=lambda kv: int(kv[0][0])):
        for metric, col in [("k_canonical", "k_canonical"), ("pi_error", "pi_error"), ("test_rmse", "test_rmse")]:
            local_c.append(add_cont(exp, f"N={nlab}", metric, [fnum(r[col]) for r in xs]))
        for metric, col in [("canonical", "canonical"), ("structural", "structural")]:
            local_r.append(add_rate(exp, f"N={nlab}", metric, [is_true(r[col]) for r in xs]))
    section_md.append(f"## {exp}\n")
    section_md.append(md_rate_table("Structure rates by N", local_r))
    section_md.append(md_cont_table("Continuous metrics by N", local_c))


def exp05_06(name: str, folder: str):
    rows = read_csv(RESULTS / folder / "seed_logs.csv")
    exp = folder
    local_c, local_r = [], []
    by = group_by(rows, "problem", "noise")
    for key, xs in sorted(by.items()):
        g = f"{key[0]}_noise{key[1]}"
        for metric, col in [("max_rel_err", "max_rel_err"), ("test_rmse", "test_rmse"), ("pruned_n", "pruned_n")]:
            local_c.append(add_cont(exp, g, metric, [fnum(r[col]) for r in xs]))
        for metric, col in [("structure_ok", "structure_ok"), ("canonical", "canonical")]:
            local_r.append(add_rate(exp, g, metric, [is_true(r[col]) for r in xs]))
    # Overall structure recovery (headline)
    local_r.append(add_rate(exp, "ALL", "structure_ok", [is_true(r["structure_ok"]) for r in rows]))
    local_r.append(add_rate(exp, "ALL", "canonical", [is_true(r["canonical"]) for r in rows]))
    section_md.append(f"## {exp}\n")
    section_md.append(md_rate_table("Structure / canonical rates", local_r[-2:]))
    section_md.append(md_rate_table("By problem × noise (structure rates)", [r for r in local_r if r["group"] != "ALL" and r["metric"] == "structure_ok"]))
    section_md.append(md_cont_table("Continuous metrics (by cell)", local_c))


def exp07():
    rows = read_csv(RESULTS / "Exp07_OperatorAblation" / "seed_logs.csv")
    exp = "Exp07_OperatorAblation"
    local_c, local_r = [], []
    by = group_by(rows, "model")
    for (model,), xs in sorted(by.items()):
        local_c.append(add_cont(exp, model, "test_rmse", [fnum(r["test_rmse"]) for r in xs]))
        local_c.append(add_cont(exp, model, "pi_error", [fnum(r["pi_error"]) for r in xs]))
        local_c.append(add_cont(exp, model, "final_n", [fnum(r["final_n"]) for r in xs]))
        local_r.append(add_rate(exp, model, "canonical", [is_true(r["canonical"]) for r in xs]))
        local_r.append(add_rate(exp, model, "explicit_r2", [is_true(r["explicit_r2"]) for r in xs]))

    # Paired vs model E (full method) on matched seeds — RMSE and canonical
    maps = {m: {int(r["seed"]): r for r in xs} for (m,), xs in by.items()}
    ref = "E"
    fam_c, fam_b = [], []
    if ref in maps:
        for m in sorted(maps):
            if m == ref:
                continue
            a = {s: fnum(maps[m][s]["test_rmse"]) for s in maps[m] if s in maps[ref]}
            b = {s: fnum(maps[ref][s]["test_rmse"]) for s in maps[ref] if s in maps[m]}
            pc = paired_continuous(a, b, f"model_{m}", f"model_{ref}", "test_rmse")
            pc["experiment"] = exp
            paired_cont_rows.append(pc)
            fam_c.append(pc)
            pb = mcnemar_exact(
                {s: is_true(maps[m][s]["canonical"]) for s in maps[m] if s in maps[ref]},
                {s: is_true(maps[ref][s]["canonical"]) for s in maps[ref] if s in maps[m]},
                f"model_{m}",
                f"model_{ref}",
                "canonical",
            )
            pb["experiment"] = exp
            paired_bin_rows.append(pb)
            fam_b.append(pb)
        apply_holm(fam_c, "wilcoxon_p")
        apply_holm(fam_b, "mcnemar_p")

    section_md.append(f"## {exp}\n")
    section_md.append(md_rate_table("Canonical / explicit r² rates by model", local_r))
    section_md.append(md_cont_table("Continuous metrics by model", local_c))
    section_md.append("**Paired vs Model E (matched seeds):** Holm-adjusted Wilcoxon / McNemar in paired CSVs.\n")


def exp08():
    rows = read_csv(RESULTS / "Exp08_PruningAblation" / "seed_logs.csv")
    exp = "Exp08_PruningAblation"
    local_c, local_r = [], []
    by = group_by(rows, "setting")
    for (setting,), xs in sorted(by.items()):
        local_c.append(add_cont(exp, setting, "test_rmse", [fnum(r["test_rmse"]) for r in xs]))
        local_c.append(add_cont(exp, setting, "pi_error", [fnum(r["pi_error"]) for r in xs]))
        local_c.append(add_cont(exp, setting, "final_n", [fnum(r["final_n"]) for r in xs]))
        local_r.append(add_rate(exp, setting, "canonical", [is_true(r["canonical"]) for r in xs]))
        local_r.append(add_rate(exp, setting, "structural", [is_true(r["structural"]) for r in xs]))

    maps = {s: {int(r["seed"]): r for r in xs} for (s,), xs in by.items()}
    ref = "r50k"
    fam_c, fam_b = [], []
    if ref in maps:
        for s in sorted(maps):
            if s == ref:
                continue
            a = {k: fnum(maps[s][k]["test_rmse"]) for k in maps[s] if k in maps[ref]}
            b = {k: fnum(maps[ref][k]["test_rmse"]) for k in maps[ref] if k in maps[s]}
            pc = paired_continuous(a, b, s, ref, "test_rmse")
            pc["experiment"] = exp
            paired_cont_rows.append(pc)
            fam_c.append(pc)
            pb = mcnemar_exact(
                {k: is_true(maps[s][k]["canonical"]) for k in maps[s] if k in maps[ref]},
                {k: is_true(maps[ref][k]["canonical"]) for k in maps[ref] if k in maps[s]},
                s,
                ref,
                "canonical",
            )
            pb["experiment"] = exp
            paired_bin_rows.append(pb)
            fam_b.append(pb)
        apply_holm(fam_c, "wilcoxon_p")
        apply_holm(fam_b, "mcnemar_p")

    section_md.append(f"## {exp}\n")
    section_md.append(md_rate_table("Canonical / structural rates by retrain setting", local_r))
    section_md.append(md_cont_table("Continuous metrics by setting", local_c))


def exp09():
    rows = read_csv(RESULTS / "Exp09_NetworkSize" / "seed_logs.csv")
    exp = "Exp09_NetworkSize"
    local_c, local_r = [], []
    by = group_by(rows, "task", "hidden")
    for key, xs in sorted(by.items(), key=lambda kv: (kv[0][0], int(kv[0][1]))):
        g = f"{key[0]}_H{key[1]}"
        local_c.append(add_cont(exp, g, "test_metric", [fnum(r["test_metric"]) for r in xs]))
        local_c.append(add_cont(exp, g, "pi_error", [fnum(r["pi_error"]) for r in xs]))
        local_r.append(add_rate(exp, g, "canonical", [is_true(r["canonical"]) for r in xs]))
        local_r.append(add_rate(exp, g, "structural", [is_true(r["structural"]) for r in xs]))
    local_r.append(add_rate(exp, "ALL", "canonical", [is_true(r["canonical"]) for r in rows]))
    section_md.append(f"## {exp}\n")
    section_md.append(md_rate_table("Canonical rates (incl. overall)", [r for r in local_r if r["metric"] == "canonical"]))
    section_md.append(md_cont_table("Continuous metrics by task × H", local_c))


def exp10():
    rows = read_csv(RESULTS / "Exp10_PruneTolerance" / "seed_logs.csv")
    exp = "Exp10_PruneTolerance"
    local_c, local_r = [], []
    by = group_by(rows, "factor")
    for (fac,), xs in sorted(by.items(), key=lambda kv: float(kv[0][0])):
        g = f"factor={fac}"
        for metric, col in [("test_rmse", "test_rmse"), ("pi_error", "pi_error"), ("compression", "compression"), ("terms", "terms")]:
            local_c.append(add_cont(exp, g, metric, [fnum(r[col]) for r in xs]))
        local_r.append(add_rate(exp, g, "canonical", [is_true(r["canonical"]) for r in xs]))
        local_r.append(add_rate(exp, g, "structural", [is_true(r["structural"]) for r in xs]))

    maps = {f: {int(r["seed"]): r for r in xs} for (f,), xs in by.items()}
    # Pair adjacent factors on compression and canonical vs factor=1.0
    ref = "1"
    if "1.0" in maps:
        ref = "1.0"
    fam_c, fam_b = [], []
    if ref in maps:
        for f in sorted(maps, key=float):
            if f == ref:
                continue
            a = {k: fnum(maps[f][k]["compression"]) for k in maps[f] if k in maps[ref]}
            b = {k: fnum(maps[ref][k]["compression"]) for k in maps[ref] if k in maps[f]}
            pc = paired_continuous(a, b, f"factor_{f}", f"factor_{ref}", "compression")
            pc["experiment"] = exp
            paired_cont_rows.append(pc)
            fam_c.append(pc)
            pb = mcnemar_exact(
                {k: is_true(maps[f][k]["canonical"]) for k in maps[f] if k in maps[ref]},
                {k: is_true(maps[ref][k]["canonical"]) for k in maps[ref] if k in maps[f]},
                f"factor_{f}",
                f"factor_{ref}",
                "canonical",
            )
            pb["experiment"] = exp
            paired_bin_rows.append(pb)
            fam_b.append(pb)
        apply_holm(fam_c, "wilcoxon_p")
        apply_holm(fam_b, "mcnemar_p")

    section_md.append(f"## {exp}\n")
    section_md.append(md_rate_table("Canonical / structural by prune factor", local_r))
    section_md.append(md_cont_table("Continuous metrics by factor", local_c))


def exp11():
    nn = read_csv(RESULTS / "Exp11_Baselines" / "nn_seed_logs.csv")
    sk = read_csv(RESULTS / "Exp11_Baselines" / "sklearn_seed_logs.csv")
    exp = "Exp11_Baselines"
    local_c, local_r = [], []
    all_rows = []
    for r in nn:
        all_rows.append({**r, "_src": "nn"})
    for r in sk:
        # Distinguish prior-feature variants in group label
        prior = "priorFE" if is_true(r.get("prior_features")) else "noFE"
        all_rows.append({**r, "method": f"{r['method']}_{prior}", "_src": "sk"})

    by = group_by(all_rows, "method")
    for (method,), xs in sorted(by.items()):
        local_c.append(add_cont(exp, method, "test_rmse", [fnum(r["test_rmse"]) for r in xs]))
        local_c.append(add_cont(exp, method, "pi_error", [fnum(r.get("pi_error")) for r in xs]))
        local_r.append(add_rate(exp, method, "canonical", [is_true(r["canonical"]) for r in xs]))
        local_r.append(
            add_rate(exp, method, "operator_recovery", [is_true(r.get("operator_recovery")) for r in xs])
        )

    # Paired HSM vs each other method on matched seeds (RMSE + operator_recovery)
    hsm = {int(r["seed"]): r for r in nn if r["method"] == "hsm"}
    others = {}
    for r in nn:
        if r["method"] != "hsm":
            others.setdefault(r["method"], {})[int(r["seed"])] = r
    for r in sk:
        prior = "priorFE" if is_true(r.get("prior_features")) else "noFE"
        others.setdefault(f"{r['method']}_{prior}", {})[int(r["seed"])] = r

    fam_c, fam_b = [], []
    for name, mp in sorted(others.items()):
        a = {s: fnum(hsm[s]["test_rmse"]) for s in hsm if s in mp}
        b = {s: fnum(mp[s]["test_rmse"]) for s in mp if s in hsm}
        pc = paired_continuous(a, b, "hsm", name, "test_rmse")
        pc["experiment"] = exp
        paired_cont_rows.append(pc)
        fam_c.append(pc)
        pb = mcnemar_exact(
            {s: is_true(hsm[s].get("operator_recovery", hsm[s].get("canonical"))) for s in hsm if s in mp},
            {s: is_true(mp[s].get("operator_recovery", mp[s].get("canonical"))) for s in mp if s in hsm},
            "hsm",
            name,
            "operator_recovery",
        )
        pb["experiment"] = exp
        paired_bin_rows.append(pb)
        fam_b.append(pb)
    apply_holm(fam_c, "wilcoxon_p")
    apply_holm(fam_b, "mcnemar_p")

    section_md.append(f"## {exp}\n")
    section_md.append(md_rate_table("Canonical / operator-recovery rates", local_r))
    section_md.append(md_cont_table("RMSE / π-error by method", local_c))
    section_md.append("**Paired HSM vs baselines (matched seeds):** Holm-adjusted tests in paired CSVs.\n")


def exp12():
    rows = read_csv(RESULTS / "Exp12_CrossProduct" / "seed_logs.csv")
    exp = "Exp12_CrossProduct"
    local_c, local_r = [], []
    by = group_by(rows, "problem")
    for (prob,), xs in sorted(by.items()):
        local_c.append(add_cont(exp, prob, "test_rmse", [fnum(r["test_rmse"]) for r in xs]))
        local_r.append(add_rate(exp, prob, "structure", [is_true(r["structure"]) for r in xs]))
        local_r.append(add_rate(exp, prob, "limitation_ok", [is_true(r["limitation_ok"]) for r in xs]))
        local_r.append(add_rate(exp, prob, "explicit_cross", [is_true(r["explicit_cross"]) for r in xs]))
    section_md.append(f"## {exp}\n")
    section_md.append(md_rate_table("Structure / limitation rates by problem", local_r))
    section_md.append(md_cont_table("RMSE by problem", local_c))
    section_md.append(
        "Note: structure-recovery frequency is the primary discovery metric here; "
        "cross/mixed expect non-exact x1*x2 (limitation_ok).\n"
    )


def exp13():
    rows = read_csv(RESULTS / "Exp13_EquationExport" / "seed_logs.csv")
    exp = "Exp13_EquationExport"
    local_c, local_r = [], []
    local_c.append(add_cont(exp, "all", "max_abs", [fnum(r["max_abs"]) for r in rows]))
    local_c.append(add_cont(exp, "all", "mean_abs", [fnum(r["mean_abs"]) for r in rows]))
    local_c.append(add_cont(exp, "all", "manual_max", [fnum(r["manual_max"]) for r in rows]))
    local_r.append(add_rate(exp, "all", "fidelity_pass", [is_true(r["fidelity_pass"]) for r in rows]))
    local_r.append(add_rate(exp, "all", "manual_pass", [is_true(r["manual_pass"]) for r in rows]))
    section_md.append(f"## {exp}\n")
    section_md.append(md_rate_table("Export fidelity pass rates", local_r))
    section_md.append(md_cont_table("Absolute export errors", local_c))


def write_outputs():
    OUT.mkdir(parents=True, exist_ok=True)
    write_csv(OUT / "continuous_summaries.csv", cont_rows)
    write_csv(OUT / "structure_rates.csv", rate_rows)
    write_csv(OUT / "paired_continuous.csv", paired_cont_rows)
    write_csv(OUT / "paired_binary.csv", paired_bin_rows)

    # Headline structure-recovery focus
    headline_bits = []
    for r in rate_rows:
        if r["metric"] in ("canonical", "structure_ok", "structure", "operator_recovery", "fidelity_pass") and (
            r["group"] in ("all", "ALL") or r["experiment"] in ("Exp08_PruningAblation", "Exp07_OperatorAblation", "Exp11_Baselines")
        ):
            if r["group"] in ("all", "ALL") or r["metric"] in ("canonical", "operator_recovery"):
                headline_bits.append(
                    f"{r['experiment']}/{r['group']}/{r['metric']}="
                    f"{r['k']}/{r['n']} ({fmt_pct(r['rate_pct'], 1)}% "
                    f"CI[{fmt_pct(r['ci95_lo_pct'], 1)}, {fmt_pct(r['ci95_hi_pct'], 1)}])"
                )

    # Compact headline: overall rates only
    focus = [
        r
        for r in rate_rows
        if r["group"] in ("all", "ALL")
        and r["metric"] in ("canonical", "structure_ok", "structural", "fidelity_pass", "manual_pass")
    ]
    lines = [
        f"Exp17 statistical reporting — {now()}",
        f"continuous groups={len(cont_rows)}, rate groups={len(rate_rows)}, "
        f"paired continuous={len(paired_cont_rows)}, paired binary={len(paired_bin_rows)}",
        "",
        "Structure-recovery (overall cells):",
    ]
    for r in focus:
        lines.append(
            f"  {r['experiment']} {r['metric']}: {r['k']}/{r['n']} = {fmt_pct(r['rate_pct'], 1)}% "
            f"(Wilson 95% CI {fmt_pct(r['ci95_lo_pct'], 1)}-{fmt_pct(r['ci95_hi_pct'], 1)}%)"
        )
    # Key paired
    lines.append("")
    lines.append("Key paired (Holm-adjusted):")
    for r in paired_cont_rows:
        if r["experiment"] in ("Exp01_CircleBoundary", "Exp11_Baselines") or (
            r["experiment"] == "Exp07_OperatorAblation" and "model_A" in r["comparison"]
        ):
            lines.append(
                f"  {r['experiment']} {r['comparison']} [{r['metric']}]: "
                f"meanDiff={fmt(r['mean_diff_a_minus_b'])}, dz={fmt(r['cohen_dz'])}, "
                f"Wilcoxon p={fmt(r['wilcoxon_p'])}, Holm p={fmt(r['holm_adj_p'])}"
            )
    (OUT / "headline.txt").write_text("\n".join(lines) + "\n", encoding="utf-8")

    md = [
        "# Exp17 — Statistical reporting",
        "",
        f"Generated: {now()}",
        "",
        "All statistics are computed from authoritative `Results/Exp0N_*/` seed logs.",
        "Continuous metrics: mean, SD, median, min, max, Student-t 95% CI.",
        "Rates: Wilson score 95% CI. Paired: Wilcoxon signed-rank + paired t + Cohen's dz; "
        "binary McNemar exact; Holm correction within each comparison family.",
        "",
        "## Files",
        "",
        "| File | Content |",
        "|------|---------|",
        "| `continuous_summaries.csv` | Per-group continuous stats |",
        "| `structure_rates.csv` | Structure / canonical / fidelity rates + Wilson CI |",
        "| `paired_continuous.csv` | Matched-seed continuous comparisons |",
        "| `paired_binary.csv` | Matched-seed McNemar comparisons |",
        "| `headline.txt` | Short resume summary |",
        "",
    ]
    md.extend(section_md)
    (OUT / "ALL_STATS.md").write_text("\n".join(md) + "\n", encoding="utf-8")

    (OUT / "INDEX.md").write_text(
        "\n".join(
            [
                "# Exp17 Statistics — index",
                "",
                f"Updated: {now()}",
                "",
                "- [ALL_STATS.md](ALL_STATS.md) — full markdown report",
                "- [continuous_summaries.csv](continuous_summaries.csv)",
                "- [structure_rates.csv](structure_rates.csv)",
                "- [paired_continuous.csv](paired_continuous.csv)",
                "- [paired_binary.csv](paired_binary.csv)",
                "- [headline.txt](headline.txt)",
                "",
                "Structure-recovery frequency is reported alongside predictive error for every repeated experiment.",
                "",
            ]
        ),
        encoding="utf-8",
    )


def update_mirror():
    mirror = ROOT / "GitHub_mirror"
    if not mirror.is_dir():
        return
    dest = mirror / "results" / "Exp17_Statistics"
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
        "| 01–16 | complete | see Results/ |",
        f"| 17 Statistical reporting | complete | "
        f"{len(cont_rows)} continuous + {len(rate_rows)} rate groups; "
        f"{len(paired_cont_rows)}+{len(paired_bin_rows)} paired |",
        "| 18–20 | queued | criteria / π-policy / manuscript gates |",
        "",
        "## Headline",
        "",
        (OUT / "headline.txt").read_text(encoding="utf-8").strip(),
        "",
        "See `results/Exp17_Statistics/INDEX.md`.",
        "",
    ]
    (mirror / "STATUS.md").write_text("\n".join(status) + "\n", encoding="utf-8")


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--root", default=str(Path(__file__).resolve().parent))
    args = ap.parse_args()
    global RESULTS, OUT, ROOT, cont_rows, rate_rows, paired_cont_rows, paired_bin_rows, section_md
    ROOT = Path(args.root)
    RESULTS = ROOT / "Results"
    OUT = RESULTS / "Exp17_Statistics"
    cont_rows, rate_rows, paired_cont_rows, paired_bin_rows, section_md = [], [], [], [], []

    builders = [
        exp01,
        exp02,
        exp03,
        exp04,
        lambda: exp05_06("Exp05", "Exp05_SyntheticRecovery"),
        lambda: exp05_06("Exp06", "Exp06_Noise"),
        exp07,
        exp08,
        exp09,
        exp10,
        exp11,
        exp12,
        exp13,
    ]
    for fn in builders:
        try:
            fn()
            print("ok", getattr(fn, "__name__", fn))
        except Exception as e:
            print(f"WARN {getattr(fn, '__name__', fn)}: {e}")

    write_outputs()
    update_mirror()
    print("Wrote", OUT)
    try:
        print((OUT / "headline.txt").read_text(encoding="utf-8"))
    except UnicodeEncodeError:
        print((OUT / "headline.txt").read_text(encoding="utf-8").encode("ascii", "replace").decode("ascii"))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
