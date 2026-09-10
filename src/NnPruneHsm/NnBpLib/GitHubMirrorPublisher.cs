using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace VnnBp.VnnBpLib
{
    /// <summary>
    /// Copies source and per-seed results into GitHub_mirror while experiments run.
    /// </summary>
    public static class GitHubMirrorPublisher
    {
        public static string MirrorRoot(string projectRoot)
        {
            return Path.Combine(projectRoot, "GitHub_mirror");
        }

        public static void EnsureLayout(string projectRoot)
        {
            string root = MirrorRoot(projectRoot);
            Directory.CreateDirectory(root);
            Directory.CreateDirectory(Path.Combine(root, "docs"));
            Directory.CreateDirectory(Path.Combine(root, "results"));
            Directory.CreateDirectory(Path.Combine(root, "src"));
            Directory.CreateDirectory(Path.Combine(root, "scripts"));
        }

        public static void CopySource(string projectRoot)
        {
            EnsureLayout(projectRoot);
            string src = Path.Combine(projectRoot, "NnPruneHsm");
            string dst = Path.Combine(MirrorRoot(projectRoot), "src", "NnPruneHsm");
            if (!Directory.Exists(src)) return;
            CopyTree(src, dst);
        }

        public static void PublishSeed(string projectRoot, string expName, string seedFolder, string statusMarkdown)
        {
            EnsureLayout(projectRoot);
            string destExp = Path.Combine(MirrorRoot(projectRoot), "results", expName);
            Directory.CreateDirectory(destExp);
            if (!string.IsNullOrEmpty(seedFolder) && Directory.Exists(seedFolder))
            {
                string name = Path.GetFileName(seedFolder);
                CopyTree(seedFolder, Path.Combine(destExp, name));
            }
            if (!string.IsNullOrEmpty(statusMarkdown))
                File.WriteAllText(Path.Combine(MirrorRoot(projectRoot), "STATUS.md"), statusMarkdown);
        }

        public static void PublishFile(string projectRoot, string expName, string filePath)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath)) return;
            EnsureLayout(projectRoot);
            string destExp = Path.Combine(MirrorRoot(projectRoot), "results", expName);
            Directory.CreateDirectory(destExp);
            File.Copy(filePath, Path.Combine(destExp, Path.GetFileName(filePath)), true);
        }

        private static void CopyTree(string src, string dst)
        {
            Directory.CreateDirectory(dst);
            string[] files = Directory.GetFiles(src);
            for (int i = 0; i < files.Length; i++)
            {
                string name = Path.GetFileName(files[i]);
                if (SkipName(name)) continue;
                try
                {
                    File.Copy(files[i], Path.Combine(dst, name), true);
                }
                catch (IOException)
                {
                    // Live experiment may still be writing; skip this file.
                }
                catch (UnauthorizedAccessException)
                {
                }
            }
            string[] dirs = Directory.GetDirectories(src);
            for (int i = 0; i < dirs.Length; i++)
            {
                string name = Path.GetFileName(dirs[i]);
                if (SkipName(name)) continue;
                CopyTree(dirs[i], Path.Combine(dst, name));
            }
        }

        private static bool SkipName(string name)
        {
            if (string.IsNullOrEmpty(name)) return true;
            string u = name.ToLowerInvariant();
            if (u == "bin" || u == "obj" || u == ".vs") return true;
            if (u.EndsWith(".suo") || u.EndsWith(".user") || u.EndsWith(".pdb")) return true;
            if (u.EndsWith(".exe") || u.EndsWith(".cache")) return true;
            return false;
        }

        public static string BuildStatus(
            string experimentTitle,
            string phase,
            System.Collections.Generic.List<Exp02SeedLog> logs,
            string extraHeadline)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("# Live experiment status");
            sb.AppendLine();
            sb.AppendLine("Last update: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
            sb.AppendLine();
            sb.AppendLine("| Experiment | Status | Degree of success |");
            sb.AppendLine("|------------|--------|-------------------|");
            string deg = logs == null || logs.Count == 0 ? "running" : Summarise(logs);
            sb.AppendFormat(CultureInfo.InvariantCulture,
                "| 02 Canonical A=b+kr^2 | {0} | {1} |\n", phase, deg);
            sb.AppendLine("| 03 Circumference | queued | — |");
            sb.AppendLine("| 04 Stochastic π | queued | — |");
            sb.AppendLine("| 01 Circle boundary | queued | — |");
            sb.AppendLine("| 05–12 | queued | — |");
            sb.AppendLine();
            if (!string.IsNullOrEmpty(extraHeadline))
            {
                sb.AppendLine("## Headline");
                sb.AppendLine();
                sb.AppendLine(extraHeadline.Trim());
                sb.AppendLine();
            }
            if (logs != null && logs.Count > 0)
            {
                sb.AppendLine("## Seeds finished so far");
                sb.AppendLine();
                sb.AppendLine("| Seed | Canonical? | Structural? | k | π error % | b | Neurons | Edges | Quad-linear | Fidelity max | Test RMSE |");
                sb.AppendLine("|------|------------|-------------|---|-----------|---|---------|-------|-------------|--------------|-----------|");
                for (int i = 0; i < logs.Count; i++)
                {
                    Exp02SeedLog r = logs[i];
                    sb.AppendFormat(CultureInfo.InvariantCulture,
                        "| {0} | {1} | {2} | {3:F5} | {4:F3} | {5:G6} | {6}->{7} | {8}->{9} | {10} | {11:G4} | {12:G4} |\n",
                        r.Seed,
                        r.CanonicalSuccess ? "yes" : "no",
                        r.StructuralSuccess ? "yes" : "no",
                        r.CanonicalSuccess ? r.RecoveredK : r.OlsK,
                        100.0 * r.PiError,
                        r.InterceptB,
                        r.DenseNeurons, r.PrunedNeurons,
                        r.DenseConnections, r.PrunedConnections,
                        r.SquareLinearHidden,
                        r.FidelityMax,
                        r.TestRmse);
                }
            }
            sb.AppendLine();
            sb.AppendLine("π is not an objective. Canonical k is taken from the pruned topology, not from OLS, when the graph reduces to A=b+kr².");
            return sb.ToString();
        }

        public static string BuildStatusExp03(
            string experimentTitle,
            string phase,
            System.Collections.Generic.List<Exp03SeedLog> allLogs,
            System.Collections.Generic.List<Exp03SeedLog> modeLogs,
            string extraHeadline)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("# Live experiment status");
            sb.AppendLine();
            sb.AppendLine("Last update: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
            sb.AppendLine();
            sb.AppendLine("| Experiment | Status | Degree of success |");
            sb.AppendLine("|------------|--------|-------------------|");
            sb.AppendLine("| 02 Canonical A=b+kr^2 | complete | 30/30 canonical (see Exp02) |");
            string deg = allLogs == null || allLogs.Count == 0 ? "running" : SummariseExp03(allLogs);
            sb.AppendFormat(CultureInfo.InvariantCulture,
                "| 03 Circumference | {0} | {1} |\n", phase, deg);
            sb.AppendLine("| 04 Stochastic π | queued | — |");
            sb.AppendLine("| 01 Circle boundary | queued | — |");
            sb.AppendLine("| 05–12 | queued | — |");
            sb.AppendLine();
            if (!string.IsNullOrEmpty(extraHeadline))
            {
                sb.AppendLine("## Headline");
                sb.AppendLine();
                sb.AppendLine(extraHeadline.Trim());
                sb.AppendLine();
            }
            System.Collections.Generic.List<Exp03SeedLog> show = modeLogs != null && modeLogs.Count > 0 ? modeLogs : allLogs;
            if (show != null && show.Count > 0)
            {
                sb.AppendLine("## Seeds finished so far");
                sb.AppendLine();
                sb.AppendLine("| Seed | Estimator | Canonical? | Structural? | k | 2π error % | b | Neurons | Edges | Fidelity max | Test RMSE |");
                sb.AppendLine("|------|-----------|------------|-------------|---|------------|---|---------|-------|--------------|-----------|");
                for (int i = 0; i < show.Count; i++)
                {
                    Exp03SeedLog r = show[i];
                    sb.AppendFormat(CultureInfo.InvariantCulture,
                        "| {0} | {1} | {2} | {3} | {4:F5} | {5:F3} | {6:G6} | {7}->{8} | {9}->{10} | {11:G4} | {12:G4} |\n",
                        r.Seed, r.Estimator,
                        r.CanonicalSuccess ? "yes" : "no",
                        r.StructuralSuccess ? "yes" : "no",
                        r.CanonicalSuccess ? r.RecoveredK : r.OlsK,
                        100.0 * r.TwoPiError,
                        r.InterceptB,
                        r.DenseNeurons, r.PrunedNeurons,
                        r.DenseConnections, r.PrunedConnections,
                        r.FidelityMax,
                        r.TestRmse);
                }
            }
            sb.AppendLine();
            sb.AppendLine("2π is not an objective. Canonical k is taken from the pruned topology when the graph reduces to C=b+kr.");
            return sb.ToString();
        }

        public static string BuildStatusExp04(
            string phase,
            System.Collections.Generic.List<Exp04SeedLog> allLogs,
            System.Collections.Generic.List<Exp04SeedLog> depthLogs,
            string extraHeadline)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("# Live experiment status");
            sb.AppendLine();
            sb.AppendLine("Last update: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
            sb.AppendLine();
            sb.AppendLine("| Experiment | Status | Degree of success |");
            sb.AppendLine("|------------|--------|-------------------|");
            sb.AppendLine("| 02 Canonical A=b+kr^2 | complete | 30/30 canonical (see Exp02) |");
            sb.AppendLine("| 03 Circumference | complete | 60/60 canonical (see Exp03) |");
            string deg = allLogs == null || allLogs.Count == 0 ? "running" : SummariseExp04(allLogs);
            sb.AppendFormat(CultureInfo.InvariantCulture,
                "| 04 Stochastic π | {0} | {1} |\n", phase, deg);
            sb.AppendLine("| 01 Circle boundary | queued | — |");
            sb.AppendLine("| 05–12 | queued | — |");
            sb.AppendLine();
            if (!string.IsNullOrEmpty(extraHeadline))
            {
                sb.AppendLine("## Headline");
                sb.AppendLine();
                sb.AppendLine(extraHeadline.Trim());
                sb.AppendLine();
            }
            System.Collections.Generic.List<Exp04SeedLog> show = depthLogs != null && depthLogs.Count > 0 ? depthLogs : allLogs;
            if (show != null && show.Count > 0)
            {
                sb.AppendLine("## Seeds finished so far (current depth / all)");
                sb.AppendLine();
                sb.AppendLine("| N | Seed | Canonical? | k | π error % | b | Neurons | Fidelity max |");
                sb.AppendLine("|---|------|------------|---|-----------|---|---------|--------------|");
                int start = Math.Max(0, show.Count - 40);
                for (int i = start; i < show.Count; i++)
                {
                    Exp04SeedLog r = show[i];
                    sb.AppendFormat(CultureInfo.InvariantCulture,
                        "| {0} | {1} | {2} | {3:F5} | {4:F3} | {5:G6} | {6}->{7} | {8:G4} |\n",
                        r.ObservationCount, r.Seed,
                        r.CanonicalSuccess ? "yes" : "no",
                        r.CanonicalSuccess ? r.RecoveredK : r.OlsK,
                        100.0 * r.PiError,
                        r.InterceptB,
                        r.DenseNeurons, r.PrunedNeurons,
                        r.FidelityMax);
                }
            }
            sb.AppendLine();
            sb.AppendLine("π is not an objective. Labels are Monte Carlo hit counts in a square; k is scored vs π only after freeze.");
            return sb.ToString();
        }

        public static string BuildStatusExp01(
            string phase,
            System.Collections.Generic.List<Exp01SeedLog> logs,
            string extraHeadline)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("# Live experiment status");
            sb.AppendLine();
            sb.AppendLine("Last update: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
            sb.AppendLine();
            sb.AppendLine("| Experiment | Status | Degree of success |");
            sb.AppendLine("|------------|--------|-------------------|");
            sb.AppendLine("| 02 Canonical A=b+kr^2 | complete | 30/30 (see Exp02) |");
            sb.AppendLine("| 03 Circumference | complete | 60/60 (see Exp03) |");
            sb.AppendLine("| 04 Stochastic π | complete | 87/90 (see Exp04) |");
            string deg = logs == null || logs.Count == 0 ? "running" : SummariseExp01(logs);
            sb.AppendFormat(CultureInfo.InvariantCulture,
                "| 01 Circle boundary | {0} | {1} |\n", phase, deg);
            sb.AppendLine("| 05–12 | queued | — |");
            sb.AppendLine();
            if (!string.IsNullOrEmpty(extraHeadline))
            {
                sb.AppendLine("## Headline");
                sb.AppendLine();
                sb.AppendLine(extraHeadline.Trim());
                sb.AppendLine();
            }
            if (logs != null && logs.Count > 0)
            {
                sb.AppendLine("## Seeds finished so far");
                sb.AppendLine();
                sb.AppendLine("| Seed | Canonical? | Structural? | Test acc | Baseline | a | b | |a-b|% | R_hat | Quad | Fid |");
                sb.AppendLine("|------|------------|-------------|----------|----------|---|---|--------|-------|------|-----|");
                for (int i = 0; i < logs.Count; i++)
                {
                    Exp01SeedLog r = logs[i];
                    sb.AppendFormat(CultureInfo.InvariantCulture,
                        "| {0} | {1} | {2} | {3:P1} | {4:P1} | {5:G4} | {6:G4} | {7:F1} | {8:G4} | {9} | {10:G3} |\n",
                        r.Seed,
                        r.CanonicalSuccess ? "yes" : "no",
                        r.StructuralSuccess ? "yes" : "no",
                        r.TestAccuracy, r.BaselineTestAccuracy,
                        r.CoeffA, r.CoeffB, 100.0 * r.RelAbDiff,
                        r.EffectiveRadius, r.QuadraticRetained, r.FidelityMax);
                }
            }
            sb.AppendLine();
            sb.AppendLine("Circle labels from (x-0.5)^2+(y-0.5)^2 <= R^2 only. No Math.PI.");
            return sb.ToString();
        }

        public static string BuildStatusExp05(
            string phase,
            System.Collections.Generic.List<Exp05SeedLog> logs,
            string extraHeadline)
        {
            return BuildStatusExp05(phase, logs, extraHeadline, false);
        }

        public static string BuildStatusExp05(
            string phase,
            System.Collections.Generic.List<Exp05SeedLog> logs,
            string extraHeadline,
            bool isExp06)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("# Live experiment status");
            sb.AppendLine();
            sb.AppendLine("Last update: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
            sb.AppendLine();
            sb.AppendLine("| Experiment | Status | Degree of success |");
            sb.AppendLine("|------------|--------|-------------------|");
            sb.AppendLine("| 01 Circle boundary | complete | 30/30 (see Exp01) |");
            sb.AppendLine("| 02 Canonical A=b+kr^2 | complete | 30/30 (see Exp02) |");
            sb.AppendLine("| 03 Circumference | complete | 60/60 (see Exp03) |");
            sb.AppendLine("| 04 Stochastic π | complete | 87/90 (see Exp04) |");
            string deg = logs == null || logs.Count == 0 ? "running" : SummariseExp05(logs);
            if (isExp06)
            {
                sb.AppendLine("| 05 Synthetic suite | complete | 150/150 (see Exp05) |");
                sb.AppendFormat(CultureInfo.InvariantCulture,
                    "| 06 Noise robustness | {0} | {1} |\n", phase, deg);
                sb.AppendLine("| 07–12 | queued | — |");
            }
            else
            {
                sb.AppendFormat(CultureInfo.InvariantCulture,
                    "| 05 Synthetic suite | {0} | {1} |\n", phase, deg);
                sb.AppendLine("| 06–12 | queued | — |");
            }
            sb.AppendLine();
            if (!string.IsNullOrEmpty(extraHeadline))
            {
                sb.AppendLine("## Headline");
                sb.AppendLine();
                sb.AppendLine(extraHeadline.Trim());
                sb.AppendLine();
            }
            if (logs != null && logs.Count > 0)
            {
                sb.AppendLine("## Recent runs");
                sb.AppendLine();
                sb.AppendLine("| Problem | Noise | Seed | Struct? | Canon? | Max-rel-err | RMSE/Acc |");
                sb.AppendLine("|---------|-------|------|---------|--------|-------------|---------|");
                int start = Math.Max(0, logs.Count - 30);
                for (int i = start; i < logs.Count; i++)
                {
                    Exp05SeedLog r = logs[i];
                    sb.AppendFormat(CultureInfo.InvariantCulture,
                        "| {0} | {1:G4} | {2} | {3} | {4} | {5:P2} | {6:G4} |\n",
                        r.ProblemId, r.Noise, r.Seed,
                        r.StructureOk ? "yes" : "no",
                        r.CanonicalSuccess ? "yes" : "no",
                        r.MaxRelCoeffError,
                        NnMath.IsFinite(r.TestAccuracy) ? r.TestAccuracy : r.TestRmse);
                }
            }
            sb.AppendLine();
            sb.AppendLine("Synthetic targets are known formulas; recovery uses topology / train-OLS Pass E. π never an objective.");
            return sb.ToString();
        }

        public static string BuildStatusExp07(
            string phase,
            System.Collections.Generic.List<Exp07SeedLog> logs,
            string extraHeadline)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("# Live experiment status");
            sb.AppendLine();
            sb.AppendLine("Last update: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
            sb.AppendLine();
            sb.AppendLine("| Experiment | Status | Degree of success |");
            sb.AppendLine("|------------|--------|-------------------|");
            sb.AppendLine("| 01–06 | complete | see prior Results/ |");
            string deg = logs == null || logs.Count == 0 ? "running" : SummariseExp07(logs);
            sb.AppendFormat(CultureInfo.InvariantCulture,
                "| 07 Operator ablation | {0} | {1} |\n", phase, deg);
            sb.AppendLine("| 08–12 | queued | — |");
            sb.AppendLine();
            if (!string.IsNullOrEmpty(extraHeadline))
            {
                sb.AppendLine("## Headline");
                sb.AppendLine();
                sb.AppendLine(extraHeadline.Trim());
                sb.AppendLine();
            }
            if (logs != null && logs.Count > 0)
            {
                sb.AppendLine("## Recent runs");
                sb.AppendLine();
                sb.AppendLine("| Model | Seed | Canon? | r²? | RMSE | Neurons | Edges |");
                sb.AppendLine("|-------|------|--------|-----|------|---------|-------|");
                int start = Math.Max(0, logs.Count - 40);
                for (int i = start; i < logs.Count; i++)
                {
                    Exp07SeedLog r = logs[i];
                    sb.AppendFormat(CultureInfo.InvariantCulture,
                        "| {0} | {1} | {2} | {3} | {4:G4} | {5}->{6} | {7}->{8} |\n",
                        r.ModelId, r.Seed,
                        r.CanonicalSuccess ? "yes" : "no",
                        r.ExplicitR2 ? "yes" : "no",
                        r.TestRmse,
                        r.DenseNeurons, r.FinalNeurons,
                        r.DenseConnections, r.FinalConnections);
                }
            }
            sb.AppendLine();
            sb.AppendLine("Area labels from pixel counts only. Ablation compares operator banks and prune vs no-prune.");
            return sb.ToString();
        }

        public static string BuildStatusExp08(
            string phase,
            System.Collections.Generic.List<Exp08SeedLog> logs,
            string extraHeadline)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("# Live experiment status");
            sb.AppendLine();
            sb.AppendLine("Last update: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
            sb.AppendLine();
            sb.AppendLine("| Experiment | Status | Degree of success |");
            sb.AppendLine("|------------|--------|-------------------|");
            sb.AppendLine("| 01–07 | complete | see prior Results/ |");
            string deg = logs == null || logs.Count == 0 ? "running" : SummariseExp08(logs);
            sb.AppendFormat(CultureInfo.InvariantCulture,
                "| 08 Pruning ablation | {0} | {1} |\n", phase, deg);
            sb.AppendLine("| 09–12 | queued | — |");
            sb.AppendLine();
            if (!string.IsNullOrEmpty(extraHeadline))
            {
                sb.AppendLine("## Headline");
                sb.AppendLine();
                sb.AppendLine(extraHeadline.Trim());
                sb.AppendLine();
            }
            if (logs != null && logs.Count > 0)
            {
                sb.AppendLine("## Recent runs");
                sb.AppendLine();
                sb.AppendLine("| Setting | Seed | Canon? | L_val | RMSE | Edges | t(s) |");
                sb.AppendLine("|---------|------|--------|-------|------|-------|------|");
                int start = Math.Max(0, logs.Count - 40);
                for (int i = start; i < logs.Count; i++)
                {
                    Exp08SeedLog r = logs[i];
                    sb.AppendFormat(CultureInfo.InvariantCulture,
                        "| {0} | {1} | {2} | {3:G4} | {4:G4} | {5}->{6} | {7:F1} |\n",
                        r.SettingId, r.Seed,
                        r.CanonicalSuccess ? "yes" : "no",
                        r.FinalValLoss, r.TestRmse,
                        r.DenseConnections, r.FinalConnections,
                        r.RuntimeSeconds);
                }
            }
            sb.AppendLine();
            sb.AppendLine("Pass E off. Compares post-prune sample-update budgets (0 / 1k / 10k / 50k).");
            return sb.ToString();
        }

        public static string BuildStatusExp09(
            string phase,
            System.Collections.Generic.List<Exp09SeedLog> logs,
            string extraHeadline)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("# Live experiment status");
            sb.AppendLine();
            sb.AppendLine("Last update: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
            sb.AppendLine();
            sb.AppendLine("| Experiment | Status | Degree of success |");
            sb.AppendLine("|------------|--------|-------------------|");
            sb.AppendLine("| 01–08 | complete | see prior Results/ |");
            string deg = logs == null || logs.Count == 0 ? "running" : SummariseExp09(logs);
            sb.AppendFormat(CultureInfo.InvariantCulture,
                "| 09 Network size | {0} | {1} |\n", phase, deg);
            sb.AppendLine("| 10–12 | queued | — |");
            sb.AppendLine();
            if (!string.IsNullOrEmpty(extraHeadline))
            {
                sb.AppendLine("## Headline");
                sb.AppendLine();
                sb.AppendLine(extraHeadline.Trim());
                sb.AppendLine();
            }
            if (logs != null && logs.Count > 0)
            {
                sb.AppendLine("## Recent runs");
                sb.AppendLine();
                sb.AppendLine("| Task | H | Seed | Canon? | Metric | Edges | PassE | t(s) |");
                sb.AppendLine("|------|---|------|--------|--------|-------|-------|------|");
                int start = Math.Max(0, logs.Count - 40);
                for (int i = start; i < logs.Count; i++)
                {
                    Exp09SeedLog r = logs[i];
                    sb.AppendFormat(CultureInfo.InvariantCulture,
                        "| {0} | {1} | {2} | {3} | {4:G4} | {5}->{6} | {7} | {8:F1} |\n",
                        r.TaskId, r.Hidden, r.Seed,
                        r.CanonicalSuccess ? "yes" : "no",
                        r.TestMetric,
                        r.DenseConnections, r.FinalConnections,
                        r.PassEUsed ? "Y" : "N",
                        r.RuntimeSeconds);
                }
            }
            sb.AppendLine();
            sb.AppendLine("Pass E on. Same final equation across initial hidden sizes (8/12/22/36/50).");
            return sb.ToString();
        }

        public static string BuildStatusExp10(
            string phase,
            System.Collections.Generic.List<Exp10SeedLog> logs,
            string extraHeadline)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("# Live experiment status");
            sb.AppendLine();
            sb.AppendLine("Last update: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
            sb.AppendLine();
            sb.AppendLine("| Experiment | Status | Degree of success |");
            sb.AppendLine("|------------|--------|-------------------|");
            sb.AppendLine("| 01–09 | complete | see prior Results/ |");
            string deg = logs == null || logs.Count == 0 ? "running" : SummariseExp10(logs);
            sb.AppendFormat(CultureInfo.InvariantCulture,
                "| 10 Prune tolerance | {0} | {1} |\n", phase, deg);
            sb.AppendLine("| 11–12 | queued | — |");
            sb.AppendLine();
            if (!string.IsNullOrEmpty(extraHeadline))
            {
                sb.AppendLine("## Headline");
                sb.AppendLine();
                sb.AppendLine(extraHeadline.Trim());
                sb.AppendLine();
            }
            if (logs != null && logs.Count > 0)
            {
                sb.AppendLine("## Recent runs");
                sb.AppendLine();
                sb.AppendLine("| Factor | Seed | Canon? | RMSE | Compress | Terms | t(s) |");
                sb.AppendLine("|--------|------|--------|------|----------|-------|------|");
                int start = Math.Max(0, logs.Count - 40);
                for (int i = start; i < logs.Count; i++)
                {
                    Exp10SeedLog r = logs[i];
                    sb.AppendFormat(CultureInfo.InvariantCulture,
                        "| {0:G4} | {1} | {2} | {3:G4} | {4:P0} | {5} | {6:F1} |\n",
                        r.PruneFactor, r.Seed,
                        r.CanonicalSuccess ? "yes" : "no",
                        r.TestRmse, r.Compression, r.EquationTerms,
                        r.RuntimeSeconds);
                }
            }
            sb.AppendLine();
            sb.AppendLine("Pass E off. Pareto of RMSE vs edge compression across prune factors.");
            return sb.ToString();
        }

        public static string BuildStatusExp11(
            string phase,
            System.Collections.Generic.List<Exp11SeedLog> logs,
            string extraHeadline)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("# Live experiment status");
            sb.AppendLine();
            sb.AppendLine("Last update: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
            sb.AppendLine();
            sb.AppendLine("| Experiment | Status | Degree of success |");
            sb.AppendLine("|------------|--------|-------------------|");
            sb.AppendLine("| 01–10 | complete | see prior Results/ |");
            string deg = logs == null || logs.Count == 0 ? "running" : SummariseExp11(logs);
            sb.AppendFormat(CultureInfo.InvariantCulture,
                "| 11 Baselines | {0} | {1} |\n", phase, deg);
            sb.AppendLine("| 12 | queued | — |");
            sb.AppendLine();
            if (!string.IsNullOrEmpty(extraHeadline))
            {
                sb.AppendLine("## Headline");
                sb.AppendLine();
                sb.AppendLine(extraHeadline.Trim());
                sb.AppendLine();
            }
            if (logs != null && logs.Count > 0)
            {
                sb.AppendLine("## Recent NN runs");
                sb.AppendLine();
                sb.AppendLine("| Method | Seed | Canon? | Op? | RMSE | Terms | t(s) |");
                sb.AppendLine("|--------|------|--------|-----|------|-------|------|");
                int start = Math.Max(0, logs.Count - 40);
                for (int i = start; i < logs.Count; i++)
                {
                    Exp11SeedLog r = logs[i];
                    sb.AppendFormat(CultureInfo.InvariantCulture,
                        "| {0} | {1} | {2} | {3} | {4:G4} | {5} | {6:F1} |\n",
                        r.MethodId, r.Seed,
                        r.CanonicalSuccess ? "yes" : "no",
                        r.OperatorRecovery ? "Y" : "N",
                        r.TestRmse, r.EquationTerms, r.RuntimeSeconds);
                }
            }
            sb.AppendLine();
            sb.AppendLine("Compare HSM vs ordinary MLP vs sklearn (poly may trivially recover with FE).");
            return sb.ToString();
        }

        public static string BuildStatusExp12(
            string phase,
            System.Collections.Generic.List<Exp12SeedLog> logs,
            string extraHeadline)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("# Live experiment status");
            sb.AppendLine();
            sb.AppendLine("Last update: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
            sb.AppendLine();
            sb.AppendLine("| Experiment | Status | Degree of success |");
            sb.AppendLine("|------------|--------|-------------------|");
            sb.AppendLine("| 01–11 | complete | see prior Results/ |");
            string deg = logs == null || logs.Count == 0 ? "running" : SummariseExp12(logs);
            sb.AppendFormat(CultureInfo.InvariantCulture,
                "| 12 Cross-product limit | {0} | {1} |\n", phase, deg);
            sb.AppendLine();
            if (!string.IsNullOrEmpty(extraHeadline))
            {
                sb.AppendLine("## Headline");
                sb.AppendLine();
                sb.AppendLine(extraHeadline.Trim());
                sb.AppendLine();
            }
            if (logs != null && logs.Count > 0)
            {
                sb.AppendLine("## Recent runs");
                sb.AppendLine();
                sb.AppendLine("| Problem | Seed | Struct? | LimitOK | RMSE | cxy |");
                sb.AppendLine("|---------|------|---------|---------|------|-----|");
                int start = Math.Max(0, logs.Count - 40);
                for (int i = start; i < logs.Count; i++)
                {
                    Exp12SeedLog r = logs[i];
                    sb.AppendFormat(CultureInfo.InvariantCulture,
                        "| {0} | {1} | {2} | {3} | {4:G4} | {5:G4} |\n",
                        r.ProblemId, r.Seed,
                        r.StructureRecovered ? "yes" : "no",
                        r.LimitationConfirmed ? "yes" : "no",
                        r.TestRmse, r.CoeffCxy);
                }
            }
            sb.AppendLine();
            sb.AppendLine("Honest negative: diagonal ops recover x1^2+x2^2; cannot form x1*x2.");
            return sb.ToString();
        }

        public static string BuildStatusExp13(
            string phase,
            System.Collections.Generic.List<Exp13SeedLog> logs,
            string extraHeadline)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("# Live experiment status");
            sb.AppendLine();
            sb.AppendLine("Last update: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
            sb.AppendLine();
            sb.AppendLine("| Experiment | Status | Degree of success |");
            sb.AppendLine("|------------|--------|-------------------|");
            sb.AppendLine("| 01–12 | complete | see prior Results/ |");
            string deg = logs == null || logs.Count == 0 ? "running" : SummariseExp13(logs);
            sb.AppendFormat(CultureInfo.InvariantCulture,
                "| 13 Equation export | {0} | {1} |\n", phase, deg);
            sb.AppendLine();
            if (!string.IsNullOrEmpty(extraHeadline))
            {
                sb.AppendLine("## Headline");
                sb.AppendLine();
                sb.AppendLine(extraHeadline.Trim());
                sb.AppendLine();
            }
            if (logs != null && logs.Count > 0)
            {
                sb.AppendLine("## Recent runs");
                sb.AppendLine();
                sb.AppendLine("| Task | Seed | Fid | Manual | Max|d| |");
                sb.AppendLine("|------|------|-----|--------|--------|");
                int start = Math.Max(0, logs.Count - 40);
                for (int i = start; i < logs.Count; i++)
                {
                    Exp13SeedLog r = logs[i];
                    sb.AppendFormat(CultureInfo.InvariantCulture,
                        "| {0} | {1} | {2} | {3} | {4:G4} |\n",
                        r.TaskId, r.Seed,
                        r.FidelityPass ? "PASS" : "FAIL",
                        r.ManualCheckPass ? "PASS" : "FAIL",
                        r.MaxAbsDiff);
                }
            }
            sb.AppendLine();
            sb.AppendLine("Exported equations must reproduce NN outputs within fidelity tolerance.");
            return sb.ToString();
        }

        private static string SummariseExp08(System.Collections.Generic.List<Exp08SeedLog> logs)
        {
            int n = logs.Count, can = 0;
            for (int i = 0; i < n; i++)
                if (logs[i].CanonicalSuccess) can++;
            return string.Format(CultureInfo.InvariantCulture, "{0}/{1} canonical across settings", can, n);
        }

        private static string SummariseExp09(System.Collections.Generic.List<Exp09SeedLog> logs)
        {
            int n = logs.Count, can = 0;
            for (int i = 0; i < n; i++)
                if (logs[i].CanonicalSuccess) can++;
            return string.Format(CultureInfo.InvariantCulture, "{0}/{1} canonical across sizes/tasks", can, n);
        }

        private static string SummariseExp10(System.Collections.Generic.List<Exp10SeedLog> logs)
        {
            int n = logs.Count, can = 0;
            for (int i = 0; i < n; i++)
                if (logs[i].CanonicalSuccess) can++;
            return string.Format(CultureInfo.InvariantCulture, "{0}/{1} canonical across factors", can, n);
        }

        private static string SummariseExp11(System.Collections.Generic.List<Exp11SeedLog> logs)
        {
            int n = 0, can = 0;
            for (int i = 0; i < logs.Count; i++)
            {
                if (logs[i].MethodId != "hsm") continue;
                n++;
                if (logs[i].CanonicalSuccess) can++;
            }
            if (n == 0)
                return string.Format(CultureInfo.InvariantCulture, "{0} NN runs", logs.Count);
            return string.Format(CultureInfo.InvariantCulture, "HSM {0}/{1} canonical", can, n);
        }

        private static string SummariseExp12(System.Collections.Generic.List<Exp12SeedLog> logs)
        {
            int n = logs.Count, lim = 0;
            for (int i = 0; i < n; i++)
                if (logs[i].LimitationConfirmed) lim++;
            return string.Format(CultureInfo.InvariantCulture, "{0}/{1} limitation-consistent", lim, n);
        }

        private static string SummariseExp13(System.Collections.Generic.List<Exp13SeedLog> logs)
        {
            int n = logs.Count, fid = 0, man = 0;
            for (int i = 0; i < n; i++)
            {
                if (logs[i].FidelityPass) fid++;
                if (logs[i].ManualCheckPass) man++;
            }
            return string.Format(CultureInfo.InvariantCulture,
                "fidelity {0}/{2}, manual {1}/{2}", fid, man, n);
        }

        private static string SummariseExp07(System.Collections.Generic.List<Exp07SeedLog> logs)
        {
            int n = logs.Count, can = 0;
            for (int i = 0; i < n; i++)
                if (logs[i].CanonicalSuccess) can++;
            return string.Format(CultureInfo.InvariantCulture, "{0}/{1} canonical across models", can, n);
        }

        private static string Summarise(System.Collections.Generic.List<Exp02SeedLog> logs)
        {
            int n = logs.Count;
            int can = 0, str = 0;
            for (int i = 0; i < n; i++)
            {
                if (logs[i].CanonicalSuccess) can++;
                if (logs[i].StructuralSuccess) str++;
            }
            return string.Format(CultureInfo.InvariantCulture,
                "{0}/{1} canonical, {2}/{1} structural", can, n, str);
        }

        private static string SummariseExp03(System.Collections.Generic.List<Exp03SeedLog> logs)
        {
            int n = logs.Count;
            int can = 0, str = 0;
            for (int i = 0; i < n; i++)
            {
                if (logs[i].CanonicalSuccess) can++;
                if (logs[i].StructuralSuccess) str++;
            }
            return string.Format(CultureInfo.InvariantCulture,
                "{0}/{1} canonical, {2}/{1} structural", can, n, str);
        }

        private static string SummariseExp04(System.Collections.Generic.List<Exp04SeedLog> logs)
        {
            int n = logs.Count;
            int can = 0;
            for (int i = 0; i < n; i++)
                if (logs[i].CanonicalSuccess) can++;
            return string.Format(CultureInfo.InvariantCulture,
                "{0}/{1} canonical across depths", can, n);
        }

        private static string SummariseExp01(System.Collections.Generic.List<Exp01SeedLog> logs)
        {
            int n = logs.Count;
            int can = 0, str = 0;
            for (int i = 0; i < n; i++)
            {
                if (logs[i].CanonicalSuccess) can++;
                if (logs[i].StructuralSuccess) str++;
            }
            return string.Format(CultureInfo.InvariantCulture,
                "{0}/{1} canonical, {2}/{1} structural", can, n, str);
        }

        private static string SummariseExp05(System.Collections.Generic.List<Exp05SeedLog> logs)
        {
            int n = logs.Count;
            int can = 0, st = 0;
            for (int i = 0; i < n; i++)
            {
                if (logs[i].CanonicalSuccess) can++;
                if (logs[i].StructureOk) st++;
            }
            return string.Format(CultureInfo.InvariantCulture,
                "{0}/{1} structure, {2}/{1} canonical", st, n, can);
        }
    }
}
