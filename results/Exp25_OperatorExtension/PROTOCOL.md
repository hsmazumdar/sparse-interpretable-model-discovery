# Experiment 25 protocol

Operator bank extensions, trained on the Exp22 and Exp23 splits already on disk.
Seeds, noise draws, and holdouts are not regenerated. No seed is dropped.
Dense epochs 150, prune factor 1.1, retrain 5000 sample-updates, rollback outside the validation strip.
Pass E may install a diagonal square only on L1, and only if validation loss stays within 1.25 times the dense baseline.

New units, taken from the trailing square-linear slots so the hidden count stays 22 (laws) or 12 (Kepler):
- product (funno 8): p = y_a * y_b of the two live incoming signals. Incoming weights are fixed at 1 on the two inputs and are not trained. The downstream weight is the coefficient. Export text is nA*nB.
- power (funno 9): y = sign(a)*|a|^k with k the trained incoming weight. The exponent k*log|a| is clipped to [-8, 8] so the exponential stays finite. Export text contains a^k.

Banks: product (2 product units), power (2 power units), both (2+2). The original bank is not retrained; its rows are copied from Exp22 and Exp23.

Kepler product bank uses the Exp23 train-fold z-score of a and P, and predictions are mapped back to years.
Kepler power and both banks use fixed scales chosen before fitting: network input a/40, training target P/100, powerInputGain=40, powerOutputDiv=100, so the power unit emits a^k/100. Predictions are multiplied by 100 to return years. The edge from each power unit to the output starts at 1. These scales are not fit to the exponent.

Gradient clipping is off. If a law-noise cell or the Kepler cell has more than 5 non-finite seeds out of 20, that cell is rerun with a maximum step of 0.5 and CLIP.txt records that the clip was used.

Term recovery uses the Exp22 rules (25% relative, 0.05 absolute, 5% residual) and, for a product law, requires nA*nB in the export. Kepler uses the Exp23 rule: on the fixed log-spaced grid, |exponent-1.5|/1.5 <= 0.05 and |const-1| <= 0.05, predictions positive.
A non-finite network stays in the denominator.
