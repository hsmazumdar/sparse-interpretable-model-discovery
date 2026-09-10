# SSD migration note ? MathModelNnPaper

**Saved:** 2026-09-09 07:35 IST  
**Purpose:** Continuity when this SSD workspace moves to another PC.  
**Read first on the new machine:** this file, then `CHECKPOINT_RESUME.md`.

---

## 1. What this project is

Neural equation discovery paper experiments (? / circle / synthetics).  
Code: `NnPruneHsm\` (.NET Framework **4.0 Client**, **x86**).  
Goal pattern: **data ? operator selection ? structural pruning ? explicit equation**.  
Plan document: `RemainingToDoExperiments.docx` (authoritative experiment list).

There is **no git remote** on this tree; treat the SSD folder itself as the source of truth.

---

## 2. Copy / keep intact (do not drop)

| Path | Role |
|------|------|
| `NnPruneHsm\` | Source + `bin\Debug\NnPruneHsm.exe` |
| `Results\Exp01_CircleBoundary\` | Authoritative Exp01 |
| `Results\Exp02_PiArea\` | Authoritative Exp02 |
| `Results\Exp03_Circumference\` | Authoritative Exp03 |
| `Results\Exp04_StochasticPi\` | Authoritative Exp04 |
| `Results\Exp05_SyntheticRecovery\` | Authoritative Exp05 |
| `GitHub_mirror\` | Reviewer mirror + `STATUS.md` |
| `RemainingToDoExperiments.docx` | Remaining experiment plan |
| `Manuscript\` / `Nn2MathModel.md` | Drafts |
| `CHECKPOINT_RESUME.md` | Short resume checklist |
| `CHECKPOINT_manifest.txt` | Machine-readable snapshot |
| `MIGRATION.md` | **This file** |
| `run_exp01.ps1` ? `run_exp05.ps1` | Build+run wrappers |

Optional / smoke folders under `Results\` (e.g. `*_smoke*`) can be kept but are **not** authoritative.

---

## 3. Progress snapshot (as of save)

| Exp | Result folder | Outcome |
|-----|---------------|---------|
| 01 Circle boundary | `Results\Exp01_CircleBoundary\` | **30/30** canonical; mean test acc **93.57%** vs no-quad **82.02%** |
| 02 Area ? \(A=b+kr^2\) | `Results\Exp02_PiArea\` | **30/30**; \(|k-\pi|/\pi\approx0.0017\%\) |
| 03 Circumference ? \(C=b+kr\) | `Results\Exp03_Circumference\` | **60/60** (raw+improved); \(k\sim6.62\), ~**5.45%** from \(2\pi\) (digitization) |
| 04 Stochastic \(k_N\to\pi\) | `Results\Exp04_StochasticPi\` | **87/90**; error **1.15%?0.088%** with \(N\) |
| 05 Synthetic S1?S5 | `Results\Exp05_SyntheticRecovery\` | **150/150** structure + canonical (15 seeds ? 5 ? noise 0 / 0.05) |

**Next experiment: Exp06 ? Noise robustness**  
(From plan: noise levels 0%, 1%, 2%, 5%, 10% on synthetics; track structure rate, coeff error, complexity.)  
**Not started** ? no `--exp06` yet.

Then: Exp07 operator ablation ? Exp08 pruning ablation ? Exp09 size ? Exp10 tolerance ? Exp11 baselines ? Exp12+ tables/figures/manuscript.

---

## 4. Boot on the new PC

1. Attach SSD; open folder (drive letter may change ? scripts use relative paths).
2. Confirm build tools:  
   `%WINDIR%\Microsoft.NET\Framework\v4.0.30319\MSBuild.exe`  
   (Windows SDK / .NET 4 targeting pack helps; build may still work via GAC with MSB3644 warnings.)
3. Rebuild if needed:
   ```powershell
   cd <this-folder>
   & "${env:WINDIR}\Microsoft.NET\Framework\v4.0.30319\MSBuild.exe" `
     .\NnPruneHsm\NnPruneHsm.csproj /p:Configuration=Debug /p:Platform=x86 /v:minimal
   ```
4. Smoke a finished experiment:
   ```powershell
   .\run_exp05.ps1 --quick
   ```
5. Tell Cursor: *?Continue from MIGRATION.md / CHECKPOINT_RESUME.md ? start Exp06.?*

Live status for reviewers: `GitHub_mirror\STATUS.md` (also copied under each `Results\ExpXX\STATUS.md`).

---

## 5. CLI map

```text
NnPruneHsm.exe --exp01   circle boundary (2-22-1)
NnPruneHsm.exe --exp02   A = b + k r^2 from pixel area
NnPruneHsm.exe --exp03   C = b + k r from digital perimeter
NnPruneHsm.exe --exp04   Monte Carlo area; k_N vs N
NnPruneHsm.exe --exp05   synthetic suite S1?S5
```

Wrappers: `.\run_exp0N.ps1` (build then run into `Results\?`).  
Common flags: `--quick`, `--seeds N`, `--out DIR`. Exp05 also: `--problems S1,S2,?`, `--noise 0,0.05`.

---

## 6. Hard design rules (do not regress)

- **Never** use `Math.PI` / \(2\pi r\) / analytical \(A=\pi r^2\) as labels or training targets. Score ? / \(2\pi\) / true coeffs **only after** freeze.
- Canonical coeffs from **topology** or **validation-gated Pass-E train-OLS**, not visual guesswork.
- Neuron bank: **funno 7** = uncentered square-linear; **funno 4** = centred quadratic-sigmoid.
- Exp01/S5 labels: \((x-0.5)^2+(y-0.5)^2 \le R^2\), \(R=0.25\).
- Do not fabricate tables; run experiments, save raw outputs, then update manuscript.

### Exp05 formulas (for Exp06 reuse)

| ID | Formula |
|----|---------|
| S1 | \(y=2.5x+1.2\) |
| S2 | \(y=1.7x^2+0.4\) |
| S3 | \(y=1.5x+2.2x^2-0.7\) |
| S4 | \(y=1.3x_1^2+0.8x_2^2+0.2\) |
| S5 | circle (same as Exp01) |

Key source files: `SyntheticProblems.cs`, `Exp05Runner.cs`, `CanonicalExtractor.cs`, `Exp0NRunner.cs`, `Program.cs`, `GitHubMirrorPublisher.cs`.

---

## 7. Cursor / chat continuity

- Agent transcripts live on the **old PC?s Cursor app data**, not necessarily on this SSD. Prefer these on-disk docs over chat history.
- Prior Cursor chat UUID (this machine): `c7befc49-f472-4dca-8cb6-49a0d39fd9ca` ? optional; docs above are enough to resume.
- After Exp06 lands: update `CHECKPOINT_RESUME.md`, `CHECKPOINT_manifest.txt`, this `MIGRATION.md` ?3, and mirror `STATUS.md`.

---

## 8. Suggested first prompt on new PC

> Open this workspace. Read `MIGRATION.md` and `CHECKPOINT_RESUME.md`. Exp01?05 are complete (authoritative folders under `Results\`). Implement and run **Exp06 noise robustness** per `RemainingToDoExperiments.docx` (noise 0/1/2/5/10% on synthetic suite; structure + coeff metrics). Reuse Exp05 generators/runners; add `--exp06` + `run_exp06.ps1`. Do not use Math.PI in labels.

---
## Office update 2026-09-09 09:15
**Exp06 Noise complete** ? `Results\Exp06_Noise\`. Next: Exp07 operator ablation.


## Office 2026-09-09 09:34
**Exp07 complete.** Next: Exp08 pruning ablation.

## Office 2026-09-09 09:49
**Exp08 complete** ? `Results\Exp08_PruningAblation\` (60 runs). Canon % rises with retrain: 0/0/8/67/100 for none/r0/1k/10k/50k. Next: Exp09 network size.

## Office 2026-09-09 10:10
**Exp09 complete** ? `Results\Exp09_NetworkSize\` (100 runs). Area+circle ? H?{8,12,22,36,50} ? 10 seeds; **99/100** canonical. Next: Exp10 prune tolerance.

## Office 2026-09-09 10:50
**Exp10 complete** ? `Results\Exp10_PruneTolerance\` (60 runs). Canon 50%?70% as factor 1.0?2.0; compression/terms follow Pareto. Next: Exp11 baselines.

## Office 2026-09-09 10:53
**Exp11 complete** ? `Results\Exp11_Baselines\`. HSM 10/10 canonical without FE; poly2/lasso match RMSE only with prior r^2 features; ordinary MLP/trees do not recover operator. Next: Exp12 cross-product limitation.

## Office 2026-09-09 11:01
**Exp12 complete** ? `Results\Exp12_CrossProduct\` (30 runs). Diag 10/10 recovered; cross/mixed 0% exact xy (limitation OK 100%). Honest negative boundary recorded. Next: Exp13+ manuscript gates.

## Office 2026-09-09 11:24
**Exp13 complete** ? `Results\Exp13_EquationExport\` (13 runs). Fidelity+manual **13/13 PASS** (worst |d|=1.4e-14). Equation-export gate satisfied. Next: Exp14?15 tables/repro packaging.

## Office 2026-09-09 11:50
**Exp14?15 complete** ? `Results\Exp14_Reproducibility\` (988 seed runs audited) + `Results\Exp15_ManuscriptTables\` (Tables A?D + E?J). Next: Exp16 figures.

## Office 2026-09-09 12:05
**Exp16 complete** ? `Results\Exp16_Figures\` (11 PNG/PDF figures from CSVs). Next: Exp17 statistical reporting.

## Office 2026-09-09 12:15
**Exp17 complete** ? `Results\Exp17_Statistics\` (245 continuous + 183 rate groups; 26+23 paired tests with Holm). Structure-recovery rates + CIs from CSVs only. Next: Exp18?20 manuscript gates.

## Office 2026-09-09 12:50
**Exp18 complete** ? `SUCCESS_CRITERIA.md` frozen; audit PASS (64 PASS / 0 FAIL / 1 WARN). Code tolerances match document. Next: Exp19 ?-never-objective, Exp20 manuscript gates.


## Office 2026-09-09 12:56
**Exp19+20 complete** ù pi-never-objective audit PASS (AllowFallback default hardened false); manuscript minimum gates **10/10 PASS**, Results/Discussion rewrite **ALLOWED**. RemainingToDo Exp01-20 done; next = manuscript update from archived CSVs.

## Office 2026-09-09 13:05
**Nn2MathModelV02.docx** ù V01 method draft + Part II appending Exp01-20 results, advice implementations, criteria/pi policy, stats, gates. Rebuild: `python _build_Nn2MathModelV02.py`.

## Office 2026-09-09 21:50
**Appendix A item 10 frozen** ù `PRIOR_ART.md`. Surviving novelty = joint validation-constrained pruneùretrainùrollback on a mixed linear/sigmoid/quadratic graph with equation-vs-network fidelity. Closest neighbours: EQL/EQLù, SymbolNet (2024), PruneSymNet (2024), KAN (2024). Do not claim first equation learner or empirical superiority over PySR/EQL/KAN (those are literature-only). Rebuild: `python _build_Nn2MathModelV02.py`. Next: editorial polish; optional public data / SR bake-off.

## Current snapshot (supersedes ù3 as of 2026-09-09 21:50)

Exp01ù20 complete (authoritative `Results\Exp01_ù` through `Exp20_ManuscriptGates\`). Manuscript rewrite ALLOWED. Prior-art wording FROZEN. Keep also: `PRIOR_ART.md`, `Nn2MathModelV02.docx`, `_build_Nn2MathModelV02.py`, `RECONCILIATION.md`, `SUCCESS_CRITERIA.md`, `PI_NEVER_OBJECTIVE.md`.

## Office 2026-09-09 22:06
**GitHub_mirror synced to V02** ó manuscript docx, reconciliation, Exp14ñ20 source/scripts, regenerated 11 figures, README/METHOD refresh. `GATES.csv` uses relative paths.

