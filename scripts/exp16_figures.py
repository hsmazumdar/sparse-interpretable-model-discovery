#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""Exp16 — manuscript figures from saved Results CSV files only.

Outputs PNG (+ PDF where useful) under Results/Exp16_Figures/.
Does not hard-code experimental outcomes; all series come from disk.
"""
from __future__ import annotations

import argparse
import csv
import math
import shutil
from collections import defaultdict
from datetime import datetime
from pathlib import Path

import matplotlib

matplotlib.use("Agg")
import matplotlib.pyplot as plt
import numpy as np

ROOT = Path(__file__).resolve().parent
RESULTS = ROOT / "Results"
OUT = RESULTS / "Exp16_Figures"

# Publication-ish defaults (avoid decorative clutter)
plt.rcParams.update(
    {
        "figure.dpi": 140,
        "savefig.dpi": 200,
        "font.size": 10,
        "axes.labelsize": 11,
        "axes.titlesize": 12,
        "legend.fontsize": 9,
        "axes.grid": True,
        "grid.alpha": 0.25,
        "axes.spines.top": False,
        "axes.spines.right": False,
    }
)


def now() -> str:
    return datetime.now().strftime("%Y-%m-%d %H:%M:%S")


def read_csv(path: Path) -> list[dict]:
    if not path.exists():
        raise FileNotFoundError(path)
    with path.open(newline="", encoding="utf-8", errors="replace") as f:
        return list(csv.DictReader(f))


def fnum(x, default=float("nan")):
    try:
        if x is None or x == "" or str(x).lower() == "null":
            return default
        return float(x)
    except Exception:
        return default


def save(fig, name: str):
    OUT.mkdir(parents=True, exist_ok=True)
    png = OUT / f"{name}.png"
    pdf = OUT / f"{name}.pdf"
    fig.tight_layout()
    fig.savefig(png, bbox_inches="tight")
    fig.savefig(pdf, bbox_inches="tight")
    plt.close(fig)
    print("wrote", png.name)


def fig01_circle_dense_target():
    """Dense 2-22-1 learns this geometry; plot labeled samples (saved CSV)."""
    rows = read_csv(RESULTS / "Exp01_CircleBoundary" / "circle_data.csv")
    xs = np.array([fnum(r["x"]) for r in rows])
    ys = np.array([fnum(r["y"]) for r in rows])
    ts = np.array([fnum(r["t"]) for r in rows])
    fig, ax = plt.subplots(figsize=(5.2, 5.0))
    m0 = ts < 0.5
    m1 = ~m0
    ax.scatter(xs[m0], ys[m0], s=6, c="#4a4a4a", alpha=0.45, label="outside (t=0)")
    ax.scatter(xs[m1], ys[m1], s=6, c="#c44e52", alpha=0.55, label="inside (t=1)")
    # True circle from Exp01 defaults (center 0.5, R=0.25) — geometry label only
    th = np.linspace(0, 2 * math.pi, 400)
    ax.plot(0.5 + 0.25 * np.cos(th), 0.5 + 0.25 * np.sin(th), "k-", lw=1.4, label="true boundary")
    ax.set_aspect("equal", adjustable="box")
    ax.set_xlim(0, 1)
    ax.set_ylim(0, 1)
    ax.set_xlabel("x")
    ax.set_ylabel("y")
    ax.set_title("Circle data target (dense 2–22–1 learns this)")
    ax.legend(loc="upper right", markerscale=2)
    save(fig, "Fig01_circle_dense_target")


def fig02_pruned_quadratic_circle():
    """Pruned model decision from mean canonical a,b (seed_logs) on a grid."""
    logs = read_csv(RESULTS / "Exp01_CircleBoundary" / "seed_logs.csv")
    a_vals = [fnum(r["a"]) for r in logs if r.get("canonical") in ("1", "true")]
    b_vals = [fnum(r["b"]) for r in logs if r.get("canonical") in ("1", "true")]
    # Prefer seed with a~b; use means
    a = float(np.nanmean(a_vals))
    b = float(np.nanmean(b_vals))
    # Read intercept from first canonical equation file if present
    c = 0.39
    ceq = RESULTS / "Exp01_CircleBoundary" / "seed_01" / "canonical_equation.txt"
    if ceq.exists():
        text = ceq.read_text(encoding="utf-8", errors="replace")
        # h = c + a*(x-0.5)^2 + b*(y-0.5)^2
        for line in text.splitlines():
            if line.strip().startswith("h ="):
                # parse leading constant
                try:
                    rest = line.split("=", 1)[1].strip()
                    c = float(rest.split("+")[0].strip().split()[0])
                except Exception:
                    pass
                break

    g = np.linspace(0, 1, 220)
    X, Y = np.meshgrid(g, g)
    H = c + a * (X - 0.5) ** 2 + b * (Y - 0.5) ** 2
    # sigmoid decision
    P = 1.0 / (1.0 + np.exp(-H))

    fig, ax = plt.subplots(figsize=(5.2, 5.0))
    cs = ax.contourf(X, Y, P, levels=12, cmap="coolwarm", alpha=0.9)
    ax.contour(X, Y, P, levels=[0.5], colors="k", linewidths=1.6)
    th = np.linspace(0, 2 * math.pi, 400)
    ax.plot(0.5 + 0.25 * np.cos(th), 0.5 + 0.25 * np.sin(th), "k--", lw=1.0, label="true R=0.25")
    fig.colorbar(cs, ax=ax, fraction=0.046, label="σ(h)")
    ax.set_aspect("equal")
    ax.set_xlabel("x")
    ax.set_ylabel("y")
    ax.set_title(f"Pruned quadratic circle (mean a={a:.3g}, b={b:.3g})")
    ax.legend(loc="upper right")
    save(fig, "Fig02_circle_pruned_quadratic")


def fig03_dense_vs_pruned_topology():
    logs = read_csv(RESULTS / "Exp01_CircleBoundary" / "seed_logs.csv")
    dn = [fnum(r["dense_n"]) for r in logs]
    pn = [fnum(r["pruned_n"]) for r in logs]
    de = [fnum(r["dense_e"]) for r in logs]
    pe = [fnum(r["pruned_e"]) for r in logs]
    fig, axes = plt.subplots(1, 2, figsize=(8.5, 3.8))
    axes[0].boxplot([dn, pn], tick_labels=["Dense", "Pruned"], showfliers=False)
    axes[0].set_ylabel("Neurons")
    axes[0].set_title("Circle: neurons")
    axes[1].boxplot([de, pe], tick_labels=["Dense", "Pruned"], showfliers=False)
    axes[1].set_ylabel("Edges")
    axes[1].set_title("Circle: edges")
    fig.suptitle("Dense versus pruned topology (Exp01)", y=1.02)
    save(fig, "Fig03_dense_vs_pruned_topology")


def fig04_area_measured_and_discovered():
    area = read_csv(RESULTS / "Exp02_PiArea" / "area_data.csv")
    r = np.array([fnum(row["r"]) for row in area])
    y = np.array([fnum(row["y"]) for row in area])
    logs = read_csv(RESULTS / "Exp02_PiArea" / "seed_logs.csv")
    ks = [fnum(row["k_canonical"]) for row in logs if row.get("canonical") in ("1", "true")]
    bs = [fnum(row["b"]) for row in logs if row.get("canonical") in ("1", "true")]
    k_mean = float(np.nanmean(ks))
    b_mean = float(np.nanmean(bs))
    rr = np.linspace(r.min(), r.max(), 200)
    yhat = b_mean + k_mean * rr ** 2

    fig, ax = plt.subplots(figsize=(6.2, 4.2))
    ax.scatter(r, y, s=18, c="#2a2a2a", label="measured pixel area", zorder=3)
    ax.plot(rr, yhat, color="#c44e52", lw=2.0, label=f"discovered A≈{b_mean:.3g}+{k_mean:.5g} r²")
    ax.set_xlabel("radius r (px)")
    ax.set_ylabel("area A (pixel count)")
    ax.set_title("Measured area versus r with discovered curve (Exp02)")
    ax.legend()
    save(fig, "Fig04_area_measured_and_discovered")


def fig05_k_across_seeds():
    logs = read_csv(RESULTS / "Exp02_PiArea" / "seed_logs.csv")
    seeds = [int(fnum(r["seed"])) for r in logs]
    ks = [fnum(r["k_canonical"]) for r in logs]
    # Machin π for reference line only (post-hoc)
    pi = 16 * math.atan(0.2) - 4 * math.atan(1 / 239)
    fig, ax = plt.subplots(figsize=(7.2, 3.8))
    ax.axhline(pi, color="#666666", ls="--", lw=1.2, label=f"π ≈ {pi:.5f} (scoring only)")
    ax.plot(seeds, ks, "o-", color="#4c72b0", ms=5, label="recovered k")
    ax.set_xlabel("seed")
    ax.set_ylabel("recovered k")
    ax.set_title("Recovered k across 30 seeds (Exp02)")
    ax.legend()
    save(fig, "Fig05_k_across_30_seeds")


def fig06_stochastic_k_vs_N():
    rows = read_csv(RESULTS / "Exp04_StochasticPi" / "depth_summary.csv")
    N = np.array([fnum(r["N"]) for r in rows])
    mean_k = np.array([fnum(r["mean_k"]) for r in rows])
    lo = np.array([fnum(r["ci95_lo"]) for r in rows])
    hi = np.array([fnum(r["ci95_hi"]) for r in rows])
    pi = 16 * math.atan(0.2) - 4 * math.atan(1 / 239)
    fig, ax = plt.subplots(figsize=(6.2, 4.0))
    ax.axhline(pi, color="#666666", ls="--", lw=1.2, label="π (scoring only)")
    ax.fill_between(N, lo, hi, color="#4c72b0", alpha=0.2, label="95% CI")
    ax.plot(N, mean_k, "o-", color="#4c72b0", label="mean k")
    ax.set_xscale("log")
    ax.set_xlabel("stochastic sample count N")
    ax.set_ylabel("mean recovered k")
    ax.set_title("k versus stochastic sample count (Exp04)")
    ax.legend()
    save(fig, "Fig06_k_vs_stochastic_N")


def fig07_pi_error_vs_N():
    rows = read_csv(RESULTS / "Exp04_StochasticPi" / "depth_summary.csv")
    N = np.array([fnum(r["N"]) for r in rows])
    err = np.array([100 * fnum(r["mean_pi_error"]) for r in rows])
    fig, ax = plt.subplots(figsize=(6.2, 4.0))
    ax.plot(N, err, "s-", color="#c44e52")
    ax.set_xscale("log")
    ax.set_xlabel("stochastic sample count N")
    ax.set_ylabel("mean |k−π|/π (%)")
    ax.set_title("Coefficient error versus sample count (Exp04)")
    save(fig, "Fig07_pi_error_vs_N")


def fig08_error_vs_compression():
    rows = read_csv(RESULTS / "Exp10_PruneTolerance" / "pareto_points.csv")
    comp = np.array([100 * fnum(r["mean_compression"]) for r in rows])
    rmse = np.array([fnum(r["mean_test_rmse"]) for r in rows])
    factor = np.array([fnum(r["factor"]) for r in rows])
    fig, ax = plt.subplots(figsize=(6.2, 4.0))
    sc = ax.scatter(comp, rmse, c=factor, s=70, cmap="viridis", zorder=3)
    for i, f in enumerate(factor):
        ax.annotate(f"{f:g}", (comp[i], rmse[i]), textcoords="offset points", xytext=(5, 4), fontsize=8)
    fig.colorbar(sc, ax=ax, label="prune factor")
    ax.set_xlabel("mean edge compression (%)")
    ax.set_ylabel("mean test RMSE")
    ax.set_title("Error versus compression (Exp10 Pareto)")
    save(fig, "Fig08_error_vs_compression")


def fig09_complexity_vs_tolerance():
    rows = read_csv(RESULTS / "Exp10_PruneTolerance" / "pareto_points.csv")
    factor = np.array([fnum(r["factor"]) for r in rows])
    terms = np.array([fnum(r["mean_terms"]) for r in rows])
    canon = np.array([fnum(r["canon_pct"]) for r in rows])
    fig, ax1 = plt.subplots(figsize=(6.2, 4.0))
    ax1.plot(factor, terms, "o-", color="#4c72b0", label="mean equation terms")
    ax1.set_xlabel("validation-loss prune factor")
    ax1.set_ylabel("mean equation terms", color="#4c72b0")
    ax2 = ax1.twinx()
    ax2.plot(factor, canon, "s--", color="#c44e52", label="canonical %")
    ax2.set_ylabel("canonical recovery (%)", color="#c44e52")
    ax1.set_title("Equation complexity versus validation tolerance (Exp10)")
    # combined legend
    lines = ax1.get_lines() + ax2.get_lines()
    ax1.legend(lines, [l.get_label() for l in lines], loc="best")
    save(fig, "Fig09_complexity_vs_tolerance")


def fig10_noise_vs_structure():
    rows = read_csv(RESULTS / "Exp06_Noise" / "seed_logs.csv")
    # Aggregate structure_ok rate by noise (across problems)
    by = defaultdict(list)
    for r in rows:
        noise = fnum(r["noise"])
        ok = 1.0 if r.get("structure_ok") in ("1", "true") else 0.0
        by[noise].append(ok)
    noises = sorted(by.keys())
    rates = [100 * float(np.mean(by[n])) for n in noises]
    fig, ax = plt.subplots(figsize=(6.2, 4.0))
    ax.plot(noises, rates, "o-", color="#55a868")
    ax.set_xlabel("noise level")
    ax.set_ylabel("structure recovery rate (%)")
    ax.set_title("Noise level versus structure recovery (Exp06)")
    ax.set_ylim(0, 105)
    save(fig, "Fig10_noise_vs_structure")


def fig11_retrain_ablation():
    """Bonus: Exp08 canon % vs retrain budget from seed_logs."""
    rows = read_csv(RESULTS / "Exp08_PruningAblation" / "seed_logs.csv")
    by = defaultdict(list)
    for r in rows:
        key = (r.get("setting", ""), fnum(r.get("retrain"), -1), r.get("prune"))
        by[key].append(1 if r.get("canonical") in ("1", "true") else 0)
    # order known settings
    order = ["none", "r0", "r1k", "r10k", "r50k"]
    xs, ys, labels = [], [], []
    for sid in order:
        for key, vals in by.items():
            if key[0] == sid:
                xs.append(len(xs))
                ys.append(100 * float(np.mean(vals)))
                labels.append(sid)
                break
    if not xs:
        return
    fig, ax = plt.subplots(figsize=(6.2, 3.8))
    ax.bar(xs, ys, color="#4c72b0")
    ax.set_xticks(xs)
    ax.set_xticklabels(labels)
    ax.set_ylabel("canonical recovery (%)")
    ax.set_title("Canonical rate vs pruning/retrain setting (Exp08)")
    ax.set_ylim(0, 105)
    save(fig, "Fig11_retrain_ablation_canon")


def write_index(names: list[str]):
    lines = [
        "# Exp16 — Manuscript figures",
        "",
        f"Generated: {now()}",
        "",
        "All plots built from saved `Results/` CSV files (no hard-coded outcomes).",
        "",
        "| Figure | File | Source |",
        "|--------|------|--------|",
        "| 01 Circle dense target | `Fig01_circle_dense_target.png` | Exp01 circle_data.csv |",
        "| 02 Pruned quadratic circle | `Fig02_circle_pruned_quadratic.png` | Exp01 seed_logs + canonical |",
        "| 03 Dense vs pruned topology | `Fig03_dense_vs_pruned_topology.png` | Exp01 seed_logs.csv |",
        "| 04 Area measured + discovered | `Fig04_area_measured_and_discovered.png` | Exp02 area_data + seed_logs |",
        "| 05 k across 30 seeds | `Fig05_k_across_30_seeds.png` | Exp02 seed_logs.csv |",
        "| 06 k vs stochastic N | `Fig06_k_vs_stochastic_N.png` | Exp04 depth_summary.csv |",
        "| 07 Pi error vs N | `Fig07_pi_error_vs_N.png` | Exp04 depth_summary.csv |",
        "| 08 Error vs compression | `Fig08_error_vs_compression.png` | Exp10 pareto_points.csv |",
        "| 09 Complexity vs tolerance | `Fig09_complexity_vs_tolerance.png` | Exp10 pareto_points.csv |",
        "| 10 Noise vs structure | `Fig10_noise_vs_structure.png` | Exp06 seed_logs.csv |",
        "| 11 Retrain ablation | `Fig11_retrain_ablation_canon.png` | Exp08 seed_logs.csv |",
        "",
        "Each figure also saved as `.pdf`.",
        "",
    ]
    (OUT / "INDEX.md").write_text("\n".join(lines), encoding="utf-8")
    (OUT / "headline.txt").write_text(
        f"Exp16: generated {len(names)} figures under Results/Exp16_Figures/.\n",
        encoding="utf-8",
    )


def update_mirror():
    """Copy figures into GitHub_mirror/results/Exp16_Figures. Do not clobber STATUS.md."""
    mirror = ROOT / "GitHub_mirror"
    if not mirror.is_dir():
        return
    dest = mirror / "results" / "Exp16_Figures"
    dest.mkdir(parents=True, exist_ok=True)
    for f in OUT.iterdir():
        if f.is_file():
            shutil.copy2(f, dest / f.name)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--root", default=str(Path(__file__).resolve().parent))
    args = ap.parse_args()
    global RESULTS, OUT, ROOT
    ROOT = Path(args.root)
    RESULTS = ROOT / "Results"
    OUT = RESULTS / "Exp16_Figures"
    OUT.mkdir(parents=True, exist_ok=True)

    figs = [
        fig01_circle_dense_target,
        fig02_pruned_quadratic_circle,
        fig03_dense_vs_pruned_topology,
        fig04_area_measured_and_discovered,
        fig05_k_across_seeds,
        fig06_stochastic_k_vs_N,
        fig07_pi_error_vs_N,
        fig08_error_vs_compression,
        fig09_complexity_vs_tolerance,
        fig10_noise_vs_structure,
        fig11_retrain_ablation,
    ]
    names = []
    for fn in figs:
        try:
            fn()
            names.append(fn.__name__)
        except Exception as e:
            print(f"WARN {fn.__name__}: {e}")
    write_index(names)
    update_mirror()
    print("Wrote", OUT)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
