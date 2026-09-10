# Pre-registered success criteria

**Status:** frozen for manuscript reporting (Exp18).  
**Rule:** train / prune / stop / select hyperparameters using only training loss, validation loss, structural simplicity, and the validation-constrained prune rule. **Never** use closeness to \(\pi\) or \(2\pi\) as an objective. Compare recovered constants to \(\pi\) only after the model is frozen.

Shared code constants live in `NnPruneHsm/NnBpLib/CanonicalExtractor.cs` (`ExperimentCriteria`) and related extractors. Do not retune these toward \(\pi\) after seeing results.

| Symbol | Value | Role |
|--------|------:|------|
| `LinearContaminationTol` | 0.05 | Max allowed wrong-degree coefficient ratio |
| `InterceptRelTol` | 0.05 | Near-zero intercept vs mean target scale |
| `FidelityMaxTol` | \(10^{-8}\) | Exported equation vs live network |
| `DegreeEps` | \(10^{-12}\) | Numerical zero for coefficients / edges |
| Circle `RelAbTol` | 0.10 | \(|a-b|\) relative for centred quadratic |
| Pass-E strip | \(1.25\times L_{\mathrm{dense}}\) | Max validation loss to accept train-OLS install |

---

## Experiment 01 — circle boundary

**Structural success.** At least one quadratic operator (funno 4 or 7) remains on a path to the output.

**Canonical success.** A type-4 hidden receives both inputs and reduces to  
\(h = c + a(x-\tfrac12)^2 + b(y-\tfrac12)^2\) with \(|a-b|/\mathrm{mean}(|a|,|b|) \le 0.10\).

**Constant recovery.** Report effective radius \(\hat R\) and error vs true \(R\) for every seed (no exclusion).

**Equation fidelity.** \(\max |y_{\mathrm{NN}}-y_{\mathrm{eq}}| \le 10^{-8}\) on the test grid.

**Paired baseline.** Same seeds without quadratic operators (reported as `baseline_acc`); not used for stopping.

---

## Experiment 02 — area law \(A = b + k r^2\)

Data: pixel counts with centres inside \(x^2+y^2\le r^2\). No `Math.PI` in labels.

| Gate | Definition | Tolerance |
|------|------------|-----------|
| Structural | ≥1 square-linear (7) or centred quadratic (4) on path to output | ≥ 1 |
| Canonical | Active graph reduces to \(A=b+kr^2\) (linear-\(r\) negligible) | \(\lvert c_1\rvert/\max(\lvert c_2\rvert,10^{-12})\le 0.05\) |
| Near-zero intercept | \(\lvert b\rvert/\mathrm{mean}(A)\) | ≤ 0.05 |
| Constant recovery | Report \(k\) and \(\lvert k-\pi\rvert/\pi\) for **every** seed | no exclusion |
| Equation fidelity | Export vs network on test split | max abs ≤ \(10^{-8}\) |

OLS on predictions is diagnostic only unless **Pass E** installs a train-OLS form that still meets the locked validation budget (strip \(1.25\times\)). \(\pi\) is never the acceptance objective.

---

## Experiment 03 — circumference \(C = b + k r\)

| Gate | Definition | Tolerance |
|------|------------|-----------|
| Structural | Linear-in-\(r\) path (hidden linear or direct I→O) | ≥ 1 |
| Canonical | Reduces to \(C=b+kr\) (quadratic contamination small) | \(\lvert c_2\rvert/\max(\lvert c_1\rvert,10^{-12})\le 0.05\) |
| Near-zero intercept | \(\lvert b\rvert/\mathrm{mean}(C)\) | ≤ 0.05 |
| Constant recovery | Report \(k\) vs \(2\pi\) for every seed after freeze | no exclusion |
| Equation fidelity | Export vs network | max abs ≤ \(10^{-8}\) |

---

## Experiment 04 — stochastic \(k_N\to\pi\)

Same Exp02 discovery gates per depth \(N\). Labels from Monte Carlo area; no `Math.PI`. Report rates and \(k\) distributions vs \(N\) for all seeds.

---

## Experiments 05–06 — synthetic recovery / noise

**Structure OK.** Problem-specific operator presence (e.g. both diagonal quadratic coeffs \(|a|,|b|>0.05\) for S1–S4-style tasks; circle uses `CircleCanonicalExtractor`).

**Canonical.** Structure recovered and coefficients comparable under the problem’s relative-error reporting.

**Fidelity.** Same `FidelityMaxTol`.

**Noise.** Exp06 uses noise ∈ {0, 0.01, 0.02, 0.05, 0.10}; do not drop failing seeds from rate denominators.

---

## Experiment 07 — operator ablation

Success is **canonical area form** under each model A–E using the same Exp02 extractor / Pass-E policy as configured per model. Compare models on matched seeds; do not redefine “success” after seeing which model wins.

---

## Experiments 08–10 — prune / size / tolerance

Same Exp02 (or Exp01 for circle size cells) structural + canonical gates. Exp08 isolates retrain length with Pass E **off**. Exp10 varies prune strip factor; record compression and complexity without post-hoc gate changes.

---

## Experiment 11 — baselines

| Method family | Success definition |
|---------------|-------------------|
| HSM | Exp02 canonical + operator recovery **without** prior \(r^2\) feature engineering |
| MLP variants | Same canonical/operator flags (expected fail without quadratic ops) |
| Sklearn | `canonical` / `operator_recovery` only if closed form matches \(A=b+kr^2\); prior-FE methods must be labelled `prior_features=1` |

Primary discovery metric: **operator / structure recovery rate**, not RMSE alone.

---

## Experiment 12 — cross-product limitation (negative result)

| Problem | Success |
|---------|---------|
| `diag` | Structure recovered (diagonal \(x_1^2,x_2^2\)); `expects_exact=1` |
| `cross` / `mixed` | **Limitation OK** = no false claim of exact \(x_1 x_2\) (`structure=0`, `explicit_cross=0`) |

Never install an \(x_1 x_2\) operator in Pass E to hide the limitation.

---

## Experiment 13 — equation export

**Fidelity pass.** Export reproduces network within `FidelityMaxTol`.  
**Manual pass.** Independent recomputation of selected test points from the export text within the same numerical class.  
Both required; do not relax after failures.

---

## Constant recovery (all \(\pi\)-scored experiments)

- Report \(\varepsilon_\pi = 100\,|k-\pi|/\pi\) (or vs \(2\pi\) for circumference) for every seed, including non-canonical runs (`k` may be null; still count the seed in denominators).
- Machin \(\pi = 16\arctan\frac15 - 4\arctan\frac1{239}\) is used **only** for post-hoc scoring.

---

## What Exp18 verifies

1. These definitions exist in code (`ExperimentCriteria`, extractors, runners) and this document.  
2. Authoritative `Results/Exp0N_*/seed_logs.csv` retain all planned seeds (no post-hoc dropping of bad \(\pi\) errors).  
3. Where `fidelity_max` is logged, `fidelity_pass` agrees with `FidelityMaxTol`.  
4. Criteria are not edited to match desired percentages after Exp01–17 completed.
