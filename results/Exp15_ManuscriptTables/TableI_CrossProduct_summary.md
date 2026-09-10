# Experiment 12 — cross-product limitation

Type-4/7 neurons implement **diagonal** quadratics only (no x1*x2 product). This experiment records that boundary honestly.

Exp12 total runs=30.
# Cross-product limitation

| Problem | Expect exact | n | Struct % | Explicit xy % | Limitation OK % | Mean RMSE | Oracle diag R² | Oracle full R² |
|---------|--------------|---|----------|---------------|-----------------|-----------|----------------|----------------|
| diag | yes | 10 | 100 | 0 | 100 | 2.219E-15 | 1.000 | 1.000 |
| cross | no | 10 | 0 | 0 | 100 | 0.003246 | 0.026 | 1.000 |
| mixed | no | 10 | 0 | 0 | 100 | 0.002439 | 0.372 | 1.000 |

# Cross-product limitation

| Problem | Expect exact | n | Struct % | Explicit xy % | Limitation OK % | Mean RMSE | Oracle diag R² | Oracle full R² |
|---------|--------------|---|----------|---------------|-----------------|-----------|----------------|----------------|
| diag | yes | 10 | 100 | 0 | 100 | 2.219E-15 | 1.000 | 1.000 |
| cross | no | 10 | 0 | 0 | 100 | 0.003246 | 0.026 | 1.000 |
| mixed | no | 10 | 0 | 0 | 100 | 0.002439 | 0.372 | 1.000 |
