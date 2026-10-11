# Experiment 25 — product and power operators

Original rows are copied from Exp22 (L1-L4) and Exp23 (Kepler). They were not retrained.
Term recovery for L1-L4 uses the Exp22 gates. A product term counts only if the export contains nA*nB.
Sine counts only if the export contains sin. Kepler uses the Exp23 exponent and constant gates.
Diverged seeds stay in the denominator. Mean RMSE skips non-finite values; the diverged column is the count.

| Bank | Law | Noise | n | Term recovery | Explicit product | Explicit power | Diverged | Mean coeff rel | Mean RMSE | Clip |
|------|-----|-------|---|---------------|------------------|----------------|----------|----------------|-----------|------|
| original | L1 | 0 | 20 | 20/20 | 0/20 | 0/20 | 0/20 | 1.93179e-15 | 2.18944e-15 | 0 |
| original | L1 | 0.02 | 20 | 20/20 | 0/20 | 0/20 | 0/20 | 0.00202741 | 0.00122112 | 0 |
| original | L1 | 0.05 | 20 | 20/20 | 0/20 | 0/20 | 0/20 | 0.00511717 | 0.00280509 | 0 |
| original | L2 | 0 | 20 | 20/20 | 0/20 | 0/20 | 0/20 | 0.00186898 | 0.00180419 | 0 |
| original | L2 | 0.02 | 20 | 18/20 | 0/20 | 0/20 | 2/20 | 0.00842174 | 0.00613262 | 0 |
| original | L2 | 0.05 | 20 | 18/20 | 0/20 | 0/20 | 2/20 | 0.0179103 | 0.0145231 | 0 |
| original | L3 | 0 | 20 | 0/20 | 0/20 | 0/20 | 11/20 | 0.000763912 | 0.00243557 | 0 |
| original | L3 | 0.02 | 20 | 0/20 | 0/20 | 0/20 | 9/20 | 0.00877992 | 0.0150794 | 0 |
| original | L3 | 0.05 | 20 | 0/20 | 0/20 | 0/20 | 11/20 | 0.01642 | 0.0266801 | 0 |
| original | L4 | 0 | 20 | 0/20 | 0/20 | 0/20 | 0/20 | 0.500621 | 0.00279823 | 0 |
| original | L4 | 0.02 | 20 | 0/20 | 0/20 | 0/20 | 0/20 | 0.502408 | 0.00781335 | 0 |
| original | L4 | 0.05 | 20 | 0/20 | 0/20 | 0/20 | 0/20 | 0.506025 | 0.0236297 | 0 |
| original | Kepler | holdout | 20 | 0/20 | 0/20 | 0/20 | 5/20 | 1 | 12.1036 | 0 |
| product | L1 | 0 | 20 | 20/20 | 0/20 | 0/20 | 0/20 | 2.32869e-15 | 2.10549e-15 | 0 |
| product | L1 | 0.02 | 20 | 20/20 | 0/20 | 0/20 | 0/20 | 0.00202741 | 0.00122112 | 0 |
| product | L1 | 0.05 | 20 | 19/20 | 1/20 | 0/20 | 1/20 | 0.00512525 | 0.00281454 | 0 |
| product | L2 | 0 | 20 | 20/20 | 18/20 | 0/20 | 0/20 | 0.00261423 | 0.00201832 | 0 |
| product | L2 | 0.02 | 20 | 19/20 | 6/20 | 0/20 | 1/20 | 0.00908275 | 0.00744481 | 0 |
| product | L2 | 0.05 | 20 | 17/20 | 7/20 | 0/20 | 3/20 | 0.0181764 | 0.0136398 | 0 |
| product | L3 | 0 | 20 | 15/20 | 20/20 | 0/20 | 5/20 | 0.000838888 | 0.00257333 | 1 |
| product | L3 | 0.02 | 20 | 14/20 | 20/20 | 0/20 | 6/20 | 0.00298589 | 0.00755327 | 1 |
| product | L3 | 0.05 | 20 | 15/20 | 20/20 | 0/20 | 5/20 | 0.00764033 | 0.0164181 | 0 |
| product | L4 | 0 | 20 | 0/20 | 17/20 | 0/20 | 0/20 | 0.500749 | 0.0025931 | 0 |
| product | L4 | 0.02 | 20 | 0/20 | 5/20 | 0/20 | 0/20 | 0.502119 | 0.00808485 | 0 |
| product | L4 | 0.05 | 20 | 0/20 | 3/20 | 0/20 | 0/20 | 0.506696 | 0.0239441 | 0 |
| product | Kepler | holdout | 20 | 0/20 | 14/20 | 0/20 | 1/20 |  | 7.58476 | 0 |
| power | L1 | 0 | 20 | 20/20 | 0/20 | 0/20 | 0/20 | 2.32869e-15 | 2.10549e-15 | 0 |
| power | L1 | 0.02 | 20 | 20/20 | 0/20 | 0/20 | 0/20 | 0.00202741 | 0.00122112 | 0 |
| power | L1 | 0.05 | 20 | 20/20 | 0/20 | 0/20 | 0/20 | 0.00511717 | 0.00280509 | 0 |
| power | L2 | 0 | 20 | 20/20 | 0/20 | 20/20 | 0/20 | 0.00196412 | 0.0018378 | 0 |
| power | L2 | 0.02 | 20 | 19/20 | 0/20 | 18/20 | 1/20 | 0.00796446 | 0.006756 | 0 |
| power | L2 | 0.05 | 20 | 18/20 | 0/20 | 18/20 | 2/20 | 0.0178895 | 0.0143214 | 0 |
| power | L3 | 0 | 20 | 0/20 | 0/20 | 19/20 | 9/20 | 0.00105484 | 0.00331124 | 1 |
| power | L3 | 0.02 | 20 | 0/20 | 0/20 | 15/20 | 15/20 | 0.00419028 | 0.0103951 | 1 |
| power | L3 | 0.05 | 20 | 0/20 | 0/20 | 11/20 | 7/20 | 0.0155854 | 0.0245747 | 1 |
| power | L4 | 0 | 20 | 0/20 | 0/20 | 19/20 | 0/20 | 0.500796 | 0.00448898 | 0 |
| power | L4 | 0.02 | 20 | 0/20 | 0/20 | 17/20 | 0/20 | 0.50238 | 0.0100976 | 0 |
| power | L4 | 0.05 | 20 | 0/20 | 0/20 | 14/20 | 0/20 | 0.505186 | 0.0176597 | 0 |
| power | Kepler | holdout | 20 | 2/20 | 0/20 | 16/20 | 0/20 | 0.255498 | 11.6864 | 0 |
| both | L1 | 0 | 20 | 20/20 | 0/20 | 0/20 | 0/20 | 2.32869e-15 | 2.10549e-15 | 0 |
| both | L1 | 0.02 | 20 | 20/20 | 0/20 | 0/20 | 0/20 | 0.00202741 | 0.00122112 | 0 |
| both | L1 | 0.05 | 20 | 20/20 | 0/20 | 0/20 | 0/20 | 0.00511717 | 0.00280509 | 0 |
| both | L2 | 0 | 20 | 20/20 | 17/20 | 19/20 | 0/20 | 0.002467 | 0.00209806 | 0 |
| both | L2 | 0.02 | 20 | 19/20 | 5/20 | 16/20 | 1/20 | 0.00938966 | 0.00752883 | 0 |
| both | L2 | 0.05 | 20 | 19/20 | 5/20 | 18/20 | 0/20 | 0.0230046 | 0.0176147 | 0 |
| both | L3 | 0 | 20 | 16/20 | 20/20 | 18/20 | 4/20 | 0.000542694 | 0.00233849 | 0 |
| both | L3 | 0.02 | 20 | 15/20 | 20/20 | 9/20 | 5/20 | 0.00313675 | 0.00709018 | 0 |
| both | L3 | 0.05 | 20 | 16/20 | 20/20 | 4/20 | 4/20 | 0.00904 | 0.0163989 | 0 |
| both | L4 | 0 | 20 | 0/20 | 8/20 | 20/20 | 0/20 | 0.501098 | 0.00581701 | 0 |
| both | L4 | 0.02 | 20 | 0/20 | 5/20 | 17/20 | 0/20 | 0.502005 | 0.0106293 | 0 |
| both | L4 | 0.05 | 20 | 0/20 | 2/20 | 17/20 | 0/20 | 0.505801 | 0.0178146 | 0 |
| both | Kepler | holdout | 20 | 2/20 | 6/20 | 19/20 | 0/20 | 0.248586 | 13.8153 | 0 |

L1 and L2 recovery versus the original Exp22 rows (same seeds):

- L1 noise 0: original 20/20; product 20/20 holds; power 20/20 holds; both 20/20 holds
- L1 noise 0.02: original 20/20; product 20/20 holds; power 20/20 holds; both 20/20 holds
- L1 noise 0.05: original 20/20; product 19/20 drop; power 20/20 holds; both 20/20 holds
- L2 noise 0: original 20/20; product 20/20 holds; power 20/20 holds; both 20/20 holds
- L2 noise 0.02: original 18/20; product 19/20 holds; power 19/20 holds; both 19/20 holds
- L2 noise 0.05: original 18/20; product 17/20 drop; power 18/20 holds; both 19/20 holds

Export check (exp25_export_check.py, export_check.csv): each exported_equation.txt was evaluated as text on the saved grid and compared with pred_grid.csv. 710 of 780 seeds are finite and all 710 match (max abs difference 1.82e-12). The other 70 are non-finite networks and stay in the denominators above.

Cells with more than 5 non-finite seeds out of 20 were rerun once with a maximum step of 0.5 (CLIP.txt in that cell): product L3 noise 0 and 0.02; power L3 noise 0, 0.02, 0.05. The table reports the clipped reruns for those cells.
