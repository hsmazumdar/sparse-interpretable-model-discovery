# -*- coding: utf-8 -*-
"""Shared scoring for the symbolic-comparison experiments.

Locked before the runs (also copied into each Results/*/PROTOCOL.md):

- Relative coefficient tolerance: 0.25 (same gate as Exp12).
- Absent-term absolute tolerance: 0.05.
- A network or gplearn function is in the readout span when the residual of a
  least-squares fit on poly-3 + sin(x1) is at most 5% of the clean-target
  standard deviation on the same points.
- Term recovery requires the span check (SINDy is in-span by construction)
  and a matching support: every true coefficient within 25% relative, every
  other coefficient at most 0.05 absolute.
- SINDy library is the full degree-3 polynomial in two variables (10 terms),
  not the true support, and it has no sine column. Thresholds are chosen on
  the validation split only.
- gplearn budget: population 500, 20 generations, metric mse, functions
  add/sub/mul/sin. PySR is not used: Julia is not installed.
"""
from __future__ import annotations

import math
from typing import Dict, List, Sequence, Tuple

import numpy as np

REL_TOL = 0.25
ZERO_ABS = 0.05
RESID_FRAC = 0.05

GP_POP = 500
GP_GEN = 20
GP_PARSIMONY = 0.001

POLY3 = [
    "1", "x1", "x2",
    "x1^2", "x1*x2", "x2^2",
    "x1^3", "x1^2*x2", "x1*x2^2", "x2^3",
]
READOUT = POLY3 + ["sin(x1)"]

LAWS = {
    "L1": "y=x1^2+x2^2",
    "L2": "y=(x1-0.4)^2+(x2+0.25)^2",
    "L3": "y=x1^2+2*x1*x2+4*x2^2",
    "L4": "y=sin(x1)+x2^2",
}

EXP12_TRUTH = {
    "diag": {"1": 0.2, "x1^2": 1.3, "x2^2": 0.8},
    "cross": {"1": 0.5, "x1*x2": 2.0},
    "mixed": {"1": 0.1, "x1^2": 1.0, "x2^2": 0.7, "x1*x2": 1.5},
}


def law_truth(law: str) -> Dict[str, float]:
    if law == "L1":
        return {"x1^2": 1.0, "x2^2": 1.0}
    if law == "L2":
        return {
            "1": 0.4 ** 2 + 0.25 ** 2,
            "x1": -2.0 * 0.4,
            "x2": 2.0 * 0.25,
            "x1^2": 1.0,
            "x2^2": 1.0,
        }
    if law == "L3":
        return {"x1^2": 1.0, "x1*x2": 2.0, "x2^2": 4.0}
    if law == "L4":
        return {"sin(x1)": 1.0, "x2^2": 1.0}
    raise KeyError(law)


def apply_terms(truth: Dict[str, float], x1, x2) -> np.ndarray:
    x1 = np.asarray(x1, dtype=float)
    x2 = np.asarray(x2, dtype=float)
    bank = {
        "1": np.ones_like(x1),
        "x1": x1,
        "x2": x2,
        "x1^2": x1 ** 2,
        "x2^2": x2 ** 2,
        "x1*x2": x1 * x2,
        "sin(x1)": np.sin(x1),
    }
    y = np.zeros_like(x1, dtype=float)
    for name, coef in truth.items():
        y = y + float(coef) * bank[name]
    return y


def clean_target(law: str, x1, x2) -> np.ndarray:
    return apply_terms(law_truth(law), x1, x2)


def clean_target_exp12(problem: str, x1, x2) -> np.ndarray:
    return apply_terms(EXP12_TRUTH[problem], x1, x2)


def rmse(a, b) -> float:
    a = np.asarray(a, dtype=float)
    b = np.asarray(b, dtype=float)
    if a.size == 0:
        return float("nan")
    d = a - b
    if not np.all(np.isfinite(d)):
        return float("nan")
    return float(np.sqrt(np.mean(d ** 2)))


def design(X: np.ndarray, names: Sequence[str]) -> np.ndarray:
    x1 = np.asarray(X[:, 0], dtype=float)
    x2 = np.asarray(X[:, 1], dtype=float) if X.shape[1] > 1 else np.zeros_like(x1)
    bank = {
        "1": np.ones_like(x1),
        "x1": x1,
        "x2": x2,
        "x1^2": x1 ** 2,
        "x1*x2": x1 * x2,
        "x2^2": x2 ** 2,
        "x1^3": x1 ** 3,
        "x1^2*x2": (x1 ** 2) * x2,
        "x1*x2^2": x1 * (x2 ** 2),
        "x2^3": x2 ** 3,
        "sin(x1)": np.sin(x1),
        "sin(x2)": np.sin(x2),
        "cos(x1)": np.cos(x1),
        "cos(x2)": np.cos(x2),
        "exp(-x1^2)": np.exp(-(x1 ** 2)),
        "exp(-x2^2)": np.exp(-(x2 ** 2)),
    }
    return np.column_stack([bank[n] for n in names])


def unit_grid(n: int = 21) -> np.ndarray:
    xs = np.linspace(-1.0, 1.0, n)
    pts = [(xs[i], xs[j]) for i in range(n) for j in range(n)]
    return np.asarray(pts, dtype=float)


def support_score(coef: np.ndarray, names: Sequence[str], truth: Dict[str, float]):
    est = {n: float(c) for n, c in zip(names, coef)}
    rels = []
    for n, ctrue in truth.items():
        if abs(ctrue) <= 1e-12:
            continue
        hat = est.get(n, 0.0)
        rels.append(abs(hat - ctrue) / max(abs(ctrue), 1e-6))
    coeff = float(np.mean(rels)) if rels else float("nan")
    true_nz = {n for n, c in truth.items() if abs(c) > 1e-12}
    fp = 0
    for n, hat in est.items():
        if abs(hat) > ZERO_ABS and n not in true_nz:
            fp += 1
    bad = 0
    for n in true_nz:
        hat = est.get(n, 0.0)
        if abs(hat - truth[n]) / max(abs(truth[n]), 1e-6) > REL_TOL:
            bad += 1
    recovered = int(bad == 0 and fp == 0)
    return recovered, coeff, fp, est


def stlsq(theta: np.ndarray, y: np.ndarray, threshold: float, iters: int = 25) -> np.ndarray:
    coef, *_ = np.linalg.lstsq(theta, y, rcond=None)
    support = np.ones(coef.shape[0], dtype=bool)
    for _ in range(iters):
        new_support = np.abs(coef) >= threshold
        if np.array_equal(new_support, support):
            break
        support = new_support
        coef = np.zeros_like(coef)
        if np.any(support):
            coef[support], *_ = np.linalg.lstsq(theta[:, support], y, rcond=None)
    coef = coef.copy()
    coef[~support] = 0.0
    coef[np.abs(coef) < threshold] = 0.0
    return coef


def fit_sindy(Xtr, ytr, Xva, yva, names: Sequence[str] = POLY3):
    scale = float(np.std(ytr))
    if scale < 1e-12:
        scale = 1.0
    fracs = (1e-8, 1e-4, 1e-3, 1e-2, 0.05, 0.1, 0.25)
    best = None
    theta_tr = design(Xtr, names)
    theta_va = design(Xva, names)
    for frac in fracs:
        coef = stlsq(theta_tr, ytr, frac * scale)
        pred = theta_va @ coef
        err = rmse(yva, pred)
        nnz = int(np.sum(np.abs(coef) > 0))
        key = (err if math.isfinite(err) else 1e300, nnz)
        if best is None or key < best[0]:
            best = (key, coef, frac)
    assert best is not None
    return best[1], best[2]


def equation_from_coef(names: Sequence[str], coef: np.ndarray) -> str:
    parts = []
    for n, c in zip(names, coef):
        if abs(float(c)) > 1e-8:
            parts.append(f"{float(c):.6g}*{n}")
    return " + ".join(parts) if parts else "0"


def make_regressor(seed: int, function_set: Tuple[str, ...]):
    from gplearn.genetic import SymbolicRegressor

    return SymbolicRegressor(
        population_size=GP_POP,
        generations=GP_GEN,
        stopping_criteria=1e-12,
        p_crossover=0.7,
        p_subtree_mutation=0.1,
        p_hoist_mutation=0.05,
        p_point_mutation=0.1,
        max_samples=0.9,
        verbose=0,
        parsimony_coefficient=GP_PARSIMONY,
        random_state=int(seed),
        n_jobs=1,
        metric="mse",
        function_set=function_set,
        const_range=(-5.0, 5.0),
        init_depth=(2, 4),
        tournament_size=20,
    )


def readout_fit(X: np.ndarray, yhat: np.ndarray, names: Sequence[str] = READOUT):
    theta = design(X, names)
    coef, *_ = np.linalg.lstsq(theta, np.asarray(yhat, dtype=float), rcond=None)
    recon = theta @ coef
    resid = rmse(yhat, recon)
    return coef, resid


def load_table(path) -> np.ndarray:
    raw = np.loadtxt(path, delimiter=",", skiprows=1)
    if raw.ndim == 1:
        raw = raw.reshape(1, -1)
    return np.asarray(raw, dtype=float)


def explicit_product_text(eq: str) -> int:
    import re
    s = (eq or "").replace(" ", "")
    return int(re.search(r"n\d+\*n\d+", s) is not None)


def explicit_sin_text(eq: str) -> int:
    return int("sin" in (eq or "").lower())


def self_check() -> None:
    truth = {"x1^2": 1.0, "x2^2": 1.0}
    coef = np.zeros(len(POLY3))
    coef[POLY3.index("x1^2")] = 1.0
    coef[POLY3.index("x2^2")] = 1.0
    rec, mae, fp, _ = support_score(coef, POLY3, truth)
    assert rec == 1 and fp == 0 and mae < 1e-12
    coef[POLY3.index("x1*x2")] = 0.2
    rec, mae, fp, _ = support_score(coef, POLY3, truth)
    assert rec == 0 and fp == 1
    rec, mae, fp, _ = support_score(np.array([0.0, 0.0, 1.0]), ["1", "x1", "x2^2"], law_truth("L4"))
    assert rec == 0 and mae > 0.4
    g = unit_grid(5)
    y = clean_target("L3", g[:, 0], g[:, 1])
    coef2, resid = readout_fit(g, y, READOUT)
    rec, mae, fp, _ = support_score(coef2, READOUT, law_truth("L3"))
    assert resid < 1e-8 and rec == 1 and mae < 1e-6
    assert explicit_product_text("2.00*n3 + n4*n5") == 1
    assert explicit_product_text("n4^2") == 0


def mean_finite(vals: List[float]) -> float:
    xs = [float(v) for v in vals if v is not None and math.isfinite(float(v))]
    if not xs:
        return float("nan")
    return float(np.mean(xs))
