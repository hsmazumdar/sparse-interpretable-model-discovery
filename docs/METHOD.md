# Method (short)

Authoritative gates: `SUCCESS_CRITERIA.md`. π-policy: `PI_NEVER_OBJECTIVE.md`.  
Default numeric tolerances must not be retuned toward π after seeing results.

## Neuron types used in the operator bank

| funno | Name | Forward |
|-------|------|---------|
| 1 | Linear | \(y=z\), \(z=b+\sum w y_i\) |
| 2 | Logistic sigmoid | \(y=\sigma(z)\) |
| 3 | Centred sigmoid | \(y=\sigma(z)-1/2\) |
| 4 | Quadratic sigmoid (centred square) | \(z=b+\sum w(y_i-0.5)^2\), \(y=\sigma(z)\) |
| 7 | Square-linear (uncentred square) | \(z=b+\sum w y_i^2\), \(y=z\) |

Default area bank (1-22-1): mixed linear, logistic-sigmoid, quadratic-sigmoid and square-linear hidden units; linear output; MSE loss.

Type 7 is the \(r^2\) operator for area. Types 1, 2, 3 and 4 are competing alternatives. The input is **raw scaled radius**, not a hand-made \(r^2\) feature.

There is **no** explicit \(x_1 x_2\) operator (Exp12 limitation).

## Scaling (area / circumference)

Network input \(r/R_{\max}\). Network target \(A/R_{\max}^2\) (or \(C/R_{\max}\)). The exported canonical polynomial is **unscaled back to pixel units** before \(k\) is reported.

## Pruning (transactional)

1. Lock dense validation loss \(L_0\).
2. Snapshot the full graph.
3. Remove a batch of least-\(\lvert w\rvert\) edges; delete dead hidden units; consider 1-in/1-out contraction (exact if linear; approximate if nonlinear).
4. Retrain for a locked number of **sample updates** on the training split (typically 10,000; Exp08 also uses 0 / 1k / 50k). Not 10,000 epochs.
5. Accept iff retrained \(L_{\mathrm{val}} \le f\,L_0\); otherwise restore the snapshot.
6. Repeat until no accepted reduction.

Default connection-prune factor \(f = 1.1\). Exp10 varies \(f \in \{1.0, 1.05, 1.1, 1.25, 1.5, 2.0\}\).

The test split is never used for pruning, stopping, or architecture choice.

## Pass E (train-OLS install)

If the surviving graph is still not a clean polynomial, a train-split OLS form may be installed **only** when validation loss stays inside the locked strip

\[
L_{\mathrm{val}} \le 1.25\, L_{\mathrm{dense}}
\]

(`SUCCESS_CRITERIA.md`). Closeness of \(k\) to \(\pi\) is **not** the accept rule.

## Canonical extraction

If every neuron that reaches the output is linear (funno 1) or square-linear (funno 7), compose a polynomial in scaled radius, then unscale. If a sigmoid remains on the path, the run is labelled **non-canonical** even if predictions are accurate.

Equation export must reproduce live-network predictions within \(10^{-8}\) (Exp13).
