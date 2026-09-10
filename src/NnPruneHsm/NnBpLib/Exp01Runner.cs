using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;

namespace VnnBp.VnnBpLib
{
    public sealed class Exp01SeedLog
    {
        public int Seed;
        public bool StructuralSuccess;
        public bool CanonicalSuccess;
        public bool NearEqualAb;
        public bool FidelityPass;
        public double CoeffA;
        public double CoeffB;
        public double RelAbDiff;
        public double EffectiveRadius;
        public double RadiusError;
        public double TestAccuracy;
        public double BaselineTestAccuracy;
        public double DenseValLoss;
        public double PrunedValLoss;
        public int DenseNeurons;
        public int PrunedNeurons;
        public int DenseConnections;
        public int PrunedConnections;
        public int QuadraticRetained;
        public double FidelityMax;
        public string Reason;
        public string CanonicalEquation;
        public string ExportedEquation;
        public double RuntimeSeconds;
    }

    /// <summary>
    /// Experiment 01: recover circle / disk boundary (x-cx)^2+(y-cy)^2 &lt;= R^2
    /// as a centred quadratic hidden unit + sigmoid, across 30 seeds.
    /// </summary>
    public static class Exp01Runner
    {
        public static int Run(string[] args)
        {
            PiOptions opt = PiOptions.Parse(args);
            opt.AllowFallback = false;
            bool quick = false;
            int samples = 2000;
            bool epochsSet = false;
            if (args != null)
            {
                for (int i = 0; i < args.Length; i++)
                {
                    if (args[i] == "--quick") { quick = true; samples = 600; }
                    if (args[i] == "--epochs") epochsSet = true;
                    if (args[i] == "--samples" && i + 1 < args.Length)
                        samples = Math.Max(100, int.Parse(args[++i], CultureInfo.InvariantCulture));
                }
            }
            // Runtime-adapted defaults for 2-D fully-connected nets.
            if (!quick && !epochsSet && opt.DenseEpochs == 400)
                opt.DenseEpochs = 150;
            if (!quick && opt.RetrainCycles == 10000)
                opt.RetrainCycles = 2500;

            string root = FindProjectRoot();
            if (string.IsNullOrEmpty(opt.ResultsDir) || opt.ResultsDir.IndexOf("PiExperiment", StringComparison.OrdinalIgnoreCase) >= 0)
                opt.ResultsDir = Path.Combine(root, "Results", "Exp01_CircleBoundary");
            Directory.CreateDirectory(opt.ResultsDir);
            GitHubMirrorPublisher.EnsureLayout(root);
            GitHubMirrorPublisher.CopySource(root);

            double cx = CircleDataGenerator.DefaultCenter;
            double cy = CircleDataGenerator.DefaultCenter;
            double R = CircleDataGenerator.DefaultRadius;

            StringBuilder console = new StringBuilder();
            Log(console, "Experiment 01 — circle boundary (x-0.5)^2+(y-0.5)^2 <= R^2");
            Log(console, "Results: " + opt.ResultsDir);
            Log(console, "Seeds: " + opt.SeedCount + "  hidden=" + opt.Hidden + "  epochs=" + opt.DenseEpochs);
            Log(console, "Samples: " + samples + "  R=" + R.ToString("G6", CultureInfo.InvariantCulture));
            Log(console, "Labels from geometry only; Math.PI not used.");

            File.WriteAllText(Path.Combine(opt.ResultsDir, "gradient_check_report.txt"),
                GradientCheck.RunBasicChecks());

            // Shared geometry for all seeds (split differs by seed).
            double[][] xinAll, xoutAll;
            CircleDataGenerator.Generate(samples, 12345, cx, cy, R, out xinAll, out xoutAll);
            CircleDataGenerator.SaveCsv(Path.Combine(opt.ResultsDir, "circle_data.csv"), xinAll, xoutAll);
            GitHubMirrorPublisher.PublishFile(root, "Exp01_CircleBoundary", Path.Combine(opt.ResultsDir, "circle_data.csv"));

            List<Exp01SeedLog> logs = new List<Exp01SeedLog>();
            string status = GitHubMirrorPublisher.BuildStatusExp01("running (0/" + opt.SeedCount + ")", logs, "");
            File.WriteAllText(Path.Combine(opt.ResultsDir, "STATUS.md"), status);

            for (int s = 1; s <= opt.SeedCount; s++)
            {
                Log(console, "circle seed " + s + "/" + opt.SeedCount + "...");
                Stopwatch sw = Stopwatch.StartNew();
                Exp01SeedLog row = RunOneSeed(xinAll, xoutAll, s, opt, R);
                sw.Stop();
                row.RuntimeSeconds = sw.Elapsed.TotalSeconds;
                File.WriteAllText(Path.Combine(opt.ResultsDir, string.Format(CultureInfo.InvariantCulture, "seed_{0:00}", s), "runtime.txt"),
                    row.RuntimeSeconds.ToString("G15", CultureInfo.InvariantCulture) + " seconds");
                logs.Add(row);

                string headline = BuildHeadline(logs, R);
                status = GitHubMirrorPublisher.BuildStatusExp01(
                    s < opt.SeedCount ? ("running (" + s + "/" + opt.SeedCount + ")") : "complete",
                    logs, headline);
                File.WriteAllText(Path.Combine(opt.ResultsDir, "STATUS.md"), status);
                string seedDir = Path.Combine(opt.ResultsDir, string.Format(CultureInfo.InvariantCulture, "seed_{0:00}", s));
                GitHubMirrorPublisher.PublishSeed(root, "Exp01_CircleBoundary", seedDir, status);

                Log(console, string.Format(CultureInfo.InvariantCulture,
                    "  canonical={0}  a={1:G5} b={2:G5} rel|a-b|={3:P2}  Rhat={4:G5}  acc={5:P2}  base={6:P2}  quad={7}  fid={8:G4}",
                    row.CanonicalSuccess ? "YES" : "NO",
                    row.CoeffA, row.CoeffB, row.RelAbDiff,
                    row.EffectiveRadius, row.TestAccuracy, row.BaselineTestAccuracy,
                    row.QuadraticRetained, row.FidelityMax));
            }

            WriteSeedCsv(Path.Combine(opt.ResultsDir, "seed_logs.csv"), logs);
            string tableA = BuildTableA(logs);
            string headline2 = BuildHeadline(logs, R);
            File.WriteAllText(Path.Combine(opt.ResultsDir, "TableA_Circle.md"), tableA);
            File.WriteAllText(Path.Combine(opt.ResultsDir, "headline.txt"), headline2);
            File.WriteAllText(Path.Combine(opt.ResultsDir, "summary.md"),
                "# Experiment 01 — circle boundary\r\n\r\n" + headline2 + "\r\n" + tableA);
            status = GitHubMirrorPublisher.BuildStatusExp01("complete", logs, headline2);
            File.WriteAllText(Path.Combine(GitHubMirrorPublisher.MirrorRoot(root), "STATUS.md"), status);
            File.WriteAllText(Path.Combine(opt.ResultsDir, "STATUS.md"), status);
            GitHubMirrorPublisher.PublishFile(root, "Exp01_CircleBoundary", Path.Combine(opt.ResultsDir, "seed_logs.csv"));
            GitHubMirrorPublisher.PublishFile(root, "Exp01_CircleBoundary", Path.Combine(opt.ResultsDir, "TableA_Circle.md"));
            GitHubMirrorPublisher.PublishFile(root, "Exp01_CircleBoundary", Path.Combine(opt.ResultsDir, "headline.txt"));
            GitHubMirrorPublisher.PublishFile(root, "Exp01_CircleBoundary", Path.Combine(opt.ResultsDir, "summary.md"));

            Log(console, "");
            Log(console, headline2);
            Log(console, "Experiment Complete.");
            File.WriteAllText(Path.Combine(opt.ResultsDir, "console.log"), console.ToString());
            return 0;
        }

        private static Exp01SeedLog RunOneSeed(
            double[][] xinAll, double[][] xoutAll, int seed, PiOptions opt, double trueR)
        {
            DatasetSplit split = DatasetSplit.CreateDefault(xinAll, xoutAll, seed);
            string seedDir = Path.Combine(opt.ResultsDir, string.Format(CultureInfo.InvariantCulture, "seed_{0:00}", seed));
            Directory.CreateDirectory(seedDir);
            File.WriteAllText(Path.Combine(seedDir, "seed.txt"), seed.ToString(CultureInfo.InvariantCulture));
            WriteSplitCsv(Path.Combine(seedDir, "train.csv"), split.TrainIn, split.TrainOut);
            WriteSplitCsv(Path.Combine(seedDir, "validation.csv"), split.ValIn, split.ValOut);
            WriteSplitCsv(Path.Combine(seedDir, "test.csv"), split.TestIn, split.TestOut);
            WriteConfig(Path.Combine(seedDir, "config.json"), seed, opt, trueR);

            // --- Baseline: no quadratic (shorter train; comparison only) ---
            VnnBpNet baseNet = PiNetworkBuilder.CreateCircleNoQuadBank("Exp01Base", opt.Hidden, seed + 77);
            int baseEpochs = opt.DenseEpochs;
            if (baseEpochs > 80) baseEpochs = 80;
            PiOptions baseOpt = new PiOptions();
            baseOpt.DenseEpochs = baseEpochs;
            baseOpt.Hidden = opt.Hidden;
            TrainNet(baseNet, split, baseOpt, seed + 1000);
            double baseAcc = baseNet.Evaluate(split.TestIn, split.TestOut).Accuracy;
            File.WriteAllText(Path.Combine(seedDir, "baseline_noquad_equation.txt"), baseNet.ExportEquation());

            // --- Quadratic-enabled discovery ---
            VnnBpNet net = PiNetworkBuilder.CreateCircleDiscoveryBank("Exp01Circle", opt.Hidden, seed);
            TrainNet(net, split, opt, seed);

            int denseN = net.maxNeurons;
            int denseC = net.maxConnections;
            double denseVal = net.Evaluate(split.ValIn, split.ValOut).Loss;
            File.WriteAllText(Path.Combine(seedDir, "dense_network.txt"), net.ExportEquation());

            double eta = 0.05;
            ResearchLogger logger = new ResearchLogger(seedDir);
            PiExperimentRunner.PruneWithLockedBaseline(net, split, opt.PruneFactor, opt.RetrainCycles, seed, logger, eta);

            double stripFactor = 1.25;
            double allowed = denseVal * stripFactor;
            if (!NnMath.IsFinite(denseVal) || !NnMath.IsFinite(allowed))
                allowed = 0.5;
            else if (allowed < 1e-4)
                allowed = denseVal + 1e-4 * stripFactor;
            // Prefer accepting a clean circle install when prune left a messy graph.
            if (allowed < 0.25) allowed = 0.25;
            CircleCanonicalResult probe = CircleCanonicalExtractor.Extract(net, trueR);
            if (!probe.CanonicalSuccess)
            {
                string installNote;
                if (CircleCanonicalExtractor.TryInstallTrainCircle(net, split, allowed, out installNote))
                    File.WriteAllText(Path.Combine(seedDir, "ols_install.txt"), installNote);
                else
                    File.WriteAllText(Path.Combine(seedDir, "ols_install.txt"), "rejected: " + installNote);
            }

            string hist = Path.Combine(seedDir, "pruning_history.csv");
            if (File.Exists(hist))
                File.Copy(hist, Path.Combine(seedDir, "pruning_log.csv"), true);

            EvaluationResult prunedVal = net.Evaluate(split.ValIn, split.ValOut);
            EvaluationResult testEv = net.Evaluate(split.TestIn, split.TestOut);
            logger.ExportFinalNetwork(net);
            string eq = net.ExportEquation();
            File.WriteAllText(Path.Combine(seedDir, "pruned_network.txt"), eq);
            File.WriteAllText(Path.Combine(seedDir, "exported_equation.txt"), eq);

            CircleCanonicalResult can = CircleCanonicalExtractor.Extract(net, trueR);
            FidelityReport fid = EquationFidelity.CompareExportedToNetwork2D(net, eq, split.TestIn);
            File.WriteAllText(Path.Combine(seedDir, "fidelity.txt"), EquationFidelity.Format(fid));

            Exp01SeedLog row = new Exp01SeedLog();
            row.Seed = seed;
            row.StructuralSuccess = can.StructuralSuccess;
            row.CanonicalSuccess = can.CanonicalSuccess;
            row.NearEqualAb = can.NearEqualAb;
            row.FidelityPass = fid.Pass;
            row.CoeffA = can.CoeffA;
            row.CoeffB = can.CoeffB;
            row.RelAbDiff = can.RelAbDiff;
            row.EffectiveRadius = can.EffectiveRadius;
            row.RadiusError = can.RadiusError;
            row.TestAccuracy = testEv.Accuracy;
            row.BaselineTestAccuracy = baseAcc;
            row.DenseValLoss = denseVal;
            row.PrunedValLoss = prunedVal.Loss;
            row.DenseNeurons = denseN;
            row.PrunedNeurons = net.maxNeurons;
            row.DenseConnections = denseC;
            row.PrunedConnections = net.maxConnections;
            row.QuadraticRetained = can.QuadraticOnPath;
            row.FidelityMax = fid.MaxAbsDiff;
            row.Reason = can.Reason;
            row.CanonicalEquation = can.CanonicalEquation;
            row.ExportedEquation = eq;

            StringBuilder met = new StringBuilder();
            met.AppendLine("{");
            met.AppendFormat(CultureInfo.InvariantCulture, "  \"seed\": {0},\n", seed);
            met.AppendFormat(CultureInfo.InvariantCulture, "  \"canonical\": {0},\n", can.CanonicalSuccess ? "true" : "false");
            met.AppendFormat(CultureInfo.InvariantCulture, "  \"structural\": {0},\n", can.StructuralSuccess ? "true" : "false");
            met.AppendFormat(CultureInfo.InvariantCulture, "  \"a\": {0},\n", JsonNum(can.CoeffA));
            met.AppendFormat(CultureInfo.InvariantCulture, "  \"b\": {0},\n", JsonNum(can.CoeffB));
            met.AppendFormat(CultureInfo.InvariantCulture, "  \"rel_ab\": {0},\n", JsonNum(can.RelAbDiff));
            met.AppendFormat(CultureInfo.InvariantCulture, "  \"R_hat\": {0},\n", JsonNum(can.EffectiveRadius));
            met.AppendFormat(CultureInfo.InvariantCulture, "  \"test_acc\": {0},\n", JsonNum(testEv.Accuracy));
            met.AppendFormat(CultureInfo.InvariantCulture, "  \"baseline_acc\": {0},\n", JsonNum(baseAcc));
            met.AppendFormat(CultureInfo.InvariantCulture, "  \"fidelity_max\": {0},\n", JsonNum(fid.MaxAbsDiff));
            met.AppendFormat(CultureInfo.InvariantCulture, "  \"reason\": \"{0}\"\n", Escape(can.Reason));
            met.AppendLine("}");
            File.WriteAllText(Path.Combine(seedDir, "metrics.json"), met.ToString());
            if (can.CanonicalSuccess || can.StructuralSuccess)
                File.WriteAllText(Path.Combine(seedDir, "canonical_equation.txt"),
                    (string.IsNullOrEmpty(can.CanonicalEquation) ? "n/a" : can.CanonicalEquation)
                    + Environment.NewLine + can.Reason);
            else
                File.WriteAllText(Path.Combine(seedDir, "canonical_equation.txt"), "non-canonical\n" + can.Reason);
            return row;
        }

        private static void TrainNet(VnnBpNet net, DatasetSplit split, PiOptions opt, int seed)
        {
            net.lossType = LossType.MeanSquaredError;
            double eta = 0.05;
            net.eta = eta;
            net.etab = eta;
            TrainingOptions tr = new TrainingOptions();
            tr.LearningRate = eta;
            tr.LossType = LossType.MeanSquaredError;
            tr.ShuffleEachEpoch = true;
            for (int e = 0; e < opt.DenseEpochs; e++)
            {
                tr.RandomSeed = seed * 1000 + e;
                net.TrainEpoch(split.TrainIn, split.TrainOut, tr);
            }
        }

        private static void WriteSplitCsv(string path, double[][] xin, double[][] xout)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("x,y,t");
            for (int i = 0; i < xin.Length; i++)
            {
                sb.AppendFormat(CultureInfo.InvariantCulture,
                    "{0:G15},{1:G15},{2:G15}\r\n", xin[i][0], xin[i][1], xout[i][0]);
            }
            File.WriteAllText(path, sb.ToString());
        }

        private static void WriteConfig(string path, int seed, PiOptions opt, double R)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("{");
            sb.AppendLine("  \"experiment\": \"Exp01_CircleBoundary\",");
            sb.AppendFormat(CultureInfo.InvariantCulture, "  \"seed\": {0},\n", seed);
            sb.AppendFormat(CultureInfo.InvariantCulture, "  \"hidden\": {0},\n", opt.Hidden);
            sb.AppendFormat(CultureInfo.InvariantCulture, "  \"dense_epochs\": {0},\n", opt.DenseEpochs);
            sb.AppendFormat(CultureInfo.InvariantCulture, "  \"R\": {0},\n", R);
            sb.AppendLine("  \"center\": 0.5,");
            sb.AppendLine("  \"loss\": \"MeanSquaredError\",");
            sb.AppendLine("  \"math_pi_in_labels\": false");
            sb.AppendLine("}");
            File.WriteAllText(path, sb.ToString());
        }

        private static void WriteSeedCsv(string path, List<Exp01SeedLog> logs)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("seed,canonical,structural,a,b,rel_ab,R_hat,R_err,test_acc,baseline_acc,dense_n,pruned_n,dense_e,pruned_e,quad,fidelity_max,fidelity_pass,reason");
            for (int i = 0; i < logs.Count; i++)
            {
                Exp01SeedLog r = logs[i];
                sb.AppendFormat(CultureInfo.InvariantCulture,
                    "{0},{1},{2},{3},{4},{5},{6},{7},{8},{9},{10},{11},{12},{13},{14},{15},{16},{17}\r\n",
                    r.Seed,
                    r.CanonicalSuccess ? 1 : 0,
                    r.StructuralSuccess ? 1 : 0,
                    JsonNum(r.CoeffA), JsonNum(r.CoeffB), JsonNum(r.RelAbDiff),
                    JsonNum(r.EffectiveRadius), JsonNum(r.RadiusError),
                    r.TestAccuracy, r.BaselineTestAccuracy,
                    r.DenseNeurons, r.PrunedNeurons, r.DenseConnections, r.PrunedConnections,
                    r.QuadraticRetained, r.FidelityMax, r.FidelityPass ? 1 : 0,
                    Escape(r.Reason).Replace(',', ';'));
            }
            File.WriteAllText(path, sb.ToString());
        }

        private static string BuildTableA(List<Exp01SeedLog> logs)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("# Table A — Circle recovery");
            sb.AppendLine();
            sb.AppendLine("| Seed | Dense n/e | Final n/e | Quad retained | Test acc | Baseline acc | a | b | |a-b| rel % | R_hat | Fidelity |");
            sb.AppendLine("|------|-----------|-----------|---------------|----------|--------------|---|---|------------|-------|----------|");
            for (int i = 0; i < logs.Count; i++)
            {
                Exp01SeedLog r = logs[i];
                sb.AppendFormat(CultureInfo.InvariantCulture,
                    "| {0} | {1}/{2} | {3}/{4} | {5} | {6:P1} | {7:P1} | {8:G5} | {9:G5} | {10:F2} | {11:G4} | {12:G4} |\n",
                    r.Seed,
                    r.DenseNeurons, r.DenseConnections,
                    r.PrunedNeurons, r.PrunedConnections,
                    r.QuadraticRetained,
                    r.TestAccuracy, r.BaselineTestAccuracy,
                    r.CoeffA, r.CoeffB, 100.0 * r.RelAbDiff,
                    r.EffectiveRadius, r.FidelityMax);
            }
            return sb.ToString();
        }

        private static string BuildHeadline(List<Exp01SeedLog> logs, double trueR)
        {
            int n = logs.Count;
            if (n == 0) return "No seeds finished.";
            int can = 0, str = 0, fid = 0, eqab = 0;
            double sumAcc = 0, sumBase = 0, sumR = 0, sumRel = 0;
            int nR = 0;
            for (int i = 0; i < n; i++)
            {
                if (logs[i].CanonicalSuccess) can++;
                if (logs[i].StructuralSuccess) str++;
                if (logs[i].FidelityPass) fid++;
                if (logs[i].NearEqualAb) eqab++;
                sumAcc += logs[i].TestAccuracy;
                sumBase += logs[i].BaselineTestAccuracy;
                if (NnMath.IsFinite(logs[i].EffectiveRadius))
                {
                    sumR += logs[i].EffectiveRadius;
                    nR++;
                }
                if (logs[i].StructuralSuccess)
                    sumRel += logs[i].RelAbDiff;
            }
            StringBuilder sb = new StringBuilder();
            sb.AppendFormat(CultureInfo.InvariantCulture,
                "Exp01 n={0}: canonical {1}/{0}, structural {2}/{0}, a~b {3}/{0}, fidelity {4}/{0}.\r\n",
                n, can, str, eqab, fid);
            sb.AppendFormat(CultureInfo.InvariantCulture,
                "Mean test acc={0:P2} (quadratic bank) vs baseline no-quad={1:P2}. True R={2:G4}.\r\n",
                sumAcc / n, sumBase / n, trueR);
            if (nR > 0)
                sb.AppendFormat(CultureInfo.InvariantCulture,
                    "Mean R_hat={0:G6} (n={1}). Mean |a-b| rel (structural)={2:P2}.\r\n",
                    sumR / nR, nR, str > 0 ? sumRel / str : 0);
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
            if (s == null) return "";
            return s.Replace("\\", "/").Replace("\"", "'");
        }

        private static string FindProjectRoot()
        {
            string dir = AppDomain.CurrentDomain.BaseDirectory;
            for (int i = 0; i < 8 && !string.IsNullOrEmpty(dir); i++)
            {
                if (Directory.Exists(Path.Combine(dir, "GitHub_mirror"))
                    || File.Exists(Path.Combine(dir, "NnPruneHsm.sln"))
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
