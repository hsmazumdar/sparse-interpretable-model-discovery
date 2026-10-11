Shared scoring for the symbolic-comparison experiments.

Locked before the runs (also copied into each Results/*/PROTOCOL.md):

- Relative coefficient tolerance: 0.25 (same gate as Exp12).
- Absent-term absolute tolerance: 0.05.
- A network or gplearn function is in the readout span when the residual of a
  least-squares fit on poly-3 + sin(x1) is at most 5% of the clean-target
  standard deviation on the same points.
- Term recovery requires the span check (SINDy is in-span by construction)
  and a matching support: every true coefficient within 25% relative, every
  other coefficient at most 0.05 absolute.
- SINDy library is the full degree-3 polynomial in two variables (10 terms),
  not the true support, and it has no sine column. Thresholds are chosen on
  the validation split only.
- gplearn budget: population 500, 20 generations, metric mse, functions
  add/sub/mul/sin. PySR is not used: Julia is not installed.
