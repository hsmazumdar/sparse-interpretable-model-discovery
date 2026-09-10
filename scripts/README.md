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

Do **not** place `Nn2MathModelV0N.docx` or `_build_Nn2MathModelV0N.py` in this mirror. The paper draft stays on the local research tree only.
