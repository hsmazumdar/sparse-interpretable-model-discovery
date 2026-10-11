# -*- coding: utf-8 -*-
"""Experiment 21: gplearn and SINDy on the frozen Exp12 splits.

The quadratic-graph numbers are the Exp12 seed log (same seeds, same files).
SINDy uses the full degree-3 polynomial library, not the true support.
"""
from __future__ import annotations

import csv
import time
from collections import defaultdict
from concurrent.futures import ProcessPoolExecutor, as_completed
from pathlib import Path

import numpy as np

import sr_common as sc

ROOT = Path(__file__).resolve().parent
EXP12 = ROOT / "Results" / "Exp12_CrossProduct"
OUT = ROOT / "Results" / "Exp21_SymbolicSameSplit"


def _fit_one(task):
    problem = task["problem"]
    seed = task["seed"]
    train = sc.load_table(task["train"])
    val = sc.load_table(task["val"])
    test = sc.load_table(task["test"])
    truth = sc.EXP12_TRUTH[problem]
    formula = task["formula"]
    rows = []
    Xtr, ytr = train[:, :2], train[:, 2]
    Xva, yva = val[:, :2], val[:, 2]
    Xte, yte = test[:, :2], test[:, 2]
    yclean = yte

    t0 = time.perf_counter()
    coef, frac = sc.fit_sindy(Xtr, ytr, Xva, yva, sc.POLY3)
    pred = sc.design(Xte, sc.POLY3) @ coef
    rec, mae, fp, _ = sc.support_score(coef, sc.POLY3, truth)
    rows.append(_row(
        problem, formula, seed, "sindy_poly3", rec, 1, mae, fp,
        sc.rmse(yclean, pred), sc.rmse(yte, pred),
        int(abs(coef[sc.POLY3.index("x1*x2")]) > sc.ZERO_ABS), 0,
        time.perf_counter() - t0, sc.equation_from_coef(sc.POLY3, coef),
        f"threshold_frac={frac:.3g}",
    ))

    for method, functions, rng in (
        ("gplearn", ("add", "sub", "mul", "sin"), seed),
        ("gplearn_poly", ("add", "sub", "mul"), seed + 91),
    ):
        t0 = time.perf_counter()
        try:
            est = sc.make_regressor(rng, functions)
            est.fit(Xtr, ytr)
            expr = str(est._program)
            pred = np.asarray(est.predict(Xte), dtype=float)
            grid = sc.unit_grid(21)
            ghat = np.asarray(est.predict(grid), dtype=float)
            names = sc.READOUT if "sin(" in expr else sc.POLY3
            gcoef, resid = sc.readout_fit(grid, ghat, names)
            gtrue = sc.clean_target_exp12(problem, grid[:, 0], grid[:, 1])
            scale = float(np.std(gtrue))
            in_span = int(np.isfinite(resid) and resid <= sc.RESID_FRAC * max(scale, 1e-8))
            rec, mae, fp, estc = sc.support_score(gcoef, names, truth)
            if not in_span:
                rec = 0
            rows.append(_row(
                problem, formula, seed, method, rec, in_span, mae, fp,
                sc.rmse(yclean, pred), sc.rmse(yte, pred),
                int(abs(estc.get("x1*x2", 0.0)) > sc.ZERO_ABS or "mul(X0, X1)" in expr or "mul(X1, X0)" in expr),
                int("sin(" in expr),
                time.perf_counter() - t0, expr, f"readout_resid={resid:.3g}",
            ))
        except Exception as exc:
            rows.append(_row(
                problem, formula, seed, method, 0, 0, float("nan"), 0,
                float("nan"), float("nan"), 0, 0, time.perf_counter() - t0,
                "", f"failed: {exc}",
            ))
    return rows


def _row(problem, formula, seed, method, rec, in_span, mae, fp, rmse_c, rmse_o,
         expl_p, expl_s, runtime, equation, note):
    eq = " ".join((equation or "").split())
    if len(eq) > 240:
        eq = eq[:240]
    return {
        "problem": problem,
        "formula": formula,
        "seed": seed,
        "method": method,
        "term_recovery": int(rec),
        "in_span": int(in_span),
        "coeff_rel_mae": mae,
        "n_false_pos": int(fp),
        "rmse_clean": rmse_c,
        "rmse_obs": rmse_o,
        "explicit_product": int(expl_p),
        "explicit_sin": int(expl_s),
        "runtime_s": runtime,
        "equation": eq,
        "note": note,
    }


def _fnum(text):
    if text is None or text == "" or text == "null":
        return float("nan")
    return float(text)


def hsm_rows():
    rows = []
    path = EXP12 / "seed_logs.csv"
    with path.open(newline="") as f:
        for rec in csv.DictReader(f):
            problem = rec["problem"]
            truth = sc.EXP12_TRUTH[problem]
            formula = (EXP12 / f"Problem_{problem}" / "seed_01" / "problem.txt").read_text(encoding="utf-8").splitlines()[-1]
            vals = [_fnum(rec["c0"]), _fnum(rec["a"]), _fnum(rec["b"]), _fnum(rec["cxy"])]
            runtime = _fnum(rec.get("runtime_s"))
            err = _fnum(rec["test_rmse"])
            if not np.all(np.isfinite(vals)):
                rows.append(_row(
                    problem, formula, int(rec["seed"]), "hsm_exp12",
                    0, 0, float("nan"), 0, err, err,
                    int(rec["explicit_cross"]), 0, runtime,
                    "readout unavailable",
                    "Exp12 log has null coefficients; counted as not recovered",
                ))
                continue
            names = ["1", "x1^2", "x2^2", "x1*x2"]
            coef = np.array(vals, dtype=float)
            _proj, mae, fp, _ = sc.support_score(coef, names, truth)
            # Exp12 structure is the exported-operator gate (explicit product on cross/mixed).
            recovered = int(rec["structure"])
            rows.append(_row(
                problem, formula, int(rec["seed"]), "hsm_exp12",
                recovered, 1, mae, fp, err, err,
                int(rec["explicit_cross"]), 0, runtime,
                f"readout c0={rec['c0']} a={rec['a']} b={rec['b']} cxy={rec['cxy']}",
                "Exp12 quadratic readout of the pruned network; explicit_cross from that log",
            ))
    return rows


def summarize(rows):
    by = defaultdict(list)
    for r in rows:
        by[(r["problem"], r["method"])].append(r)
    lines = [
        "# Same-split comparison on Exp12",
        "",
        "Frozen Exp12 train/validation/test files. HSM is the published Exp12 graph, not a rerun.",
        "SINDy library: all degree-3 monomials in x1, x2. gplearn: add, sub, mul, sin.",
        "",
        "| Problem | Method | n | Term recovery | Mean coeff rel | Mean test RMSE | Explicit product |",
        "|---------|--------|---|---------------|----------------|----------------|------------------|",
    ]
    order_p = ["diag", "cross", "mixed"]
    order_m = ["hsm_exp12", "sindy_poly3", "gplearn_poly", "gplearn"]
    for p in order_p:
        for m in order_m:
            xs = by.get((p, m), [])
            if not xs:
                continue
            n = len(xs)
            rec = sum(int(r["term_recovery"]) for r in xs)
            mae = sc.mean_finite([r["coeff_rel_mae"] for r in xs])
            err = sc.mean_finite([r["rmse_clean"] for r in xs])
            expl = sum(int(r["explicit_product"]) for r in xs)
            lines.append(
                f"| {p} | {m} | {n} | {rec}/{n} | {mae:.4g} | {err:.4g} | {expl}/{n} |"
            )
    lines.append("")
    lines.append(_reading(by))
    lines.append("")
    return "\n".join(lines) + "\n"


def _reading(by) -> str:
    def rate(problem, method):
        xs = by.get((problem, method), [])
        if not xs:
            return "n/a"
        rec = sum(int(r["term_recovery"]) for r in xs)
        return f"{rec}/{len(xs)}"

    return "\n".join([
        "## Reading",
        "",
        f"Axis-aligned squares (`diag`): HSM {rate('diag','hsm_exp12')}, "
        f"SINDy {rate('diag','sindy_poly3')}, gplearn with sin {rate('diag','gplearn')}, "
        f"gplearn polynomials only {rate('diag','gplearn_poly')}.",
        f"Pure product (`cross`): HSM export {rate('cross','hsm_exp12')}, "
        f"SINDy {rate('cross','sindy_poly3')}, gplearn with sin {rate('cross','gplearn')}, "
        f"gplearn polynomials only {rate('cross','gplearn_poly')}.",
        f"Mixed squares plus product (`mixed`): HSM export {rate('mixed','hsm_exp12')}, "
        f"SINDy {rate('mixed','sindy_poly3')}, gplearn with sin {rate('mixed','gplearn')}, "
        f"gplearn polynomials only {rate('mixed','gplearn_poly')}.",
        "HSM term recovery is the Exp12 structure flag. On cross and mixed that flag is explicit x1*x2 in the export.",
        "Coefficient error remains the quadratic readout of the live network, so an approximation is still visible.",
        "gplearn is population 500, 20 generations, parsimony 0.001. "
        "`gplearn` may use sin; `gplearn_poly` is add, sub, and mul only.",
    ])


def main():
    sc.self_check()
    # attach exp12 clean target used by the worker
    OUT.mkdir(parents=True, exist_ok=True)
    tasks = []
    for problem, truth in sc.EXP12_TRUTH.items():
        pdir = EXP12 / f"Problem_{problem}"
        formula = (pdir / "seed_01" / "problem.txt").read_text(encoding="utf-8").splitlines()[-1].strip()
        for seed_dir in sorted(pdir.glob("seed_*")):
            seed = int(seed_dir.name.split("_")[1])
            tasks.append({
                "problem": problem,
                "formula": formula,
                "seed": seed,
                "train": str(seed_dir / "train.csv"),
                "val": str(seed_dir / "validation.csv"),
                "test": str(seed_dir / "test.csv"),
            })
    rows = hsm_rows()
    workers = 4
    with ProcessPoolExecutor(max_workers=workers) as pool:
        futs = [pool.submit(_fit_one, t) for t in tasks]
        done = 0
        for fut in as_completed(futs):
            rows.extend(fut.result())
            done += 1
            print(f"exp21 {done}/{len(tasks)}", flush=True)
    fields = list(rows[0].keys())
    with (OUT / "seed_logs.csv").open("w", newline="") as f:
        w = csv.DictWriter(f, fieldnames=fields)
        w.writeheader()
        w.writerows(rows)
    text = summarize(rows)
    (OUT / "headline.txt").write_text(text, encoding="utf-8")
    (OUT / "INDEX.md").write_text(text, encoding="utf-8")
    (OUT / "PROTOCOL.md").write_text(sc.__doc__ or "", encoding="utf-8")
    print(text)


if __name__ == "__main__":
    main()
