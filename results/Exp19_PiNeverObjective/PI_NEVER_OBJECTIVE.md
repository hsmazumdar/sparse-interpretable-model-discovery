# Policy: do not optimize toward π

**Exp19 freeze.** During training, pruning, hyperparameter selection, or early stopping:

- **Never** use closeness of recovered k to π or 2π as an objective, acceptance rule, or architecture-search score.
- Use only: training loss, validation loss, structural simplicity, and the locked validation-constrained prune rule.
- Only **after** the model (and optional Pass E install under the validation budget) is frozen may k be compared with Machin π.

## Allowed post-hoc uses

| Use | When |
|-----|------|
| `PiDataGenerator.ReferencePi()` / `ReferenceTwoPi()` | Scoring `pi_error` / `two_pi_error` in metrics and tables |
| Best/worst seed by |k−π| | Summary reporting only |
| Manuscript / Exp17 statistics | After freeze |

## Forbidden (circular)

| Pattern | Status in this codebase |
|---------|-------------------------|
| Label targets with `Math.PI` or 2πr | Not used (geometry / Monte Carlo only) |
| Grow hidden size because |k−π| is large (`AllowFallback`) | Disabled (`AllowFallback = false` on Exp runners and default) |
| Accept/reject prune because k≈π | Not present; prune uses validation loss vs dense baseline |
| Pass E accept because k≈π | Pass E uses train OLS + validation strip only |

Machin reference: π = 16·arctan(1/5) − 4·arctan(1/239) in `ReferencePi()`.
