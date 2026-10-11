# Experiment 22 protocol

Laws (inputs uniform on [-1,1]^2, independent of the split seed):

- L1 axis-aligned squares: y = x1^2 + x2^2
- L2 shifted quadratic: y = (x1 - 0.4)^2 + (x2 + 0.25)^2
- L3 product: y = x1^2 + 2 x1 x2 + 4 x2^2
- L4 trigonometric: y = sin(x1) + x2^2

Noise is additive and absolute, std in {0, 0.02, 0.05}, using the Exp05 sum-of-12-uniforms generator. The same 800-point draw is split 70/15/15 by seed, matching DatasetSplit.

The graph is the Exp12 operator bank (linear, sigmoid, centred quadratic-sigmoid, uncentred square-linear). Dense training 150 epochs, prune factor 1.1, 5000 retrain sample-updates, rollback outside the validation strip. Pass E may replace the graph by a diagonal square fit only on L1, and only if validation loss stays within 1.25 times the dense baseline. It does not install a product or a sine.

Scoring is in exp22_score.py. Term recovery requires every true coefficient within 25% relative and every other readout coefficient at most 0.05 absolute, plus a residual within 5% of the clean-target standard deviation. The readout is the full degree-3 polynomial. A sin(x1) column is added only when a gplearn expression actually calls sin, because sin(x), x, and x^3 are not separately identifiable on [-1, 1]. The network bank has no sine, so its readout stays polynomial. A product or a sine counts as recovered for the network only when that operator appears in the exported equation. SINDy is sequential-thresholded least squares on the full degree-3 polynomial library (10 monomials). That library is not the true support and has no sine column.
