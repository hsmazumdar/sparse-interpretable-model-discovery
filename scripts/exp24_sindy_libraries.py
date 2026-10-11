# -*- coding: utf-8 -*-
"""Experiment 24: SINDy when the library is not the true support.

Uses the Exp22 train/validation/test files for L1, L2, L3. Does not retrain HSM
and does not write into Exp21-23 folders.
"""
from __future__ import annotations

import csv
from collections import defaultdict
from pathlib import Path

import numpy as np

import sr_common as sc

ROOT = Path(__file__).resolve().parent
SRC = ROOT / "Results" / "Exp22_KnownLaws"
OUT = ROOT / "Results" / "Exp24_SINDyLibraryMismatch"

# Locked before any fit. "per input" means each coordinate gets its own
# sin, cos, and exp(-x^2) column. No library contains a shifted square (x-c)^2.
LIBS = [
    ("deg1", ["1", "x1", "x2"]),
    ("deg2_no_cross", ["1", "x1", "x2", "x1^2", "x2^2"]),
    ("deg2_no_squares", ["1", "x1", "x2", "x1*x2"]),
    ("generic", [
        "1", "x1", "x2",
        "sin(x1)", "sin(x2)", "cos(x1)", "cos(x2)",
        "exp(-x1^2)", "exp(-x2^2)",
    ]),
    ("deg3", list(sc.POLY3)),
]

EXPANDED = {"1", "x1", "x2", "x1^2", "x2^2"}

PROTOCOL = """# Experiment 24 protocol

SINDy (STLSQ) on the Exp22 splits for L1, L2, and L3. Noise levels 0, 0.02, 0.05. Seeds 1..20. No seed is dropped. HSM is not retrained: the hsm rows are copied from Exp22.

Threshold grid, same as Exp22: fractions (1e-8, 1e-4, 1e-3, 1e-2, 0.05, 0.1, 0.25) times std(y_train), chosen on the validation split by RMSE then by fewer nonzeros.

Libraries, fixed before the fits:

- deg1: {1, x1, x2}
- deg2_no_cross: {1, x1, x2, x1^2, x2^2}
- deg2_no_squares: {1, x1, x2, x1*x2}
- generic: {1, x1, x2, sin(x1), sin(x2), cos(x1), cos(x2), exp(-x1^2), exp(-x2^2)}
- deg3: the Exp22 degree-3 library, ten monomials, no sine

Term recovery uses the Exp22 gates: every true coefficient within 25% relative, every other coefficient in that library at most 0.05 absolute. A true term that the library does not contain counts as missing.

L2 is scored on the expanded polynomial 0.2225 - 0.8 x1 + 0.5 x2 + x1^2 + x2^2. None of the libraries has a shifted-square column (x-c)^2, so a recovery is the expanded polynomial, not the parenthesized shifted form.

Clean RMSE is against the noise-free formula on the test inputs.
"""


def _fmt(v):
    if v is None or not np.isfinite(v):
        return ""
    return f"{float(v):.6g}"


def score_sindy():
    rows = []
    laws = ["L1", "L2", "L3"]
    for law in laws:
        truth = sc.law_truth(law)
        formula = sc.LAWS[law]
        for noise_dir in sorted((SRC / law).glob("noise_*")):
            noise = float(noise_dir.name.replace("noise_", "").replace("p", "."))
            for seed_dir in sorted(noise_dir.glob("seed_*")):
                seed = int(seed_dir.name.split("_")[1])
                train = sc.load_table(seed_dir / "train.csv")
                val = sc.load_table(seed_dir / "validation.csv")
                test = sc.load_table(seed_dir / "test.csv")
                Xtr, ytr = train[:, :2], train[:, 2]
                Xva, yva = val[:, :2], val[:, 2]
                Xte, yte = test[:, :2], test[:, 2]
                yclean = sc.clean_target(law, Xte[:, 0], Xte[:, 1])
                for lib_name, names in LIBS:
                    coef, frac = sc.fit_sindy(Xtr, ytr, Xva, yva, names)
                    pred = sc.design(Xte, names) @ coef
                    rec, mae, fp, _ = sc.support_score(coef, names, truth)
                    form = ""
                    if law == "L2":
                        has_expanded = EXPANDED.issubset(set(names))
                        if rec == 1 and has_expanded:
                            form = "expanded_polynomial"
                        elif rec == 1:
                            form = "recovered_without_expanded_basis"
                        else:
                            form = "not_recovered"
                    rows.append({
                        "law": law,
                        "formula": formula,
                        "noise": noise,
                        "seed": seed,
                        "library": lib_name,
                        "method": "sindy",
                        "term_recovery": int(rec),
                        "coeff_rel": mae,
                        "n_false_pos": int(fp),
                        "rmse_clean": sc.rmse(yclean, pred),
                        "expression_form": form,
                        "equation": sc.equation_from_coef(names, coef),
                        "note": f"threshold_frac={frac:.3g}; shifted_square_column=no",
                    })
    return rows


def copy_hsm():
    rows = []
    src = SRC / "seed_logs.csv"
    with src.open(newline="", encoding="utf-8") as f:
        for r in csv.DictReader(f):
            if r["method"] != "hsm":
                continue
            if r["law"] not in ("L1", "L2", "L3"):
                continue
            form = ""
            if r["law"] == "L2":
                form = "expanded_polynomial" if r["term_recovery"] == "1" else "not_recovered"
            def fnum(key):
                try:
                    return float(r[key])
                except (TypeError, ValueError):
                    return float("nan")
            rows.append({
                "law": r["law"],
                "formula": r["formula"],
                "noise": float(r["noise"]),
                "seed": int(r["seed"]),
                "library": "hsm",
                "method": "hsm",
                "term_recovery": int(r["term_recovery"]),
                "coeff_rel": fnum("coeff_rel_mae"),
                "n_false_pos": r.get("n_false_pos") or 0,
                "rmse_clean": fnum("rmse_clean"),
                "expression_form": form,
                "equation": r.get("equation") or "",
                "note": "copied from Exp22; " + (r.get("note") or ""),
            })
    return rows


def write_tables(rows):
    OUT.mkdir(parents=True, exist_ok=True)
    (OUT / "PROTOCOL.md").write_text(PROTOCOL, encoding="utf-8")
    fields = [
        "law", "formula", "noise", "seed", "library", "method", "term_recovery",
        "coeff_rel", "n_false_pos", "rmse_clean", "expression_form", "equation", "note",
    ]
    with (OUT / "seed_logs.csv").open("w", newline="", encoding="utf-8") as f:
        w = csv.DictWriter(f, fieldnames=fields)
        w.writeheader()
        for r in rows:
            w.writerow(r)

    by = defaultdict(list)
    for r in rows:
        by[(r["law"], float(r["noise"]), r["library"])].append(r)

    lines = [
        "# Experiment 24 — SINDy library mismatch",
        "",
        "HSM rows are copied from Exp22. SINDy was refit on those same splits.",
        "L2 recovery is the expanded polynomial. No library contains (x-c)^2.",
        "",
        "| Law | Noise | Library | n | Term recovery | Mean coeff rel | Mean clean RMSE | L2 expanded |",
        "|-----|-------|---------|---|---------------|----------------|-----------------|-------------|",
    ]
    head = ["Exp24 SINDy libraries vs HSM (term recovery, mean coeff rel, mean clean RMSE)", ""]
    order = ["hsm", "deg1", "deg2_no_cross", "deg2_no_squares", "generic", "deg3"]
    for law in ("L1", "L2", "L3"):
        for noise in (0.0, 0.02, 0.05):
            for lib in order:
                xs = by.get((law, noise, lib), [])
                if not xs:
                    continue
                n = len(xs)
                rec = sum(int(r["term_recovery"]) for r in xs)
                mae = sc.mean_finite([r["coeff_rel"] for r in xs])
                rm = sc.mean_finite([r["rmse_clean"] for r in xs])
                expanded = ""
                if law == "L2":
                    expanded = str(sum(1 for r in xs if r["expression_form"] == "expanded_polynomial")) + "/" + str(n)
                lines.append(
                    f"| {law} | {noise:g} | {lib} | {n} | {rec}/{n} | {_fmt(mae)} | {_fmt(rm)} | {expanded} |"
                )
                head.append(f"{law} noise {noise:g} {lib}: recovery {rec}/{n}, coeff {_fmt(mae)}, rmse {_fmt(rm)}, L2expanded {expanded}")
    text = "\n".join(lines) + "\n"
    (OUT / "INDEX.md").write_text(text, encoding="utf-8")
    (OUT / "headline.txt").write_text("\n".join(head) + "\n", encoding="utf-8")
    print(text)


def main():
    sc.self_check()
    rows = score_sindy() + copy_hsm()
    rows.sort(key=lambda r: (r["law"], float(r["noise"]), int(r["seed"]), r["library"]))
    write_tables(rows)


if __name__ == "__main__":
    main()
