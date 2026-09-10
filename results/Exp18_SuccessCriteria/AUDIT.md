# Exp18 audit report

Generated: 2026-09-09 12:49:52

**Overall: PASS** — PASS=64, FAIL=0, WARN=1, SKIP=0

| Status | Check | Experiment | Detail |
|--------|-------|------------|--------|
| PASS | code_constant | ExperimentCriteria | LinearContaminationTol expected=0.05 found=0.05 |
| PASS | code_constant | ExperimentCriteria | InterceptRelTol expected=0.05 found=0.05 |
| PASS | code_constant | ExperimentCriteria | FidelityMaxTol expected=1e-08 found=1e-08 |
| PASS | code_constant | ExperimentCriteria | DegreeEps expected=1e-12 found=1e-12 |
| PASS | code_constant | ExperimentCriteria | RelAbTol expected=0.1 found=0.1 |
| PASS | pass_e_strip_1.25 | runners | files_with_1.25=12 |
| PASS | seed_log_exists | Exp01_CircleBoundary/seed_logs.csv | path=K:/PaperSubmissions2026/MathModelNnPaper/Results/Exp01_CircleBoundary/seed_logs.csv |
| PASS | required_columns | Exp01_CircleBoundary/seed_logs.csv | ok |
| PASS | all_seeds_retained | Exp01_CircleBoundary/seed_logs.csv | rows=30 expected=30 non_canonical_kept=0 |
| PASS | fidelity_pass_matches_tol | Exp01_CircleBoundary/seed_logs.csv | checked=30 mismatches=0 tol=1e-08 |
| PASS | seed_log_exists | Exp02_PiArea/seed_logs.csv | path=K:/PaperSubmissions2026/MathModelNnPaper/Results/Exp02_PiArea/seed_logs.csv |
| PASS | required_columns | Exp02_PiArea/seed_logs.csv | ok |
| PASS | all_seeds_retained | Exp02_PiArea/seed_logs.csv | rows=30 expected=30 non_canonical_kept=0 |
| PASS | fidelity_pass_matches_tol | Exp02_PiArea/seed_logs.csv | checked=30 mismatches=0 tol=1e-08 |
| PASS | canonical_has_pi_error | Exp02_PiArea/seed_logs.csv | canonical=30 missing_pi_error=0 |
| PASS | seed_log_exists | Exp03_Circumference/seed_logs.csv | path=K:/PaperSubmissions2026/MathModelNnPaper/Results/Exp03_Circumference/seed_logs.csv |
| PASS | required_columns | Exp03_Circumference/seed_logs.csv | ok |
| PASS | all_seeds_retained | Exp03_Circumference/seed_logs.csv | rows=60 expected=60 non_canonical_kept=0 |
| PASS | fidelity_pass_matches_tol | Exp03_Circumference/seed_logs.csv | checked=60 mismatches=0 tol=1e-08 |
| PASS | canonical_has_two_pi_error | Exp03_Circumference/seed_logs.csv | canonical=60 missing=0 |
| PASS | seed_log_exists | Exp04_StochasticPi/seed_logs.csv | path=K:/PaperSubmissions2026/MathModelNnPaper/Results/Exp04_StochasticPi/seed_logs.csv |
| PASS | required_columns | Exp04_StochasticPi/seed_logs.csv | ok |
| PASS | all_seeds_retained | Exp04_StochasticPi/seed_logs.csv | rows=90 expected=90 non_canonical_kept=3 |
| PASS | fidelity_pass_matches_tol | Exp04_StochasticPi/seed_logs.csv | checked=90 mismatches=0 tol=1e-08 |
| PASS | canonical_has_pi_error | Exp04_StochasticPi/seed_logs.csv | canonical=87 missing_pi_error=0 |
| PASS | seed_log_exists | Exp05_SyntheticRecovery/seed_logs.csv | path=K:/PaperSubmissions2026/MathModelNnPaper/Results/Exp05_SyntheticRecovery/seed_logs.csv |
| PASS | required_columns | Exp05_SyntheticRecovery/seed_logs.csv | ok |
| PASS | all_seeds_retained | Exp05_SyntheticRecovery/seed_logs.csv | rows=150 expected=150 non_canonical_kept=0 |
| PASS | fidelity_pass_matches_tol | Exp05_SyntheticRecovery/seed_logs.csv | checked=150 mismatches=0 tol=1e-08 |
| PASS | seed_log_exists | Exp06_Noise/seed_logs.csv | path=K:/PaperSubmissions2026/MathModelNnPaper/Results/Exp06_Noise/seed_logs.csv |
| PASS | required_columns | Exp06_Noise/seed_logs.csv | ok |
| PASS | all_seeds_retained | Exp06_Noise/seed_logs.csv | rows=250 expected=250 non_canonical_kept=0 |
| PASS | fidelity_pass_matches_tol | Exp06_Noise/seed_logs.csv | checked=250 mismatches=0 tol=1e-08 |
| PASS | seed_log_exists | Exp07_OperatorAblation/seed_logs.csv | path=K:/PaperSubmissions2026/MathModelNnPaper/Results/Exp07_OperatorAblation/seed_logs.csv |
| PASS | required_columns | Exp07_OperatorAblation/seed_logs.csv | ok |
| PASS | all_seeds_retained | Exp07_OperatorAblation/seed_logs.csv | rows=75 expected=75 non_canonical_kept=53 |
| WARN | canonical_has_pi_error | Exp07_OperatorAblation/seed_logs.csv | canonical=22 missing_pi_error=3 |
| PASS | seed_log_exists | Exp08_PruningAblation/seed_logs.csv | path=K:/PaperSubmissions2026/MathModelNnPaper/Results/Exp08_PruningAblation/seed_logs.csv |
| PASS | required_columns | Exp08_PruningAblation/seed_logs.csv | ok |
| PASS | all_seeds_retained | Exp08_PruningAblation/seed_logs.csv | rows=60 expected=60 non_canonical_kept=39 |
| PASS | canonical_has_pi_error | Exp08_PruningAblation/seed_logs.csv | canonical=21 missing_pi_error=0 |
| PASS | seed_log_exists | Exp09_NetworkSize/seed_logs.csv | path=K:/PaperSubmissions2026/MathModelNnPaper/Results/Exp09_NetworkSize/seed_logs.csv |
| PASS | required_columns | Exp09_NetworkSize/seed_logs.csv | ok |
| PASS | all_seeds_retained | Exp09_NetworkSize/seed_logs.csv | rows=100 expected=100 non_canonical_kept=1 |
| PASS | canonical_has_pi_error | Exp09_NetworkSize/seed_logs.csv | canonical=99 missing_pi_error=0 |
| PASS | seed_log_exists | Exp10_PruneTolerance/seed_logs.csv | path=K:/PaperSubmissions2026/MathModelNnPaper/Results/Exp10_PruneTolerance/seed_logs.csv |
| PASS | required_columns | Exp10_PruneTolerance/seed_logs.csv | ok |
| PASS | all_seeds_retained | Exp10_PruneTolerance/seed_logs.csv | rows=60 expected=60 non_canonical_kept=24 |
| PASS | canonical_has_pi_error | Exp10_PruneTolerance/seed_logs.csv | canonical=36 missing_pi_error=0 |
| PASS | seed_log_exists | Exp11_Baselines/nn_seed_logs.csv | path=K:/PaperSubmissions2026/MathModelNnPaper/Results/Exp11_Baselines/nn_seed_logs.csv |
| PASS | required_columns | Exp11_Baselines/nn_seed_logs.csv | ok |
| PASS | all_seeds_retained | Exp11_Baselines/nn_seed_logs.csv | rows=30 expected=30 non_canonical_kept=20 |
| PASS | canonical_has_pi_error | Exp11_Baselines/nn_seed_logs.csv | canonical=10 missing_pi_error=0 |
| PASS | seed_log_exists | Exp11_Baselines/sklearn_seed_logs.csv | path=K:/PaperSubmissions2026/MathModelNnPaper/Results/Exp11_Baselines/sklearn_seed_logs.csv |
| PASS | required_columns | Exp11_Baselines/sklearn_seed_logs.csv | ok |
| PASS | all_seeds_retained | Exp11_Baselines/sklearn_seed_logs.csv | rows=70 expected=70 non_canonical_kept=50 |
| PASS | canonical_has_pi_error | Exp11_Baselines/sklearn_seed_logs.csv | canonical=20 missing_pi_error=0 |
| PASS | seed_log_exists | Exp12_CrossProduct/seed_logs.csv | path=K:/PaperSubmissions2026/MathModelNnPaper/Results/Exp12_CrossProduct/seed_logs.csv |
| PASS | required_columns | Exp12_CrossProduct/seed_logs.csv | ok |
| PASS | all_seeds_retained | Exp12_CrossProduct/seed_logs.csv | rows=30 expected=30 |
| PASS | exp12_limitation_logic | Exp12_CrossProduct/seed_logs.csv | non_exact_rows=20 limitation_logic_ok=20 |
| PASS | seed_log_exists | Exp13_EquationExport/seed_logs.csv | path=K:/PaperSubmissions2026/MathModelNnPaper/Results/Exp13_EquationExport/seed_logs.csv |
| PASS | required_columns | Exp13_EquationExport/seed_logs.csv | ok |
| PASS | all_seeds_retained | Exp13_EquationExport/seed_logs.csv | rows=13 expected=13 non_canonical_kept=13 |
| PASS | exp13_fidelity_consistency | Exp13_EquationExport/seed_logs.csv | mismatches=0 |

