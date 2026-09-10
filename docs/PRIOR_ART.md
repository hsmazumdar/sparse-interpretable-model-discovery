# Prior-art comparison (Appendix A item 10)

**Status:** frozen for novelty wording  
**Freeze date:** 2026-09-09  
**Scope:** literature comparison of method families; not a head-to-head bake-off against every named tool.  
**Rebuild:** `python _build_Nn2MathModelV02.py` (Part II §23).

This document is the authoritative reading rule for novelty. Claims that are not listed under **Surviving novelty** must not appear in the abstract, introduction, or conclusion.

---

## 1. What was searched

Five literatures that meet this problem, with representative 2009–2026 sources:

| Family | Question asked | Representative sources |
|--------|----------------|------------------------|
| Network pruning | Compress a trained net under an accuracy budget | Han et al. 2015; Frankle & Carbin 2019; Hoefler et al. 2021 |
| Symbolic regression / equation discovery | Search expression trees or tokens for a closed form | Schmidt & Lipson 2009; Petersen et al. 2021; Landajuela et al. 2022; Cranmer 2023; Makke & Chawla 2024 |
| Neural equation learners | Differentiable nets whose activations *are* elementary ops | Martius & Lampert 2016 (EQL); Sahoo et al. 2018 (EQL÷); Tsoi et al. 2024 (SymbolNet); Wu et al. 2024 (PruneSymNet) |
| Sparse identification / polynomial features | Sparse linear fit on a user library | Brunton et al. 2016 (SINDy); poly2 / Lasso (Exp11) |
| Heterogeneous / quadratic / interpretable nets | Non-MLP neurons; spline-edge nets | Fan quadratic neurons; Liao et al. 2024; Liu et al. 2024 (KAN) |

Search date: 2026-09-09. Sources: published papers, arXiv, and survey articles (symbolic-regression surveys 2024–2026; KAN practitioner guides 2025). This freeze does **not** claim completeness of all concurrent preprints.

---

## 2. Closest neighbours (must distinguish, not ignore)

These four lines are the nearest prior art. The paper is **not** the first neural equation learner, the first quadratic neuron, or the first prune-to-expression network.

### 2.1 EQL / EQL÷ (Martius & Lampert 2016; Sahoo, Lampert & Martius 2018)

A feed-forward net whose units are elementary functions (identity, sine, cosine, multiply, later division), trained by gradient descent with sparsity regularisation, then read out as an equation. Closest conceptual ancestor: train a structured net, sparsify, export algebra.

**Difference.** EQL sparsifies *during* training with an L1-style penalty and model selection. NnPruneHsm trains an overcomplete mixed sigmoid/quadratic graph, then applies *transactional* magnitude pruning: each attempt is retrained for a locked sample-update budget and accepted only if validation loss stays within a locked factor of the original dense network; rejected attempts roll back completely. The operator bank is linear / logistic-sigmoid / centred-sigmoid / centred quadratic-sigmoid (funno 4) / uncentred square-linear (funno 7), not EQL’s sin/cos/×/÷ set.

### 2.2 SymbolNet (Tsoi, Lončar, Dasu & Harris 2024)

Neural symbolic regression with *adaptive dynamic pruning* of weights, inputs and operators in a **single** training phase, jointly minimising loss and expression complexity for FPGA-style compression (HEP jet tagging, MNIST, SVHN).

**Difference.** SymbolNet is a compression-oriented, one-phase sparsity schedule. NnPruneHsm is a validation-constrained discovery procedure with explicit rollback, dense-baseline gating, graph contraction, and equation-vs-network fidelity (Exp13). Hardware latency is not the objective here.

### 2.3 PruneSymNet (Wu, Li et al. 2024)

A symbolic net whose nodes are elementary unary/binary operators; greedy (plus beam) pruning from the output backward keeps the lowest-loss incident edges so each node retains the arity of its operator.

**Difference.** PruneSymNet prunes an already-symbolic operator graph by local loss. NnPruneHsm starts from a heterogeneous *neural* bank (including sigmoids that are not elementary algebraic ops), ranks by weight magnitude, retrains, and tests against a locked dense validation budget. Nonlinear 1-in/1-out contraction is labelled approximate and validation-gated (not claimed exact).

### 2.4 KAN (Liu et al. 2024)

Learnable univariate functions on edges (B-splines by default), node-level sparsify-then-prune, then optional symbolic snap of univariate maps.

**Difference.** KAN parameterises *edges* as splines; interpretability is a sum of univariate functions. NnPruneHsm parameterises *nodes* as mixed linear/sigmoid/quadratic maps with ordinary weights, then contracts the surviving graph. No spline basis, no Kolmogorov–Arnold claim.

---

## 3. Other families (positioning, not straw men)

| Family | What it does | Relation to this work |
|--------|----------------|------------------------|
| GP / Eureqa / PySR / DSR / uDSR / AI Feynman | Search or tokenise expression trees; never require a trained heterogeneous NN | Complementary. Strong at closed-form search. Exp11 did **not** run PySR/uDSR/gplearn in the reported table (gplearn is optional and skipped if missing). Do not claim empirical superiority over SR. |
| SINDy / PDE-FIND | Sparse regression on a **user-specified** library, usually for dynamics \(\dot x = f(x)\) | Complementary. Recovers terms only if they sit in the library. This pipeline does not difference time series and does not require a hand-built \(\pi r^2\) feature. |
| Magnitude / lottery-ticket / structured pruning | Compress homogeneous MLPs for efficiency | Ancestor of the prune step. Rarely exports a scientific equation; Exp11 `mlp_pruned` (no quadratic ops) → 0/10 canonical on area. |
| Poly2 / Lasso on polynomial features | Sparse linear model after explicit \(r,r^2\) (or \(x_i x_j\)) features | Exp11: 10/10 canonical **with prior FE**. Not operator discovery. Must be labelled as such. |
| Trees / forests / boosting | Accurate black-box piecewise models | Exp11: 0% canonical; no closed form. Fair as predictors, not as equation methods. |
| Fan quadratic neurons; Liao et al. 2024 heterogeneous AE | Quadratic + conventional neurons for representation / anomaly detection | Shares heterogeneous/quadratic *representation*. Does not prune-retrain-rollback to an exported geometric law. |
| PINNs (Raissi 2019) | Enforce known PDEs while fitting data | Inverse use of physics. This paper forbids \(\pi\) as a training target (Exp19). |
| NAMs / GAMs | Additive univariate shape functions | Interpretable, not a sparse multiplicative/quadratic graph export. |

---

## 4. Surviving novelty (may be claimed)

Claim only the **joint** pipeline, with evidence pointers. Individual pieces are not new.

1. **Mixed operator graph for geometric laws.** Linear, logistic-sigmoid, centred-sigmoid, centred quadratic-sigmoid and uncentred square-linear units in one graph, reduced to \(A=b+kr^2\) from pixel counts and to circular quadratics from labels that never contain \(\pi\) (Exp02 30/30; Exp01 30/30; Exp07 Model A 0/15 vs E 15/15; Exp19 audit PASS).
2. **Transactional validation-constrained prune–retrain.** Magnitude prune, dead-path cleanup, 1-in/1-out contraction, locked sample-update retrain, accept only inside a dense-baseline validation strip, otherwise full rollback. Empirically, retrain length matters (Exp08: none/r0 = 0% canonical; r10k = 67%; r50k = 100% with Pass E off).
3. **Exact linear vs approximate nonlinear contraction**, the latter justified only by retraining and the validation test — not algebraically exact.
4. **Equation export with network fidelity**, not only fit-to-labels (Exp13: 13/13, \(\max|y_{\mathrm{NN}}-y_{\mathrm{eq}}|\le 10^{-8}\)).
5. **Honest operator limitation.** No explicit \(x_1 x_2\) in the bank; Exp12 records 0% exact cross-product recovery and 30/30 `limitation_ok`.

One-sentence contribution (use this, not stronger):

> A validation-constrained prune–retrain–rollback procedure on a heterogeneous linear/sigmoid/quadratic neural graph that exports an explicit low-order model whose predictions match the live network, demonstrated on geometry and synthetic recovery tasks without using \(\pi\) as an objective.

---

## 5. Claims that do **not** survive (forbidden)

- First neural equation learner, first quadratic neuron, or first prune-to-expression method.
- Universal superiority over symbolic regression, EQL, SymbolNet, PruneSymNet, KAN, SINDy, or well-tuned MLPs.
- That Exp11 is a bake-off against PySR / EQL / KAN (it is sklearn + no-quad MLP + poly2/lasso with prior features).
- Exact nonlinear contraction; optimality of 10k updates; Q1 acceptance.
- Circumference \(k=2\pi\) to high accuracy (Exp03 digitization bias ~5%).
- Perfect Exp04 / Exp09 (87/90 and 99/100).
- That poly2/lasso with \(r^2\) features are fair *operator-discovery* baselines.

---

## 6. Experimental vs literature-only

| Comparison | Status | Where |
|------------|--------|--------|
| Dense MLP, pruned MLP (no quad), linreg, poly2, lasso_poly2, trees, RF, GBoost, sklearn MLP | **Measured** (Exp11) | `Results/Exp11_Baselines/` |
| Operator ablation A–E | **Measured** (Exp07) | `Results/Exp07_OperatorAblation/` |
| Prune / retrain length | **Measured** (Exp08) | `Results/Exp08_PruningAblation/` |
| EQL, SymbolNet, PruneSymNet, KAN, PySR, uDSR, SINDy | **Literature only** | this file; Part II §23 |
| gplearn SymbolicRegressor | Optional in `exp11_sklearn.py`; **not** in the reported Exp11 table | do not cite as a result |

A same-split bake-off against EQL/KAN/PySR remains desirable future work. It is **not** required to freeze the wording in §4.

---

## 7. Bibliography (comparison set)

1. Han, S., Pool, J., Tran, J. & Dally, W. Learning both weights and connections for efficient neural networks. *NeurIPS* (2015).
2. Frankle, J. & Carbin, M. The lottery ticket hypothesis. *ICLR* (2019).
3. Hoefler, T. et al. Sparsity in deep learning: pruning and growth for efficient inference and training in neural networks. *JMLR* (2021).
4. Schmidt, M. & Lipson, H. Distilling free-form natural laws from experimental data. *Science* 324, 81–85 (2009).
5. Petersen, B. K. et al. Deep symbolic regression: recovering mathematical expressions from data via risk-seeking policy gradients. *ICLR* (2021).
6. Landajuela, M. et al. A unified framework for deep symbolic regression. *NeurIPS* (2022).
7. Cranmer, M. Interpretable machine learning for science with PySR and SymbolicRegression.jl. arXiv:2305.01582 (2023).
8. Udrescu, S.-M. & Tegmark, M. AI Feynman: a physics-inspired method for symbolic regression. *Science Advances* (2020).
9. Makke, N. & Chawla, S. Interpretable scientific discovery with symbolic regression: a review. *Artificial Intelligence Review* (2024).
10. Martius, G. & Lampert, C. H. Extrapolation and learning equations. arXiv:1610.02995 (2016).
11. Sahoo, S. S., Lampert, C. H. & Martius, G. Learning equations for extrapolation and control. *ICML* (2018).
12. Tsoi, H. F., Lončar, V., Dasu, S. & Harris, P. SymbolNet: neural symbolic regression with adaptive dynamic pruning. arXiv:2401.09949 (2024); *Machine Learning: Science and Technology* (2025).
13. Wu, M., Li, W. et al. PruneSymNet: a symbolic neural network and pruning algorithm for symbolic regression. arXiv:2401.15103 (2024).
14. Brunton, S. L., Proctor, J. L. & Kutz, J. N. Discovering governing equations from data by sparse identification of nonlinear dynamical systems. *PNAS* (2016).
15. Liu, Z. et al. KAN: Kolmogorov–Arnold networks. arXiv:2404.19756 (2024).
16. Liao, J.-X. et al. Quadratic neuron-empowered heterogeneous autoencoder for unsupervised anomaly detection. *IEEE Trans. Artificial Intelligence* (2024).
17. Fan, F., Cong, W. & Wang, G. A new type of neurons for machine learning. *Int. J. Numer. Method. Biomed. Eng.* (2018) and follow-on quadratic-neuron papers.
18. Chrysos, G. et al. P-nets: Deep polynomial neural networks. *CVPR* / related polynomial-network line (2020–).
19. Raissi, M., Perdikaris, P. & Karniadakis, G. E. Physics-informed neural networks. *J. Comput. Phys.* (2019).
20. Agarwal, R. et al. Neural additive models. *NeurIPS* (2021).

---

## 8. Gate 10 verdict

**PASS (wording freeze), with the limits in §5–§6.**  
Novelty claims that survive are the joint validation-constrained heterogeneous prune-to-equation pipeline plus the measured Exp01–20 evidence. Component-wise priority and universal superiority do **not** survive.
