# Nn2MathModel V02 — results ↔ claims reconciliation

Generated: 2026-09-09 21:53

## Part I patches applied

- Abstract rewritten for completed Exp01–20
- §7.1 protocol note reconciled
- §7.2 S3 definition reconciled with Exp05/Exp12
- §7.2 S3 diagnostic paragraph reconciled
- §7.3 public-data wording reconciled
- §8 circle illustration marked superseded
- §9 planning list marked historical
- §10 completeness sentence reconciled
- §10 diagonal-operator limitation reconciled
- §10 'single illustration' limitation removed
- §2 related-work paragraph frozen to PRIOR_ART.md
- §10 literature-comparison limitation updated
- References paragraph replaced with freeze bibliography pointer
- §11 conclusion reconciled
- Appendix A intro reconciled
- Appendix A gate updated: Build, unit tests and all gradient check...
- Appendix A gate updated: Evaluation is non-mutating and fixed-see...
- Appendix A gate updated: Pruning rollback restores the complete m...
- Appendix A gate updated: The 10,000-sample retrain step is logged...
- Appendix A gate updated: At least four synthetic and several real...
- Appendix A gate updated: Strong baselines and the principal ablat...
- Appendix A gate updated: Results include repeated seeds, confiden...
- Appendix A gate updated: Exported equations reproduce network pre...
- Appendix A gate updated: Code, configurations, data instructions ...
- Appendix A gate updated: Novelty claims survive a current prior-a...

## Authoritative numeric snapshot (from Results/ CSVs)

- Exp01 circle: 30/30 (canonical)
- Exp02 area: 30/30 (canonical)
- Exp03 circ: 60/60 (canonical)
- Exp04 stochastic: 87/90 (canonical)
- Exp05 structure: 150/150 (structure_ok)
- Exp06 structure: 250/250 (structure_ok)
- Exp09 size: 99/100 (canonical)
- Exp13 export: fidelity+manual 13/13

## Claim corrections (must not reappear)

1. Do not say experiments/baselines/multi-seed stats are 'not yet in this draft' — they are in Part II.
2. Do not say implemented Exp05 S3 requires x1*x2 — S3 is 1-D mixed polynomial; cross-product = Exp12.
3. Do not imply Exp02/Exp08 always use exactly 10k retrain as the only successful setting — Exp08 shows 10k→67% canon (Pass E off) and 50k→100%; main Exp02 uses Pass E under val strip.
4. Do not claim Exp04 is 100% canonical — it is 87/90; three failures keep non-polynomial graphs.
5. Do not claim Exp09 is perfect — 99/100 (one area H=12 seed failed).
6. Circumference canonical form ≠ accurate 2π: expect ~5% digitization bias (Exp03).
7. Do not claim first neural equation learner / first quadratic neuron / superiority over PySR, EQL, SymbolNet, PruneSymNet or KAN (PRIOR_ART.md freeze).

## Appendix A item 10

FROZEN 2026-09-09. Surviving novelty = joint validation-constrained heterogeneous prune-to-equation pipeline. Full comparison: PRIOR_ART.md / Part II §23.

