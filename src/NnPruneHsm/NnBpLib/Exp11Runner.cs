using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;

namespace VnnBp.VnnBpLib
{
    public sealed class Exp11SeedLog
    {
        public string MethodId;
        public string MethodName;
        public string Family;
        public int Seed;
        public bool CanonicalSuccess;
        public bool StructuralSuccess;
        public bool OperatorRecovery;
        public bool PriorFeatureEngineering;
        public bool AutoSimplify;
        public double RecoveredK;
        public double PiError;
        public double TestRmse;
        public double FinalValLoss;
        public int DenseNeurons;
        public int FinalNeurons;
        public int DenseConnections;
        public int FinalConnections;
        public int EquationTerms;
        public string Equation;
        public double RuntimeSeconds;
        public string Reason;
    }

    /// <summary>
    /// Experiment 11 — baselines on pixel-area data (same splits).
    /// NN methods here: heterogeneous HSM (prune+Pass E), dense lin/sig MLP,
    /// pruned lin/sig MLP. Sklearn baselines are run by exp11_sklearn.py on the
    /// exported split CSVs (invoked at end when Python is available).
    /// </summary>
    public static class Exp11Runner
    {
        private struct MethodSpec
        {
            public string Id;
            public string Name;
            public char Bank; // 'M' mixed, 'L' lin/sig
            public bool DoPrune;
            public bool PassE;
        }

        public static int Run(string[] args)
        {
            PiOptions opt = PiOptions.Parse(args);
            opt.RunArea = true;
            opt.RunCirc = false;
            opt.AllowFallback = false;
            bool quick = false;
            bool seedsSet = false;
            bool skipSklearn = false;
            if (args != null)
            {
                for (int i = 0; i < args.Length; i++)
                {
                    if (args[i] == "--quick") quick = true;
                    if (args[i] == "--seeds") seedsSet = true;
                    if (args[i] == "--no-sklearn") skipSklearn = true;
                }
            }
            if (!quick && !seedsSet) opt.SeedCount = 10;
            if (!quick && opt.RadiusStep == 10) opt.RadiusStep = 2;
            if (!quick && opt.DenseEpochs == 400) opt.DenseEpochs = 150;
            if (quick && opt.RadiusStep < 5) opt.RadiusStep = 10;

            string root = FindProjectRoot();
            if (string.IsNullOrEmpty(opt.ResultsDir) || opt.ResultsDir.IndexOf("PiExperiment", StringComparison.OrdinalIgnoreCase) >= 0)
                opt.ResultsDir = Path.Combine(root, "Results", "Exp11_Baselines");
            Directory.CreateDirectory(opt.ResultsDir);
            GitHubMirrorPublisher.EnsureLayout(root);
            GitHubMirrorPublisher.CopySource(root);

            MethodSpec[] methods = new MethodSpec[]
            {
                Spec("hsm", "heterogeneous prune+PassE (ours)", 'M', true, true),
                Spec("mlp_dense", "ordinary dense MLP (lin/sig)", 'L', false, false),
                Spec("mlp_pruned", "pruned ordinary MLP (lin/sig)", 'L', true, false),
            };

            StringBuilder console = new StringBuilder();
            Log(console, "Experiment 11 — baselines vs HSM (area)");
            Log(console, "Results: " + opt.ResultsDir);
            Log(console, "Seeds: " + opt.SeedCount + "  epochs=" + opt.DenseEpochs);
            Log(console, "Radii: " + opt.RadiusLo + ".." + opt.RadiusHi + " step " + opt.RadiusStep);
            Log(console, "Compare predictive accuracy, equation size, operator recovery, feature engineering.");
            Log(console, "π not an objective.");

            int[] radii = PiDataGenerator.RadiusRange(opt.RadiusLo, opt.RadiusHi, opt.RadiusStep);
            PiSample[] area = PiDataGenerator.GenerateAreaData(radii, opt.GridResolution);
            if (opt.MinRadius > 0)
                area = PiDataGenerator.FilterMinRadius(area, opt.MinRadius);
            PiDataGenerator.SaveCsv(Path.Combine(opt.ResultsDir, "area_data.csv"), area);
            Log(console, "Samples: " + area.Length);

            double meanA = 0;
            for (int i = 0; i < area.Length; i++) meanA += Math.Abs(area[i].Y);
            if (area.Length > 0) meanA /= area.Length;

            List<Exp11SeedLog> all = new List<Exp11SeedLog>();
            string status = GitHubMirrorPublisher.BuildStatusExp11("running", all, "");
            File.WriteAllText(Path.Combine(opt.ResultsDir, "STATUS.md"), status);

            // Shared splits per seed (written once; used by all NN methods + sklearn).
            for (int s = 1; s <= opt.SeedCount; s++)
            {
                string splitDir = Path.Combine(opt.ResultsDir, string.Format(CultureInfo.InvariantCulture, "seed_{0:00}", s));
                Directory.CreateDirectory(splitDir);
                WriteSharedSplit(area, s, opt, splitDir);
            }

            for (int mi = 0; mi < methods.Length; mi++)
            {
                MethodSpec spec = methods[mi];
                string methodDir = Path.Combine(opt.ResultsDir, "Method_" + spec.Id);
                Directory.CreateDirectory(methodDir);
                Log(console, "");
                Log(console, "=== " + spec.Id + ": " + spec.Name + " ===");

                for (int s = 1; s <= opt.SeedCount; s++)
                {
                    Log(console, spec.Id + " seed " + s + "/" + opt.SeedCount + "...");
                    Stopwatch sw = Stopwatch.StartNew();
                    Exp11SeedLog row = RunNn(area, meanA, s, opt, spec, methodDir);
                    sw.Stop();
                    row.RuntimeSeconds = sw.Elapsed.TotalSeconds;
                    all.Add(row);
                    File.WriteAllText(Path.Combine(methodDir, string.Format(CultureInfo.InvariantCulture, "seed_{0:00}", s), "runtime.txt"),
                        row.RuntimeSeconds.ToString("G15", CultureInfo.InvariantCulture) + " seconds");

                    status = GitHubMirrorPublisher.BuildStatusExp11("running", all, BuildHeadlineNn(all, methods));
                    File.WriteAllText(Path.Combine(opt.ResultsDir, "STATUS.md"), status);
                    GitHubMirrorPublisher.PublishSeed(root, "Exp11_Baselines",
                        Path.Combine(methodDir, string.Format(CultureInfo.InvariantCulture, "seed_{0:00}", s)), status);

                    Log(console, string.Format(CultureInfo.InvariantCulture,
                        "  canon={0}  op={1}  rmse={2:G4}  terms={3}  n {4}->{5}  e {6}->{7}  t={8:F1}s",
                        row.CanonicalSuccess ? "YES" : "NO",
                        row.OperatorRecovery ? "Y" : "N",
                        row.TestRmse, row.EquationTerms,
                        row.DenseNeurons, row.FinalNeurons,
                        row.DenseConnections, row.FinalConnections,
                        row.RuntimeSeconds));
                }
            }

            WriteSeedCsv(Path.Combine(opt.ResultsDir, "nn_seed_logs.csv"), all);
            string headlineNn = BuildHeadlineNn(all, methods);
            File.WriteAllText(Path.Combine(opt.ResultsDir, "headline_nn.txt"), headlineNn);

            if (!skipSklearn)
            {
                Log(console, "");
                Log(console, "Invoking sklearn baselines (exp11_sklearn.py)...");
                int pyCode = RunSklearn(root, opt.ResultsDir, console);
                Log(console, "sklearn exit=" + pyCode);
            }

            string combined = MergeTables(opt.ResultsDir, headlineNn);
            File.WriteAllText(Path.Combine(opt.ResultsDir, "headline.txt"), combined);
            File.WriteAllText(Path.Combine(opt.ResultsDir, "summary.md"),
                "# Experiment 11 — baselines\r\n\r\n" + combined);
            status = GitHubMirrorPublisher.BuildStatusExp11("complete", all, combined);
            File.WriteAllText(Path.Combine(GitHubMirrorPublisher.MirrorRoot(root), "STATUS.md"), status);
            File.WriteAllText(Path.Combine(opt.ResultsDir, "STATUS.md"), status);
            GitHubMirrorPublisher.PublishFile(root, "Exp11_Baselines", Path.Combine(opt.ResultsDir, "nn_seed_logs.csv"));
            GitHubMirrorPublisher.PublishFile(root, "Exp11_Baselines", Path.Combine(opt.ResultsDir, "headline.txt"));
            GitHubMirrorPublisher.PublishFile(root, "Exp11_Baselines", Path.Combine(opt.ResultsDir, "summary.md"));
            string skCsv = Path.Combine(opt.ResultsDir, "sklearn_seed_logs.csv");
            if (File.Exists(skCsv))
                GitHubMirrorPublisher.PublishFile(root, "Exp11_Baselines", skCsv);

            Log(console, "");
            Log(console, combined);
            Log(console, "Experiment Complete.");
            File.WriteAllText(Path.Combine(opt.ResultsDir, "console.log"), console.ToString());
            return 0;
        }

        private static MethodSpec Spec(string id, string name, char bank, bool prune, bool passE)
        {
            MethodSpec s = new MethodSpec();
            s.Id = id; s.Name = name; s.Bank = bank; s.DoPrune = prune; s.PassE = passE;
            return s;
        }

        private static void WriteSharedSplit(PiSample[] samples, int seed, PiOptions opt, string seedDir)
        {
            double rMax = Math.Max(1.0, opt.RadiusHi);
            double yScale = rMax * rMax;
            double[][] xinRaw, xoutRaw;
            PiDataGenerator.ToArrays(samples, out xinRaw, out xoutRaw);
            double[][] xin = new double[xinRaw.Length][];
            double[][] xout = new double[xoutRaw.Length][];
            for (int i = 0; i < xinRaw.Length; i++)
            {
                xin[i] = new double[] { xinRaw[i][0] / rMax };
                xout[i] = new double[] { xoutRaw[i][0] / yScale };
            }
            DatasetSplit split = DatasetSplit.CreateDefault(xin, xout, seed);
            File.WriteAllText(Path.Combine(seedDir, "seed.txt"), seed.ToString(CultureInfo.InvariantCulture));
            WriteSplitCsv(Path.Combine(seedDir, "train.csv"), split.TrainIn, split.TrainOut, rMax, yScale);
            WriteSplitCsv(Path.Combine(seedDir, "validation.csv"), split.ValIn, split.ValOut, rMax, yScale);
            WriteSplitCsv(Path.Combine(seedDir, "test.csv"), split.TestIn, split.TestOut, rMax, yScale);
            File.WriteAllText(Path.Combine(seedDir, "scales.txt"),
                string.Format(CultureInfo.InvariantCulture, "rMax={0:G15}\nyScale={1:G15}\n", rMax, yScale));
        }

        private static Exp11SeedLog RunNn(
            PiSample[] samples, double meanA, int seed, PiOptions opt, MethodSpec spec, string methodDir)
        {
            double rMax = Math.Max(1.0, opt.RadiusHi);
            double yScale = rMax * rMax;
            double[][] xinRaw, xoutRaw;
            PiDataGenerator.ToArrays(samples, out xinRaw, out xoutRaw);
            double[][] xin = new double[xinRaw.Length][];
            double[][] xout = new double[xoutRaw.Length][];
            for (int i = 0; i < xinRaw.Length; i++)
            {
                xin[i] = new double[] { xinRaw[i][0] / rMax };
                xout[i] = new double[] { xoutRaw[i][0] / yScale };
            }
            DatasetSplit split = DatasetSplit.CreateDefault(xin, xout, seed);

            string seedDir = Path.Combine(methodDir, string.Format(CultureInfo.InvariantCulture, "seed_{0:00}", seed));
            Directory.CreateDirectory(seedDir);
            File.WriteAllText(Path.Combine(seedDir, "seed.txt"), seed.ToString(CultureInfo.InvariantCulture));
            File.WriteAllText(Path.Combine(seedDir, "method.txt"), spec.Id);

            VnnBpNet net = CreateBank(spec, opt.Hidden, seed);
            net.lossType = LossType.MeanSquaredError;
            double eta = 0.05;
            net.eta = eta;
            net.etab = eta;

            TrainingOptions tr = new TrainingOptions();
            tr.LearningRate = eta;
            tr.LossType = LossType.MeanSquaredError;
            tr.ShuffleEachEpoch = true;
            ResearchLogger logger = new ResearchLogger(seedDir);

            for (int e = 0; e < opt.DenseEpochs; e++)
            {
                tr.RandomSeed = seed * 1000 + e;
                net.TrainEpoch(split.TrainIn, split.TrainOut, tr);
            }

            int denseN = net.maxNeurons;
            int denseC = net.maxConnections;
            double denseVal = net.Evaluate(split.ValIn, split.ValOut).Loss;
            File.WriteAllText(Path.Combine(seedDir, "dense_network.txt"), net.ExportEquation());

            if (spec.DoPrune)
            {
                double stripFactor = opt.PruneFactor < 1.25 ? 1.25 : opt.PruneFactor;
                PiExperimentRunner.PruneWithLockedBaseline(net, split, opt.PruneFactor, opt.RetrainCycles, seed, logger, eta);
                int stripCycles = opt.RetrainCycles > 3000 ? 3000 : opt.RetrainCycles;
                PiExperimentRunner.PruneSigmoidPathsForCanonical(
                    net, split, stripFactor, stripCycles, seed, logger, eta, denseVal);

                if (spec.PassE)
                {
                    double allowed = denseVal * stripFactor;
                    if (denseVal < 1e-12)
                        allowed = denseVal + 1e-8 + (stripFactor - 1.0) * 1e-8;
                    CanonicalResult probe = CanonicalExtractor.ExtractArea(net, rMax, yScale);
                    if (!probe.CanonicalSuccess)
                    {
                        string note;
                        if (CanonicalExtractor.TryInstallTrainQuadratic(net, split, allowed, out note))
                            File.WriteAllText(Path.Combine(seedDir, "ols_install.txt"), note);
                        else
                            File.WriteAllText(Path.Combine(seedDir, "ols_install.txt"), "rejected: " + note);
                    }
                }
            }

            EvaluationResult finalVal = net.Evaluate(split.ValIn, split.ValOut);
            logger.ExportFinalNetwork(net);
            string eq = net.ExportEquation();
            File.WriteAllText(Path.Combine(seedDir, "exported_equation.txt"), eq);
            File.WriteAllText(Path.Combine(seedDir, "pruned_network.txt"), eq);

            CanonicalResult can = CanonicalExtractor.ExtractArea(net, rMax, yScale);
            CanonicalExtractor.ApplyInterceptGate(can, meanA);
            double testRmse = RmsePhysical(net, split.TestIn, split.TestOut, yScale);
            double pi = PiDataGenerator.ReferencePi();
            double piErr = can.CanonicalSuccess && NnMath.IsFinite(can.SlopeK)
                ? Math.Abs(can.SlopeK - pi) / pi : double.NaN;

            bool opRec = can.StructuralSuccess || can.CanonicalSuccess
                || PiNetworkBuilder.CountHiddenOfType(net, 7) > 0
                || PiNetworkBuilder.CountHiddenOfType(net, 4) > 0;

            Exp11SeedLog row = new Exp11SeedLog();
            row.MethodId = spec.Id;
            row.MethodName = spec.Name;
            row.Family = "nn";
            row.Seed = seed;
            row.CanonicalSuccess = can.CanonicalSuccess;
            row.StructuralSuccess = can.StructuralSuccess;
            row.OperatorRecovery = opRec;
            row.PriorFeatureEngineering = false;
            row.AutoSimplify = spec.DoPrune;
            row.RecoveredK = can.CanonicalSuccess ? can.SlopeK : 0;
            row.PiError = piErr;
            row.TestRmse = testRmse;
            row.FinalValLoss = finalVal.Loss;
            row.DenseNeurons = denseN;
            row.FinalNeurons = net.maxNeurons;
            row.DenseConnections = denseC;
            row.FinalConnections = net.maxConnections;
            row.EquationTerms = CountEquationTerms(eq);
            row.Equation = can.CanonicalSuccess ? can.CanonicalEquation : eq;
            row.Reason = can.Reason;

            StringBuilder met = new StringBuilder();
            met.AppendLine("{");
            met.AppendFormat(CultureInfo.InvariantCulture, "  \"method\": \"{0}\",\n", spec.Id);
            met.AppendFormat(CultureInfo.InvariantCulture, "  \"canonical\": {0},\n", can.CanonicalSuccess ? "true" : "false");
            met.AppendFormat(CultureInfo.InvariantCulture, "  \"operator_recovery\": {0},\n", opRec ? "true" : "false");
            met.AppendFormat(CultureInfo.InvariantCulture, "  \"test_rmse\": {0},\n", JsonNum(testRmse));
            met.AppendFormat(CultureInfo.InvariantCulture, "  \"terms\": {0},\n", row.EquationTerms);
            met.AppendFormat(CultureInfo.InvariantCulture, "  \"prior_features\": false\n");
            met.AppendLine("}");
            File.WriteAllText(Path.Combine(seedDir, "metrics.json"), met.ToString());
            if (can.CanonicalSuccess)
                File.WriteAllText(Path.Combine(seedDir, "canonical_equation.txt"), can.CanonicalEquation);
            return row;
        }

        private static VnnBpNet CreateBank(MethodSpec spec, int hidden, int seed)
        {
            string name = "Exp11_" + spec.Id;
            if (spec.Bank == 'L')
                return PiNetworkBuilder.CreateLinSigOnlyBank(name, hidden, seed);
            return PiNetworkBuilder.CreateAreaDiscoveryBank(name, hidden, seed);
        }

        private static int RunSklearn(string projectRoot, string resultsDir, StringBuilder console)
        {
            string script = Path.Combine(projectRoot, "exp11_sklearn.py");
            if (!File.Exists(script))
            {
                Log(console, "WARN: exp11_sklearn.py not found at " + script);
                return 2;
            }
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName = "python";
                psi.Arguments = "\"" + script + "\" --out \"" + resultsDir + "\"";
                psi.UseShellExecute = false;
                psi.RedirectStandardOutput = true;
                psi.RedirectStandardError = true;
                psi.CreateNoWindow = true;
                psi.WorkingDirectory = projectRoot;
                using (Process p = Process.Start(psi))
                {
                    if (p == null) return 3;
                    string stdout = p.StandardOutput.ReadToEnd();
                    string stderr = p.StandardError.ReadToEnd();
                    p.WaitForExit();
                    if (!string.IsNullOrEmpty(stdout))
                    {
                        console.Append(stdout);
                        Console.Write(stdout);
                    }
                    if (!string.IsNullOrEmpty(stderr))
                    {
                        console.Append(stderr);
                        Console.Error.Write(stderr);
                    }
                    return p.ExitCode;
                }
            }
            catch (Exception ex)
            {
                Log(console, "WARN: sklearn invoke failed: " + ex.Message);
                return 4;
            }
        }

        private static string MergeTables(string resultsDir, string headlineNn)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine(headlineNn.TrimEnd());
            string skHead = Path.Combine(resultsDir, "headline_sklearn.txt");
            if (File.Exists(skHead))
            {
                sb.AppendLine();
                sb.Append(File.ReadAllText(skHead).TrimEnd());
                sb.AppendLine();
            }
            string skTable = Path.Combine(resultsDir, "Table_Baselines.md");
            if (File.Exists(skTable))
            {
                sb.AppendLine();
                sb.Append(File.ReadAllText(skTable).TrimEnd());
                sb.AppendLine();
            }
            return sb.ToString();
        }

        private static void WriteSplitCsv(string path, double[][] xin, double[][] xout, double rMax, double yScale)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("r,A,r_scaled,A_scaled");
            for (int i = 0; i < xin.Length; i++)
            {
                sb.AppendFormat(CultureInfo.InvariantCulture,
                    "{0:G15},{1:G15},{2:G15},{3:G15}\r\n",
                    xin[i][0] * rMax, xout[i][0] * yScale, xin[i][0], xout[i][0]);
            }
            File.WriteAllText(path, sb.ToString());
        }

        private static double RmsePhysical(VnnBpNet net, double[][] xin, double[][] xout, double yScale)
        {
            if (xin == null || xin.Length == 0) return double.NaN;
            double s = 0;
            int oi = net.outputs[0].sno;
            for (int i = 0; i < xin.Length; i++)
            {
                net.inputs[0].value = xin[i][0];
                net.outputs[0].value = 0;
                net.UpdateNet();
                double d = net.neuron[oi].nuOut * yScale - xout[i][0] * yScale;
                s += d * d;
            }
            return Math.Sqrt(s / xin.Length);
        }

        private static int CountEquationTerms(string eq)
        {
            if (string.IsNullOrEmpty(eq)) return 0;
            int n = 0;
            string[] lines = eq.Replace("\r\n", "\n").Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (line.Length == 0 || line.IndexOf('=') < 0) continue;
                n++;
                for (int c = 0; c < line.Length; c++)
                    if (line[c] == '+') n++;
            }
            return n;
        }

        private static void WriteSeedCsv(string path, List<Exp11SeedLog> logs)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("method,family,seed,canonical,structural,operator_recovery,prior_features,auto_simplify,k,pi_error,test_rmse,val_loss,dense_n,final_n,dense_e,final_e,terms,runtime_s,reason");
            for (int i = 0; i < logs.Count; i++)
            {
                Exp11SeedLog r = logs[i];
                sb.AppendFormat(CultureInfo.InvariantCulture,
                    "{0},{1},{2},{3},{4},{5},{6},{7},{8},{9},{10},{11},{12},{13},{14},{15},{16},{17},{18}\r\n",
                    r.MethodId, r.Family, r.Seed,
                    r.CanonicalSuccess ? 1 : 0, r.StructuralSuccess ? 1 : 0,
                    r.OperatorRecovery ? 1 : 0, r.PriorFeatureEngineering ? 1 : 0, r.AutoSimplify ? 1 : 0,
                    JsonNum(r.CanonicalSuccess ? r.RecoveredK : double.NaN), JsonNum(r.PiError),
                    JsonNum(r.TestRmse), JsonNum(r.FinalValLoss),
                    r.DenseNeurons, r.FinalNeurons, r.DenseConnections, r.FinalConnections,
                    r.EquationTerms,
                    r.RuntimeSeconds.ToString("G15", CultureInfo.InvariantCulture),
                    Escape(r.Reason).Replace(',', ';'));
            }
            File.WriteAllText(path, sb.ToString());
        }

        private static string BuildHeadlineNn(List<Exp11SeedLog> logs, MethodSpec[] methods)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendFormat(CultureInfo.InvariantCulture, "Exp11 NN runs={0}.\r\n", logs.Count);
            sb.AppendLine("# NN / HSM baselines (area)");
            sb.AppendLine();
            sb.AppendLine("| Method | n | Canon % | Op recover % | Mean RMSE | Mean terms | Mean final e | Prior FE | Auto simplify |");
            sb.AppendLine("|--------|---|---------|--------------|-----------|------------|--------------|----------|---------------|");
            for (int m = 0; m < methods.Length; m++)
            {
                MethodSpec spec = methods[m];
                int n = 0, can = 0, op = 0;
                double sumR = 0, sumT = 0, sumE = 0;
                int nR = 0;
                for (int i = 0; i < logs.Count; i++)
                {
                    if (logs[i].MethodId != spec.Id) continue;
                    n++;
                    if (logs[i].CanonicalSuccess) can++;
                    if (logs[i].OperatorRecovery) op++;
                    if (NnMath.IsFinite(logs[i].TestRmse)) { sumR += logs[i].TestRmse; nR++; }
                    sumT += logs[i].EquationTerms;
                    sumE += logs[i].FinalConnections;
                }
                if (n == 0) continue;
                sb.AppendFormat(CultureInfo.InvariantCulture,
                    "| {0} | {1} | {2:F0} | {3:F0} | {4:G4} | {5:F1} | {6:F1} | no | {7} |\n",
                    spec.Id, n, 100.0 * can / n, 100.0 * op / n,
                    nR > 0 ? sumR / nR : double.NaN,
                    sumT / n, sumE / n,
                    spec.DoPrune ? "yes" : "no");
            }
            return sb.ToString();
        }

        private static void Log(StringBuilder sb, string line)
        {
            sb.AppendLine(line);
            Console.WriteLine(line);
        }

        private static string JsonNum(double v)
        {
            if (!NnMath.IsFinite(v)) return "null";
            return v.ToString("G15", CultureInfo.InvariantCulture);
        }

        private static string Escape(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        private static string FindProjectRoot()
        {
            string dir = AppDomain.CurrentDomain.BaseDirectory;
            for (int i = 0; i < 6 && !string.IsNullOrEmpty(dir); i++)
            {
                if (File.Exists(Path.Combine(dir, "NnPruneHsm.sln"))
                    || File.Exists(Path.Combine(dir, "RemainingToDoExperiments.docx"))
                    || Directory.Exists(Path.Combine(dir, "NnPruneHsm")))
                    return dir;
                DirectoryInfo parent = Directory.GetParent(dir);
                if (parent == null) break;
                dir = parent.FullName;
            }
            return AppDomain.CurrentDomain.BaseDirectory;
        }
    }
}
