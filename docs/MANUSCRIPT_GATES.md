# Exp20 — Manuscript rewrite gates

Generated: 2026-09-09 22:06:30

**Minimum gates: PASS** (10/10 PASS).

**Manuscript Results/Discussion rewrite: ALLOWED.**

## Minimum gates (from RemainingToDoExperiments)

| # | Gate | Status | Detail |
|---|------|--------|--------|
| 1 | 30-seed circle experiment complete | **PASS** | n=30 canonical=30/30 |
| 2 | 30-seed canonical area experiment complete | **PASS** | n=30 canonical=30/30 |
| 3 | equation export verified | **PASS** | n=13 fidelity_pass=13 manual_pass=13 |
| 4 | stochastic convergence experiment complete | **PASS** | rows=90 depths=6 (100,1000,10000,300,3000,30000) canonical=87 |
| 5 | operator ablation complete | **PASS** | rows=75 models=['A', 'B', 'C', 'D', 'E'] |
| 6 | pruning ablation complete | **PASS** | rows=60 settings=['none', 'r0', 'r10k', 'r1k', 'r50k'] |
| 7 | at least four synthetic equations complete | **PASS** | problems=['S1', 'S2', 'S3', 'S4', 'S5'] rows=150 |
| 8 | at least one negative/limitation experiment complete | **PASS** | rows=30 cross_mixed=20 limitation_ok=30/30 |
| 9 | baseline comparison complete | **PASS** | nn=30 sklearn=70 methods=['dtree', 'gboost', 'hsm', 'lasso_poly2', 'linreg', 'mlp_dense', 'mlp_pruned', 'mlp_sk', 'poly2', 'rforest'] |
| 10 | all raw data and scripts archived | **PASS** | missing=0 exp02_seed_dirs=31 |

## Additional completed work (beyond minimum)

| Item | Status | Detail |
|------|--------|--------|
| Exp03 circumference | PASS | rows=60 |
| Exp06 noise | PASS | rows=250 |
| Exp09 network size | PASS | rows=100 |
| Exp10 prune tolerance | PASS | rows=60 |
| Exp15 tables | PASS | present |
| Exp16 figures | PASS | present |
| Exp17 statistics | PASS | present |
| Exp18 criteria | PASS | present |
| Exp19 pi policy | PASS | present |

## Next step

If all minimum gates PASS, update the manuscript Results/Discussion from
`Results/Exp15_ManuscriptTables`, `Exp16_Figures`, `Exp17_Statistics`,
`SUCCESS_CRITERIA.md`, and `PI_NEVER_OBJECTIVE.md`. Do not invent numbers;
cite seed logs and summary tables only.

