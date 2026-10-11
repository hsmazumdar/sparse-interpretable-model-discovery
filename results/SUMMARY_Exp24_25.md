# Summary of Experiments 24 and 25

All counts are out of 20 seeds (Kepler: 20 holdouts). No seed was dropped. Splits are the Exp22 and Exp23 files. Term-recovery gates are the Exp22/Exp23 gates. Full per-cell numbers, including mean coefficient error and clean RMSE, are in `Exp24_SINDyLibraryMismatch/INDEX.md` and `Exp25_OperatorExtension/INDEX.md`.

## Experiment 24: SINDy with libraries chosen without the true terms

Cell = term recovery (mean clean test RMSE). HSM rows are copied from Exp22.

| Law | Noise | HSM | SINDy deg1 | SINDy deg2, no cross | SINDy deg2, no squares | SINDy generic | SINDy deg3 |
|-----|-------|-----|------------|----------------------|------------------------|---------------|------------|
| L1 | 0 | 20/20 (2e-15) | 0/20 (0.41) | 20/20 (4e-16) | 0/20 (0.41) | 0/20 (0.0008) | 20/20 (4e-16) |
| L1 | 0.02 | 20/20 (0.0012) | 0/20 (0.41) | 20/20 (0.0011) | 0/20 (0.41) | 0/20 (0.0018) | 20/20 (0.0012) |
| L1 | 0.05 | 20/20 (0.0028) | 0/20 (0.43) | 20/20 (0.0028) | 0/20 (0.43) | 0/20 (0.0038) | 20/20 (0.0028) |
| L2 | 0 | 20/20 (0.0018) | 0/20 (0.41) | 20/20 (9e-16) | 0/20 (0.41) | 0/20 (0.0008) | 20/20 (9e-16) |
| L2 | 0.02 | 18/20 (0.0061) | 0/20 (0.42) | 20/20 (0.0021) | 0/20 (0.42) | 0/20 (0.0027) | 20/20 (0.0024) |
| L2 | 0.05 | 18/20 (0.0145) | 0/20 (0.42) | 20/20 (0.0063) | 0/20 (0.42) | 0/20 (0.0083) | 20/20 (0.0069) |
| L3 | 0 | 0/20 (0.0024*) | 0/20 (1.38) | 0/20 (0.64) | 0/20 (1.21) | 0/20 (0.65) | 20/20 (1e-15) |
| L3 | 0.02 | 0/20 (0.015*) | 0/20 (1.37) | 0/20 (0.68) | 0/20 (1.25) | 0/20 (0.68) | 20/20 (0.0022) |
| L3 | 0.05 | 0/20 (0.027*) | 0/20 (1.35) | 0/20 (0.64) | 0/20 (1.19) | 0/20 (0.65) | 20/20 (0.0023) |

\* HSM L3 RMSE is over finite seeds only; 11, 9, and 11 of 20 seeds are non-finite. The 2 HSM L2 misses at each nonzero noise level are non-finite seeds.

The generic library is {1, x1, x2, sin, cos, exp(-x^2) of each input}. L2 recoveries, by HSM and by SINDy, are of the expanded polynomial 0.2225 - 0.8 x1 + 0.5 x2 + x1^2 + x2^2. No library and no HSM export contains the shifted form (x1-0.4)^2 + (x2+0.25)^2 as a separate term.

## Experiment 25: product and power operators in the HSM bank

Cell = term recovery; non-finite seeds (kept in the denominator). "original" rows are copied from Exp22 and Exp23.

| Law | Noise | original | + product | + power | + both |
|-----|-------|----------|-----------|---------|--------|
| L1 | 0 | 20/20; 0 | 20/20; 0 | 20/20; 0 | 20/20; 0 |
| L1 | 0.02 | 20/20; 0 | 20/20; 0 | 20/20; 0 | 20/20; 0 |
| L1 | 0.05 | 20/20; 0 | 19/20; 1 | 20/20; 0 | 20/20; 0 |
| L2 | 0 | 20/20; 0 | 20/20; 0 | 20/20; 0 | 20/20; 0 |
| L2 | 0.02 | 18/20; 2 | 19/20; 1 | 19/20; 1 | 19/20; 1 |
| L2 | 0.05 | 18/20; 2 | 17/20; 3 | 18/20; 2 | 19/20; 0 |
| L3 | 0 | 0/20; 11 | 15/20; 5 (clip) | 0/20; 9 (clip) | 16/20; 4 |
| L3 | 0.02 | 0/20; 9 | 14/20; 6 (clip) | 0/20; 15 (clip) | 15/20; 5 |
| L3 | 0.05 | 0/20; 11 | 15/20; 5 | 0/20; 7 (clip) | 16/20; 4 |
| L4 | 0 | 0/20; 0 | 0/20; 0 | 0/20; 0 | 0/20; 0 |
| L4 | 0.02 | 0/20; 0 | 0/20; 0 | 0/20; 0 | 0/20; 0 |
| L4 | 0.05 | 0/20; 0 | 0/20; 0 | 0/20; 0 | 0/20; 0 |
| Kepler | holdout | 0/20; 5 | 0/20; 1 | 2/20; 0 | 2/20; 0 |

- x1*x2 appears in the exported text on 20/20 L3 seeds for the product and both banks. It also appears where the law has no product: L2 noise 0 in 18/20 (product) and 17/20 (both), L4 noise 0 in 17/20 (product), and Kepler in 14/20 (product).
- a^k appears in Kepler exports on 16/20 (power) and 19/20 (both) seeds, but the exponent passes the gate (|k-1.5|/1.5 <= 0.05 and |c-1| <= 0.05) on only 2/20. Mean holdout RMSE in years: original 12.1 (15 finite seeds), product 7.6, power 11.7, both 13.8.
- "clip" marks cells that had more than 5 non-finite seeds out of 20 and were rerun once with a maximum step of 0.5. The table reports the rerun. The power L3 noise 0.02 cell still had 15/20 non-finite after clipping.
- Regression check against Exp22: L1 and L2 hold in 16 of 18 bank × noise cells. Two cells lose one seed each: product L1 noise 0.05 (19/20 vs 20/20) and product L2 noise 0.05 (17/20 vs 18/20). Both losses are non-finite seeds.
- Export versus live network: all 710 finite seeds match the saved network predictions on the grid (max abs difference 1.8e-12). The other 70 are the non-finite seeds counted above.

Reference numbers from Exp22/Exp23 (same splits): SINDy deg3 L3 20/20 at every noise level; gplearn (add, sub, mul) L3 18, 15, 16/20; gplearn with sin L4 20, 19, 19/20; Kepler gplearn with sqrt 15/20, log-log OLS 20/20, SINDy {1, a, a^2, a^3} 0/20.

## Plain reading

1. HSM beats SINDy only when the SINDy library lacks the squares: on L1 and L2, HSM recovers 18-20/20, while degree-1, degree-2-without-squares, and generic libraries recover 0/20. The generic library still fits L1/L2 to RMSE 0.0008-0.008 without the true terms.
2. HSM ties SINDy when the library contains the squares (degree-2 without the cross term, or degree-3: 20/20 on L1 and L2). Every L2 recovery from either method is the expanded polynomial, never the shifted form.
3. On the product law L3, the product neuron raises HSM from 0/20 to 14-16/20 with x1*x2 named in the export. That is close to gplearn (add, sub, mul) at 15-18/20, but below SINDy degree-3 at 20/20, and 4-6/20 HSM seeds still diverge.
4. On sin(x1)+x2^2, no HSM bank recovers the sine (0/20). gplearn with sin wins (19-20/20). SINDy degree-3 also gets 0/20.
5. On Kepler, the power neuron reaches 2/20 (power and both banks), compared with 0/20 for the original bank, 15/20 for gplearn, and 20/20 for log-log OLS. HSM loses on this dataset.
