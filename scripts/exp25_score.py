# -*- coding: utf-8 -*-
"""Score Experiment 25. Original-bank rows are copied from Exp22 and Exp23."""
from __future__ import annotations

import csv
import re
from collections import defaultdict
from pathlib import Path

import numpy as np

import exp23_kepler as ek
import sr_common as sc

ROOT = Path(__file__).resolve().parent
OUT = ROOT / "Results" / "Exp25_OperatorExtension"
EXP22 = ROOT / "Results" / "Exp22_KnownLaws" / "seed_logs.csv"
EXP23 = ROOT / "Results" / "Exp23_Kepler" / "seed_logs.csv"

POW_RE = re.compile(r"a\^k|\|n\d+")


def _fmt(v):
    if v is None:
        return ""
    try:
        if not np.isfinite(float(v)):
            return ""
    except (TypeError, ValueError):
        return ""
    return f"{float(v):.6g}"


def _fnum(text):
    try:
        return float(text)
    except (TypeError, ValueError):
        return float("nan")


def explicit_power(eq: str) -> int:
    return int(POW_RE.search(eq or "") is not None)


def _clip_flag(path: Path) -> int:
    return int((path / "CLIP.txt").exists())


def score_law_seed(bank, law, noise, seed, seed_dir: Path):
    formula = sc.LAWS[law]
    truth = sc.law_truth(law)
    cell = seed_dir.parent
    clip = _clip_flag(cell)
    rt = float("nan")
    rp = seed_dir / "runtime.txt"
    if rp.exists():
        rt = _fnum(rp.read_text(encoding="utf-8").split()[0])
    eq = ""
    ep = seed_dir / "exported_equation.txt"
    if ep.exists():
        eq = ep.read_text(encoding="utf-8", errors="replace")
    blank = {
        "bank": bank, "law": law, "formula": formula, "noise": noise, "seed": seed,
        "term_recovery": 0, "coeff_rel": float("nan"), "rmse": float("nan"),
        "explicit_product": sc.explicit_product_text(eq), "explicit_power": explicit_power(eq),
        "diverged": 1, "clip": clip, "runtime_s": rt,
        "equation": " ".join(eq.split())[:240], "note": "",
    }
    grid_path = seed_dir / "pred_grid.csv"
    test_path = seed_dir / "pred_test.csv"
    if not grid_path.exists() or not test_path.exists():
        blank["note"] = "missing predictions"
        return blank
    grid = sc.load_table(grid_path)
    test = sc.load_table(test_path)
    expect = sc.unit_grid(21)
    if grid.shape[0] != expect.shape[0] or np.max(np.abs(grid[:, :2] - expect)) > 1e-8:
        blank["note"] = "grid mismatch"
        return blank
    yhat = grid[:, 2]
    yhat_te = test[:, 3]
    diverged = int((not np.all(np.isfinite(yhat))) or (not np.all(np.isfinite(yhat_te))) or (seed_dir / "diverged.txt").exists())
    if diverged:
        blank["diverged"] = 1
        blank["note"] = "non-finite network"
        return blank
    coef, resid = sc.readout_fit(grid[:, :2], yhat, sc.POLY3)
    gtrue = sc.clean_target(law, grid[:, 0], grid[:, 1])
    scale = float(np.std(gtrue))
    in_span = bool(np.isfinite(resid) and resid <= sc.RESID_FRAC * max(scale, 1e-8))
    rec, mae, fp, _ = sc.support_score(coef, sc.POLY3, truth)
    if not in_span:
        rec = 0
    if abs(truth.get("x1*x2", 0.0)) > 1e-12 and not sc.explicit_product_text(eq):
        rec = 0
    if abs(truth.get("sin(x1)", 0.0)) > 1e-12 and not sc.explicit_sin_text(eq):
        rec = 0
    yclean = sc.clean_target(law, test[:, 0], test[:, 1])
    fid = ""
    fpth = seed_dir / "fidelity.txt"
    if fpth.exists():
        fid = fpth.read_text(encoding="utf-8").strip()
    return {
        "bank": bank, "law": law, "formula": formula, "noise": noise, "seed": seed,
        "term_recovery": int(rec), "coeff_rel": mae, "rmse": sc.rmse(yclean, yhat_te),
        "explicit_product": sc.explicit_product_text(eq), "explicit_power": explicit_power(eq),
        "diverged": 0, "clip": clip, "runtime_s": rt,
        "equation": sc.equation_from_coef(sc.POLY3, coef),
        "note": f"readout_resid={resid:.3g}; false_pos={fp}; fidelity={fid}",
    }


def copy_original_laws():
    rows = []
    with EXP22.open(newline="", encoding="utf-8") as f:
        for r in csv.DictReader(f):
            if r["method"] != "hsm":
                continue
            rmse = _fnum(r["rmse_clean"])
            diverged = int((not np.isfinite(rmse)) or ("non-finite" in (r.get("note") or "")))
            rows.append({
                "bank": "original", "law": r["law"], "formula": r["formula"],
                "noise": float(r["noise"]), "seed": int(r["seed"]),
                "term_recovery": int(r["term_recovery"]),
                "coeff_rel": _fnum(r["coeff_rel_mae"]),
                "rmse": rmse,
                "explicit_product": int(r["explicit_product"]),
                "explicit_power": 0,
                "diverged": diverged, "clip": 0,
                "runtime_s": _fnum(r["runtime_s"]),
                "equation": r.get("equation") or "",
                "note": "copied from Exp22; " + (r.get("note") or ""),
            })
    return rows


def score_kepler_seed(bank, seed, seed_dir: Path):
    cell = seed_dir.parent
    clip = _clip_flag(cell)
    rt = float("nan")
    rp = seed_dir / "runtime.txt"
    if rp.exists():
        rt = _fnum(rp.read_text(encoding="utf-8").split()[0])
    eq = ""
    ep = seed_dir / "exported_equation.txt"
    if ep.exists():
        eq = ep.read_text(encoding="utf-8", errors="replace")
    test = sc.load_table(seed_dir / "pred_test.csv")
    grid = sc.load_table(seed_dir / "pred_grid.csv")
    hold = ek.power_metrics(grid[:, 0], grid[:, 2])
    te_rmse = sc.rmse(test[:, 2], test[:, 3])
    diverged = int((not np.all(np.isfinite(grid[:, 2]))) or (not np.all(np.isfinite(test[:, 3]))) or (seed_dir / "diverged.txt").exists())
    if diverged:
        hold["term_recovery"] = 0
    return {
        "bank": bank, "law": "Kepler", "formula": "P=a^(3/2)", "noise": "",
        "seed": seed, "term_recovery": int(hold["term_recovery"]) if not diverged else 0,
        "coeff_rel": hold["exponent_rel"], "rmse": te_rmse,
        "explicit_product": sc.explicit_product_text(eq), "explicit_power": explicit_power(eq),
        "diverged": diverged, "clip": clip, "runtime_s": rt,
        "equation": " ".join(eq.split())[:240],
        "note": f"exponent={hold['exponent']}; const={hold['const']}; const_rel={hold['const_rel']}",
    }


def copy_original_kepler():
    rows = []
    with EXP23.open(newline="", encoding="utf-8") as f:
        for r in csv.DictReader(f):
            if r["method"] != "hsm":
                continue
            if int(r["seed"]) == 0:
                continue
            rmse = _fnum(r["test_rmse_year"])
            diverged = int(not np.isfinite(rmse))
            rows.append({
                "bank": "original", "law": "Kepler", "formula": "P=a^(3/2)", "noise": "",
                "seed": int(r["seed"]), "term_recovery": int(r["term_recovery"]),
                "coeff_rel": _fnum(r["exponent_rel"]), "rmse": rmse,
                "explicit_product": sc.explicit_product_text(r.get("equation") or ""),
                "explicit_power": explicit_power(r.get("equation") or ""),
                "diverged": diverged, "clip": 0, "runtime_s": _fnum(r["runtime_s"]),
                "equation": r.get("equation") or "",
                "note": "copied from Exp23; " + (r.get("note") or ""),
            })
    return rows


def collect():
    rows = copy_original_laws() + copy_original_kepler()
    for bank in ("product", "power", "both"):
        broot = OUT / bank
        if not broot.exists():
            continue
        for law in ("L1", "L2", "L3", "L4"):
            law_dir = broot / law
            if not law_dir.exists():
                continue
            for noise_dir in sorted(law_dir.glob("noise_*")):
                noise = float(noise_dir.name.replace("noise_", "").replace("p", "."))
                for seed_dir in sorted(noise_dir.glob("seed_*")):
                    seed = int(seed_dir.name.split("_")[1])
                    rows.append(score_law_seed(bank, law, noise, seed, seed_dir))
        kdir = broot / "kepler"
        if kdir.exists():
            for seed_dir in sorted(kdir.glob("seed_*")):
                seed = int(seed_dir.name.split("_")[1])
                rows.append(score_kepler_seed(bank, seed, seed_dir))
    return rows


def write_outputs(rows):
    OUT.mkdir(parents=True, exist_ok=True)
    fields = [
        "bank", "law", "formula", "noise", "seed", "term_recovery", "coeff_rel", "rmse",
        "explicit_product", "explicit_power", "diverged", "clip", "runtime_s", "equation", "note",
    ]
    with (OUT / "seed_logs.csv").open("w", newline="", encoding="utf-8") as f:
        w = csv.DictWriter(f, fieldnames=fields)
        w.writeheader()
        for r in rows:
            w.writerow(r)

    by = defaultdict(list)
    for r in rows:
        by[(r["bank"], r["law"], "" if r["noise"] == "" else float(r["noise"]))].append(r)

    lines = [
        "# Experiment 25 — product and power operators",
        "",
        "Original rows are copied from Exp22 (L1-L4) and Exp23 (Kepler). They were not retrained.",
        "Term recovery for L1-L4 uses the Exp22 gates. A product term counts only if the export contains nA*nB.",
        "Sine counts only if the export contains sin. Kepler uses the Exp23 exponent and constant gates.",
        "Diverged seeds stay in the denominator. Mean RMSE skips non-finite values; the diverged column is the count.",
        "",
        "| Bank | Law | Noise | n | Term recovery | Explicit product | Explicit power | Diverged | Mean coeff rel | Mean RMSE | Clip |",
        "|------|-----|-------|---|---------------|------------------|----------------|----------|----------------|-----------|------|",
    ]
    banks = ["original", "product", "power", "both"]
    laws = ["L1", "L2", "L3", "L4", "Kepler"]
    noises = [0.0, 0.02, 0.05, ""]
    head = ["Exp25 operator extension", ""]
    for bank in banks:
        for law in laws:
            for noise in noises:
                if law == "Kepler" and noise != "":
                    continue
                if law != "Kepler" and noise == "":
                    continue
                key_noise = "" if law == "Kepler" else float(noise)
                xs = by.get((bank, law, key_noise), [])
                if not xs:
                    continue
                n = len(xs)
                rec = sum(int(r["term_recovery"]) for r in xs)
                ep = sum(int(r["explicit_product"]) for r in xs)
                ew = sum(int(r["explicit_power"]) for r in xs)
                dv = sum(int(r["diverged"]) for r in xs)
                mae = sc.mean_finite([r["coeff_rel"] for r in xs])
                rm = sc.mean_finite([r["rmse"] for r in xs])
                clip = max(int(r["clip"]) for r in xs)
                noise_txt = "holdout" if law == "Kepler" else f"{float(noise):g}"
                lines.append(
                    f"| {bank} | {law} | {noise_txt} | {n} | {rec}/{n} | {ep}/{n} | {ew}/{n} | {dv}/{n} | {_fmt(mae)} | {_fmt(rm)} | {clip} |"
                )
                head.append(
                    f"{bank} {law} {noise_txt}: recovery {rec}/{n}, product {ep}/{n}, power {ew}/{n}, diverged {dv}/{n}, coeff {_fmt(mae)}, rmse {_fmt(rm)}, clip {clip}"
                )
    # L1/L2 regression versus the copied original rows.
    lines += ["", "L1 and L2 recovery versus the original Exp22 rows (same seeds):", ""]
    for law in ("L1", "L2"):
        for noise in (0.0, 0.02, 0.05):
            base = by.get(("original", law, noise), [])
            if not base:
                continue
            base_rec = sum(int(r["term_recovery"]) for r in base)
            bits = [f"original {base_rec}/{len(base)}"]
            for bank in ("product", "power", "both"):
                xs = by.get((bank, law, noise), [])
                if not xs:
                    bits.append(f"{bank} missing")
                    continue
                rec = sum(int(r["term_recovery"]) for r in xs)
                flag = "drop" if rec < base_rec else "holds"
                bits.append(f"{bank} {rec}/{len(xs)} {flag}")
            lines.append(f"- {law} noise {noise:g}: " + "; ".join(bits))
    text = "\n".join(lines) + "\n"
    (OUT / "INDEX.md").write_text(text, encoding="utf-8")
    (OUT / "headline.txt").write_text("\n".join(head) + "\n", encoding="utf-8")
    print(text)


def main():
    sc.self_check()
    rows = collect()
    rows.sort(key=lambda r: (r["bank"], r["law"], str(r["noise"]), int(r["seed"])))
    write_outputs(rows)


if __name__ == "__main__":
    main()
