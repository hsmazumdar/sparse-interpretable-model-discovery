# Mirror scripts

Python helpers used after Exp13 (tables, figures, stats, gates). They expect the **parent research tree** layout (`Results/`, `NnPruneHsm/`) when run with `--root` pointing at that tree.

From this mirror folder:

```powershell
python .\scripts\exp16_figures.py --root <path-to-MathModelNnPaper>
```

Exp02 (C# only, mirror-native):

```powershell
.\scripts\reproduce_exp02.ps1
```

| Script | Role |
|--------|------|
| `reproduce_exp02.ps1` | Build + run `--exp02` into `results/Exp02_PiArea` |
| `exp11_sklearn.py` | Sklearn baselines (called from Exp11) |
| `exp14_15_build.py` | Reproducibility audit + manuscript tables |
| `exp16_figures.py` | 11 CSV-driven figures |
| `exp17_stats.py` | CIs and paired tests |
| `exp18_criteria.py` | Success-criteria freeze audit |
| `exp19_pi_policy.py` | π-never-objective audit |
| `exp20_manuscript_gates.py` | Minimum rewrite gates |
| `sr_common.py` | Shared scoring for Exp21–25 (tolerances, SINDy STLSQ, gplearn, readout) |
| `exp21_same_split.py` | SINDy and gplearn on the frozen Exp12 splits |
| `exp22_score.py` | Scores the four known laws (run after `--exp22`) |
| `exp23_kepler.py` | Kepler data, log–log, SINDy, gplearn (then `--exp23`) |
| `exp24_sindy_libraries.py` | SINDy library-mismatch refits on the Exp22 splits |
| `exp25_score.py` | Scores the product/power banks (run after `--exp25`) |
| `exp25_export_check.py` | Text-parses every Exp25 export and compares it with `pred_grid.csv` |

The Exp21–25 scripts resolve paths relative to their own folder (`<script dir>/Results/...`), so copy them to the research-tree root next to `Results/` before running. They need Python 3 with numpy, scikit-learn and gplearn 0.4.3.

Do **not** place `Nn2MathModelV0N.docx` or `_build_Nn2MathModelV0N.py` in this mirror. The paper draft stays on the local research tree only.
