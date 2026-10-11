# Experiment 22 — four known laws

Term recovery: true coefficients within 25% relative, other readout coefficients
at most 0.05 absolute, and (HSM, gplearn) residual within 5% of the clean scale.
The readout is the full degree-3 polynomial. sin(x1) is added only when the
gplearn expression actually calls sin, because sin(x) and x, x^3 are not unique on [-1, 1].
The HSM bank has no sine, so its readout stays polynomial. A product or sine counts
as recovered only when that operator is in the exported equation.
SINDy is STLSQ on all 10 degree-3 monomials. The library is not the true support.

| Law | Noise | Method | n | Term recovery | Mean coeff rel | Mean RMSE clean | Explicit product | Explicit sin |
|-----|-------|--------|---|---------------|----------------|-----------------|------------------|--------------|
| L1 | 0 | hsm | 20 | 20/20 | 1.932e-15 | 2.189e-15 | 0/20 | 0/20 |
| L1 | 0 | sindy_poly3 | 20 | 20/20 | 5.329e-16 | 3.561e-16 | 0/20 | 0/20 |
| L1 | 0 | gplearn_poly | 20 | 19/20 | 0.04141 | 0.02067 | 0/20 | 0/20 |
| L1 | 0 | gplearn | 20 | 17/20 | 0.05623 | 0.03272 | 0/20 | 3/20 |
| L1 | 0.02 | hsm | 20 | 20/20 | 0.002027 | 0.001221 | 0/20 | 0/20 |
| L1 | 0.02 | sindy_poly3 | 20 | 20/20 | 0.001621 | 0.001173 | 0/20 | 0/20 |
| L1 | 0.02 | gplearn_poly | 20 | 19/20 | 0.05266 | 0.02249 | 0/20 | 0/20 |
| L1 | 0.02 | gplearn | 20 | 16/20 | 0.06527 | 0.04385 | 0/20 | 4/20 |
| L1 | 0.05 | hsm | 20 | 20/20 | 0.005117 | 0.002805 | 0/20 | 0/20 |
| L1 | 0.05 | sindy_poly3 | 20 | 20/20 | 0.004583 | 0.002793 | 0/20 | 0/20 |
| L1 | 0.05 | gplearn_poly | 20 | 19/20 | 0.03888 | 0.01681 | 0/20 | 0/20 |
| L1 | 0.05 | gplearn | 20 | 18/20 | 0.05997 | 0.03409 | 0/20 | 2/20 |
| L2 | 0 | hsm | 20 | 20/20 | 0.001869 | 0.001804 | 0/20 | 0/20 |
| L2 | 0 | sindy_poly3 | 20 | 20/20 | 8.57e-16 | 8.55e-16 | 0/20 | 0/20 |
| L2 | 0 | gplearn_poly | 20 | 2/20 | 0.3368 | 0.2109 | 0/20 | 0/20 |
| L2 | 0 | gplearn | 20 | 0/20 | 1.15 | 0.1839 | 2/20 | 20/20 |
| L2 | 0.02 | hsm | 20 | 18/20 | 0.008422 | 0.006133 | 0/20 | 0/20 |
| L2 | 0.02 | sindy_poly3 | 20 | 20/20 | 0.004264 | 0.002386 | 0/20 | 0/20 |
| L2 | 0.02 | gplearn_poly | 20 | 8/20 | 0.3588 | 0.2398 | 0/20 | 0/20 |
| L2 | 0.02 | gplearn | 20 | 1/20 | 0.9512 | 0.1939 | 2/20 | 20/20 |
| L2 | 0.05 | hsm | 20 | 18/20 | 0.01791 | 0.01452 | 0/20 | 0/20 |
| L2 | 0.05 | sindy_poly3 | 20 | 20/20 | 0.01248 | 0.006876 | 0/20 | 0/20 |
| L2 | 0.05 | gplearn_poly | 20 | 0/20 | 0.4114 | 0.2503 | 0/20 | 0/20 |
| L2 | 0.05 | gplearn | 20 | 0/20 | 0.7461 | 0.1996 | 3/20 | 19/20 |
| L3 | 0 | hsm | 20 | 0/20 | 0.0007639 | 0.002436 | 0/20 | 0/20 |
| L3 | 0 | sindy_poly3 | 20 | 20/20 | 7.901e-16 | 1.37e-15 | 20/20 | 0/20 |
| L3 | 0 | gplearn_poly | 20 | 18/20 | 0.04669 | 0.06642 | 20/20 | 0/20 |
| L3 | 0 | gplearn | 20 | 7/20 | 0.1592 | 0.1954 | 20/20 | 11/20 |
| L3 | 0.02 | hsm | 20 | 0/20 | 0.00878 | 0.01508 | 0/20 | 0/20 |
| L3 | 0.02 | sindy_poly3 | 20 | 20/20 | 0.0009761 | 0.002163 | 20/20 | 0/20 |
| L3 | 0.02 | gplearn_poly | 20 | 15/20 | 0.08552 | 0.1332 | 20/20 | 0/20 |
| L3 | 0.02 | gplearn | 20 | 10/20 | 0.1243 | 0.1453 | 20/20 | 12/20 |
| L3 | 0.05 | hsm | 20 | 0/20 | 0.01642 | 0.02668 | 0/20 | 0/20 |
| L3 | 0.05 | sindy_poly3 | 20 | 20/20 | 0.001964 | 0.002307 | 20/20 | 0/20 |
| L3 | 0.05 | gplearn_poly | 20 | 16/20 | 0.06833 | 0.09136 | 20/20 | 0/20 |
| L3 | 0.05 | gplearn | 20 | 8/20 | 0.1403 | 0.1686 | 19/20 | 15/20 |
| L4 | 0 | hsm | 20 | 0/20 | 0.5006 | 0.002798 | 0/20 | 0/20 |
| L4 | 0 | sindy_poly3 | 20 | 0/20 | 0.5 | 0.0003052 | 0/20 | 0/20 |
| L4 | 0 | gplearn_poly | 20 | 0/20 | 0.5 | 0.05394 | 0/20 | 0/20 |
| L4 | 0 | gplearn | 20 | 20/20 | 0.0006039 | 0.0006143 | 0/20 | 20/20 |
| L4 | 0.02 | hsm | 20 | 0/20 | 0.5024 | 0.007813 | 0/20 | 0/20 |
| L4 | 0.02 | sindy_poly3 | 20 | 0/20 | 0.5009 | 0.001387 | 0/20 | 0/20 |
| L4 | 0.02 | gplearn_poly | 20 | 0/20 | 0.5 | 0.05461 | 0/20 | 0/20 |
| L4 | 0.02 | gplearn | 20 | 19/20 | 0.0256 | 0.01616 | 0/20 | 20/20 |
| L4 | 0.05 | hsm | 20 | 0/20 | 0.506 | 0.02363 | 0/20 | 0/20 |
| L4 | 0.05 | sindy_poly3 | 20 | 0/20 | 0.5017 | 0.00414 | 0/20 | 0/20 |
| L4 | 0.05 | gplearn_poly | 20 | 0/20 | 0.5 | 0.05691 | 0/20 | 0/20 |
| L4 | 0.05 | gplearn | 20 | 19/20 | 0.025 | 0.003414 | 0/20 | 19/20 |

## Reading

Axis-aligned squares (`L1`), noise 0: HSM 20/20, SINDy 20/20, gplearn polynomials 19/20, gplearn with sin 17/20.
Shifted quadratic (`L2`), noise 0: HSM 20/20, SINDy 20/20, gplearn polynomials 2/20, gplearn with sin 0/20.
Product (`L3`), noise 0: HSM 0/20, SINDy 20/20, gplearn polynomials 18/20, gplearn with sin 7/20.
Sine plus square (`L4`), noise 0: HSM 0/20, SINDy 0/20, gplearn polynomials 0/20, gplearn with sin 20/20.
HSM export contains an explicit product on L3 noise 0: 0/20.
HSM export contains a sine on L4 noise 0: 0/20.
L3 noise 0 HSM predictions were non-finite on 11/20 seeds. Those seeds stay in the denominator.
Rates at noise 0.02 and 0.05 are in the table. No seed is dropped.

