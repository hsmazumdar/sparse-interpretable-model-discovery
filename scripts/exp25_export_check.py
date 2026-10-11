# -*- coding: utf-8 -*-
"""Evaluate every Exp25 exported_equation.txt as text and compare to pred_grid.csv.

The parser is independent of the C# network. A seed passes when the max absolute
difference between the text evaluation and the saved network grid is <= 1e-8
times max(1, max|yhat|). Non-finite networks are reported separately.
"""
from __future__ import annotations

import csv
import math
import re
from pathlib import Path

import numpy as np

import sr_common as sc

ROOT = Path(__file__).resolve().parent
OUT = ROOT / "Results" / "Exp25_OperatorExtension"
TERM = re.compile(r"([+-]?\s*[0-9.eE+-]+)\*(\(n(\d+)( - 0\.5)?\)\^2|n(\d+))")
POW = re.compile(r"sign\(n(\d+)\)\*\|n\d+(?:\*([0-9.eE+-]+))?\|\^([0-9.eE+-]+)(?:/([0-9.eE+-]+))? a\^k")
PROD = re.compile(r"^n(\d+)\*n(\d+)$")


def sigmoid(z):
    return 1.0 / (1.0 + math.exp(-z)) if z >= 0 else math.exp(z) / (1.0 + math.exp(z))


def parse(text):
    nodes = []
    for line in text.splitlines():
        line = line.strip()
        if not line or "=" not in line:
            continue
        left, right = line.split("=", 1)
        nid = int(left.strip()[1:])
        right = right.strip()
        nodes.append((nid, right))
    return nodes


def eval_nodes(nodes, x1, x2):
    v = {0: x1, 1: x2}
    for nid, body in nodes:
        m = PROD.match(body)
        if m:
            v[nid] = v[int(m.group(1))] * v[int(m.group(2))]
            continue
        m = POW.match(body)
        if m:
            a = v[int(m.group(1))]
            gain = float(m.group(2)) if m.group(2) else 1.0
            k = float(m.group(3))
            div = float(m.group(4)) if m.group(4) else 1.0
            src = gain * a
            mag = max(abs(src), 1e-8)
            z = min(8.0, max(-8.0, k * math.log(mag)))
            p = math.exp(z)
            if src < 0:
                p = -p
            v[nid] = p / div
            continue
        sig = False
        centred = False
        if body.startswith("sigmoid("):
            sig = True
            close = body.rfind(")")
            tail = body[close + 1:].strip()
            centred = tail == "- 0.5"
            body = body[len("sigmoid("):close]
        parts = body.split(" + ")
        z = float(parts[0])
        for part in parts[1:]:
            w_txt, src = part.split("*", 1)
            w = float(w_txt)
            src = src.strip()
            if src.startswith("("):
                inner = src[1:src.index(")")]
                y = v[int(inner.split()[0][1:])]
                if "- 0.5" in inner:
                    y = y - 0.5
                z += w * y * y
            else:
                z += w * v[int(src[1:])]
        if sig:
            z = sigmoid(z)
            if centred:
                z -= 0.5
        v[nid] = z
    out = max(n for n, _ in nodes)
    return v[out]


def kepler_maps(seed_dir):
    s = {}
    for line in (seed_dir / "scale.txt").read_text(encoding="utf-8").splitlines():
        if "=" in line:
            k, val = line.split("=", 1)
            s[k.strip()] = val.strip()
    if s.get("mode") == "fixed":
        return (lambda a: a / 40.0), (lambda z: z * 100.0)
    ma, sa, mp, sp = (float(s[k]) for k in ("meanA", "stdA", "meanP", "stdP"))
    return (lambda a: (a - ma) / sa), (lambda z: z * sp + mp)


def check(seed_dir, kepler):
    eq_path = seed_dir / "exported_equation.txt"
    grid = sc.load_table(seed_dir / "pred_grid.csv")
    if not np.all(np.isfinite(grid[:, 2])) or (seed_dir / "diverged.txt").exists():
        return "nonfinite", float("nan")
    nodes = parse(eq_path.read_text(encoding="utf-8"))
    diffs = []
    if kepler:
        fin, fout = kepler_maps(seed_dir)
    for row in grid:
        if kepler:
            y = fout(eval_nodes(nodes, fin(row[0]), 0.0))
        else:
            y = eval_nodes(nodes, row[0], row[1])
        diffs.append(abs(y - row[2]))
    d = float(np.max(diffs))
    tol = 1e-8 * max(1.0, float(np.max(np.abs(grid[:, 2]))))
    return ("pass" if d <= tol else "fail"), d


def main():
    rows = []
    for bank in ("product", "power", "both"):
        for cell in sorted((OUT / bank).glob("*")):
            kepler = cell.name == "kepler"
            cells = [cell] if kepler else sorted(cell.glob("noise_*"))
            for c in cells:
                for sd in sorted(c.glob("seed_*")):
                    status, d = check(sd, kepler)
                    rows.append({"bank": bank, "cell": str(c.relative_to(OUT / bank)), "seed": sd.name,
                                 "status": status, "max_abs_diff": d})
    with (OUT / "export_check.csv").open("w", newline="", encoding="utf-8") as f:
        w = csv.DictWriter(f, fieldnames=["bank", "cell", "seed", "status", "max_abs_diff"])
        w.writeheader()
        w.writerows(rows)
    n = len(rows)
    npass = sum(r["status"] == "pass" for r in rows)
    nfail = sum(r["status"] == "fail" for r in rows)
    nnf = sum(r["status"] == "nonfinite" for r in rows)
    worst = max((r["max_abs_diff"] for r in rows if r["status"] != "nonfinite"), default=float("nan"))
    print(f"seeds {n} pass {npass} fail {nfail} nonfinite {nnf} worst_diff {worst:.3g}")
    for r in rows:
        if r["status"] == "fail":
            print("FAIL", r)


if __name__ == "__main__":
    main()
