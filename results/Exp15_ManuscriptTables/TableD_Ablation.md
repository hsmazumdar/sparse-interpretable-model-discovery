# Table D — Operator ablation

| Model | Description | Test error | Neurons | Edges | Equation terms | Quadratic recovered? | Canonical equation? |
|---|---|---|---|---|---|---|---|
| A | lin/sig only + prune | 13160 | 5.9 | 12.4 | 17.3 | 0% | 0% |
| B | mixed + prune | 1179.3 | 11.7 | 37.9 | 48.6 | 100% | 13% |
| C | square-linear only + prune | 1075.8 | 17.8 | 114.7 | 131.5 | 100% | 33% |
| D | mixed, no prune | 1979.9 | 24.0 | 276.0 | 299.0 | 100% | 0% |
| E | mixed + prune + Pass E | 20.025 | 3.0 | 2.0 | 4.0 | 100% | 100% |
