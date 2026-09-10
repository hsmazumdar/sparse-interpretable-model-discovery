# Table — Baselines comparison (area)

| Method | Family | Canon % | Op % | Mean RMSE | Mean terms | Prior FE | Notes |
|--------|--------|---------|------|-----------|------------|----------|-------|
| hsm | nn | 100 | 100 | 21.55 | 4.0 | no | HSM |
| mlp_dense | nn | 0 | 0 | 1.767e+04 | 299.0 | no | no quadratic ops |
| mlp_pruned | nn | 0 | 0 | 1.571e+04 | 9.4 | no | no quadratic ops |
| linreg | sklearn | 0 | 0 | 4.876e+04 | 2.0 | no | cannot form r^2 |
| poly2 | sklearn | 100 | 100 | 19.89 | 3.0 | yes | trivial if FE given |
| lasso_poly2 | sklearn | 100 | 100 | 19.89 | 3.0 | yes | FE + sparsity |
| dtree | sklearn | 0 | 0 | 6264 | 63.7 | no | no closed form |
| rforest | sklearn | 0 | 0 | 2920 | 100.0 | no | no closed form |
| gboost | sklearn | 0 | 0 | 4268 | 100.0 | no | no closed form |
| mlp_sk | sklearn | 0 | 0 | 7.095e+04 | 67.0 | no | black-box dense |
