# -*- coding: utf-8 -*-
"""Experiment 23: Kepler's third law on the NASA Planetary Fact Sheet.

Nine heliocentric bodies (Moon excluded). Semi-major axis and period are the
fact-sheet distance and orbital period, scaled by this table's Earth values so
Earth is (1 AU, 1 year). The short equation is P = a^(3/2).

Twenty shared holdouts: 6 train, 1 validation, 2 test. Log-log regression,
degree-3 polynomial SINDy (no a^(3/2) column), and gplearn with add/sub/mul/sqrt.
The quadratic graph is scored when NnPruneHsm --exp23 has written predictions.
"""
from __future__ import annotations

import csv
import time
from collections import defaultdict
from pathlib import Path

import numpy as np

import sr_common as sc

ROOT = Path(__file__).resolve().parent
OUT = ROOT / "Results" / "Exp23_Kepler"

# NASA Planetary Fact Sheet, metric:
# https://nssdc.gsfc.nasa.gov/planetary/factsheet/
# Columns: distance from Sun (10^6 km), orbital period (days).
# Moon is omitted; its fact-sheet distance is geocentric.
NASA = [
    ("Mercury", 57.9, 88.0),
    ("Venus", 108.2, 224.7),
    ("Earth", 149.6, 365.2),
    ("Mars", 228.0, 687.0),
    ("Jupiter", 778.5, 4331.0),
    ("Saturn", 1432.0, 10747.0),
    ("Uranus", 2867.0, 30589.0),
    ("Neptune", 4515.0, 59800.0),
    ("Pluto", 5906.4, 90560.0),
]
EARTH_KM = 149.6
EARTH_DAYS = 365.2
EXP_TOL = 0.05
CONST_TOL = 0.05


def bodies():
    rows = []
    for name, km, days in NASA:
        rows.append((name, km / EARTH_KM, 0.0, days / EARTH_DAYS))
    return rows


def write_splits(rows):
    OUT.mkdir(parents=True, exist_ok=True)
    with (OUT / "bodies.csv").open("w", newline="") as f:
        w = csv.writer(f)
        w.writerow(["name", "a_AU", "P_year", "P2_over_a3"])
        for name, a, _x2, p in rows:
            w.writerow([name, f"{a:.10g}", f"{p:.10g}", f"{(p * p) / (a ** 3):.10g}"])
    grid_a = np.logspace(np.log10(0.3), np.log10(45.0), 40)
    with (OUT / "grid.csv").open("w", newline="") as f:
        w = csv.writer(f)
        w.writerow(["x1", "x2"])
        for a in grid_a:
            w.writerow([f"{a:.10g}", "0"])
    idx = np.arange(len(rows))
    for seed in range(1, 21):
        rng = np.random.RandomState(1000 + seed)
        perm = rng.permutation(idx)
        test_i = perm[:2]
        val_i = perm[2:3]
        train_i = perm[3:]
        seed_dir = OUT / f"seed_{seed:02d}"
        seed_dir.mkdir(parents=True, exist_ok=True)
        _write_xy(seed_dir / "train.csv", rows, train_i)
        _write_xy(seed_dir / "validation.csv", rows, val_i)
        _write_xy(seed_dir / "test.csv", rows, test_i)
        (seed_dir / "indices.txt").write_text(
            "train " + ",".join(rows[i][0] for i in train_i) + "\n"
            + "val " + ",".join(rows[i][0] for i in val_i) + "\n"
            + "test " + ",".join(rows[i][0] for i in test_i) + "\n",
            encoding="utf-8",
        )


def _write_xy(path, rows, indices):
    with path.open("w", newline="") as f:
        w = csv.writer(f)
        w.writerow(["x1", "x2", "y"])
        for i in indices:
            name, a, x2, p = rows[i]
            w.writerow([f"{a:.10g}", f"{x2:.1f}", f"{p:.10g}"])


def poly1(a):
    a = np.asarray(a, dtype=float).reshape(-1)
    return np.column_stack([np.ones_like(a), a, a ** 2, a ** 3])


def stlsq_1d(a, y, threshold):
    theta = poly1(a)
    return sc.stlsq(theta, np.asarray(y, dtype=float), threshold)


def fit_sindy_1d(a_tr, y_tr, a_va, y_va):
    scale = float(np.std(y_tr)) or 1.0
    best = None
    for frac in (1e-6, 1e-3, 1e-2, 0.05, 0.1, 0.25):
        coef = stlsq_1d(a_tr, y_tr, frac * scale)
        pred = poly1(a_va) @ coef
        err = sc.rmse(y_va, pred)
        nnz = int(np.sum(np.abs(coef) > 0))
        key = (err if np.isfinite(err) else 1e300, nnz)
        if best is None or key < best[0]:
            best = (key, coef, frac)
    return best[1], best[2]


def power_metrics(a, p_hat):
    a = np.asarray(a, dtype=float)
    p = np.asarray(p_hat, dtype=float)
    out = {
        "term_recovery": 0,
        "exponent": float("nan"),
        "const": float("nan"),
        "exponent_rel": float("nan"),
        "const_rel": float("nan"),
        "grid_rmse": float("nan"),
    }
    if p.size == 0 or not np.all(np.isfinite(p)) or np.any(p <= 1e-8):
        return out
    b, logc = np.polyfit(np.log(a), np.log(p), 1)
    c = float(np.exp(logc))
    out["exponent"] = float(b)
    out["const"] = c
    out["exponent_rel"] = abs(float(b) - 1.5) / 1.5
    out["const_rel"] = abs(c - 1.0)
    out["grid_rmse"] = sc.rmse(p, a ** 1.5)
    out["term_recovery"] = int(out["exponent_rel"] <= EXP_TOL and out["const_rel"] <= CONST_TOL)
    return out


def score_prediction(method, seed, expr, runtime, a_te, p_te, p_hat_te, a_grid, p_grid, note):
    m = power_metrics(a_grid, p_grid)
    return {
        "method": method,
        "seed": seed,
        "term_recovery": m["term_recovery"],
        "exponent": m["exponent"],
        "const": m["const"],
        "exponent_rel": m["exponent_rel"],
        "const_rel": m["const_rel"],
        "grid_rmse": m["grid_rmse"],
        "test_rmse_year": sc.rmse(p_te, p_hat_te),
        "runtime_s": runtime,
        "equation": " ".join((expr or "").split())[:240],
        "note": note,
    }


def eval_symbolic():
    grid = sc.load_table(OUT / "grid.csv")
    a_grid = grid[:, 0]
    rows = []
    # Full-sample display fits (not in the 20-seed rate).
    body = list(bodies())
    a_all = np.array([b[1] for b in body])
    p_all = np.array([b[3] for b in body])
    b_emp, logc_emp = np.polyfit(np.log(a_all), np.log(p_all), 1)
    rows.append(score_prediction(
        "loglog_ols", 0, f"P={np.exp(logc_emp):.6g}*a^{b_emp:.6g}", 0.0,
        a_all, p_all, np.exp(logc_emp) * a_all ** b_emp,
        a_grid, np.exp(logc_emp) * a_grid ** b_emp,
        f"full sample; empirical exponent on the 9 bodies is {b_emp:.6g}",
    ))

    for seed in range(1, 21):
        seed_dir = OUT / f"seed_{seed:02d}"
        tr = sc.load_table(seed_dir / "train.csv")
        va = sc.load_table(seed_dir / "validation.csv")
        te = sc.load_table(seed_dir / "test.csv")
        a_tr, p_tr = tr[:, 0], tr[:, 2]
        a_va, p_va = va[:, 0], va[:, 2]
        a_te, p_te = te[:, 0], te[:, 2]

        t0 = time.perf_counter()
        b, logc = np.polyfit(np.log(a_tr), np.log(p_tr), 1)
        c = float(np.exp(logc))
        rows.append(score_prediction(
            "loglog_ols", seed, f"P={c:.6g}*a^{b:.6g}", time.perf_counter() - t0,
            a_te, p_te, c * a_te ** b, a_grid, c * a_grid ** b, "classical log-log fit on the training bodies",
        ))

        t0 = time.perf_counter()
        coef, frac = fit_sindy_1d(a_tr, p_tr, a_va, p_va)
        names = ["1", "a", "a^2", "a^3"]
        expr = " + ".join(f"{float(c_):.6g}*{n}" for n, c_ in zip(names, coef) if abs(float(c_)) > 1e-8) or "0"
        pred_te = poly1(a_te) @ coef
        pred_g = poly1(a_grid) @ coef
        rows.append(score_prediction(
            "sindy_poly3", seed, expr, time.perf_counter() - t0,
            a_te, p_te, pred_te, a_grid, pred_g, f"threshold_frac={frac:.3g}; library 1,a,a^2,a^3",
        ))

        t0 = time.perf_counter()
        try:
            est = sc.make_regressor(seed, ("add", "sub", "mul", "sqrt"))
            est.fit(a_tr.reshape(-1, 1), p_tr)
            expr = str(est._program)
            pred_te = np.asarray(est.predict(a_te.reshape(-1, 1)), dtype=float)
            pred_g = np.asarray(est.predict(a_grid.reshape(-1, 1)), dtype=float)
            rows.append(score_prediction(
                "gplearn", seed, expr, time.perf_counter() - t0,
                a_te, p_te, pred_te, a_grid, pred_g, "functions add, sub, mul, sqrt",
            ))
        except Exception as exc:
            rows.append(score_prediction(
                "gplearn", seed, "", time.perf_counter() - t0,
                a_te, p_te, np.full(a_te.shape, np.nan), a_grid, np.full(a_grid.shape, np.nan),
                f"failed: {exc}",
            ))

        pred_path = seed_dir / "pred_test.csv"
        grid_path = seed_dir / "pred_grid.csv"
        if pred_path.exists() and grid_path.exists():
            te_hat = sc.load_table(pred_path)
            g_hat = sc.load_table(grid_path)
            eq_path = seed_dir / "exported_equation.txt"
            eq = eq_path.read_text(encoding="utf-8", errors="replace") if eq_path.exists() else ""
            rt = seed_dir / "hsm_runtime.txt"
            runtime = float(rt.read_text(encoding="utf-8").strip()) if rt.exists() else float("nan")
            rows.append(score_prediction(
                "hsm", seed, "standardized quadratic graph; " + eq.replace("\n", " ")[:180],
                runtime, te_hat[:, 0], te_hat[:, 2], te_hat[:, 3],
                g_hat[:, 0], g_hat[:, 2],
                "inputs standardized on the training fold; x2 fixed at 0; predictions in years",
            ))
        print(f"exp23 seed {seed}", flush=True)
    return rows, float(b_emp)


def summarize(rows, empirical_exp):
    scored = [r for r in rows if int(r["seed"]) != 0]
    by = defaultdict(list)
    for r in scored:
        by[r["method"]].append(r)
    lines = [
        "# Experiment 23 — Kepler's third law",
        "",
        "NASA Planetary Fact Sheet distances and orbital periods, Moon excluded,",
        "scaled by this table's Earth values (149.6e6 km, 365.2 days).",
        f"Empirical log-log exponent on all 9 bodies: {empirical_exp:.6g} (law is 1.5).",
        "Term recovery: the model's predictions on a fixed log-spaced grid satisfy",
        f"|exponent-1.5|/1.5 <= {EXP_TOL} and |const-1| <= {CONST_TOL} for P = const * a^exponent.",
        "SINDy's library is {1, a, a^2, a^3}, which does not contain a^(3/2).",
        "",
        "| Method | n | Power recovery | Mean |exp-1.5|/1.5 | Mean |c-1| | Mean holdout RMSE (years) |",
        "|--------|---|----------------|---------------------|------------|----------------------------|",
    ]
    for method in ("loglog_ols", "sindy_poly3", "gplearn", "hsm"):
        xs = by.get(method, [])
        if not xs:
            continue
        n = len(xs)
        rec = sum(int(r["term_recovery"]) for r in xs)
        er = sc.mean_finite([r["exponent_rel"] for r in xs])
        cr = sc.mean_finite([r["const_rel"] for r in xs])
        rm = sc.mean_finite([r["test_rmse_year"] for r in xs])
        lines.append(f"| {method} | {n} | {rec}/{n} | {er:.4g} | {cr:.4g} | {rm:.4g} |")
    full = [r for r in rows if int(r["seed"]) == 0 and r["method"] == "loglog_ols"]
    lines.append("")
    if full:
        lines.append("Full-sample log-log equation: " + full[0]["equation"])
    # Quote one recovered gplearn expression if any, else the first.
    gp = [r for r in scored if r["method"] == "gplearn" and r["equation"]]
    gp_hit = [r for r in gp if int(r["term_recovery"]) == 1]
    shown = (gp_hit or gp)
    if shown:
        lines.append("Example gplearn expression (seed %s): %s" % (shown[0]["seed"], shown[0]["equation"]))
    if "hsm" not in by:
        lines.append("HSM predictions are not in this table yet. Run NnPruneHsm --exp23 and rerun this script.")
    else:
        lines.append("HSM was trained on training-fold standardized (a, 0) and scored in years. It has no a^(3/2) operator.")
    lines.append("")
    return "\n".join(lines) + "\n"


def main():
    sc.self_check()
    write_splits(bodies())
    rows, empirical = eval_symbolic()
    fields = list(rows[0].keys())
    with (OUT / "seed_logs.csv").open("w", newline="") as f:
        w = csv.DictWriter(f, fieldnames=fields)
        w.writeheader()
        w.writerows(rows)
    text = summarize(rows, empirical)
    (OUT / "headline.txt").write_text(text, encoding="utf-8")
    (OUT / "INDEX.md").write_text(text, encoding="utf-8")
    protocol = (
        "# Experiment 23 protocol\n\n"
        "Source: NASA Planetary Fact Sheet (metric), Moon excluded.\n"
        "https://nssdc.gsfc.nasa.gov/planetary/factsheet/\n\n"
        "a_AU = distance_1e6km / 149.6, P_year = period_days / 365.2.\n"
        "Target law P = a^(3/2). Twenty seeds, RandomState(1000+seed),\n"
        "permutation holdout of 2 test, 1 validation, 6 train.\n"
        "Power recovery on a fixed grid a in logspace(0.3, 45, 40),\n"
        "not on the two held-out planets alone.\n"
    )
    (OUT / "PROTOCOL.md").write_text(protocol, encoding="utf-8")
    print(text)


if __name__ == "__main__":
    main()
