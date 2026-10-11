# Same-split comparison on Exp12

Frozen Exp12 train/validation/test files. HSM is the published Exp12 graph, not a rerun.
SINDy library: all degree-3 monomials in x1, x2. gplearn: add, sub, mul, sin.

| Problem | Method | n | Term recovery | Mean coeff rel | Mean test RMSE | Explicit product |
|---------|--------|---|---------------|----------------|----------------|------------------|
| diag | hsm_exp12 | 10 | 10/10 | 7.441e-15 | 2.219e-15 | 0/10 |
| diag | sindy_poly3 | 10 | 10/10 | 1.008e-15 | 1.864e-15 | 0/10 |
| diag | gplearn_poly | 10 | 3/10 | 0.4452 | 0.1658 | 1/10 |
| diag | gplearn | 10 | 1/10 | 0.3771 | 0.1232 | 0/10 |
| cross | hsm_exp12 | 10 | 0/10 | 0.001953 | 0.003246 | 0/10 |
| cross | sindy_poly3 | 10 | 10/10 | 2.831e-16 | 1.44e-15 | 10/10 |
| cross | gplearn_poly | 10 | 6/10 | 0.2439 | 0.179 | 10/10 |
| cross | gplearn | 10 | 7/10 | 0.09127 | 0.08232 | 10/10 |
| mixed | hsm_exp12 | 10 | 0/10 | 0.002424 | 0.002439 | 0/10 |
| mixed | sindy_poly3 | 10 | 10/10 | 9.162e-16 | 1.682e-15 | 10/10 |
| mixed | gplearn_poly | 10 | 0/10 | 0.4365 | 0.1764 | 10/10 |
| mixed | gplearn | 10 | 0/10 | 0.2375 | 0.1043 | 10/10 |

## Reading

Axis-aligned squares (`diag`): HSM 10/10, SINDy 10/10, gplearn with sin 1/10, gplearn polynomials only 3/10.
Pure product (`cross`): HSM export 0/10, SINDy 10/10, gplearn with sin 7/10, gplearn polynomials only 6/10.
Mixed squares plus product (`mixed`): HSM export 0/10, SINDy 10/10, gplearn with sin 0/10, gplearn polynomials only 0/10.
HSM term recovery is the Exp12 structure flag. On cross and mixed that flag is explicit x1*x2 in the export.
Coefficient error remains the quadratic readout of the live network, so an approximation is still visible.
gplearn is population 500, 20 generations, parsimony 0.001. `gplearn` may use sin; `gplearn_poly` is add, sub, and mul only.

