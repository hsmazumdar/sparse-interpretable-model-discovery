# Experiment 23 — Kepler's third law

NASA Planetary Fact Sheet distances and orbital periods, Moon excluded,
scaled by this table's Earth values (149.6e6 km, 365.2 days).
Empirical log-log exponent on all 9 bodies: 1.49852 (law is 1.5).
Term recovery: the model's predictions on a fixed log-spaced grid satisfy
|exponent-1.5|/1.5 <= 0.05 and |const-1| <= 0.05 for P = const * a^exponent.
SINDy's library is {1, a, a^2, a^3}, which does not contain a^(3/2).

| Method | n | Power recovery | Mean |exp-1.5|/1.5 | Mean |c-1| | Mean holdout RMSE (years) |
|--------|---|----------------|---------------------|------------|----------------------------|
| loglog_ols | 20 | 20/20 | 0.001208 | 0.0003031 | 0.7217 |
| sindy_poly3 | 20 | 0/20 | 0.06923 | 0.2315 | 6.476 |
| gplearn | 20 | 15/20 | 0.01116 | 0.03898 | 0.3747 |
| hsm | 20 | 0/20 | 1 | 27.91 | 12.1 |

Full-sample log-log equation: P=0.999819*a^1.49852
Example gplearn expression (seed 1): mul(sqrt(X0), X0)
HSM was trained on training-fold standardized (a, 0) and scored in years. It has no a^(3/2) operator.

