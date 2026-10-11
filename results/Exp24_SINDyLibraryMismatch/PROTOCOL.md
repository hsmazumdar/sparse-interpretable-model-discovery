# Experiment 24 protocol

SINDy (STLSQ) on the Exp22 splits for L1, L2, and L3. Noise levels 0, 0.02, 0.05. Seeds 1..20. No seed is dropped. HSM is not retrained: the hsm rows are copied from Exp22.

Threshold grid, same as Exp22: fractions (1e-8, 1e-4, 1e-3, 1e-2, 0.05, 0.1, 0.25) times std(y_train), chosen on the validation split by RMSE then by fewer nonzeros.

Libraries, fixed before the fits:

- deg1: {1, x1, x2}
- deg2_no_cross: {1, x1, x2, x1^2, x2^2}
- deg2_no_squares: {1, x1, x2, x1*x2}
- generic: {1, x1, x2, sin(x1), sin(x2), cos(x1), cos(x2), exp(-x1^2), exp(-x2^2)}
- deg3: the Exp22 degree-3 library, ten monomials, no sine

Term recovery uses the Exp22 gates: every true coefficient within 25% relative, every other coefficient in that library at most 0.05 absolute. A true term that the library does not contain counts as missing.

L2 is scored on the expanded polynomial 0.2225 - 0.8 x1 + 0.5 x2 + x1^2 + x2^2. None of the libraries has a shifted-square column (x-c)^2, so a recovery is the expanded polynomial, not the parenthesized shifted form.

Clean RMSE is against the noise-free formula on the test inputs.
