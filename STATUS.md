# Live experiment status

Last update: 2026-09-09 22:12 IST  
Mirror sync: code, results, figures, prior-art freeze. Unpublished manuscript is **not** in this folder.

| Experiment | Status | Degree of success |
|------------|--------|-------------------|
| 01–18 | complete | see `results/` |
| 19 π-never-objective | complete | see Exp19 |
| 20 Manuscript gates | complete | minimum PASS; rewrite ALLOWED |
| Appendix A item 10 prior-art | **frozen** | `docs/PRIOR_ART.md`; joint-pipeline novelty only |
| Exp16 figures | mirrored | 11 PNG + 11 PDF |

## Headline (prior-art freeze)

Literature comparison frozen 2026-09-09.
Surviving claim: validation-constrained prune–retrain–rollback on a mixed linear/sigmoid/quadratic graph with equation-vs-network fidelity.
Closest neighbours (not ignored): EQL/EQL÷, SymbolNet 2024, PruneSymNet 2024, KAN 2024.
Forbidden: first neural equation learner; empirical superiority over PySR/EQL/KAN (those were not Exp11).

## Headline (Exp20)

Exp20 manuscript rewrite gates — 2026-09-09 22:06:30
Minimum gates=PASS (10/10 PASS, 0 FAIL)
Rewrite Results/Discussion: ALLOWED
  [PASS] 30-seed circle experiment complete: n=30 canonical=30/30
  [PASS] 30-seed canonical area experiment complete: n=30 canonical=30/30
  [PASS] equation export verified: n=13 fidelity_pass=13 manual_pass=13
  [PASS] stochastic convergence experiment complete: rows=90 depths=6 (100,1000,10000,300,3000,30000) canonical=87
  [PASS] operator ablation complete: rows=75 models=['A', 'B', 'C', 'D', 'E']
  [PASS] pruning ablation complete: rows=60 settings=['none', 'r0', 'r10k', 'r1k', 'r50k']
  [PASS] at least four synthetic equations complete: problems=['S1', 'S2', 'S3', 'S4', 'S5'] rows=150
  [PASS] at least one negative/limitation experiment complete: rows=30 cross_mixed=20 limitation_ok=30/30
  [PASS] baseline comparison complete: nn=30 sklearn=70 methods=['dtree', 'gboost', 'hsm', 'lasso_poly2', 'linreg', 'mlp_dense', 'mlp_pruned', 'mlp_sk', 'poly2', 'rforest']
  [PASS] all raw data and scripts archived: missing=0 exp02_seed_dirs=31

## Headline (Exp19)

Exp19 pi-never-objective audit — 2026-09-09 12:55:50
Overall=PASS  PASS=71 FAIL=0 WARN=0
No executable Math.PI labeling; ReferencePi used only for post-hoc scoring.
AllowFallback=false on Exp runners; default PiOptions.AllowFallback=false.

## Mirror contents for V02 claims

- Numeric Exp01–20 headlines: `results/Exp0N_*/headline.txt`
- Tables A–J: `results/Exp15_ManuscriptTables/`
- Figures 01–11: `results/Exp16_Figures/Fig*.png` (and `.pdf`)
- Claim audit: `docs/RECONCILIATION.md`
- Source `--exp01`…`--exp20`: `src/NnPruneHsm/`
- Python Exp14–20: `scripts/`

See `README.md`. Exp14 `GAP_REPORT.md` still lists missing per-seed files (e.g. `predictions.csv`); that does not reopen the Exp20 minimum gates.
