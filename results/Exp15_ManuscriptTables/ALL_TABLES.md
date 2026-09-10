# Manuscript summary tables (Exp15)

Generated: 2026-09-09 11:49:49

Primary tables A–D built from authoritative `Results/Exp0N_*` CSVs.
Extended ablation/baseline headlines copied as TableE–J companions.

## Contents

- `TableA_Circle.md` / `.csv`
- `TableB_PiArea.md` / `.csv`
- `TableC_Stochastic.md` / `.csv`
- `TableD_Ablation.md` / `.csv`
- `TableE_PruningAblation_headline.txt`
- `TableE_PruningAblation_summary.md`
- `TableE_PruningAblation_seed_logs.csv`
- `TableF_NetworkSize_headline.txt`
- `TableF_NetworkSize_summary.md`
- `TableF_NetworkSize_seed_logs.csv`
- `TableG_PruneTolerance_headline.txt`
- `TableG_PruneTolerance_summary.md`
- `TableG_PruneTolerance_pareto_points.csv`
- `TableG_PruneTolerance_seed_logs.csv`
- `TableH_Baselines_headline.txt`
- `TableH_Baselines_summary.md`
- `TableH_Baselines_Table_Baselines.md`
- `TableH_Baselines_nn_seed_logs.csv`
- `TableH_Baselines_sklearn_seed_logs.csv`
- `TableI_CrossProduct_headline.txt`
- `TableI_CrossProduct_summary.md`
- `TableI_CrossProduct_seed_logs.csv`
- `TableJ_EquationExport_headline.txt`
- `TableJ_EquationExport_summary.md`
- `TableJ_EquationExport_seed_logs.csv`

---

# Table A — Circle recovery

| Seed | Dense n | Dense e | Final n | Final e | Quad retained | Test acc | a | b | |a-b| rel % | Fidelity |
|---|---|---|---|---|---|---|---|---|---|---|
| 1 | 25 | 299 | 4 | 3 | 1 | 93.38% | -9.5169 | -9.5169 | 0.00 | PASS |
| 2 | 25 | 299 | 4 | 3 | 1 | 92.72% | -9.5795 | -9.5795 | 0.00 | PASS |
| 3 | 25 | 299 | 4 | 3 | 1 | 89.74% | -9.4836 | -9.4836 | 0.00 | PASS |
| 4 | 25 | 299 | 4 | 3 | 1 | 92.72% | -9.7074 | -9.7074 | 0.00 | PASS |
| 5 | 25 | 299 | 4 | 3 | 1 | 93.38% | -9.6819 | -9.6819 | 0.00 | PASS |
| 6 | 25 | 299 | 4 | 3 | 1 | 92.38% | -9.4967 | -9.4967 | 0.00 | PASS |
| 7 | 25 | 299 | 4 | 3 | 1 | 92.38% | -9.5136 | -9.5136 | 0.00 | PASS |
| 8 | 25 | 299 | 4 | 3 | 1 | 91.72% | -9.7197 | -9.7197 | 0.00 | PASS |
| 9 | 25 | 299 | 9 | 20 | 2 | 98.01% | -1.1002 | -1.0233 | 7.24 | PASS |
| 10 | 25 | 299 | 4 | 3 | 1 | 91.72% | -9.5066 | -9.5066 | 0.00 | PASS |
| 11 | 25 | 299 | 4 | 3 | 1 | 88.74% | -9.6493 | -9.6493 | 0.00 | PASS |
| 12 | 25 | 299 | 4 | 3 | 1 | 93.71% | -9.612 | -9.612 | 0.00 | PASS |
| 13 | 25 | 299 | 4 | 3 | 1 | 92.72% | -9.5503 | -9.5503 | 0.00 | PASS |
| 14 | 25 | 299 | 20 | 56 | 8 | 99.67% | 0.60382 | 0.65646 | 8.35 | PASS |
| 15 | 25 | 299 | 4 | 3 | 1 | 91.72% | -9.5266 | -9.5266 | 0.00 | PASS |
| 16 | 25 | 299 | 4 | 3 | 1 | 93.71% | -9.6617 | -9.6617 | 0.00 | PASS |
| 17 | 25 | 299 | 4 | 3 | 1 | 92.05% | -9.4035 | -9.4035 | 0.00 | PASS |
| 18 | 25 | 299 | 25 | 288 | 12 | 99.01% | -0.9294 | -0.92689 | 0.27 | PASS |
| 19 | 25 | 299 | 4 | 3 | 1 | 92.72% | -9.6113 | -9.6113 | 0.00 | PASS |
| 20 | 25 | 299 | 4 | 3 | 1 | 92.72% | -9.4096 | -9.4096 | 0.00 | PASS |
| 21 | 25 | 299 | 4 | 3 | 1 | 93.38% | -9.5479 | -9.5479 | 0.00 | PASS |
| 22 | 25 | 299 | 25 | 277 | 12 | 99.01% | -0.6614 | -0.64367 | 2.72 | PASS |
| 23 | 25 | 299 | 4 | 3 | 1 | 91.39% | -10.008 | -10.008 | 0.00 | PASS |
| 24 | 25 | 299 | 15 | 43 | 5 | 98.34% | -0.89384 | -0.92799 | 3.75 | PASS |
| 25 | 25 | 299 | 4 | 3 | 1 | 91.39% | -9.5761 | -9.5761 | 0.00 | PASS |
| 26 | 25 | 299 | 4 | 3 | 1 | 95.36% | -9.7817 | -9.7817 | 0.00 | PASS |
| 27 | 25 | 299 | 4 | 3 | 1 | 94.37% | -9.7782 | -9.7782 | 0.00 | PASS |
| 28 | 25 | 299 | 4 | 3 | 1 | 94.70% | -9.7596 | -9.7596 | 0.00 | PASS |
| 29 | 25 | 299 | 4 | 3 | 1 | 93.05% | -9.6009 | -9.6009 | 0.00 | PASS |
| 30 | 25 | 299 | 4 | 3 | 1 | 91.06% | -9.5803 | -9.5803 | 0.00 | PASS |


# Table B — Pi area recovery

| Seed | Final n/e | Canonical kr^2? | Bias b | Recovered k | Pi error % | Quad retained? | Test RMSE |
|---|---|---|---|---|---|---|---|
| 1 | 3/2 | yes | 9.9217 | 3.1416 | 0.0016 | yes | 21.337 |
| 2 | 3/2 | yes | 9.0086 | 3.1417 | 0.0021 | yes | 19.273 |
| 3 | 3/2 | yes | 9.7784 | 3.1416 | 0.0017 | yes | 19.657 |
| 4 | 3/2 | yes | 9.7308 | 3.1416 | 0.0015 | yes | 18.23 |
| 5 | 3/2 | yes | 10.477 | 3.1416 | 0.0018 | yes | 19.18 |
| 6 | 3/2 | yes | 9.5788 | 3.1416 | 0.0018 | yes | 18.475 |
| 7 | 3/2 | yes | 11.367 | 3.1416 | 0.0014 | yes | 18.904 |
| 8 | 3/2 | yes | 9.4549 | 3.1417 | 0.0018 | yes | 20.378 |
| 9 | 3/2 | yes | 9.449 | 3.1417 | 0.0018 | yes | 21.952 |
| 10 | 3/2 | yes | 9.8183 | 3.1417 | 0.0020 | yes | 21.939 |
| 11 | 3/2 | yes | 3.2957 | 3.1417 | 0.0034 | yes | 18.662 |
| 12 | 3/2 | yes | 9.9234 | 3.1416 | 0.0017 | yes | 18.841 |
| 13 | 3/2 | yes | 8.2217 | 3.1417 | 0.0022 | yes | 21.343 |
| 14 | 3/2 | yes | 9.4748 | 3.1417 | 0.0019 | yes | 20.813 |
| 15 | 3/2 | yes | 8.8293 | 3.1417 | 0.0019 | yes | 21.516 |
| 16 | 3/2 | yes | 8.2102 | 3.1417 | 0.0021 | yes | 21.147 |
| 17 | 3/2 | yes | 9.3045 | 3.1417 | 0.0018 | yes | 18.284 |
| 18 | 3/2 | yes | 10.732 | 3.1416 | 0.0017 | yes | 22.822 |
| 19 | 3/2 | yes | 10.228 | 3.1416 | 0.0016 | yes | 21.521 |
| 20 | 3/2 | yes | 9.9849 | 3.1416 | 0.0016 | yes | 19.512 |
| 21 | 3/2 | yes | 20.095 | 3.1416 | 0.0001 | yes | 22.281 |
| 22 | 3/2 | yes | 8.8443 | 3.1417 | 0.0023 | yes | 21.232 |
| 23 | 3/2 | yes | 9.7491 | 3.1416 | 0.0017 | yes | 21.234 |
| 24 | 3/2 | yes | 9.1465 | 3.1417 | 0.0019 | yes | 23.176 |
| 25 | 3/2 | yes | 11.798 | 3.1416 | 0.0011 | yes | 24.626 |
| 26 | 3/2 | yes | 9.6103 | 3.1417 | 0.0021 | yes | 19.342 |
| 27 | 3/2 | yes | 10.241 | 3.1416 | 0.0014 | yes | 19.073 |
| 28 | 3/2 | yes | 9.7414 | 3.1416 | 0.0015 | yes | 20.454 |
| 29 | 3/2 | yes | 11.497 | 3.1416 | 0.0014 | yes | 19.34 |
| 30 | 3/2 | yes | 4.4548 | 3.1416 | 0.0006 | yes | 24.117 |


# Table C — Stochastic convergence

| Samples N | Mean k | Std k | 95% CI | Pi error % | Canonical recovery % |
|---|---|---|---|---|---|
| 100 | 3.1621 | 0.038229 | [3.1427, 3.1814] | 1.1536 | 100.0 |
| 300 | 3.1422 | 0.034633 | [3.1246, 3.1597] | 0.8782 | 100.0 |
| 1000 | 3.1439 | 0.014053 | [3.1365, 3.1513] | 0.3604 | 93.3 |
| 3000 | 3.1404 | 0.013639 | [3.1329, 3.1478] | 0.3193 | 86.7 |
| 10000 | 3.1397 | 0.0097684 | [3.1348, 3.1447] | 0.2362 | 100.0 |
| 30000 | 3.1407 | 0.0036641 | [3.1389, 3.1426] | 0.0882 | 100.0 |


# Table D — Operator ablation

| Model | Description | Test error | Neurons | Edges | Equation terms | Quadratic recovered? | Canonical equation? |
|---|---|---|---|---|---|---|---|
| A | lin/sig only + prune | 13160 | 5.9 | 12.4 | 17.3 | 0% | 0% |
| B | mixed + prune | 1179.3 | 11.7 | 37.9 | 48.6 | 100% | 13% |
| C | square-linear only + prune | 1075.8 | 17.8 | 114.7 | 131.5 | 100% | 33% |
| D | mixed, no prune | 1979.9 | 24.0 | 276.0 | 299.0 | 100% | 0% |
| E | mixed + prune + Pass E | 20.025 | 3.0 | 2.0 | 4.0 | 100% | 100% |

