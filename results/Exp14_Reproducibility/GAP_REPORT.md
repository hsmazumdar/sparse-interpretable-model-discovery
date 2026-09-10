# Exp14 — Reproducibility gap report

Generated: 2026-09-09 11:49:48

Required per-run files (from RemainingToDoExperiments Exp14):

- `config.json`
- `seed.txt`
- `train.csv`
- `validation.csv`
- `test.csv`
- `dense_network.txt`
- `pruned_network.txt`
- `exported_equation.txt`
- `metrics.json`
- `pruning_log.csv`
- `predictions.csv`
- `runtime.txt`

---

## Exp01_CircleBoundary

Seed runs: **30**. Mean coverage: **91.7%**.

| Missing file | # seed dirs lacking it |
|--------------|------------------------|
| `predictions.csv` | 30 |

## Exp02_PiArea

Seed runs: **30**. Mean coverage: **100.0%**.
All required files present in every seed dir.

## Exp03_Circumference

Seed runs: **60**. Mean coverage: **100.0%**.
All required files present in every seed dir.

## Exp04_StochasticPi

Seed runs: **90**. Mean coverage: **91.7%**.

| Missing file | # seed dirs lacking it |
|--------------|------------------------|
| `predictions.csv` | 90 |

## Exp05_SyntheticRecovery

Seed runs: **150**. Mean coverage: **83.3%**.

| Missing file | # seed dirs lacking it |
|--------------|------------------------|
| `config.json` | 150 |
| `predictions.csv` | 150 |

## Exp06_Noise

Seed runs: **250**. Mean coverage: **83.3%**.

| Missing file | # seed dirs lacking it |
|--------------|------------------------|
| `config.json` | 250 |
| `predictions.csv` | 250 |

## Exp07_OperatorAblation

Seed runs: **75**. Mean coverage: **56.7%**.

| Missing file | # seed dirs lacking it |
|--------------|------------------------|
| `config.json` | 75 |
| `train.csv` | 75 |
| `validation.csv` | 75 |
| `test.csv` | 75 |
| `predictions.csv` | 75 |
| `pruning_log.csv` | 15 |

## Exp08_PruningAblation

Seed runs: **60**. Mean coverage: **56.7%**.

| Missing file | # seed dirs lacking it |
|--------------|------------------------|
| `config.json` | 60 |
| `train.csv` | 60 |
| `validation.csv` | 60 |
| `test.csv` | 60 |
| `predictions.csv` | 60 |
| `pruning_log.csv` | 12 |

## Exp09_NetworkSize

Seed runs: **100**. Mean coverage: **58.3%**.

| Missing file | # seed dirs lacking it |
|--------------|------------------------|
| `config.json` | 100 |
| `train.csv` | 100 |
| `validation.csv` | 100 |
| `test.csv` | 100 |
| `predictions.csv` | 100 |

## Exp10_PruneTolerance

Seed runs: **60**. Mean coverage: **58.3%**.

| Missing file | # seed dirs lacking it |
|--------------|------------------------|
| `config.json` | 60 |
| `train.csv` | 60 |
| `validation.csv` | 60 |
| `test.csv` | 60 |
| `predictions.csv` | 60 |

## Exp11_Baselines

Seed runs: **40**. Mean coverage: **50.0%**.
Note: shared train/val/test CSVs live under `Exp11_Baselines/seed_XX/`; method runs under `Method_*/seed_XX/` inherit those splits.

| Missing file | # seed dirs lacking it |
|--------------|------------------------|
| `config.json` | 40 |
| `predictions.csv` | 40 |
| `train.csv` | 30 |
| `validation.csv` | 30 |
| `test.csv` | 30 |
| `pruning_log.csv` | 20 |
| `dense_network.txt` | 10 |
| `pruned_network.txt` | 10 |
| `exported_equation.txt` | 10 |
| `metrics.json` | 10 |
| `runtime.txt` | 10 |

## Exp12_CrossProduct

Seed runs: **30**. Mean coverage: **83.3%**.

| Missing file | # seed dirs lacking it |
|--------------|------------------------|
| `config.json` | 30 |
| `predictions.csv` | 30 |

## Exp13_EquationExport

Seed runs: **13**. Mean coverage: **75.0%**.

| Missing file | # seed dirs lacking it |
|--------------|------------------------|
| `metrics.json` | 13 |
| `predictions.csv` | 13 |
| `runtime.txt` | 13 |

