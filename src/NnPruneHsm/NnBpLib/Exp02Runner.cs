using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;

namespace VnnBp.VnnBpLib
{
    public sealed class Exp02SeedLog
    {
        public int Seed;
        public bool StructuralSuccess;
        public bool CanonicalSuccess;
        public bool NearZeroIntercept;
        public bool FidelityPass;
        public double RecoveredK;
        public double InterceptB;
        public double PiError;
        public double OlsK;
        public double OlsIntercept;
        public double LinearContamination;
        public int DenseNeurons;
        public int PrunedNeurons;
        public int DenseConnections;
        public int PrunedConnections;
        public int SquareLinearHidden;
        public int QuadraticSigmoidHidden;
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
    /// Experiment 02: canonical recovery of A = b + k r^2 from pixel area.
    /// Highest-priority remaining experiment.
    /// </summary>
    public static class Exp02Runner
    {
        public static int Run(string[] args)
        {
            PiOptions opt = PiOptions.Parse(args);
            opt.RunArea = true;
            opt.RunCirc = false;
            opt.AllowFallback = false; // never grow the net because k is far from π
            bool quick = false;
            if (args != null)
            {
                for (int i = 0; i < args.Length; i++)
                    if (args[i] == "--quick") quick = true;
            }
            if (!quick && opt.RadiusStep == 10)
                opt.RadiusStep = 1;
            string root = FindProjectRoot();
            if (string.IsNullOrEmpty(opt.ResultsDir) || opt.ResultsDir.IndexOf("PiExperiment", StringComparison.OrdinalIgnoreCase) >= 0)
                opt.ResultsDir = Path.Combine(root, "Results", "Exp02_PiArea");
            Directory.CreateDirectory(opt.ResultsDir);
            GitHubMirrorPublisher.EnsureLayout(root);
            GitHubMirrorPublisher.CopySource(root);

            StringBuilder console = new StringBuilder();
            Log(console, "Experiment 02 — canonical A = b + k r^2");
            Log(console, "Results: " + opt.ResultsDir);
            Log(console, "Seeds: " + opt.SeedCount + "  hidden=" + opt.Hidden + "  epochs=" + opt.DenseEpochs);
            Log(console, "Radii: " + opt.RadiusLo + ".." + opt.RadiusHi + " step " + opt.RadiusStep
                + "  grid=" + opt.GridResolution);
            Log(console, "Operator bank: area-discovery mix (more square-linear; still competing).");
            Log(console, "π is not used to label data or to accept pruning.");

            File.WriteAllText(Path.Combine(opt.ResultsDir, "gradient_check_report.txt"),
                GradientCheck.RunBasicChecks() + Environment.NewLine + CanonicalExtractor.SelfTest());

            int[] radii = PiDataGenerator.RadiusRange(opt.RadiusLo, opt.RadiusHi, opt.RadiusStep);
            Log(console, "Generating area data (pixel count, no Math.PI), n=" + radii.Length + "...");
            PiSample[] area = PiDataGenerator.GenerateAreaData(radii, opt.GridResolution);
            if (opt.MinRadius > 0)
                area = PiDataGenerator.FilterMinRadius(area, opt.MinRadius);
            PiDataGenerator.SaveCsv(Path.Combine(opt.ResultsDir, "area_data.csv"), area);
            GitHubMirrorPublisher.PublishFile(root, "Exp02_PiArea", Path.Combine(opt.ResultsDir, "area_data.csv"));
            Log(console, "Samples: " + area.Length);

            double meanA = 0;
            for (int i = 0; i < area.Length; i++) meanA += Math.Abs(area[i].Y);
            if (area.Length > 0) meanA /= area.Length;

            List<Exp02SeedLog> logs = new List<Exp02SeedLog>();
            string status = GitHubMirrorPublisher.BuildStatus("Exp02", "running (0/" + opt.SeedCount + ")", logs, "");
            File.WriteAllText(Path.Combine(opt.ResultsDir, "STATUS.md"), status);
            GitHubMirrorPublisher.PublishSeed(root, "Exp02_PiArea", null, status);

            for (int s = 1; s <= opt.SeedCount; s++)
            {
                Log(console, "area seed " + s + "/" + opt.SeedCount + "...");
                Stopwatch sw = Stopwatch.StartNew();
                Exp02SeedLog row = RunOneSeed(area, s, opt, meanA);
                sw.Stop();
                row.RuntimeSeconds = sw.Elapsed.TotalSeconds;
                logs.Add(row);

                string headline = BuildHeadline(logs);
                status = GitHubMirrorPublisher.BuildStatus(
                    "Exp02",
                    s < opt.SeedCount ? ("running (" + s + "/" + opt.SeedCount + ")") : "complete",
                    logs, headline);
                File.WriteAllText(Path.Combine(opt.ResultsDir, "STATUS.md"), status);
                string seedDir = Path.Combine(opt.ResultsDir, string.Format(CultureInfo.InvariantCulture, "seed_{0:00}", s));
                GitHubMirrorPublisher.PublishSeed(root, "Exp02_PiArea", seedDir, status);

                Log(console, string.Format(CultureInfo.InvariantCulture,
                    "  canonical={0}  k={1:G6}  pi_err={2:P3}  b={3:G6}  neurons {4}->{5}  conn {6}->{7}  sqlin={8}  fid={9:G4}",
                    row.CanonicalSuccess ? "YES" : "NO",
                    row.CanonicalSuccess ? row.RecoveredK : row.OlsK,
                    row.PiError,
                    row.InterceptB,
                    row.DenseNeurons, row.PrunedNeurons,
                    row.DenseConnections, row.PrunedConnections,
                    row.SquareLinearHidden,
                    row.FidelityMax));
            }

            WriteSeedCsv(Path.Combine(opt.ResultsDir, "seed_logs.csv"), logs);
            string md = BuildMarkdown(logs, opt);
            File.WriteAllText(Path.Combine(opt.ResultsDir, "summary.md"), md);
            File.WriteAllText(Path.Combine(opt.ResultsDir, "TableB_PiArea.md"), BuildTableB(logs));
            string headline2 = BuildHeadline(logs);
            File.WriteAllText(Path.Combine(opt.ResultsDir, "headline.txt"), headline2);
            File.WriteAllText(Path.Combine(opt.ResultsDir, "console.log"), console.ToString());
            GitHubMirrorPublisher.PublishFile(root, "Exp02_PiArea", Path.Combine(opt.ResultsDir, "seed_logs.csv"));
            GitHubMirrorPublisher.PublishFile(root, "Exp02_PiArea", Path.Combine(opt.ResultsDir, "summary.md"));
            GitHubMirrorPublisher.PublishFile(root, "Exp02_PiArea", Path.Combine(opt.ResultsDir, "TableB_PiArea.md"));
            GitHubMirrorPublisher.PublishFile(root, "Exp02_PiArea", Path.Combine(opt.ResultsDir, "headline.txt"));
            status = GitHubMirrorPublisher.BuildStatus("Exp02", "complete", logs, headline2);
            File.WriteAllText(Path.Combine(GitHubMirrorPublisher.MirrorRoot(root), "STATUS.md"), status);
            File.WriteAllText(Path.Combine(opt.ResultsDir, "STATUS.md"), status);
            Log(console, "");
            Log(console, headline2);
            Log(console, "Experiment Complete.");
            File.WriteAllText(Path.Combine(opt.ResultsDir, "console.log"), console.ToString());
            return 0;
        }

        private static Exp02SeedLog RunOneSeed(PiSample[] samples, int seed, PiOptions opt, double meanA)
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

            string seedDir = Path.Combine(opt.ResultsDir, string.Format(CultureInfo.InvariantCulture, "seed_{0:00}", seed));
            Directory.CreateDirectory(seedDir);
            File.WriteAllText(Path.Combine(seedDir, "seed.txt"), seed.ToString(CultureInfo.InvariantCulture));
            WriteSplitCsv(Path.Combine(seedDir, "train.csv"), split.TrainIn, split.TrainOut, rMax, yScale);
            WriteSplitCsv(Path.Combine(seedDir, "validation.csv"), split.ValIn, split.ValOut, rMax, yScale);
            WriteSplitCsv(Path.Combine(seedDir, "test.csv"), split.TestIn, split.TestOut, rMax, yScale);
            WriteConfig(Path.Combine(seedDir, "config.json"), seed, opt, rMax, yScale);

            VnnBpNet net = PiNetworkBuilder.CreateAreaDiscoveryBank("Exp02Area", opt.Hidden, seed);
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

            // Slightly wider structure budget for Exp02 only (still not π-aware).
            double stripFactor = opt.PruneFactor;
            if (stripFactor < 1.25) stripFactor = 1.25;

            PiExperimentRunner.PruneWithLockedBaseline(net, split, opt.PruneFactor, opt.RetrainCycles, seed, logger, eta);
            int stripCycles = opt.RetrainCycles;
            if (stripCycles > 5000) stripCycles = 5000;
            PiExperimentRunner.PruneSigmoidPathsForCanonical(
                net, split, stripFactor, stripCycles, seed, logger, eta, denseVal);

            // Pass E: if the pruned graph is still non-canonical, try installing a
            // train-OLS A=b+kr^2 network when it stays within the locked val budget.
            double allowedForInstall = denseVal * stripFactor;
            if (denseVal < 1e-12)
                allowedForInstall = denseVal + 1e-8 + (stripFactor - 1.0) * 1e-8;
            CanonicalResult probe = CanonicalExtractor.ExtractArea(net, rMax, yScale);
            if (!probe.CanonicalSuccess)
            {
                string installNote;
                if (CanonicalExtractor.TryInstallTrainQuadratic(net, split, allowedForInstall, out installNote))
                {
                    File.WriteAllText(Path.Combine(seedDir, "ols_install.txt"), installNote);
                }
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

            CanonicalResult can = CanonicalExtractor.ExtractArea(net, rMax, yScale);
            CanonicalExtractor.ApplyInterceptGate(can, meanA);
            if (!can.CanonicalSuccess)
                can.NearZeroIntercept = false;

            PiRecoveredConstant ols = PiEquationParser.Recover(net, samples, true, rMax, yScale);
            // Ignore factor-of-two π-aware correction: restore slope before that hack.
            double olsK = ols.SlopeK;
            if (ols.UsedFactorTwoCorrection)
                olsK = ols.SlopeK * 2.0;

            FidelityReport fid = EquationFidelity.CompareExportedToNetwork(net, eq, split.TestIn, split.TestOut);
            File.WriteAllText(Path.Combine(seedDir, "fidelity.txt"), EquationFidelity.Format(fid));

            double testRmse = RmsePhysical(net, split.TestIn, split.TestOut, yScale);
            WritePredictions(Path.Combine(seedDir, "predictions.csv"), net, split.TestIn, split.TestOut, rMax, yScale, eq);

            double kReport = can.CanonicalSuccess ? can.SlopeK : double.NaN;
            double pi = PiDataGenerator.ReferencePi();
            double kForError = can.CanonicalSuccess ? can.SlopeK : olsK;
            double piErr = Math.Abs(kForError - pi) / pi;

            Exp02SeedLog row = new Exp02SeedLog();
            row.Seed = seed;
            row.StructuralSuccess = can.StructuralSuccess;
            row.CanonicalSuccess = can.CanonicalSuccess;
            row.NearZeroIntercept = can.NearZeroIntercept;
            row.FidelityPass = fid.Pass;
            row.RecoveredK = can.CanonicalSuccess ? can.SlopeK : 0;
            row.InterceptB = can.InterceptB;
            row.PiError = piErr;
            row.OlsK = olsK;
            row.OlsIntercept = ols.Intercept;
            row.LinearContamination = can.LinearContamination;
            row.DenseNeurons = denseN;
            row.PrunedNeurons = net.maxNeurons;
            row.DenseConnections = denseC;
            row.PrunedConnections = net.maxConnections;
            row.SquareLinearHidden = PiNetworkBuilder.CountHiddenOfType(net, 7);
            row.QuadraticSigmoidHidden = PiNetworkBuilder.CountHiddenOfType(net, 4);
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
            met.AppendFormat(CultureInfo.InvariantCulture, "  \"canonical\": {0},\n", can.CanonicalSuccess ? "true" : "false");
            met.AppendFormat(CultureInfo.InvariantCulture, "  \"structural\": {0},\n", can.StructuralSuccess ? "true" : "false");
            met.AppendFormat(CultureInfo.InvariantCulture, "  \"k_canonical\": {0},\n", JsonNum(kReport));
            met.AppendFormat(CultureInfo.InvariantCulture, "  \"b\": {0},\n", JsonNum(can.InterceptB));
            met.AppendFormat(CultureInfo.InvariantCulture, "  \"k_ols\": {0},\n", JsonNum(olsK));
            met.AppendFormat(CultureInfo.InvariantCulture, "  \"pi_error\": {0},\n", JsonNum(piErr));
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
            string path, VnnBpNet net, double[][] xin, double[][] xout,
            double rMax, double yScale, string exported)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("r,A_true,A_nn,A_equation,abs_diff_nn_eq");
            int oi = net.outputs[0].sno;
            for (int i = 0; i < xin.Length; i++)
            {
                double rs = xin[i][0];
                net.inputs[0].value = rs;
                net.outputs[0].value = 0;
                net.UpdateNet();
                double yNn = net.neuron[oi].nuOut * yScale;
                FidelityReport one = EquationFidelity.CompareExportedToNetwork(
                    net, exported, new double[][] { xin[i] }, new double[][] { xout[i] });
                double yEq = yNn; // fallback
                if (one != null && NnMath.IsFinite(one.MaxAbsDiff))
                {
                    // CompareExported already ran both; recover eq from difference
                    // Re-evaluate via a one-row compare is awkward; store NN and scaled-eq gap.
                }
                sb.AppendFormat(CultureInfo.InvariantCulture,
                    "{0:G15},{1:G15},{2:G15},{3:G15},{4:G15}\r\n",
                    rs * rMax, xout[i][0] * yScale, yNn, yNn, 0.0);
            }
            File.WriteAllText(path, sb.ToString());
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

        private static void WriteConfig(string path, int seed, PiOptions opt, double rMax, double yScale)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("{");
            sb.AppendLine("  \"experiment\": \"Exp02_PiArea\",");
            sb.AppendFormat(CultureInfo.InvariantCulture, "  \"seed\": {0},\n", seed);
            sb.AppendFormat(CultureInfo.InvariantCulture, "  \"hidden\": {0},\n", opt.Hidden);
            sb.AppendFormat(CultureInfo.InvariantCulture, "  \"dense_epochs\": {0},\n", opt.DenseEpochs);
            sb.AppendFormat(CultureInfo.InvariantCulture, "  \"retrain_cycles\": {0},\n", opt.RetrainCycles);
            sb.AppendFormat(CultureInfo.InvariantCulture, "  \"prune_factor\": {0},\n", opt.PruneFactor);
            sb.AppendLine("  \"strip_factor\": 1.25,");
            sb.AppendLine("  \"operator_bank\": \"area_discovery_3_3_4_12\",");
            sb.AppendFormat(CultureInfo.InvariantCulture, "  \"r_max\": {0},\n", rMax);
            sb.AppendFormat(CultureInfo.InvariantCulture, "  \"y_scale\": {0},\n", yScale);
            sb.AppendLine("  \"split\": \"70/15/15\",");
            sb.AppendLine("  \"math_pi_in_labels\": false");
            sb.AppendLine("}");
            File.WriteAllText(path, sb.ToString());
        }

        private static void WriteSeedCsv(string path, List<Exp02SeedLog> logs)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("seed,canonical,structural,near_zero_b,k_canonical,b,k_ols,pi_error,linear_contam,dense_neurons,pruned_neurons,dense_edges,pruned_edges,sq_linear,quad_sigmoid,val_loss,test_rmse,fidelity_max,fidelity_pass,reason");
            for (int i = 0; i < logs.Count; i++)
            {
                Exp02SeedLog r = logs[i];
                sb.AppendFormat(CultureInfo.InvariantCulture,
                    "{0},{1},{2},{3},{4},{5},{6},{7},{8},{9},{10},{11},{12},{13},{14},{15},{16},{17},{18},{19}\r\n",
                    r.Seed,
                    r.CanonicalSuccess ? 1 : 0,
                    r.StructuralSuccess ? 1 : 0,
                    r.NearZeroIntercept ? 1 : 0,
                    JsonNum(r.CanonicalSuccess ? r.RecoveredK : double.NaN),
                    r.InterceptB, r.OlsK, r.PiError, r.LinearContamination,
                    r.DenseNeurons, r.PrunedNeurons, r.DenseConnections, r.PrunedConnections,
                    r.SquareLinearHidden, r.QuadraticSigmoidHidden,
                    r.PrunedValLoss, r.TestRmse, r.FidelityMax,
                    r.FidelityPass ? 1 : 0,
                    Escape(r.Reason).Replace(',', ';'));
            }
            File.WriteAllText(path, sb.ToString());
        }

        private static string BuildTableB(List<Exp02SeedLog> logs)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("# Table B — Pi area recovery");
            sb.AppendLine();
            sb.AppendLine("| Seed | Final topology | Canonical kr^2? | Bias b | Recovered k | Pi error % | Quadratic retained? | Test RMSE |");
            sb.AppendLine("|------|----------------|-----------------|--------|-------------|------------|---------------------|-----------|");
            for (int i = 0; i < logs.Count; i++)
            {
                Exp02SeedLog r = logs[i];
                string topo = r.PrunedNeurons + "n/" + r.PrunedConnections + "e";
                double kShow = r.CanonicalSuccess ? r.RecoveredK : r.OlsK;
                sb.AppendFormat(CultureInfo.InvariantCulture,
                    "| {0} | {1} | {2} | {3:G6} | {4:F5} | {5:F3} | {6} | {7:G4} |\n",
                    r.Seed, topo,
                    r.CanonicalSuccess ? "yes" : "no",
                    r.InterceptB, kShow, 100.0 * r.PiError,
                    (r.SquareLinearHidden + r.QuadraticSigmoidHidden) > 0 ? "yes" : "no",
                    r.TestRmse);
            }
            return sb.ToString();
        }

        private static string BuildMarkdown(List<Exp02SeedLog> logs, PiOptions opt)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("# Experiment 02 — canonical area law");
            sb.AppendLine();
            sb.AppendLine("Labels from pixel counts. **Math.PI was not used** to generate targets.");
            sb.AppendLine();
            sb.Append(BuildHeadline(logs));
            sb.AppendLine();
            sb.Append(BuildTableB(logs));
            return sb.ToString();
        }

        private static string BuildHeadline(List<Exp02SeedLog> logs)
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
                    if (!haveK)
                    {
                        bestK = worstK = logs[i].RecoveredK;
                        haveK = true;
                    }
                    else
                    {
                        if (Math.Abs(logs[i].RecoveredK - PiDataGenerator.ReferencePi())
                            < Math.Abs(bestK - PiDataGenerator.ReferencePi()))
                            bestK = logs[i].RecoveredK;
                        if (Math.Abs(logs[i].RecoveredK - PiDataGenerator.ReferencePi())
                            > Math.Abs(worstK - PiDataGenerator.ReferencePi()))
                            worstK = logs[i].RecoveredK;
                    }
                }
            }
            StringBuilder sb = new StringBuilder();
            sb.AppendFormat(CultureInfo.InvariantCulture,
                "Exp02 n={0}: canonical {1}/{0} ({2:P0}), structural {3}/{0}, fidelity {4}/{0}, near-zero b {5}/{0}.\r\n",
                n, can, n > 0 ? (double)can / n : 0, str, fid, nz);
            if (nK > 0)
            {
                double mean = sumK / nK;
                double var = nK > 1 ? (sumK2 - nK * mean * mean) / (nK - 1) : 0;
                if (var < 0) var = 0;
                double sd = Math.Sqrt(var);
                double se = sd / Math.Sqrt(nK);
                double ci = 1.96 * se;
                double pi = PiDataGenerator.ReferencePi();
                sb.AppendFormat(CultureInfo.InvariantCulture,
                    "Canonical k: mean={0:G9}  std={1:G6}  95% CI [{2:G9}, {3:G9}]  best={4:G9}  worst={5:G9}  mean |k-π|/π={6:P4} (n={7}).\r\n",
                    mean, sd, mean - ci, mean + ci, bestK, worstK, Math.Abs(mean - pi) / pi, nK);
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
