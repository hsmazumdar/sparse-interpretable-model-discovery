# Pruning ablation (area)

| Setting | Retrain | n | Canon % | Struct % | Mean L_val | Mean test RMSE | Mean neurons | Mean edges | Mean runtime s |
|---------|---------|---|---------|----------|------------|----------------|--------------|------------|----------------|
| no pruning | — | 12 | 0 | 100 | 3.751E-05 | 2055 | 24.0 | 276.0 | 0.24 |
| prune, 0 retrain cycles | 0 | 12 | 0 | 100 | 3.26E-05 | 1942 | 24.0 | 266.7 | 0.26 |
| prune, 1,000 sample updates | 1000 | 12 | 8 | 100 | 2.505E-05 | 1501 | 14.2 | 71.2 | 0.43 |
| prune, 10,000 sample updates | 10000 | 12 | 67 | 100 | 3.777E-06 | 311.5 | 5.8 | 12.4 | 0.54 |
| prune, 50,000 sample updates | 50000 | 12 | 100 | 100 | 3.368E-09 | 20.72 | 3.0 | 2.0 | 0.78 |
