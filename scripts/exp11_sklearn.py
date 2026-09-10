#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""Exp11 sklearn baselines on C#-exported area splits (same train/test).

Compares predictive RMSE, equation size / sparsity, operator recovery (A≈b+kr²),
and whether prior polynomial feature engineering was required.

π is never used as a training objective — only for optional post-hoc |k-π|/π.
"""
from __future__ import annotations

import argparse
import csv
import math
import os
import re
import sys
import time
from collections import defaultdict
from pathlib import Path

import numpy as np

try:
    from sklearn.linear_model import LinearRegression, Lasso
    from sklearn.preprocessing import PolynomialFeatures
    from sklearn.pipeline import Pipeline
    from sklearn.tree import DecisionTreeRegressor
    from sklearn.ensemble import RandomForestRegressor, GradientBoostingRegressor
    from sklearn.neural_network import MLPRegressor
except ImportError as e:
    print("ERROR: scikit-learn required:", e, file=sys.stderr)
    sys.exit(1)


def machin_pi() -> float:
    """Post-hoc scoring only (same spirit as C# ReferencePi)."""
    return 16.0 * math.atan(0.2) - 4.0 * math.atan(1.0 / 239.0)


def load_split(path: Path):
    r, a = [], []
    with path.open(newline="") as f:
        reader = csv.DictReader(f)
        for row in reader:
            r.append(float(row["r"]))
            a.append(float(row["A"]))
    return np.asarray(r, dtype=float).reshape(-1, 1), np.asarray(a, dtype=float)


def rmse(y_true, y_pred) -> float:
    y_true = np.asarray(y_true, dtype=float)
    y_pred = np.asarray(y_pred, dtype=float)
    return float(np.sqrt(np.mean((y_true - y_pred) ** 2)))


def poly2_structure(pipe, r_train) -> tuple[bool, bool, float, float, str, int]:
    """Return structural, canonical, k, b, equation, n_terms for deg-2 poly OLS/Lasso."""
    poly: PolynomialFeatures = pipe.named_steps["poly"]
    model = pipe.named_steps["model"]
    # Feature order for degree=2, include_bias=False on r: [r, r^2]
    # With include_bias=True in PolynomialFeatures: [1, r, r^2]
    coef = np.asarray(model.coef_, dtype=float).ravel()
    intercept = float(model.intercept_) if hasattr(model, "intercept_") else 0.0
    names = list(poly.get_feature_names_out(["r"]))
    # Map coefficients
    c = {"1": intercept, "r": 0.0, "r^2": 0.0}
    # If bias in features, intercept may be 0 and coef has bias term
    for name, w in zip(names, coef):
        key = name.replace(" ", "")
        if key in ("1",):
            c["1"] = float(w) + intercept
        elif key == "r":
            c["r"] = float(w)
        elif key in ("r^2", "r**2"):
            c["r^2"] = float(w)
        else:
            # unexpected
            pass
    # If PolynomialFeatures include_bias=False, intercept is separate
    if "1" not in names:
        c["1"] = intercept

    b = c["1"]
    c1 = c["r"]
    c2 = c["r^2"]
    r_mean = float(np.mean(r_train))
    # Structure: quadratic present and dominates linear contribution at mean r
    lin_mag = abs(c1) * r_mean
    quad_mag = abs(c2) * (r_mean ** 2)
    structural = abs(c2) > 1e-12 and quad_mag >= 0.05 * max(lin_mag, quad_mag, 1e-12)
    # Canonical A=b+kr^2: linear term small vs quadratic
    canonical = structural and (lin_mag <= 0.05 * max(quad_mag, 1e-12))
    eq = f"A = {b:.6g} + {c1:.6g}*r + {c2:.6g}*r^2"
    if canonical:
        eq = f"A = {b:.6g} + {c2:.6g}*r^2"
    n_terms = sum(1 for v in (b, c1, c2) if abs(v) > 1e-10)
    k = c2 if canonical or structural else float("nan")
    return structural, canonical, k, b, eq, n_terms


def lin_structure(model, r_train) -> tuple[bool, bool, float, float, str, int]:
    c1 = float(np.asarray(model.coef_).ravel()[0])
    b = float(model.intercept_)
    # Linear-only cannot be A=b+kr^2
    eq = f"A = {b:.6g} + {c1:.6g}*r"
    return False, False, float("nan"), b, eq, 2 if abs(b) > 1e-10 else 1


def fit_methods(Xtr, ytr, Xte, yte, seed: int):
    rows = []
    pi = machin_pi()

    def add(method_id, name, prior_fe, auto_simp, pred, structural, canonical, k, eq, terms, t0):
        err = rmse(yte, pred)
        pi_err = abs(k - pi) / pi if canonical and math.isfinite(k) else float("nan")
        rows.append(
            {
                "method": method_id,
                "name": name,
                "family": "sklearn",
                "seed": seed,
                "canonical": int(canonical),
                "structural": int(structural),
                "operator_recovery": int(structural or canonical),
                "prior_features": int(prior_fe),
                "auto_simplify": int(auto_simp),
                "k": k if math.isfinite(k) else "",
                "pi_error": pi_err if math.isfinite(pi_err) else "",
                "test_rmse": err,
                "terms": terms,
                "equation": eq.replace(",", ";"),
                "runtime_s": time.perf_counter() - t0,
            }
        )

    # Linear regression (raw r) — no FE beyond using r
    t0 = time.perf_counter()
    lr = LinearRegression()
    lr.fit(Xtr, ytr)
    st, can, k, b, eq, terms = lin_structure(lr, Xtr)
    add("linreg", "linear regression on r", False, False, lr.predict(Xte), st, can, k, eq, terms, t0)

    # Polynomial regression degree 2 (explicit FE)
    t0 = time.perf_counter()
    poly = Pipeline(
        [
            ("poly", PolynomialFeatures(degree=2, include_bias=False)),
            ("model", LinearRegression()),
        ]
    )
    poly.fit(Xtr, ytr)
    st, can, k, b, eq, terms = poly2_structure(poly, Xtr)
    add("poly2", "polynomial regression deg-2", True, False, poly.predict(Xte), st, can, k, eq, terms, t0)

    # Lasso on poly features
    t0 = time.perf_counter()
    lasso = Pipeline(
        [
            ("poly", PolynomialFeatures(degree=2, include_bias=False)),
            # Small alpha: area magnitudes are large; alpha=1 zeros the quadratic.
            ("model", Lasso(alpha=1e-4, max_iter=50000, random_state=seed)),
        ]
    )
    lasso.fit(Xtr, ytr)
    st, can, k, b, eq, terms = poly2_structure(lasso, Xtr)
    add("lasso_poly2", "Lasso on poly-2 features", True, True, lasso.predict(Xte), st, can, k, eq, terms, t0)

    # Decision tree
    t0 = time.perf_counter()
    dt = DecisionTreeRegressor(max_depth=6, random_state=seed)
    dt.fit(Xtr, ytr)
    leaves = int(dt.get_n_leaves())
    add(
        "dtree",
        "decision tree",
        False,
        False,
        dt.predict(Xte),
        False,
        False,
        float("nan"),
        f"black-box tree leaves={leaves}",
        leaves,
        t0,
    )

    # Random forest
    t0 = time.perf_counter()
    rf = RandomForestRegressor(n_estimators=100, max_depth=8, random_state=seed, n_jobs=-1)
    rf.fit(Xtr, ytr)
    add(
        "rforest",
        "random forest",
        False,
        False,
        rf.predict(Xte),
        False,
        False,
        float("nan"),
        "black-box ensemble",
        100,
        t0,
    )

    # Gradient boosting
    t0 = time.perf_counter()
    gb = GradientBoostingRegressor(random_state=seed)
    gb.fit(Xtr, ytr)
    add(
        "gboost",
        "gradient boosting",
        False,
        False,
        gb.predict(Xte),
        False,
        False,
        float("nan"),
        "black-box boosting",
        int(getattr(gb, "n_estimators_", 100)),
        t0,
    )

    # Sklearn MLP (ordinary dense NN baseline in sklearn)
    t0 = time.perf_counter()
    r_scale = max(float(np.max(np.abs(Xtr))), 1.0)
    a_scale = max(float(np.max(np.abs(ytr))), 1.0)
    n_tr = len(ytr)
    mlp_kw = dict(
        hidden_layer_sizes=(22,),
        activation="tanh",
        max_iter=2000,
        random_state=seed,
    )
    # early_stopping needs a non-trivial validation slice
    if n_tr >= 20:
        mlp_kw["early_stopping"] = True
        mlp_kw["validation_fraction"] = 0.15
    else:
        mlp_kw["early_stopping"] = False
    mlp = MLPRegressor(**mlp_kw)
    mlp.fit(Xtr / r_scale, ytr / a_scale)
    pred = mlp.predict(Xte / r_scale) * a_scale
    n_w = sum(w.size for w in mlp.coefs_) + sum(b.size for b in mlp.intercepts_)
    add(
        "mlp_sk",
        "sklearn MLPRegressor (dense)",
        False,
        False,
        pred,
        False,
        False,
        float("nan"),
        f"black-box MLP weights~={n_w}",
        n_w,
        t0,
    )

    # Symbolic regression optional
    try:
        from gplearn.genetic import SymbolicRegressor  # type: ignore

        t0 = time.perf_counter()
        est = SymbolicRegressor(
            population_size=500,
            generations=20,
            stopping_criteria=0.01,
            p_crossover=0.7,
            p_subtree_mutation=0.1,
            p_hoist_mutation=0.05,
            p_point_mutation=0.1,
            max_samples=0.9,
            verbose=0,
            parsimony_coefficient=0.01,
            random_state=seed,
            n_jobs=1,
        )
        est.fit(Xtr, ytr)
        prog = str(est._program)
        # Heuristic: contains r**2 or multiply(r,r)
        structural = ("r**2" in prog) or ("mul(X0, X0)" in prog) or ("X0**2" in prog)
        canonical = structural and ("X0" in prog or "r" in prog)
        add(
            "symbolic",
            "symbolic regression (gplearn)",
            False,
            True,
            est.predict(Xte),
            structural,
            canonical,
            float("nan"),
            prog.replace(",", ";")[:200],
            prog.count("+") + prog.count("*") + 1,
            t0,
        )
    except Exception:
        pass

    return rows


def summarize(rows):
    by = defaultdict(list)
    for r in rows:
        by[r["method"]].append(r)
    lines = [
        "# Sklearn / classical baselines (area)",
        "",
        "| Method | n | Canon % | Op recover % | Mean RMSE | Mean terms | Prior FE | Auto simplify |",
        "|--------|---|---------|--------------|-----------|------------|----------|---------------|",
    ]
    order = ["linreg", "poly2", "lasso_poly2", "dtree", "rforest", "gboost", "mlp_sk", "symbolic"]
    for mid in order:
        if mid not in by:
            continue
        xs = by[mid]
        n = len(xs)
        can = sum(r["canonical"] for r in xs)
        op = sum(r["operator_recovery"] for r in xs)
        rm = np.mean([r["test_rmse"] for r in xs])
        tm = np.mean([r["terms"] for r in xs])
        fe = "yes" if xs[0]["prior_features"] else "no"
        sm = "yes" if xs[0]["auto_simplify"] else "no"
        lines.append(
            f"| {mid} | {n} | {100*can/n:.0f} | {100*op/n:.0f} | {rm:.4g} | {tm:.1f} | {fe} | {sm} |"
        )
    return "\n".join(lines) + "\n"


def combined_table(results_dir: Path, sk_rows):
    """Merge NN + sklearn into one comparison table if nn_seed_logs.csv exists."""
    nn_path = results_dir / "nn_seed_logs.csv"
    lines = [
        "# Table — Baselines comparison (area)",
        "",
        "| Method | Family | Canon % | Op % | Mean RMSE | Mean terms | Prior FE | Notes |",
        "|--------|--------|---------|------|-----------|------------|----------|-------|",
    ]
    # NN
    if nn_path.exists():
        by = defaultdict(list)
        with nn_path.open(newline="") as f:
            for row in csv.DictReader(f):
                by[row["method"]].append(row)
        for mid, xs in by.items():
            n = len(xs)
            can = sum(int(r["canonical"]) for r in xs)
            op = sum(int(r["operator_recovery"]) for r in xs)
            rms = [float(r["test_rmse"]) for r in xs if r["test_rmse"] not in ("", "null")]
            tms = [float(r["terms"]) for r in xs]
            fe = "no"
            note = "HSM" if mid == "hsm" else "no quadratic ops"
            lines.append(
                f"| {mid} | nn | {100*can/n:.0f} | {100*op/n:.0f} | "
                f"{(np.mean(rms) if rms else float('nan')):.4g} | {np.mean(tms):.1f} | {fe} | {note} |"
            )
    # sklearn
    by = defaultdict(list)
    for r in sk_rows:
        by[r["method"]].append(r)
    notes = {
        "linreg": "cannot form r^2",
        "poly2": "trivial if FE given",
        "lasso_poly2": "FE + sparsity",
        "dtree": "no closed form",
        "rforest": "no closed form",
        "gboost": "no closed form",
        "mlp_sk": "black-box dense",
        "symbolic": "optional gplearn",
    }
    for mid, xs in by.items():
        n = len(xs)
        can = sum(r["canonical"] for r in xs)
        op = sum(r["operator_recovery"] for r in xs)
        rm = np.mean([r["test_rmse"] for r in xs])
        tm = np.mean([r["terms"] for r in xs])
        fe = "yes" if xs[0]["prior_features"] else "no"
        lines.append(
            f"| {mid} | sklearn | {100*can/n:.0f} | {100*op/n:.0f} | {rm:.4g} | {tm:.1f} | {fe} | {notes.get(mid,'')} |"
        )
    return "\n".join(lines) + "\n"


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--out", required=True, help="Exp11_Baselines results folder")
    args = ap.parse_args()
    results = Path(args.out)
    if not results.is_dir():
        print("ERROR: results dir missing:", results, file=sys.stderr)
        return 2

    seed_dirs = sorted(
        [p for p in results.iterdir() if p.is_dir() and re.fullmatch(r"seed_\d+", p.name)],
        key=lambda p: p.name,
    )
    if not seed_dirs:
        print("ERROR: no seed_XX folders with splits in", results, file=sys.stderr)
        return 3

    all_rows = []
    for sd in seed_dirs:
        seed = int(sd.name.split("_")[1])
        tr = sd / "train.csv"
        te = sd / "test.csv"
        if not tr.exists() or not te.exists():
            print("skip", sd, "(missing csv)")
            continue
        Xtr, ytr = load_split(tr)
        Xte, yte = load_split(te)
        print(f"sklearn seed {seed}: train={len(ytr)} test={len(yte)}")
        rows = fit_methods(Xtr, ytr, Xte, yte, seed)
        all_rows.extend(rows)

    out_csv = results / "sklearn_seed_logs.csv"
    fields = [
        "method",
        "name",
        "family",
        "seed",
        "canonical",
        "structural",
        "operator_recovery",
        "prior_features",
        "auto_simplify",
        "k",
        "pi_error",
        "test_rmse",
        "terms",
        "equation",
        "runtime_s",
    ]
    with out_csv.open("w", newline="", encoding="utf-8") as f:
        w = csv.DictWriter(f, fieldnames=fields)
        w.writeheader()
        for r in all_rows:
            w.writerow(r)

    head = f"Exp11 sklearn runs={len(all_rows)}.\n" + summarize(all_rows)
    (results / "headline_sklearn.txt").write_text(head, encoding="utf-8")
    table = combined_table(results, all_rows)
    (results / "Table_Baselines.md").write_text(table, encoding="utf-8")
    print(head)
    print(table)
    print("Wrote", out_csv)
    return 0


if __name__ == "__main__":
    sys.exit(main())
