# Experiment 04 — stochastic k_N -> pi

Exp04 total runs=90: canonical 87/90. Depths: 100,300,1000,3000,10000,30000.
Exp04 N=100: canonical 15/15, mean k=3.16205985, std=0.0382294, mean |k-pi|/pi=1.1536 %.
Exp04 N=300: canonical 15/15, mean k=3.1421599, std=0.0346329, mean |k-pi|/pi=0.8782 %.
Exp04 N=1000: canonical 14/15, mean k=3.1439107, std=0.0140527, mean |k-pi|/pi=0.3604 %.
Exp04 N=3000: canonical 13/15, mean k=3.14035117, std=0.0136385, mean |k-pi|/pi=0.3193 %.
Exp04 N=10000: canonical 15/15, mean k=3.13973371, std=0.00976839, mean |k-pi|/pi=0.2362 %.
Exp04 N=30000: canonical 15/15, mean k=3.14070693, std=0.00366409, mean |k-pi|/pi=0.0882 %.

# Table C — Stochastic convergence

| Samples N | Mean k | Std k | 95% CI | Pi error % | Canonical recovery % |
|-----------|--------|-------|--------|------------|----------------------|
| 100 | 3.16206 | 0.03823 | [3.14271, 3.18141] | 1.154 | 100.0 |
| 300 | 3.14216 | 0.03463 | [3.12463, 3.15969] | 0.878 | 100.0 |
| 1000 | 3.14391 | 0.01405 | [3.13655, 3.15127] | 0.360 | 93.3 |
| 3000 | 3.14035 | 0.01364 | [3.13294, 3.14777] | 0.319 | 86.7 |
| 10000 | 3.13973 | 0.009768 | [3.13479, 3.14468] | 0.236 | 100.0 |
| 30000 | 3.14071 | 0.003664 | [3.13885, 3.14256] | 0.088 | 100.0 |

## Convergence vs N

Fit log(std_k) ~ a + b log(N). Expected b ≈ -0.5 if uncertainty ~ N^{-1/2}.

Fitted slope b = -0.387663 (ideal -0.5). intercept a = -1.34662.

pi is scored only after freeze; never used as a training/pruning objective.
