# -*- coding: utf-8 -*-
"""Score Experiment 22: HSM grid readout, SINDy poly-3, gplearn.

Reads Results/Exp22_KnownLaws produced by NnPruneHsm --exp22.
"""
from __future__ import annotations

import csv
import math
import time
from collections import defaultdict
from concurrent.futures import ProcessPoolExecutor, as_completed
from pathlib import Path

import numpy as np

import sr_common as sc

ROOT = Path(__file__).resolve().parent
DATA = ROOT / "Results" / "Exp22_KnownLaws"
OUT = DATA


def _noise_from_dir(name: str) -> float:
    return float(name.replace("noise_", "").replace("p", "."))


def _blank(law, formula, noise, seed, method, note, runtime=float("nan")):
    return {
        "law": law,
        "formula": formula,
        "noise": noise,
        "seed": seed,
        "method": method,
        "term_recovery": 0,
        "in_span": 0,
        "coeff_rel_mae": float("nan"),
        "n_false_pos": 0,
        "rmse_clean": float("nan"),
        "rmse_obs": float("nan"),
        "explicit_product": 0,
        "explicit_sin": 0,
        "runtime_s": runtime,
        "equation": "",
        "note": note,
    }


def _pack(law, formula, noise, seed, method, rec, in_span, mae, fp, rc, ro, ep, es, runtime, equation, note):
    eq = " ".join((equation or "").split())
    if len(eq) > 240:
        eq = eq[:240]
    return {
        "law": law,
        "formula": formula,
        "noise": noise,
        "seed": seed,
        "method": method,
        "term_recovery": int(rec),
        "in_span": int(in_span),
        "coeff_rel_mae": mae,
        "n_false_pos": int(fp),
        "rmse_clean": rc,
        "rmse_obs": ro,
        "explicit_product": int(ep),
        "explicit_sin": int(es),
        "runtime_s": runtime,
        "equation": eq,
        "note": note,
    }


def _gplearn_row(law, formula, noise, seed, method, functions, rng_seed, Xtr, ytr, Xte, yte, yclean, truth, t0):
    try:
        est = sc.make_regressor(rng_seed, functions)
        est.fit(Xtr, ytr)
        expr = str(est._program)
        pred = np.asarray(est.predict(Xte), dtype=float)
        grid = sc.unit_grid(21)
        ghat = np.asarray(est.predict(grid), dtype=float)
        # sin(x) is nearly x - x^3/6 on [-1, 1], so a sine column makes polynomial
        # coefficients non-unique. Include it only when the expression uses sin.
        names = sc.READOUT if "sin(" in expr else sc.POLY3
        gcoef, resid = sc.readout_fit(grid, ghat, names)
        gtrue = sc.clean_target(law, grid[:, 0], grid[:, 1])
        scale = float(np.std(gtrue))
        in_span = int(np.isfinite(resid) and resid <= sc.RESID_FRAC * max(scale, 1e-8))
        rec, mae, fp, estc = sc.support_score(gcoef, names, truth)
        if not in_span:
            rec = 0
        finite = bool(np.all(np.isfinite(pred)))
        return _pack(
            law, formula, noise, seed, method, rec, in_span, mae, fp,
            sc.rmse(yclean, pred) if finite else float("nan"),
            sc.rmse(yte, pred) if finite else float("nan"),
            int(abs(estc.get("x1*x2", 0.0)) > sc.ZERO_ABS),
            int("sin(" in expr),
            time.perf_counter() - t0, expr, f"readout_resid={resid:.3g}",
        )
    except Exception as exc:
        return _blank(law, formula, noise, seed, method, f"failed: {exc}", time.perf_counter() - t0)


def score_seed(task):
    law = task["law"]
    noise = task["noise"]
    seed = task["seed"]
    seed_dir = Path(task["dir"])
    formula = sc.LAWS[law]
    truth = sc.law_truth(law)
    rows = []
    train = sc.load_table(seed_dir / "train.csv")
    val = sc.load_table(seed_dir / "validation.csv")
    test = sc.load_table(seed_dir / "test.csv")
    Xtr, ytr = train[:, :2], train[:, 2]
    Xva, yva = val[:, :2], val[:, 2]
    Xte, yte = test[:, :2], test[:, 2]
    yclean = sc.clean_target(law, Xte[:, 0], Xte[:, 1])

    t0 = time.perf_counter()
    coef, frac = sc.fit_sindy(Xtr, ytr, Xva, yva, sc.POLY3)
    pred = sc.design(Xte, sc.POLY3) @ coef
    rec, mae, fp, _ = sc.support_score(coef, sc.POLY3, truth)
    rows.append(_pack(
        law, formula, noise, seed, "sindy_poly3", rec, 1, mae, fp,
        sc.rmse(yclean, pred), sc.rmse(yte, pred),
        int(abs(coef[sc.POLY3.index("x1*x2")]) > sc.ZERO_ABS), 0,
        time.perf_counter() - t0, sc.equation_from_coef(sc.POLY3, coef),
        f"threshold_frac={frac:.3g}",
    ))

    t0 = time.perf_counter()
    rows.append(_gplearn_row(
        law, formula, noise, seed, "gplearn", ("add", "sub", "mul", "sin"),
        seed + 17, Xtr, ytr, Xte, yte, yclean, truth, t0,
    ))
    t0 = time.perf_counter()
    rows.append(_gplearn_row(
        law, formula, noise, seed, "gplearn_poly", ("add", "sub", "mul"),
        seed + 91, Xtr, ytr, Xte, yte, yclean, truth, t0,
    ))

    grid_path = seed_dir / "pred_grid.csv"
    if not grid_path.exists():
        rows.append(_blank(law, formula, noise, seed, "hsm", "missing pred_grid.csv"))
        return rows
    grid_raw = sc.load_table(grid_path)
    Xg, yhat = grid_raw[:, :2], grid_raw[:, 2]
    # Guard: the C# grid is 21x21 in the same order as unit_grid.
    expect = sc.unit_grid(21)
    if Xg.shape[0] != expect.shape[0] or np.max(np.abs(Xg - expect)) > 1e-8:
        rows.append(_blank(law, formula, noise, seed, "hsm", "pred_grid does not match the 21x21 lattice"))
        return rows
    t_runtime = float("nan")
    rt = seed_dir / "runtime.txt"
    if rt.exists():
        t_runtime = float(rt.read_text(encoding="utf-8").strip().split()[0])
    eq = ""
    eq_path = seed_dir / "exported_equation.txt"
    if eq_path.exists():
        eq = eq_path.read_text(encoding="utf-8", errors="replace")
    if not np.all(np.isfinite(yhat)):
        rows.append(_blank(law, formula, noise, seed, "hsm", "non-finite network grid", t_runtime))
        return rows
    gcoef, resid = sc.readout_fit(Xg, yhat, sc.POLY3)
    gtrue = sc.clean_target(law, Xg[:, 0], Xg[:, 1])
    scale = float(np.std(gtrue))
    in_span = int(np.isfinite(resid) and resid <= sc.RESID_FRAC * max(scale, 1e-8))
    rec, mae, fp, estc = sc.support_score(gcoef, sc.POLY3, truth)
    if not in_span:
        rec = 0
    # Product and sine are not in the operator bank. A matching readout is an
    # approximation, not an exported term. L1 and L2 stay on the readout gate.
    if abs(truth.get("x1*x2", 0.0)) > 1e-12 and not sc.explicit_product_text(eq):
        rec = 0
    if abs(truth.get("sin(x1)", 0.0)) > 1e-12 and not sc.explicit_sin_text(eq):
        rec = 0
    pred_test = sc.load_table(seed_dir / "pred_test.csv")
    yhat_te = pred_test[:, 3]
    rows.append(_pack(
        law, formula, noise, seed, "hsm", rec, in_span, mae, fp,
        sc.rmse(yclean, yhat_te), sc.rmse(pred_test[:, 2], yhat_te),
        sc.explicit_product_text(eq),
        sc.explicit_sin_text(eq),
        t_runtime, sc.equation_from_coef(sc.POLY3, gcoef),
        f"readout_resid={resid:.3g}; export_product={sc.explicit_product_text(eq)}",
    ))
    return rows


def summarize(rows):
    by = defaultdict(list)
    for r in rows:
        by[(r["law"], float(r["noise"]), r["method"])].append(r)
    lines = [
        "# Experiment 22 — four known laws",
        "",
        "Term recovery: true coefficients within 25% relative, other readout coefficients",
        "at most 0.05 absolute, and (HSM, gplearn) residual within 5% of the clean scale.",
        "The readout is the full degree-3 polynomial. sin(x1) is added only when the",
        "gplearn expression actually calls sin, because sin(x) and x, x^3 are not unique on [-1, 1].",
        "The HSM bank has no sine, so its readout stays polynomial. A product or sine counts",
        "as recovered only when that operator is in the exported equation.",
        "SINDy is STLSQ on all 10 degree-3 monomials. The library is not the true support.",
        "",
        "| Law | Noise | Method | n | Term recovery | Mean coeff rel | Mean RMSE clean | Explicit product | Explicit sin |",
        "|-----|-------|--------|---|---------------|----------------|-----------------|------------------|--------------|",
    ]
    laws = ["L1", "L2", "L3", "L4"]
    noises = [0.0, 0.02, 0.05]
    methods = ["hsm", "sindy_poly3", "gplearn_poly", "gplearn"]
    for law in laws:
        for noise in noises:
            for method in methods:
                xs = by.get((law, noise, method), [])
                if not xs:
                    continue
                n = len(xs)
                rec = sum(int(r["term_recovery"]) for r in xs)
                mae = sc.mean_finite([r["coeff_rel_mae"] for r in xs])
                err = sc.mean_finite([r["rmse_clean"] for r in xs])
                ep = sum(int(r["explicit_product"]) for r in xs)
                es = sum(int(r["explicit_sin"]) for r in xs)
                lines.append(
                    f"| {law} | {noise:g} | {method} | {n} | {rec}/{n} | {mae:.4g} | {err:.4g} | {ep}/{n} | {es}/{n} |"
                )
    lines.append("")
    lines.append(_reading(by))
    lines.append("")
    return "\n".join(lines) + "\n"


def _finite_num(value) -> bool:
    if value is None or value == "" or value == "nan":
        return False
    try:
        return math.isfinite(float(value))
    except (TypeError, ValueError):
        return False


def _rate(by, law, noise, method):
    xs = by.get((law, noise, method), [])
    if not xs:
        return "n/a"
    rec = sum(int(r["term_recovery"]) for r in xs)
    return f"{rec}/{len(xs)}"


def _reading(by) -> str:
    lines = ["## Reading", ""]
    for law, label in (
        ("L1", "Axis-aligned squares"),
        ("L2", "Shifted quadratic"),
        ("L3", "Product"),
        ("L4", "Sine plus square"),
    ):
        lines.append(
            f"{label} (`{law}`), noise 0: HSM {_rate(by, law, 0.0, 'hsm')}, "
            f"SINDy {_rate(by, law, 0.0, 'sindy_poly3')}, "
            f"gplearn polynomials {_rate(by, law, 0.0, 'gplearn_poly')}, "
            f"gplearn with sin {_rate(by, law, 0.0, 'gplearn')}."
        )
    hsm_prod = by.get(("L3", 0.0, "hsm"), [])
    hsm_sin = by.get(("L4", 0.0, "hsm"), [])
    if hsm_prod:
        ep = sum(int(r["explicit_product"]) for r in hsm_prod)
        lines.append(f"HSM export contains an explicit product on L3 noise 0: {ep}/{len(hsm_prod)}.")
    if hsm_sin:
        es = sum(int(r["explicit_sin"]) for r in hsm_sin)
        lines.append(f"HSM export contains a sine on L4 noise 0: {es}/{len(hsm_sin)}.")
    xs = by.get(("L3", 0.0, "hsm"), [])
    if xs:
        bad = sum(1 for r in xs if not _finite_num(r["rmse_clean"]))
        lines.append(
            f"L3 noise 0 HSM predictions were non-finite on {bad}/{len(xs)} seeds. Those seeds stay in the denominator."
        )
    lines.append("Rates at noise 0.02 and 0.05 are in the table. No seed is dropped.")
    return "\n".join(lines)


def main():
    sc.self_check()
    tasks = []
    for law_dir in sorted(p for p in DATA.iterdir() if p.is_dir() and p.name.startswith("L")):
        for noise_dir in sorted(p for p in law_dir.iterdir() if p.is_dir() and p.name.startswith("noise_")):
            noise = _noise_from_dir(noise_dir.name)
            for seed_dir in sorted(noise_dir.glob("seed_*")):
                tasks.append({
                    "law": law_dir.name,
                    "noise": noise,
                    "seed": int(seed_dir.name.split("_")[1]),
                    "dir": str(seed_dir),
                })
    if not tasks:
        raise SystemExit(f"No seeds under {DATA}. Run NnPruneHsm --exp22 first.")
    rows = []
    with ProcessPoolExecutor(max_workers=4) as pool:
        futs = [pool.submit(score_seed, t) for t in tasks]
        done = 0
        for fut in as_completed(futs):
            rows.extend(fut.result())
            done += 1
            print(f"exp22 score {done}/{len(tasks)}", flush=True)
    rows.sort(key=lambda r: (r["law"], float(r["noise"]), int(r["seed"]), r["method"]))
    fields = list(rows[0].keys())
    with (OUT / "seed_logs.csv").open("w", newline="") as f:
        w = csv.DictWriter(f, fieldnames=fields)
        w.writeheader()
        w.writerows(rows)
    text = summarize(rows)
    (OUT / "headline.txt").write_text(text, encoding="utf-8")
    (OUT / "INDEX.md").write_text(text, encoding="utf-8")
    print(text)


if __name__ == "__main__":
    main()
