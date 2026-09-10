using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;

namespace VnnBp.VnnBpLib
{
    public sealed class Exp04SeedLog
    {
        public int ObservationCount;
        public int Seed;
        public bool StructuralSuccess;
        public bool CanonicalSuccess;
        public bool NearZeroIntercept;
        public bool FidelityPass;
        public double RecoveredK;
        public double InterceptB;
        public double PiError;
        public double OlsK;
        public double LinearContamination;
        public int DenseNeurons;
        public int PrunedNeurons;
        public int DenseConnections;
        public int PrunedConnections;
        public int SquareLinearHidden;
        public double TestRmse;
        public double FidelityMax;
        public string Reason;
        public string CanonicalEquation;
        public double RuntimeSeconds;
    }

    /// <summary>
    /// Experiment 04: progressive stochastic recovery of k_N → π.
    /// For each observation depth N, Monte-Carlo area labels (no Math.PI) feed the
    /// same Exp02-style discovery pipeline; report mean/std/CI of recovered k.
    /// </summary>
    public static class Exp04Runner
    {
        public static int Run(string[] args)
        {
            PiOptions opt = PiOptions.Parse(args);
            opt.RunArea = true;
            opt.RunCirc = false;
            opt.AllowFallback = false;
            bool quick = false;
            bool seedsSet = false;
            string depthsArg = null;
            if (args != null)
            {
                for (int i = 0; i < args.Length; i++)
                {
                    if (args[i] == "--quick") quick = true;
                    if (args[i] == "--seeds") seedsSet = true;
                    if ((args[i] == "--depths" || args[i] == "--N") && i + 1 < args.Length)
                        depthsArg = args[++i];
                }
            }
            // Runtime-adapted defaults: coarser radii than Exp02; 15 seeds unless set.
            if (!quick && opt.RadiusStep == 10)
                opt.RadiusStep = 5;
            if (!quick && !seedsSet)
                opt.SeedCount = 15;

            int[] depths = ParseDepths(depthsArg, quick);
            string root = FindProjectRoot();
            if (string.IsNullOrEmpty(opt.ResultsDir) || opt.ResultsDir.IndexOf("PiExperiment", StringComparison.OrdinalIgnoreCase) >= 0)
                opt.ResultsDir = Path.Combine(root, "Results", "Exp04_StochasticPi");
            Directory.CreateDirectory(opt.ResultsDir);
            GitHubMirrorPublisher.EnsureLayout(root);
            GitHubMirrorPublisher.CopySource(root);

            StringBuilder console = new StringBuilder();
            Log(console, "Experiment 04 — stochastic k_N -> pi");
            Log(console, "Results: " + opt.ResultsDir);
            Log(console, "Seeds/depth: " + opt.SeedCount + "  hidden=" + opt.Hidden + "  epochs=" + opt.DenseEpochs);
            Log(console, "Radii: " + opt.RadiusLo + ".." + opt.RadiusHi + " step " + opt.RadiusStep);
            Log(console, "Observation depths N: " + JoinInts(depths));
            Log(console, "Labels: Monte Carlo hits in [-r,r]^2 * 4 r^2 / N. pi is not used.");

            File.WriteAllText(Path.Combine(opt.ResultsDir, "gradient_check_report.txt"),
                GradientCheck.RunBasicChecks() + Environment.NewLine + CanonicalExtractor.SelfTest());

            int[] radii = PiDataGenerator.RadiusRange(opt.RadiusLo, opt.RadiusHi, opt.RadiusStep);
            List<Exp04SeedLog> allLogs = new List<Exp04SeedLog>();

            for (int d = 0; d < depths.Length; d++)
            {
                int N = depths[d];
                string depthDir = Path.Combine(opt.ResultsDir, string.Format(CultureInfo.InvariantCulture, "N_{0}", N));
                Directory.CreateDirectory(depthDir);
                Log(console, "");
                Log(console, "=== N = " + N + " ===");

                // Shared geometry stream seed for this depth (seeded per trial below).
                List<Exp04SeedLog> depthLogs = new List<Exp04SeedLog>();
                string status = GitHubMirrorPublisher.BuildStatusExp04(
                    "running N=" + N + " (0/" + opt.SeedCount + ")", allLogs, depthLogs, "");
                File.WriteAllText(Path.Combine(opt.ResultsDir, "STATUS.md"), status);

                for (int s = 1; s <= opt.SeedCount; s++)
                {
                    Log(console, "N=" + N + " seed " + s + "/" + opt.SeedCount + "...");
                    PiSample[] area = PiDataGenerator.GenerateStochasticAreaData(radii, N, s);
                    if (opt.MinRadius > 0)
                        area = PiDataGenerator.FilterMinRadius(area, opt.MinRadius);
                    PiDataGenerator.SaveCsv(Path.Combine(depthDir,
                        string.Format(CultureInfo.InvariantCulture, "area_data_seed{0:00}.csv", s)), area);

                    double meanA = 0;
                    for (int i = 0; i < area.Length; i++) meanA += Math.Abs(area[i].Y);
                    if (area.Length > 0) meanA /= area.Length;

                    string seedDir = Path.Combine(depthDir, string.Format(CultureInfo.InvariantCulture, "seed_{0:00}", s));
                    Stopwatch sw = Stopwatch.StartNew();
                    Exp04SeedLog row = RunOneSeed(area, s, N, opt, meanA, seedDir);
                    sw.Stop();
                    row.RuntimeSeconds = sw.Elapsed.TotalSeconds;
                    File.WriteAllText(Path.Combine(seedDir, "runtime.txt"),
                        row.RuntimeSeconds.ToString("G15", CultureInfo.InvariantCulture) + " seconds");
                    depthLogs.Add(row);
                    allLogs.Add(row);

                    string headline = BuildHeadline(allLogs);
                    bool more = s < opt.SeedCount || d < depths.Length - 1;
                    status = GitHubMirrorPublisher.BuildStatusExp04(
                        more
                            ? ("running N=" + N + " (" + s + "/" + opt.SeedCount + ")")
                            : "complete",
                        allLogs, depthLogs, headline);
                    File.WriteAllText(Path.Combine(opt.ResultsDir, "STATUS.md"), status);
                    GitHubMirrorPublisher.PublishSeed(root, Path.Combine("Exp04_StochasticPi", "N_" + N), seedDir, status);

                    Log(console, string.Format(CultureInfo.InvariantCulture,
                        "  canonical={0}  k={1:G6}  pi_err={2:P3}  b={3:G6}  neurons {4}->{5}  fid={6:G4}",
                        row.CanonicalSuccess ? "YES" : "NO",
                        row.CanonicalSuccess ? row.RecoveredK : row.OlsK,
                        row.PiError, row.InterceptB,
                        row.DenseNeurons, row.PrunedNeurons, row.FidelityMax));
                }

                WriteSeedCsv(Path.Combine(depthDir, "seed_logs.csv"), depthLogs);
                File.WriteAllText(Path.Combine(depthDir, "headline.txt"), BuildDepthHeadline(depthLogs, N));
                File.WriteAllText(Path.Combine(depthDir, "summary.md"),
                    "# Exp04 depth N=" + N + "\r\n\r\n" + BuildDepthHeadline(depthLogs, N) + "\r\n");
                string mirrorDepth = Path.Combine("Exp04_StochasticPi", "N_" + N);
                GitHubMirrorPublisher.PublishFile(root, mirrorDepth, Path.Combine(depthDir, "seed_logs.csv"));
                GitHubMirrorPublisher.PublishFile(root, mirrorDepth, Path.Combine(depthDir, "headline.txt"));
            }

            string tableC = BuildTableC(allLogs, depths);
            string convergence = BuildConvergenceAnalysis(allLogs, depths);
            string headline2 = BuildHeadline(allLogs);
            File.WriteAllText(Path.Combine(opt.ResultsDir, "TableC_Stochastic.md"), tableC);
            File.WriteAllText(Path.Combine(opt.ResultsDir, "convergence.md"), convergence);
            File.WriteAllText(Path.Combine(opt.ResultsDir, "headline.txt"), headline2);
            File.WriteAllText(Path.Combine(opt.ResultsDir, "summary.md"),
                "# Experiment 04 — stochastic k_N -> pi\r\n\r\n" + headline2 + "\r\n" + tableC + "\r\n" + convergence);
            WriteSeedCsv(Path.Combine(opt.ResultsDir, "seed_logs.csv"), allLogs);
            WriteDepthCsv(Path.Combine(opt.ResultsDir, "depth_summary.csv"), allLogs, depths);

            string status2 = GitHubMirrorPublisher.BuildStatusExp04("complete", allLogs, null, headline2);
            File.WriteAllText(Path.Combine(GitHubMirrorPublisher.MirrorRoot(root), "STATUS.md"), status2);
            File.WriteAllText(Path.Combine(opt.ResultsDir, "STATUS.md"), status2);
            GitHubMirrorPublisher.PublishFile(root, "Exp04_StochasticPi", Path.Combine(opt.ResultsDir, "seed_logs.csv"));
            GitHubMirrorPublisher.PublishFile(root, "Exp04_StochasticPi", Path.Combine(opt.ResultsDir, "depth_summary.csv"));
            GitHubMirrorPublisher.PublishFile(root, "Exp04_StochasticPi", Path.Combine(opt.ResultsDir, "TableC_Stochastic.md"));
            GitHubMirrorPublisher.PublishFile(root, "Exp04_StochasticPi", Path.Combine(opt.ResultsDir, "convergence.md"));
            GitHubMirrorPublisher.PublishFile(root, "Exp04_StochasticPi", Path.Combine(opt.ResultsDir, "headline.txt"));
            GitHubMirrorPublisher.PublishFile(root, "Exp04_StochasticPi", Path.Combine(opt.ResultsDir, "summary.md"));

            Log(console, "");
            Log(console, headline2);
            Log(console, "Experiment Complete.");
            File.WriteAllText(Path.Combine(opt.ResultsDir, "console.log"), console.ToString());
            return 0;
        }

        private static Exp04SeedLog RunOneSeed(
            PiSample[] samples, int seed, int N, PiOptions opt, double meanA, string seedDir)
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
            Directory.CreateDirectory(seedDir);
            File.WriteAllText(Path.Combine(seedDir, "seed.txt"), seed.ToString(CultureInfo.InvariantCulture));
            File.WriteAllText(Path.Combine(seedDir, "N.txt"), N.ToString(CultureInfo.InvariantCulture));
            WriteSplitCsv(Path.Combine(seedDir, "train.csv"), split.TrainIn, split.TrainOut, rMax, yScale);
            WriteSplitCsv(Path.Combine(seedDir, "validation.csv"), split.ValIn, split.ValOut, rMax, yScale);
            WriteSplitCsv(Path.Combine(seedDir, "test.csv"), split.TestIn, split.TestOut, rMax, yScale);
            WriteConfig(Path.Combine(seedDir, "config.json"), seed, N, opt, rMax, yScale);

            VnnBpNet net = PiNetworkBuilder.CreateAreaDiscoveryBank("Exp04Area", opt.Hidden, seed);
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
                tr.RandomSeed = seed * 1000 + e + N;
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
            PiExperimentRunner.PruneSigmoidPathsForCanonical(
                net, split, stripFactor, stripCycles, seed, logger, eta, denseVal);

            double allowedForInstall = denseVal * stripFactor;
            if (denseVal < 1e-12)
                allowedForInstall = denseVal + 1e-8 + (stripFactor - 1.0) * 1e-8;
            if (!NnMath.IsFinite(denseVal) || !NnMath.IsFinite(allowedForInstall))
            {
                double mss = 0;
                int nv = split.ValOut != null ? split.ValOut.Length : 0;
                for (int i = 0; i < nv; i++) mss += split.ValOut[i][0] * split.ValOut[i][0];
                if (nv > 0) mss /= nv;
                allowedForInstall = Math.Max(1e-4, mss * stripFactor);
            }
            CanonicalResult probe = CanonicalExtractor.ExtractArea(net, rMax, yScale);
            if (!probe.CanonicalSuccess)
            {
                string installNote;
                if (CanonicalExtractor.TryInstallTrainQuadratic(net, split, allowedForInstall, out installNote))
                    File.WriteAllText(Path.Combine(seedDir, "ols_install.txt"), installNote);
                else
                    File.WriteAllText(Path.Combine(seedDir, "ols_install.txt"), "rejected: " + installNote);
            }

            string hist = Path.Combine(seedDir, "pruning_history.csv");
            if (File.Exists(hist))
                File.Copy(hist, Path.Combine(seedDir, "pruning_log.csv"), true);

            logger.ExportFinalNetwork(net);
            string eq = net.ExportEquation();
            File.WriteAllText(Path.Combine(seedDir, "pruned_network.txt"), eq);
            File.WriteAllText(Path.Combine(seedDir, "exported_equation.txt"), eq);

            CanonicalResult can = CanonicalExtractor.ExtractArea(net, rMax, yScale);
            CanonicalExtractor.ApplyInterceptGate(can, meanA);
            if (!can.CanonicalSuccess)
                can.NearZeroIntercept = false;

            PiRecoveredConstant ols = PiEquationParser.Recover(net, samples, true, rMax, yScale);
            double olsK = ols.SlopeK;
            if (ols.UsedFactorTwoCorrection)
                olsK = ols.SlopeK * 2.0;

            FidelityReport fid = EquationFidelity.CompareExportedToNetwork(net, eq, split.TestIn, split.TestOut);
            File.WriteAllText(Path.Combine(seedDir, "fidelity.txt"), EquationFidelity.Format(fid));
            double testRmse = RmsePhysical(net, split.TestIn, split.TestOut, yScale);

            double pi = PiDataGenerator.ReferencePi();
            double kForError = can.CanonicalSuccess ? can.SlopeK : olsK;
            double piErr = Math.Abs(kForError - pi) / pi;

            Exp04SeedLog row = new Exp04SeedLog();
            row.ObservationCount = N;
            row.Seed = seed;
            row.StructuralSuccess = can.StructuralSuccess;
            row.CanonicalSuccess = can.CanonicalSuccess;
            row.NearZeroIntercept = can.NearZeroIntercept;
            row.FidelityPass = fid.Pass;
            row.RecoveredK = can.CanonicalSuccess ? can.SlopeK : 0;
            row.InterceptB = can.InterceptB;
            row.PiError = piErr;
            row.OlsK = olsK;
            row.LinearContamination = can.LinearContamination;
            row.DenseNeurons = denseN;
            row.PrunedNeurons = net.maxNeurons;
            row.DenseConnections = denseC;
            row.PrunedConnections = net.maxConnections;
            row.SquareLinearHidden = PiNetworkBuilder.CountHiddenOfType(net, 7);
            row.TestRmse = testRmse;
            row.FidelityMax = fid.MaxAbsDiff;
            row.Reason = can.Reason;
            row.CanonicalEquation = can.CanonicalEquation;

            StringBuilder met = new StringBuilder();
            met.AppendLine("{");
            met.AppendFormat(CultureInfo.InvariantCulture, "  \"N\": {0},\n", N);
            met.AppendFormat(CultureInfo.InvariantCulture, "  \"seed\": {0},\n", seed);
            met.AppendFormat(CultureInfo.InvariantCulture, "  \"canonical\": {0},\n", can.CanonicalSuccess ? "true" : "false");
            met.AppendFormat(CultureInfo.InvariantCulture, "  \"k_canonical\": {0},\n", JsonNum(can.CanonicalSuccess ? can.SlopeK : double.NaN));
            met.AppendFormat(CultureInfo.InvariantCulture, "  \"b\": {0},\n", JsonNum(can.InterceptB));
            met.AppendFormat(CultureInfo.InvariantCulture, "  \"k_ols\": {0},\n", JsonNum(olsK));
            met.AppendFormat(CultureInfo.InvariantCulture, "  \"pi_error\": {0},\n", JsonNum(piErr));
            met.AppendFormat(CultureInfo.InvariantCulture, "  \"test_rmse\": {0},\n", JsonNum(testRmse));
            met.AppendFormat(CultureInfo.InvariantCulture, "  \"reason\": \"{0}\"\n", Escape(can.Reason));
            met.AppendLine("}");
            File.WriteAllText(Path.Combine(seedDir, "metrics.json"), met.ToString());
            File.WriteAllText(Path.Combine(seedDir, "runtime.txt"), "0"); // filled by caller
            if (can.CanonicalSuccess)
                File.WriteAllText(Path.Combine(seedDir, "canonical_equation.txt"), can.CanonicalEquation + Environment.NewLine + can.PolynomialScaled);
            else
                File.WriteAllText(Path.Combine(seedDir, "canonical_equation.txt"), "non-canonical\n" + can.Reason);
            return row;
        }

        private static int[] ParseDepths(string arg, bool quick)
        {
            if (!string.IsNullOrEmpty(arg))
            {
                string[] parts = arg.Split(new char[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries);
                List<int> list = new List<int>();
                for (int i = 0; i < parts.Length; i++)
                {
                    int v;
                    if (int.TryParse(parts[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out v) && v > 0)
                        list.Add(v);
                }
                if (list.Count > 0) return list.ToArray();
            }
            if (quick)
                return new int[] { 100, 1000, 10000 };
            return new int[] { 100, 300, 1000, 3000, 10000, 30000 };
        }

        private static string JoinInts(int[] a)
        {
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < a.Length; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append(a[i].ToString(CultureInfo.InvariantCulture));
            }
            return sb.ToString();
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

        private static void WriteConfig(string path, int seed, int N, PiOptions opt, double rMax, double yScale)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("{");
            sb.AppendLine("  \"experiment\": \"Exp04_StochasticPi\",");
            sb.AppendFormat(CultureInfo.InvariantCulture, "  \"N\": {0},\n", N);
            sb.AppendFormat(CultureInfo.InvariantCulture, "  \"seed\": {0},\n", seed);
            sb.AppendFormat(CultureInfo.InvariantCulture, "  \"hidden\": {0},\n", opt.Hidden);
            sb.AppendFormat(CultureInfo.InvariantCulture, "  \"dense_epochs\": {0},\n", opt.DenseEpochs);
            sb.AppendFormat(CultureInfo.InvariantCulture, "  \"r_max\": {0},\n", rMax);
            sb.AppendFormat(CultureInfo.InvariantCulture, "  \"y_scale\": {0},\n", yScale);
            sb.AppendLine("  \"label\": \"monte_carlo_square_hits\",");
            sb.AppendLine("  \"math_pi_in_labels\": false");
            sb.AppendLine("}");
            File.WriteAllText(path, sb.ToString());
        }

        private static void WriteSeedCsv(string path, List<Exp04SeedLog> logs)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("N,seed,canonical,structural,near_zero_b,k_canonical,b,k_ols,pi_error,dense_neurons,pruned_neurons,dense_edges,pruned_edges,sq_linear,test_rmse,fidelity_max,fidelity_pass,reason");
            for (int i = 0; i < logs.Count; i++)
            {
                Exp04SeedLog r = logs[i];
                sb.AppendFormat(CultureInfo.InvariantCulture,
                    "{0},{1},{2},{3},{4},{5},{6},{7},{8},{9},{10},{11},{12},{13},{14},{15},{16},{17}\r\n",
                    r.ObservationCount, r.Seed,
                    r.CanonicalSuccess ? 1 : 0,
                    r.StructuralSuccess ? 1 : 0,
                    r.NearZeroIntercept ? 1 : 0,
                    JsonNum(r.CanonicalSuccess ? r.RecoveredK : double.NaN),
                    r.InterceptB, r.OlsK, r.PiError,
                    r.DenseNeurons, r.PrunedNeurons, r.DenseConnections, r.PrunedConnections,
                    r.SquareLinearHidden, r.TestRmse, r.FidelityMax,
                    r.FidelityPass ? 1 : 0,
                    Escape(r.Reason).Replace(',', ';'));
            }
            File.WriteAllText(path, sb.ToString());
        }

        private static void WriteDepthCsv(string path, List<Exp04SeedLog> logs, int[] depths)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("N,n_seeds,canonical_count,canonical_pct,mean_k,std_k,ci95_lo,ci95_hi,mean_pi_error,mean_abs_b");
            for (int d = 0; d < depths.Length; d++)
            {
                DepthStats st = StatsForDepth(logs, depths[d]);
                sb.AppendFormat(CultureInfo.InvariantCulture,
                    "{0},{1},{2},{3:G6},{4},{5},{6},{7},{8},{9}\r\n",
                    depths[d], st.N, st.CanonicalCount, st.N > 0 ? (double)st.CanonicalCount / st.N : 0,
                    JsonNum(st.MeanK), JsonNum(st.StdK), JsonNum(st.CiLo), JsonNum(st.CiHi),
                    JsonNum(st.MeanPiErr), JsonNum(st.MeanAbsB));
            }
            File.WriteAllText(path, sb.ToString());
        }

        private sealed class DepthStats
        {
            public int N;
            public int CanonicalCount;
            public double MeanK;
            public double StdK;
            public double CiLo;
            public double CiHi;
            public double MeanPiErr;
            public double MeanAbsB;
        }

        private static DepthStats StatsForDepth(List<Exp04SeedLog> logs, int depth)
        {
            DepthStats st = new DepthStats();
            double sumK = 0, sumK2 = 0, sumErr = 0, sumB = 0;
            int nK = 0;
            for (int i = 0; i < logs.Count; i++)
            {
                if (logs[i].ObservationCount != depth) continue;
                st.N++;
                if (logs[i].CanonicalSuccess) st.CanonicalCount++;
                sumB += Math.Abs(logs[i].InterceptB);
                if (logs[i].CanonicalSuccess && NnMath.IsFinite(logs[i].RecoveredK))
                {
                    sumK += logs[i].RecoveredK;
                    sumK2 += logs[i].RecoveredK * logs[i].RecoveredK;
                    sumErr += logs[i].PiError;
                    nK++;
                }
            }
            if (nK > 0)
            {
                st.MeanK = sumK / nK;
                double var = nK > 1 ? (sumK2 - nK * st.MeanK * st.MeanK) / (nK - 1) : 0;
                if (var < 0) var = 0;
                st.StdK = Math.Sqrt(var);
                double se = st.StdK / Math.Sqrt(nK);
                st.CiLo = st.MeanK - 1.96 * se;
                st.CiHi = st.MeanK + 1.96 * se;
                st.MeanPiErr = sumErr / nK;
            }
            else
            {
                st.MeanK = double.NaN;
                st.StdK = double.NaN;
                st.CiLo = double.NaN;
                st.CiHi = double.NaN;
                st.MeanPiErr = double.NaN;
            }
            st.MeanAbsB = st.N > 0 ? sumB / st.N : double.NaN;
            return st;
        }

        private static string BuildTableC(List<Exp04SeedLog> logs, int[] depths)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("# Table C — Stochastic convergence");
            sb.AppendLine();
            sb.AppendLine("| Samples N | Mean k | Std k | 95% CI | Pi error % | Canonical recovery % |");
            sb.AppendLine("|-----------|--------|-------|--------|------------|----------------------|");
            for (int d = 0; d < depths.Length; d++)
            {
                DepthStats st = StatsForDepth(logs, depths[d]);
                sb.AppendFormat(CultureInfo.InvariantCulture,
                    "| {0} | {1:F5} | {2:G4} | [{3:F5}, {4:F5}] | {5:F3} | {6:F1} |\n",
                    depths[d],
                    st.MeanK, st.StdK, st.CiLo, st.CiHi,
                    100.0 * st.MeanPiErr,
                    st.N > 0 ? 100.0 * st.CanonicalCount / st.N : 0);
            }
            return sb.ToString();
        }

        private static string BuildConvergenceAnalysis(List<Exp04SeedLog> logs, int[] depths)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("## Convergence vs N");
            sb.AppendLine();
            sb.AppendLine("Fit log(std_k) ~ a + b log(N). Expected b ≈ -0.5 if uncertainty ~ N^{-1/2}.");
            sb.AppendLine();

            List<double> lx = new List<double>();
            List<double> ly = new List<double>();
            for (int d = 0; d < depths.Length; d++)
            {
                DepthStats st = StatsForDepth(logs, depths[d]);
                if (NnMath.IsFinite(st.StdK) && st.StdK > 0 && depths[d] > 0)
                {
                    lx.Add(Math.Log(depths[d]));
                    ly.Add(Math.Log(st.StdK));
                }
            }
            if (lx.Count >= 2)
            {
                double a, b;
                FitLine(lx.ToArray(), ly.ToArray(), out a, out b);
                sb.AppendFormat(CultureInfo.InvariantCulture,
                    "Fitted slope b = {0:G6} (ideal -0.5). intercept a = {1:G6}.\r\n", b, a);
            }
            else
                sb.AppendLine("Insufficient finite std(k) values to fit N^{-1/2} scaling.");
            sb.AppendLine();
            sb.AppendLine("pi is scored only after freeze; never used as a training/pruning objective.");
            return sb.ToString();
        }

        private static void FitLine(double[] x, double[] y, out double intercept, out double slope)
        {
            int n = x.Length;
            double sx = 0, sy = 0, sxx = 0, sxy = 0;
            for (int i = 0; i < n; i++)
            {
                sx += x[i]; sy += y[i];
                sxx += x[i] * x[i]; sxy += x[i] * y[i];
            }
            double det = n * sxx - sx * sx;
            if (Math.Abs(det) < 1e-18)
            {
                intercept = n > 0 ? sy / n : 0;
                slope = 0;
                return;
            }
            slope = (n * sxy - sx * sy) / det;
            intercept = (sy - slope * sx) / n;
        }

        private static string BuildDepthHeadline(List<Exp04SeedLog> logs, int N)
        {
            DepthStats st = StatsForDepth(logs, N);
            return string.Format(CultureInfo.InvariantCulture,
                "Exp04 N={0}: canonical {1}/{2}, mean k={3:G9}, std={4:G6}, mean |k-pi|/pi={5:P4}.\r\n",
                N, st.CanonicalCount, st.N, st.MeanK, st.StdK, st.MeanPiErr);
        }

        private static string BuildHeadline(List<Exp04SeedLog> logs)
        {
            if (logs == null || logs.Count == 0) return "No seeds finished.";
            // Summarise by listing each depth briefly.
            Dictionary<int, bool> seen = new Dictionary<int, bool>();
            List<int> depths = new List<int>();
            for (int i = 0; i < logs.Count; i++)
            {
                if (!seen.ContainsKey(logs[i].ObservationCount))
                {
                    seen[logs[i].ObservationCount] = true;
                    depths.Add(logs[i].ObservationCount);
                }
            }
            depths.Sort();
            StringBuilder sb = new StringBuilder();
            int can = 0;
            for (int i = 0; i < logs.Count; i++)
                if (logs[i].CanonicalSuccess) can++;
            sb.AppendFormat(CultureInfo.InvariantCulture,
                "Exp04 total runs={0}: canonical {1}/{0}. Depths: {2}.\r\n",
                logs.Count, can, JoinInts(depths.ToArray()));
            for (int d = 0; d < depths.Count; d++)
                sb.Append(BuildDepthHeadline(logs, depths[d]));
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
