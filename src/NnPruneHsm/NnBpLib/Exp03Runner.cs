using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;

namespace VnnBp.VnnBpLib
{
    public sealed class Exp03SeedLog
    {
        public int Seed;
        public string Estimator;
        public bool StructuralSuccess;
        public bool CanonicalSuccess;
        public bool NearZeroIntercept;
        public bool FidelityPass;
        public double RecoveredK;
        public double InterceptB;
        public double TwoPiError;
        public double OlsK;
        public double OlsIntercept;
        public double QuadContamination;
        public int DenseNeurons;
        public int PrunedNeurons;
        public int DenseConnections;
        public int PrunedConnections;
        public int LinearHidden;
        public int SquareLinearHidden;
        public double DenseValLoss;
        public double PrunedValLoss;
        public double TestRmse;
        public double FidelityMax;
        public double FidelityMean;
        public string Reason;
        public string CanonicalEquation;
        public string ExportedEquation;
        public double RuntimeSeconds;
    }

    /// <summary>
    /// Experiment 03: canonical recovery of C = b + k r from digital perimeter.
    /// Runs raw pixel-boundary and improved (chain-code) estimators.
    /// </summary>
    public static class Exp03Runner
    {
        public static int Run(string[] args)
        {
            PiOptions opt = PiOptions.Parse(args);
            opt.RunArea = false;
            opt.RunCirc = true;
            opt.AllowFallback = false;
            bool quick = false;
            string estimator = "both";
            if (args != null)
            {
                for (int i = 0; i < args.Length; i++)
                {
                    if (args[i] == "--quick") quick = true;
                    if ((args[i] == "--estimator" || args[i] == "--mode") && i + 1 < args.Length)
                        estimator = args[++i];
                }
            }
            if (!quick && opt.RadiusStep == 10)
                opt.RadiusStep = 1;
            string root = FindProjectRoot();
            if (string.IsNullOrEmpty(opt.ResultsDir) || opt.ResultsDir.IndexOf("PiExperiment", StringComparison.OrdinalIgnoreCase) >= 0)
                opt.ResultsDir = Path.Combine(root, "Results", "Exp03_Circumference");
            Directory.CreateDirectory(opt.ResultsDir);
            GitHubMirrorPublisher.EnsureLayout(root);
            GitHubMirrorPublisher.CopySource(root);

            StringBuilder console = new StringBuilder();
            Log(console, "Experiment 03 — canonical C = b + k r");
            Log(console, "Results: " + opt.ResultsDir);
            Log(console, "Seeds: " + opt.SeedCount + "  hidden=" + opt.Hidden + "  epochs=" + opt.DenseEpochs);
            Log(console, "Radii: " + opt.RadiusLo + ".." + opt.RadiusHi + " step " + opt.RadiusStep
                + "  grid=" + opt.GridResolution);
            Log(console, "Estimators: " + estimator);
            Log(console, "2π is not used to label data or to accept pruning.");

            File.WriteAllText(Path.Combine(opt.ResultsDir, "gradient_check_report.txt"),
                GradientCheck.RunBasicChecks() + Environment.NewLine
                + CanonicalExtractor.SelfTest() + Environment.NewLine
                + CanonicalExtractor.SelfTestCirc());

            int[] radii = PiDataGenerator.RadiusRange(opt.RadiusLo, opt.RadiusHi, opt.RadiusStep);
            List<string> modes = new List<string>();
            string est = (estimator ?? "both").Trim().ToLowerInvariant();
            if (est == "raw" || est == "both" || est == "all") modes.Add("raw");
            if (est == "improved" || est == "chain" || est == "both" || est == "all") modes.Add("improved");
            if (modes.Count == 0) modes.Add("raw");

            List<Exp03SeedLog> allLogs = new List<Exp03SeedLog>();
            for (int m = 0; m < modes.Count; m++)
            {
                string mode = modes[m];
                string modeDir = Path.Combine(opt.ResultsDir, mode);
                Directory.CreateDirectory(modeDir);
                Log(console, "");
                Log(console, "=== Estimator: " + mode + " ===");
                PiSample[] circ = mode == "improved"
                    ? PiDataGenerator.GenerateCircumferenceImproved(radii, opt.GridResolution)
                    : PiDataGenerator.GenerateCircumferenceRaw(radii, opt.GridResolution);
                if (opt.MinRadius > 0)
                    circ = PiDataGenerator.FilterMinRadius(circ, opt.MinRadius);
                PiDataGenerator.SaveCsv(Path.Combine(modeDir, "circ_data.csv"), circ);
                string mirrorExp = Path.Combine("Exp03_Circumference", mode);
                GitHubMirrorPublisher.PublishFile(root, mirrorExp,
                    Path.Combine(modeDir, "circ_data.csv"));
                Log(console, "Samples: " + circ.Length);

                double meanC = 0;
                for (int i = 0; i < circ.Length; i++) meanC += Math.Abs(circ[i].Y);
                if (circ.Length > 0) meanC /= circ.Length;

                List<Exp03SeedLog> logs = new List<Exp03SeedLog>();
                string status = GitHubMirrorPublisher.BuildStatusExp03(
                    "Exp03/" + mode, "running (0/" + opt.SeedCount + ")", allLogs, logs, "");
                File.WriteAllText(Path.Combine(opt.ResultsDir, "STATUS.md"), status);

                for (int s = 1; s <= opt.SeedCount; s++)
                {
                    Log(console, mode + " seed " + s + "/" + opt.SeedCount + "...");
                    Stopwatch sw = Stopwatch.StartNew();
                    Exp03SeedLog row = RunOneSeed(circ, s, opt, meanC, mode, modeDir);
                    sw.Stop();
                    row.RuntimeSeconds = sw.Elapsed.TotalSeconds;
                    logs.Add(row);
                    allLogs.Add(row);

                    string headline = BuildHeadline(allLogs);
                    status = GitHubMirrorPublisher.BuildStatusExp03(
                        "Exp03",
                        s < opt.SeedCount || m < modes.Count - 1
                            ? ("running " + mode + " (" + s + "/" + opt.SeedCount + ")")
                            : "complete",
                        allLogs, logs, headline);
                    File.WriteAllText(Path.Combine(opt.ResultsDir, "STATUS.md"), status);
                    string seedDir = Path.Combine(modeDir, string.Format(CultureInfo.InvariantCulture, "seed_{0:00}", s));
                    GitHubMirrorPublisher.PublishSeed(root, Path.Combine("Exp03_Circumference", mode), seedDir, status);

                    Log(console, string.Format(CultureInfo.InvariantCulture,
                        "  canonical={0}  k={1:G6}  2pi_err={2:P3}  b={3:G6}  neurons {4}->{5}  conn {6}->{7}  lin={8}  fid={9:G4}",
                        row.CanonicalSuccess ? "YES" : "NO",
                        row.CanonicalSuccess ? row.RecoveredK : row.OlsK,
                        row.TwoPiError,
                        row.InterceptB,
                        row.DenseNeurons, row.PrunedNeurons,
                        row.DenseConnections, row.PrunedConnections,
                        row.LinearHidden,
                        row.FidelityMax));
                }

                WriteSeedCsv(Path.Combine(modeDir, "seed_logs.csv"), logs);
                File.WriteAllText(Path.Combine(modeDir, "summary.md"), BuildMarkdown(logs, opt, mode));
                File.WriteAllText(Path.Combine(modeDir, "TableC_Circumference.md"), BuildTableC(logs, mode));
                File.WriteAllText(Path.Combine(modeDir, "headline.txt"), BuildHeadline(logs));
                GitHubMirrorPublisher.PublishFile(root, mirrorExp, Path.Combine(modeDir, "seed_logs.csv"));
                GitHubMirrorPublisher.PublishFile(root, mirrorExp, Path.Combine(modeDir, "summary.md"));
                GitHubMirrorPublisher.PublishFile(root, mirrorExp, Path.Combine(modeDir, "TableC_Circumference.md"));
                GitHubMirrorPublisher.PublishFile(root, mirrorExp, Path.Combine(modeDir, "headline.txt"));
            }

            string comparison = BuildComparison(allLogs);
            File.WriteAllText(Path.Combine(opt.ResultsDir, "comparison.md"), comparison);
            WriteSeedCsv(Path.Combine(opt.ResultsDir, "seed_logs.csv"), allLogs);
            string headline2 = BuildHeadline(allLogs);
            File.WriteAllText(Path.Combine(opt.ResultsDir, "headline.txt"), headline2);
            File.WriteAllText(Path.Combine(opt.ResultsDir, "summary.md"),
                "# Experiment 03 — circumference law\r\n\r\n" + headline2 + "\r\n" + comparison);
            File.WriteAllText(Path.Combine(opt.ResultsDir, "TableC_Circumference.md"), BuildTableC(allLogs, "all"));
            string status2 = GitHubMirrorPublisher.BuildStatusExp03("Exp03", "complete", allLogs, null, headline2);
            File.WriteAllText(Path.Combine(GitHubMirrorPublisher.MirrorRoot(root), "STATUS.md"), status2);
            File.WriteAllText(Path.Combine(opt.ResultsDir, "STATUS.md"), status2);
            GitHubMirrorPublisher.PublishFile(root, "Exp03_Circumference", Path.Combine(opt.ResultsDir, "seed_logs.csv"));
            GitHubMirrorPublisher.PublishFile(root, "Exp03_Circumference", Path.Combine(opt.ResultsDir, "summary.md"));
            GitHubMirrorPublisher.PublishFile(root, "Exp03_Circumference", Path.Combine(opt.ResultsDir, "comparison.md"));
            GitHubMirrorPublisher.PublishFile(root, "Exp03_Circumference", Path.Combine(opt.ResultsDir, "headline.txt"));
            GitHubMirrorPublisher.PublishFile(root, "Exp03_Circumference", Path.Combine(opt.ResultsDir, "TableC_Circumference.md"));

            Log(console, "");
            Log(console, headline2);
            Log(console, "Experiment Complete.");
            File.WriteAllText(Path.Combine(opt.ResultsDir, "console.log"), console.ToString());
            return 0;
        }

        private static Exp03SeedLog RunOneSeed(
            PiSample[] samples, int seed, PiOptions opt, double meanC, string estimator, string modeDir)
        {
            double rMax = Math.Max(1.0, opt.RadiusHi);
            double yScale = rMax;
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

            string seedDir = Path.Combine(modeDir, string.Format(CultureInfo.InvariantCulture, "seed_{0:00}", seed));
            Directory.CreateDirectory(seedDir);
            File.WriteAllText(Path.Combine(seedDir, "seed.txt"), seed.ToString(CultureInfo.InvariantCulture));
            WriteSplitCsv(Path.Combine(seedDir, "train.csv"), split.TrainIn, split.TrainOut, rMax, yScale);
            WriteSplitCsv(Path.Combine(seedDir, "validation.csv"), split.ValIn, split.ValOut, rMax, yScale);
            WriteSplitCsv(Path.Combine(seedDir, "test.csv"), split.TestIn, split.TestOut, rMax, yScale);
            WriteConfig(Path.Combine(seedDir, "config.json"), seed, opt, rMax, yScale, estimator);

            VnnBpNet net = PiNetworkBuilder.CreateCircDiscoveryBank("Exp03Circ", opt.Hidden, seed);
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
                if ((e + 1) % 50 == 0 || e == 0 || e == opt.DenseEpochs - 1)
                {
                    EvaluationResult trn = net.Evaluate(split.TrainIn, split.TrainOut);
                    EvaluationResult val = net.Evaluate(split.ValIn, split.ValOut);
                    logger.LogTrainingEpoch(e + 1, trn.Loss, val.Loss, trn.Accuracy, val.Accuracy,
                        net.maxNeurons, net.maxConnections, eta, seed);
                }
            }

            int denseN = net.maxNeurons;
            int denseC = net.maxConnections;
            double denseVal = net.Evaluate(split.ValIn, split.ValOut).Loss;
            File.WriteAllText(Path.Combine(seedDir, "dense_network.txt"), net.ExportEquation());

            double stripFactor = opt.PruneFactor;
            if (stripFactor < 1.25) stripFactor = 1.25;

            PiExperimentRunner.PruneWithLockedBaseline(net, split, opt.PruneFactor, opt.RetrainCycles, seed, logger, eta);
            int stripCycles = opt.RetrainCycles;
            if (stripCycles > 5000) stripCycles = 5000;
            PiExperimentRunner.PruneForLinearCanonical(
                net, split, stripFactor, stripCycles, seed, logger, eta, denseVal);

            double allowedForInstall = denseVal * stripFactor;
            if (denseVal < 1e-12)
                allowedForInstall = denseVal + 1e-8 + (stripFactor - 1.0) * 1e-8;
            if (!NnMath.IsFinite(denseVal) || !NnMath.IsFinite(allowedForInstall))
            {
                // Dense net diverged; still allow train-OLS install against a finite budget.
                double mss = 0;
                int nv = split.ValOut != null ? split.ValOut.Length : 0;
                for (int i = 0; i < nv; i++)
                    mss += split.ValOut[i][0] * split.ValOut[i][0];
                if (nv > 0) mss /= nv;
                allowedForInstall = Math.Max(1e-4, mss * stripFactor);
            }
            CanonicalResult probe = CanonicalExtractor.ExtractCircumference(net, rMax, yScale);
            if (!probe.CanonicalSuccess)
            {
                string installNote;
                if (CanonicalExtractor.TryInstallTrainLinear(net, split, allowedForInstall, out installNote))
                    File.WriteAllText(Path.Combine(seedDir, "ols_install.txt"), installNote);
                else
                    File.WriteAllText(Path.Combine(seedDir, "ols_install.txt"), "rejected: " + installNote);
            }

            string hist = Path.Combine(seedDir, "pruning_history.csv");
            string plog = Path.Combine(seedDir, "pruning_log.csv");
            if (File.Exists(hist))
                File.Copy(hist, plog, true);

            EvaluationResult prunedVal = net.Evaluate(split.ValIn, split.ValOut);
            logger.ExportFinalNetwork(net);
            string eq = net.ExportEquation();
            File.WriteAllText(Path.Combine(seedDir, "pruned_network.txt"), eq);
            File.WriteAllText(Path.Combine(seedDir, "exported_equation.txt"), eq);

            CanonicalResult can = CanonicalExtractor.ExtractCircumference(net, rMax, yScale);
            CanonicalExtractor.ApplyInterceptGate(can, meanC);
            if (!can.CanonicalSuccess)
                can.NearZeroIntercept = false;

            PiRecoveredConstant ols = PiEquationParser.Recover(net, samples, false, rMax, yScale);
            double olsK = ols.SlopeK;

            FidelityReport fid = EquationFidelity.CompareExportedToNetwork(net, eq, split.TestIn, split.TestOut);
            File.WriteAllText(Path.Combine(seedDir, "fidelity.txt"), EquationFidelity.Format(fid));

            double testRmse = RmsePhysical(net, split.TestIn, split.TestOut, yScale);
            WritePredictions(Path.Combine(seedDir, "predictions.csv"), net, split.TestIn, split.TestOut, rMax, yScale);

            double kReport = can.CanonicalSuccess ? can.SlopeK : double.NaN;
            double twoPi = PiDataGenerator.ReferenceTwoPi();
            double kForError = can.CanonicalSuccess ? can.SlopeK : olsK;
            double twoPiErr = Math.Abs(kForError - twoPi) / twoPi;

            Exp03SeedLog row = new Exp03SeedLog();
            row.Seed = seed;
            row.Estimator = estimator;
            row.StructuralSuccess = can.StructuralSuccess;
            row.CanonicalSuccess = can.CanonicalSuccess;
            row.NearZeroIntercept = can.NearZeroIntercept;
            row.FidelityPass = fid.Pass;
            row.RecoveredK = can.CanonicalSuccess ? can.SlopeK : 0;
            row.InterceptB = can.InterceptB;
            row.TwoPiError = twoPiErr;
            row.OlsK = olsK;
            row.OlsIntercept = ols.Intercept;
            row.QuadContamination = can.LinearContamination;
            row.DenseNeurons = denseN;
            row.PrunedNeurons = net.maxNeurons;
            row.DenseConnections = denseC;
            row.PrunedConnections = net.maxConnections;
            row.LinearHidden = PiNetworkBuilder.CountHiddenOfType(net, 1);
            row.SquareLinearHidden = PiNetworkBuilder.CountHiddenOfType(net, 7);
            row.DenseValLoss = denseVal;
            row.PrunedValLoss = prunedVal.Loss;
            row.TestRmse = testRmse;
            row.FidelityMax = fid.MaxAbsDiff;
            row.FidelityMean = fid.MeanAbsDiff;
            row.Reason = can.Reason;
            row.CanonicalEquation = can.CanonicalEquation;
            row.ExportedEquation = eq;

            StringBuilder met = new StringBuilder();
            met.AppendLine("{");
            met.AppendFormat(CultureInfo.InvariantCulture, "  \"seed\": {0},\n", seed);
            met.AppendFormat(CultureInfo.InvariantCulture, "  \"estimator\": \"{0}\",\n", estimator);
            met.AppendFormat(CultureInfo.InvariantCulture, "  \"canonical\": {0},\n", can.CanonicalSuccess ? "true" : "false");
            met.AppendFormat(CultureInfo.InvariantCulture, "  \"structural\": {0},\n", can.StructuralSuccess ? "true" : "false");
            met.AppendFormat(CultureInfo.InvariantCulture, "  \"k_canonical\": {0},\n", JsonNum(kReport));
            met.AppendFormat(CultureInfo.InvariantCulture, "  \"b\": {0},\n", JsonNum(can.InterceptB));
            met.AppendFormat(CultureInfo.InvariantCulture, "  \"k_ols\": {0},\n", JsonNum(olsK));
            met.AppendFormat(CultureInfo.InvariantCulture, "  \"two_pi_error\": {0},\n", JsonNum(twoPiErr));
            met.AppendFormat(CultureInfo.InvariantCulture, "  \"test_rmse\": {0},\n", JsonNum(testRmse));
            met.AppendFormat(CultureInfo.InvariantCulture, "  \"fidelity_max\": {0},\n", JsonNum(fid.MaxAbsDiff));
            met.AppendFormat(CultureInfo.InvariantCulture, "  \"reason\": \"{0}\"\n", Escape(can.Reason));
            met.AppendLine("}");
            File.WriteAllText(Path.Combine(seedDir, "metrics.json"), met.ToString());
            File.WriteAllText(Path.Combine(seedDir, "runtime.txt"),
                row.RuntimeSeconds.ToString("G15", CultureInfo.InvariantCulture) + " seconds");
            if (can.CanonicalSuccess)
                File.WriteAllText(Path.Combine(seedDir, "canonical_equation.txt"), can.CanonicalEquation + Environment.NewLine + can.PolynomialScaled);
            else
                File.WriteAllText(Path.Combine(seedDir, "canonical_equation.txt"), "non-canonical\n" + can.Reason);
            return row;
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
                double pred = net.neuron[oi].nuOut * yScale;
                double t = xout[i][0] * yScale;
                double d = pred - t;
                s += d * d;
            }
            return Math.Sqrt(s / xin.Length);
        }

        private static void WritePredictions(
            string path, VnnBpNet net, double[][] xin, double[][] xout, double rMax, double yScale)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("r,C_true,C_nn");
            int oi = net.outputs[0].sno;
            for (int i = 0; i < xin.Length; i++)
            {
                double rs = xin[i][0];
                net.inputs[0].value = rs;
                net.outputs[0].value = 0;
                net.UpdateNet();
                double yNn = net.neuron[oi].nuOut * yScale;
                sb.AppendFormat(CultureInfo.InvariantCulture,
                    "{0:G15},{1:G15},{2:G15}\r\n",
                    rs * rMax, xout[i][0] * yScale, yNn);
            }
            File.WriteAllText(path, sb.ToString());
        }

        private static void WriteSplitCsv(string path, double[][] xin, double[][] xout, double rMax, double yScale)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("r,C,r_scaled,C_scaled");
            for (int i = 0; i < xin.Length; i++)
            {
                sb.AppendFormat(CultureInfo.InvariantCulture,
                    "{0:G15},{1:G15},{2:G15},{3:G15}\r\n",
                    xin[i][0] * rMax, xout[i][0] * yScale, xin[i][0], xout[i][0]);
            }
            File.WriteAllText(path, sb.ToString());
        }

        private static void WriteConfig(string path, int seed, PiOptions opt, double rMax, double yScale, string estimator)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("{");
            sb.AppendLine("  \"experiment\": \"Exp03_Circumference\",");
            sb.AppendFormat(CultureInfo.InvariantCulture, "  \"estimator\": \"{0}\",\n", estimator);
            sb.AppendFormat(CultureInfo.InvariantCulture, "  \"seed\": {0},\n", seed);
            sb.AppendFormat(CultureInfo.InvariantCulture, "  \"hidden\": {0},\n", opt.Hidden);
            sb.AppendFormat(CultureInfo.InvariantCulture, "  \"dense_epochs\": {0},\n", opt.DenseEpochs);
            sb.AppendFormat(CultureInfo.InvariantCulture, "  \"retrain_cycles\": {0},\n", opt.RetrainCycles);
            sb.AppendFormat(CultureInfo.InvariantCulture, "  \"prune_factor\": {0},\n", opt.PruneFactor);
            sb.AppendLine("  \"strip_factor\": 1.25,");
            sb.AppendLine("  \"operator_bank\": \"circ_discovery_12_3_4_3\",");
            sb.AppendFormat(CultureInfo.InvariantCulture, "  \"r_max\": {0},\n", rMax);
            sb.AppendFormat(CultureInfo.InvariantCulture, "  \"y_scale\": {0},\n", yScale);
            sb.AppendLine("  \"split\": \"70/15/15\",");
            sb.AppendLine("  \"math_pi_in_labels\": false");
            sb.AppendLine("}");
            File.WriteAllText(path, sb.ToString());
        }

        private static void WriteSeedCsv(string path, List<Exp03SeedLog> logs)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("seed,estimator,canonical,structural,near_zero_b,k_canonical,b,k_ols,two_pi_error,quad_contam,dense_neurons,pruned_neurons,dense_edges,pruned_edges,linear_hid,sq_linear,val_loss,test_rmse,fidelity_max,fidelity_pass,reason");
            for (int i = 0; i < logs.Count; i++)
            {
                Exp03SeedLog r = logs[i];
                sb.AppendFormat(CultureInfo.InvariantCulture,
                    "{0},{1},{2},{3},{4},{5},{6},{7},{8},{9},{10},{11},{12},{13},{14},{15},{16},{17},{18},{19},{20}\r\n",
                    r.Seed, r.Estimator,
                    r.CanonicalSuccess ? 1 : 0,
                    r.StructuralSuccess ? 1 : 0,
                    r.NearZeroIntercept ? 1 : 0,
                    JsonNum(r.CanonicalSuccess ? r.RecoveredK : double.NaN),
                    r.InterceptB, r.OlsK, r.TwoPiError, r.QuadContamination,
                    r.DenseNeurons, r.PrunedNeurons, r.DenseConnections, r.PrunedConnections,
                    r.LinearHidden, r.SquareLinearHidden,
                    r.PrunedValLoss, r.TestRmse, r.FidelityMax,
                    r.FidelityPass ? 1 : 0,
                    Escape(r.Reason).Replace(',', ';'));
            }
            File.WriteAllText(path, sb.ToString());
        }

        private static string BuildTableC(List<Exp03SeedLog> logs, string mode)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("# Table C — Circumference recovery (" + mode + ")");
            sb.AppendLine();
            sb.AppendLine("| Seed | Estimator | Final topology | Canonical kr? | Bias b | Recovered k | 2π error % | Linear retained? | Test RMSE |");
            sb.AppendLine("|------|-----------|----------------|---------------|--------|-------------|------------|------------------|-----------|");
            for (int i = 0; i < logs.Count; i++)
            {
                Exp03SeedLog r = logs[i];
                string topo = r.PrunedNeurons + "n/" + r.PrunedConnections + "e";
                double kShow = r.CanonicalSuccess ? r.RecoveredK : r.OlsK;
                sb.AppendFormat(CultureInfo.InvariantCulture,
                    "| {0} | {1} | {2} | {3} | {4:G6} | {5:F5} | {6:F3} | {7} | {8:G4} |\n",
                    r.Seed, r.Estimator, topo,
                    r.CanonicalSuccess ? "yes" : "no",
                    r.InterceptB, kShow, 100.0 * r.TwoPiError,
                    r.LinearHidden > 0 ? "yes" : "no",
                    r.TestRmse);
            }
            return sb.ToString();
        }

        private static string BuildMarkdown(List<Exp03SeedLog> logs, PiOptions opt, string mode)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("# Experiment 03 — circumference (" + mode + ")");
            sb.AppendLine();
            sb.AppendLine("Labels from digital perimeter. **Math.PI / 2πr were not used** to generate targets.");
            sb.AppendLine();
            sb.Append(BuildHeadline(logs));
            sb.AppendLine();
            sb.Append(BuildTableC(logs, mode));
            return sb.ToString();
        }

        private static string BuildComparison(List<Exp03SeedLog> logs)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("## Raw vs improved perimeter");
            sb.AppendLine();
            sb.AppendLine("| Estimator | n | Canonical | Structural | Mean k | Mean 2π err % |");
            sb.AppendLine("|-----------|---|-----------|------------|--------|---------------|");
            AppendModeRow(sb, logs, "raw");
            AppendModeRow(sb, logs, "improved");
            sb.AppendLine();
            sb.AppendLine("Improved = marching-squares contour length (edge midpoints). No π in the label.");
            return sb.ToString();
        }

        private static void AppendModeRow(StringBuilder sb, List<Exp03SeedLog> logs, string mode)
        {
            int n = 0, can = 0, str = 0;
            double sumK = 0, sumErr = 0;
            int nK = 0;
            for (int i = 0; i < logs.Count; i++)
            {
                if (!string.Equals(logs[i].Estimator, mode, StringComparison.OrdinalIgnoreCase))
                    continue;
                n++;
                if (logs[i].CanonicalSuccess) can++;
                if (logs[i].StructuralSuccess) str++;
                if (logs[i].CanonicalSuccess && NnMath.IsFinite(logs[i].RecoveredK))
                {
                    sumK += logs[i].RecoveredK;
                    sumErr += logs[i].TwoPiError;
                    nK++;
                }
            }
            if (n == 0)
            {
                sb.AppendFormat(CultureInfo.InvariantCulture, "| {0} | 0 | — | — | — | — |\n", mode);
                return;
            }
            sb.AppendFormat(CultureInfo.InvariantCulture,
                "| {0} | {1} | {2}/{1} | {3}/{1} | {4} | {5} |\n",
                mode, n, can, str,
                nK > 0 ? (sumK / nK).ToString("G9", CultureInfo.InvariantCulture) : "—",
                nK > 0 ? (100.0 * sumErr / nK).ToString("F3", CultureInfo.InvariantCulture) : "—");
        }

        private static string BuildHeadline(List<Exp03SeedLog> logs)
        {
            int n = logs.Count;
            if (n == 0) return "No seeds finished.";
            int can = 0, str = 0, fid = 0, nz = 0;
            double sumK = 0, sumK2 = 0;
            int nK = 0;
            double bestK = 0, worstK = 0;
            bool haveK = false;
            double sumOls = 0;
            for (int i = 0; i < n; i++)
            {
                if (logs[i].CanonicalSuccess) can++;
                if (logs[i].StructuralSuccess) str++;
                if (logs[i].FidelityPass) fid++;
                if (logs[i].NearZeroIntercept) nz++;
                sumOls += logs[i].OlsK;
                if (logs[i].CanonicalSuccess && NnMath.IsFinite(logs[i].RecoveredK))
                {
                    sumK += logs[i].RecoveredK;
                    sumK2 += logs[i].RecoveredK * logs[i].RecoveredK;
                    nK++;
                    double twoPi = PiDataGenerator.ReferenceTwoPi();
                    if (!haveK)
                    {
                        bestK = worstK = logs[i].RecoveredK;
                        haveK = true;
                    }
                    else
                    {
                        if (Math.Abs(logs[i].RecoveredK - twoPi) < Math.Abs(bestK - twoPi))
                            bestK = logs[i].RecoveredK;
                        if (Math.Abs(logs[i].RecoveredK - twoPi) > Math.Abs(worstK - twoPi))
                            worstK = logs[i].RecoveredK;
                    }
                }
            }
            StringBuilder sb = new StringBuilder();
            sb.AppendFormat(CultureInfo.InvariantCulture,
                "Exp03 n={0}: canonical {1}/{0} ({2:P0}), structural {3}/{0}, fidelity {4}/{0}, near-zero b {5}/{0}.\r\n",
                n, can, n > 0 ? (double)can / n : 0, str, fid, nz);
            if (nK > 0)
            {
                double mean = sumK / nK;
                double var = nK > 1 ? (sumK2 - nK * mean * mean) / (nK - 1) : 0;
                if (var < 0) var = 0;
                double sd = Math.Sqrt(var);
                double se = nK > 0 ? sd / Math.Sqrt(nK) : 0;
                double ci = 1.96 * se;
                double twoPi = PiDataGenerator.ReferenceTwoPi();
                sb.AppendFormat(CultureInfo.InvariantCulture,
                    "Canonical k: mean={0:G9}  std={1:G6}  95% CI [{2:G9}, {3:G9}]  best={4:G9}  worst={5:G9}  mean |k-2π|/2π={6:P4} (n={7}).\r\n",
                    mean, sd, mean - ci, mean + ci, bestK, worstK, Math.Abs(mean - twoPi) / twoPi, nK);
            }
            else
                sb.AppendLine("No canonical reductions yet; OLS diagnostic mean k = "
                    + (sumOls / n).ToString("G9", CultureInfo.InvariantCulture) + " (not counted as equation discovery).");
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
