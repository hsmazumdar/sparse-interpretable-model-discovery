# Sparse Interpretable Model Discovery

Heterogeneous neural graph → validation-constrained prune–retrain–rollback → exported equation.

This repository holds the `.NET Framework 4.0` Client (x86) implementation (`src/NnPruneHsm`) and archived seed logs, tables, and figures. The unpublished manuscript is **not** included.

It is not ordinary weight compression. The measured claim is whether the pruned graph recovers **both structure and coefficients**, with equation-vs-network fidelity.

## Frozen vs live

| Path | Meaning |
|------|---------|
| `STATUS.md` | Live dashboard (Exp01–20 complete; prior-art freeze) |
| `docs/SUCCESS_CRITERIA.md` | Pre-registered gates (Exp18 freeze) |
| `docs/PI_NEVER_OBJECTIVE.md` | π is never a train/prune objective |
| `docs/PRIOR_ART.md` | Appendix A item 10 — literature freeze |
| `docs/RECONCILIATION.md` | V02 results ↔ claims audit |
| `docs/METHOD.md` | Operators, prune rule, Pass-E strip |
| `docs/CHECKPOINT_RESUME.md` | Short resume (no paper file) |
| `results/` | Authoritative run folders (never invent numbers) |
| `src/NnPruneHsm/` | C# source used for Exp01–25 (no `bin` / `obj`) |
| `scripts/` | Python Exp11/14–25 helpers + Exp02 reproduce wrapper |

**π is never used to generate labels.** Pixel counts and perimeters come from geometry or Monte Carlo. π is scored only after a model is frozen (Exp19).

## Reproduce Experiment 02 (area law \(A = b + k r^2\))

Requirements: Windows, .NET Framework 4.0 Client, MSBuild.

```powershell
.\scripts\reproduce_exp02.ps1
```

Or:

```powershell
cd src\NnPruneHsm
msbuild NnPruneHsm.csproj /p:Configuration=Debug /p:Platform=x86
.\bin\Debug\NnPruneHsm.exe --exp02 --out ..\..\results\Exp02_PiArea
```

CLI in this snapshot: `--exp01` … `--exp20`, `--exp22`, `--exp23`, `--exp25` (Exp14–25 also have Python entry points under `scripts/`).

## Experiment status

| Exp | Result folder | Headline |
|-----|---------------|----------|
| 01 Circle | `results/Exp01_CircleBoundary/` | 30/30 canonical; mean test acc 93.57% vs no-quad 82.02% |
| 02 Area | `results/Exp02_PiArea/` | 30/30; \|k−π\|/π ≈ 0.0017% |
| 03 Circumference | `results/Exp03_Circumference/` | 60/60 form; ~5.46% digitization bias vs 2π |
| 04 Stochastic | `results/Exp04_StochasticPi/` | 87/90; error falls with N |
| 05 Synthetics | `results/Exp05_SyntheticRecovery/` | 150/150 structure |
| 06 Noise | `results/Exp06_Noise/` | 250/250 structure |
| 07 Operators | `results/Exp07_OperatorAblation/` | Model A 0/15 vs E 15/15 |
| 08 Retrain | `results/Exp08_PruningAblation/` | 0% / 0% / 8% / 67% / 100% |
| 09 Size | `results/Exp09_NetworkSize/` | 99/100 |
| 10 Tolerance | `results/Exp10_PruneTolerance/` | Pareto vs factor |
| 11 Baselines | `results/Exp11_Baselines/` | HSM 10/10 without FE; poly2 only with prior r² |
| 12 Cross-product | `results/Exp12_CrossProduct/` | 0% exact xy; 30/30 limitation_ok |
| 13 Export | `results/Exp13_EquationExport/` | fidelity+manual 13/13 |
| 14–15 | `results/Exp14_Reproducibility/`, `Exp15_ManuscriptTables/` | Archive audit + Tables A–J |
| 16 Figures | `results/Exp16_Figures/` | 11 PNG/PDF from CSVs |
| 17–20 | `results/Exp17_Statistics/` … `Exp20_ManuscriptGates/` | CIs; criteria; π-policy; 10/10 gates |
| 21 Same-split Exp12 | `results/Exp21_SymbolicSameSplit/` | diag: graph 10/10, SINDy 10/10; cross/mixed: graph 0/10, SINDy 10/10 |
| 22 Known laws | `results/Exp22_KnownLaws/` | σ=0: L1 20/20, L2 20/20, L3 0/20, L4 0/20 (graph); SINDy 20/20/20/0; gplearn+sin wins L4 |
| 23 Kepler | `results/Exp23_Kepler/` | log–log 20/20, gplearn 15/20, SINDy 0/20, graph 0/20 |
| 24 SINDy libraries | `results/Exp24_SINDyLibraryMismatch/` | L1/L2 need x1², x2² in the library (0/20 without, 20/20 with) |
| 25 Product/power | `results/Exp25_OperatorExtension/` | L3 0/20 → 14–16/20 with a fixed x1·x2 feature; Kepler 2/20 with power path; export check 710/710 |

`results/SUMMARY_Exp24_25.md` summarises Exp24–25. For Exp21–25 only the cell-level files are mirrored (`seed_logs.csv`, `PROTOCOL.md`, `INDEX.md`, `headline.txt`, `CLIP.txt`, and small supporting CSVs); the per-seed folders (splits, predictions, exports) stay in the research tree. In those logs the method value `hsm` is the heterogeneous quadratic neural graph (HQNG).

Do not treat a best seed as the result. Structure-recovery **frequency** is the headline metric. Denominators retain every planned run.

## Novelty (do not over-claim)

Surviving wording is the **joint** prune–retrain–rollback pipeline on a mixed linear/sigmoid/quadratic graph with equation-vs-network fidelity (`docs/PRIOR_ART.md`). Closest neighbours: EQL/EQL÷, SymbolNet (2024), PruneSymNet (2024), KAN (2024). Exp11 did **not** run PySR/EQL/KAN.

## Known archive gaps

`results/Exp14_Reproducibility/GAP_REPORT.md` lists missing per-seed files (often `predictions.csv` / `config.json` on later experiments). Exp02 seed packs are complete. Exp20 “missing=0” refers to the minimum gate file list, not every Exp14 filename.
