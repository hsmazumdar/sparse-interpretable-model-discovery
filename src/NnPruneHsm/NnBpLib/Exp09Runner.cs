using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;

namespace VnnBp.VnnBpLib
{
    public sealed class Exp09SeedLog
    {
        public string TaskId;
        public int Hidden;
        public int Seed;
        public bool CanonicalSuccess;
        public bool StructuralSuccess;
        public double RecoveredK;
        public double PiError;
        public double CoeffA;
        public double CoeffB;
        public double RelAbDiff;
        public double EffectiveRadius;
        public double TestMetric;
        public double DenseValLoss;
        public double FinalValLoss;
        public int DenseNeurons;
        public int FinalNeurons;
        public int DenseConnections;
        public int FinalConnections;
        public double RuntimeSeconds;
        public string Reason;
        public bool PassEUsed;
    }

    /// <summary>
    /// Experiment 09 — initial network size sensitivity.
    /// Repeats main equation recovery (area A=b+kr² and 2-D circle) at several
    /// initial hidden sizes to show the law is not an artifact of one topology.
    /// Pass E enabled (same as Exp01/Exp02 main pipeline).
    /// </summary>
    public static class Exp09Runner
    {
        private static readonly int[] DefaultSizes = new int[] { 8, 12, 22, 36, 50 };

        public static int Run(string[] args)
        {
            PiOptions opt = PiOptions.Parse(args);
            opt.AllowFallback = false;
            bool quick = false;
            bool seedsSet = false;
            bool doArea = true;
            bool doCircle = true;
            int[] sizes = DefaultSizes;
            int circleSamples = 2000;
            if (args != null)
            {
                for (int i = 0; i < args.Length; i++)
                {
                    if (args[i] == "--quick") quick = true;
                    if (args[i] == "--seeds") seedsSet = true;
                    if (args[i] == "--area-only") { doArea = true; doCircle = false; }
                    if (args[i] == "--circle-only") { doArea = false; doCircle = true; }
                    if (args[i] == "--sizes" && i + 1 < args.Length)
                        sizes = ParseSizes(args[++i]);
                    if (args[i] == "--samples" && i + 1 < args.Length)
                        circleSamples = Math.Max(100, int.Parse(args[++i], CultureInfo.InvariantCulture));
                }
            }
            if (!quick && !seedsSet) opt.SeedCount = 10;
            if (!quick && opt.RadiusStep == 10) opt.RadiusStep = 2;
            if (!quick && opt.DenseEpochs == 400) opt.DenseEpochs = 150;
            if (quick)
            {
                if (sizes == DefaultSizes || sizes == null || sizes.Length == 0)
                    sizes = new int[] { 8, 22 };
                if (opt.RadiusStep < 5) opt.RadiusStep = 10;
                circleSamples = 600;
            }
            if (sizes == null || sizes.Length == 0)
                sizes = DefaultSizes;

            string root = FindProjectRoot();
            if (string.IsNullOrEmpty(opt.ResultsDir) || opt.ResultsDir.IndexOf("PiExperiment", StringComparison.OrdinalIgnoreCase) >= 0)
                opt.ResultsDir = Path.Combine(root, "Results", "Exp09_NetworkSize");
            Directory.CreateDirectory(opt.ResultsDir);
            GitHubMirrorPublisher.EnsureLayout(root);
            GitHubMirrorPublisher.CopySource(root);

            StringBuilder console = new StringBuilder();
            Log(console, "Experiment 09 — initial network size sensitivity");
            Log(console, "Results: " + opt.ResultsDir);
            Log(console, "Seeds: " + opt.SeedCount + "  epochs=" + opt.DenseEpochs);
            Log(console, "Sizes: " + JoinInts(sizes));
            Log(console, "Tasks: " + (doArea ? "area " : "") + (doCircle ? "circle" : ""));
            Log(console, "Pass E on (main Exp01/02 recovery). π not an objective.");

            List<Exp09SeedLog> all = new List<Exp09SeedLog>();
            string status = GitHubMirrorPublisher.BuildStatusExp09("running", all, "");
            File.WriteAllText(Path.Combine(opt.ResultsDir, "STATUS.md"), status);

            if (doArea)
            {
                int[] radii = PiDataGenerator.RadiusRange(opt.RadiusLo, opt.RadiusHi, opt.RadiusStep);
                PiSample[] area = PiDataGenerator.GenerateAreaData(radii, opt.GridResolution);
                if (opt.MinRadius > 0)
                    area = PiDataGenerator.FilterMinRadius(area, opt.MinRadius);
                PiDataGenerator.SaveCsv(Path.Combine(opt.ResultsDir, "area_data.csv"), area);
                Log(console, "Area samples: " + area.Length);

                double meanA = 0;
                for (int i = 0; i < area.Length; i++) meanA += Math.Abs(area[i].Y);
                if (area.Length > 0) meanA /= area.Length;

                for (int hi = 0; hi < sizes.Length; hi++)
                {
                    int H = sizes[hi];
                    string setDir = Path.Combine(opt.ResultsDir, string.Format(CultureInfo.InvariantCulture, "Area_H{0:00}", H));
                    Directory.CreateDirectory(setDir);
                    Log(console, "");
                    Log(console, "=== area hidden=" + H + " ===");

                    for (int s = 1; s <= opt.SeedCount; s++)
                    {
                        Log(console, "area H=" + H + " seed " + s + "/" + opt.SeedCount + "...");
                        Stopwatch sw = Stopwatch.StartNew();
                        Exp09SeedLog row = RunArea(area, meanA, s, H, opt, setDir);
                        sw.Stop();
                        row.RuntimeSeconds = sw.Elapsed.TotalSeconds;
                        all.Add(row);
                        File.WriteAllText(Path.Combine(setDir, string.Format(CultureInfo.InvariantCulture, "seed_{0:00}", s), "runtime.txt"),
                            row.RuntimeSeconds.ToString("G15", CultureInfo.InvariantCulture) + " seconds");

                        status = GitHubMirrorPublisher.BuildStatusExp09("running", all, BuildHeadline(all, sizes, doArea, doCircle));
                        File.WriteAllText(Path.Combine(opt.ResultsDir, "STATUS.md"), status);
                        GitHubMirrorPublisher.PublishSeed(root, "Exp09_NetworkSize",
                            Path.Combine(setDir, string.Format(CultureInfo.InvariantCulture, "seed_{0:00}", s)), status);

                        Log(console, string.Format(CultureInfo.InvariantCulture,
                            "  canon={0}  Lval={1:G4}  rmse={2:G4}  n {3}->{4}  e {5}->{6}  PassE={7}  t={8:F1}s",
                            row.CanonicalSuccess ? "YES" : "NO",
                            row.FinalValLoss, row.TestMetric,
                            row.DenseNeurons, row.FinalNeurons,
                            row.DenseConnections, row.FinalConnections,
                            row.PassEUsed ? "Y" : "N",
                            row.RuntimeSeconds));
                    }
                }
            }

            if (doCircle)
            {
                double cx = CircleDataGenerator.DefaultCenter;
                double cy = CircleDataGenerator.DefaultCenter;
                double R = CircleDataGenerator.DefaultRadius;
                double[][] xinAll, xoutAll;
                CircleDataGenerator.Generate(circleSamples, 12345, cx, cy, R, out xinAll, out xoutAll);
                CircleDataGenerator.SaveCsv(Path.Combine(opt.ResultsDir, "circle_data.csv"), xinAll, xoutAll);
                Log(console, "Circle samples: " + circleSamples + "  R=" + R.ToString("G6", CultureInfo.InvariantCulture));

                // Circle uses shorter post-prune retrain like Exp01.
                int circleRetrain = opt.RetrainCycles;
                if (circleRetrain > 2500) circleRetrain = 2500;
                if (quick && circleRetrain > 1000) circleRetrain = 1000;

                for (int hi = 0; hi < sizes.Length; hi++)
                {
                    int H = sizes[hi];
                    string setDir = Path.Combine(opt.ResultsDir, string.Format(CultureInfo.InvariantCulture, "Circle_H{0:00}", H));
                    Directory.CreateDirectory(setDir);
                    Log(console, "");
                    Log(console, "=== circle hidden=" + H + " ===");

                    for (int s = 1; s <= opt.SeedCount; s++)
                    {
                        Log(console, "circle H=" + H + " seed " + s + "/" + opt.SeedCount + "...");
                        Stopwatch sw = Stopwatch.StartNew();
                        Exp09SeedLog row = RunCircle(xinAll, xoutAll, s, H, opt, setDir, R, circleRetrain);
                        sw.Stop();
                        row.RuntimeSeconds = sw.Elapsed.TotalSeconds;
                        all.Add(row);
                        File.WriteAllText(Path.Combine(setDir, string.Format(CultureInfo.InvariantCulture, "seed_{0:00}", s), "runtime.txt"),
                            row.RuntimeSeconds.ToString("G15", CultureInfo.InvariantCulture) + " seconds");

                        status = GitHubMirrorPublisher.BuildStatusExp09("running", all, BuildHeadline(all, sizes, doArea, doCircle));
                        File.WriteAllText(Path.Combine(opt.ResultsDir, "STATUS.md"), status);
                        GitHubMirrorPublisher.PublishSeed(root, "Exp09_NetworkSize",
                            Path.Combine(setDir, string.Format(CultureInfo.InvariantCulture, "seed_{0:00}", s)), status);

                        Log(console, string.Format(CultureInfo.InvariantCulture,
                            "  canon={0}  acc={1:P1}  n {2}->{3}  e {4}->{5}  PassE={6}  t={7:F1}s",
                            row.CanonicalSuccess ? "YES" : "NO",
                            row.TestMetric,
                            row.DenseNeurons, row.FinalNeurons,
                            row.DenseConnections, row.FinalConnections,
                            row.PassEUsed ? "Y" : "N",
                            row.RuntimeSeconds));
                    }
                }
            }

            WriteSeedCsv(Path.Combine(opt.ResultsDir, "seed_logs.csv"), all);
            string headline2 = BuildHeadline(all, sizes, doArea, doCircle);
            string table = BuildTable(all, sizes, doArea, doCircle);
            File.WriteAllText(Path.Combine(opt.ResultsDir, "headline.txt"), headline2);
            File.WriteAllText(Path.Combine(opt.ResultsDir, "summary.md"),
                "# Experiment 09 — network size\r\n\r\n" + headline2 + "\r\n" + table);
            status = GitHubMirrorPublisher.BuildStatusExp09("complete", all, headline2);
            File.WriteAllText(Path.Combine(GitHubMirrorPublisher.MirrorRoot(root), "STATUS.md"), status);
            File.WriteAllText(Path.Combine(opt.ResultsDir, "STATUS.md"), status);
            GitHubMirrorPublisher.PublishFile(root, "Exp09_NetworkSize", Path.Combine(opt.ResultsDir, "seed_logs.csv"));
            GitHubMirrorPublisher.PublishFile(root, "Exp09_NetworkSize", Path.Combine(opt.ResultsDir, "headline.txt"));
            GitHubMirrorPublisher.PublishFile(root, "Exp09_NetworkSize", Path.Combine(opt.ResultsDir, "summary.md"));

            Log(console, "");
            Log(console, headline2);
            Log(console, "Experiment Complete.");
            File.WriteAllText(Path.Combine(opt.ResultsDir, "console.log"), console.ToString());
            return 0;
        }

        private static Exp09SeedLog RunArea(
            PiSample[] samples, double meanA, int seed, int hidden, PiOptions opt, string setDir)
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

            string seedDir = Path.Combine(setDir, string.Format(CultureInfo.InvariantCulture, "seed_{0:00}", seed));
            Directory.CreateDirectory(seedDir);
            File.WriteAllText(Path.Combine(seedDir, "seed.txt"), seed.ToString(CultureInfo.InvariantCulture));
            File.WriteAllText(Path.Combine(seedDir, "hidden.txt"), hidden.ToString(CultureInfo.InvariantCulture));

            VnnBpNet net = PiNetworkBuilder.CreateAreaDiscoveryBank("Exp09Area_H" + hidden, hidden, seed);
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
            PiExperimentRunner.PruneSigmoidPathsForCanonical(
                net, split, stripFactor, stripCycles, seed, logger, eta, denseVal);

            bool passE = false;
            double allowedForInstall = denseVal * stripFactor;
            if (denseVal < 1e-12)
                allowedForInstall = denseVal + 1e-8 + (stripFactor - 1.0) * 1e-8;
            CanonicalResult probe = CanonicalExtractor.ExtractArea(net, rMax, yScale);
            if (!probe.CanonicalSuccess)
            {
                string installNote;
                if (CanonicalExtractor.TryInstallTrainQuadratic(net, split, allowedForInstall, out installNote))
                {
                    passE = true;
                    File.WriteAllText(Path.Combine(seedDir, "ols_install.txt"), installNote);
                }
                else
                    File.WriteAllText(Path.Combine(seedDir, "ols_install.txt"), "rejected: " + installNote);
            }

            EvaluationResult finalVal = net.Evaluate(split.ValIn, split.ValOut);
            logger.ExportFinalNetwork(net);
            string eq = net.ExportEquation();
            File.WriteAllText(Path.Combine(seedDir, "exported_equation.txt"), eq);

            CanonicalResult can = CanonicalExtractor.ExtractArea(net, rMax, yScale);
            CanonicalExtractor.ApplyInterceptGate(can, meanA);
            double testRmse = RmsePhysical(net, split.TestIn, split.TestOut, yScale);
            double pi = PiDataGenerator.ReferencePi();
            double piErr = can.CanonicalSuccess && NnMath.IsFinite(can.SlopeK)
                ? Math.Abs(can.SlopeK - pi) / pi : double.NaN;

            Exp09SeedLog row = new Exp09SeedLog();
            row.TaskId = "area";
            row.Hidden = hidden;
            row.Seed = seed;
            row.CanonicalSuccess = can.CanonicalSuccess;
            row.StructuralSuccess = can.StructuralSuccess;
            row.RecoveredK = can.CanonicalSuccess ? can.SlopeK : 0;
            row.PiError = piErr;
            row.TestMetric = testRmse;
            row.DenseValLoss = denseVal;
            row.FinalValLoss = finalVal.Loss;
            row.DenseNeurons = denseN;
            row.FinalNeurons = net.maxNeurons;
            row.DenseConnections = denseC;
            row.FinalConnections = net.maxConnections;
            row.Reason = can.Reason;
            row.PassEUsed = passE;

            WriteMetrics(seedDir, row);
            if (can.CanonicalSuccess)
                File.WriteAllText(Path.Combine(seedDir, "canonical_equation.txt"), can.CanonicalEquation);
            return row;
        }

        private static Exp09SeedLog RunCircle(
            double[][] xinAll, double[][] xoutAll, int seed, int hidden,
            PiOptions opt, string setDir, double trueR, int retrainCycles)
        {
            DatasetSplit split = DatasetSplit.CreateDefault(xinAll, xoutAll, seed);
            string seedDir = Path.Combine(setDir, string.Format(CultureInfo.InvariantCulture, "seed_{0:00}", seed));
            Directory.CreateDirectory(seedDir);
            File.WriteAllText(Path.Combine(seedDir, "seed.txt"), seed.ToString(CultureInfo.InvariantCulture));
            File.WriteAllText(Path.Combine(seedDir, "hidden.txt"), hidden.ToString(CultureInfo.InvariantCulture));

            VnnBpNet net = PiNetworkBuilder.CreateCircleDiscoveryBank("Exp09Circle_H" + hidden, hidden, seed);
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

            PiExperimentRunner.PruneWithLockedBaseline(net, split, opt.PruneFactor, retrainCycles, seed, logger, eta);

            bool passE = false;
            double stripFactor = 1.25;
            double allowed = denseVal * stripFactor;
            if (!NnMath.IsFinite(denseVal) || !NnMath.IsFinite(allowed))
                allowed = 0.5;
            else if (allowed < 1e-4)
                allowed = denseVal + 1e-4 * stripFactor;
            if (allowed < 0.25) allowed = 0.25;
            CircleCanonicalResult probe = CircleCanonicalExtractor.Extract(net, trueR);
            if (!probe.CanonicalSuccess)
            {
                string installNote;
                if (CircleCanonicalExtractor.TryInstallTrainCircle(net, split, allowed, out installNote))
                {
                    passE = true;
                    File.WriteAllText(Path.Combine(seedDir, "ols_install.txt"), installNote);
                }
                else
                    File.WriteAllText(Path.Combine(seedDir, "ols_install.txt"), "rejected: " + installNote);
            }

            EvaluationResult finalVal = net.Evaluate(split.ValIn, split.ValOut);
            EvaluationResult testEv = net.Evaluate(split.TestIn, split.TestOut);
            logger.ExportFinalNetwork(net);
            string eq = net.ExportEquation();
            File.WriteAllText(Path.Combine(seedDir, "exported_equation.txt"), eq);

            CircleCanonicalResult can = CircleCanonicalExtractor.Extract(net, trueR);

            Exp09SeedLog row = new Exp09SeedLog();
            row.TaskId = "circle";
            row.Hidden = hidden;
            row.Seed = seed;
            row.CanonicalSuccess = can.CanonicalSuccess;
            row.StructuralSuccess = can.StructuralSuccess;
            row.CoeffA = can.CoeffA;
            row.CoeffB = can.CoeffB;
            row.RelAbDiff = can.RelAbDiff;
            row.EffectiveRadius = can.EffectiveRadius;
            row.TestMetric = testEv.Accuracy;
            row.DenseValLoss = denseVal;
            row.FinalValLoss = finalVal.Loss;
            row.DenseNeurons = denseN;
            row.FinalNeurons = net.maxNeurons;
            row.DenseConnections = denseC;
            row.FinalConnections = net.maxConnections;
            row.Reason = can.Reason;
            row.PassEUsed = passE;

            WriteMetrics(seedDir, row);
            if (can.CanonicalSuccess || can.StructuralSuccess)
                File.WriteAllText(Path.Combine(seedDir, "canonical_equation.txt"),
                    (string.IsNullOrEmpty(can.CanonicalEquation) ? "n/a" : can.CanonicalEquation)
                    + Environment.NewLine + can.Reason);
            return row;
        }

        private static void WriteMetrics(string seedDir, Exp09SeedLog row)
        {
            StringBuilder met = new StringBuilder();
            met.AppendLine("{");
            met.AppendFormat(CultureInfo.InvariantCulture, "  \"task\": \"{0}\",\n", row.TaskId);
            met.AppendFormat(CultureInfo.InvariantCulture, "  \"hidden\": {0},\n", row.Hidden);
            met.AppendFormat(CultureInfo.InvariantCulture, "  \"seed\": {0},\n", row.Seed);
            met.AppendFormat(CultureInfo.InvariantCulture, "  \"canonical\": {0},\n", row.CanonicalSuccess ? "true" : "false");
            met.AppendFormat(CultureInfo.InvariantCulture, "  \"pass_e\": {0},\n", row.PassEUsed ? "true" : "false");
            met.AppendFormat(CultureInfo.InvariantCulture, "  \"test_metric\": {0},\n", JsonNum(row.TestMetric));
            met.AppendFormat(CultureInfo.InvariantCulture, "  \"final_n\": {0},\n", row.FinalNeurons);
            met.AppendFormat(CultureInfo.InvariantCulture, "  \"final_e\": {0}\n", row.FinalConnections);
            met.AppendLine("}");
            File.WriteAllText(Path.Combine(seedDir, "metrics.json"), met.ToString());
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

        private static void WriteSeedCsv(string path, List<Exp09SeedLog> logs)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("task,hidden,seed,canonical,structural,k,pi_error,a,b,rel_ab,R_hat,test_metric,dense_val,final_val,dense_n,final_n,dense_e,final_e,pass_e,runtime_s,reason");
            for (int i = 0; i < logs.Count; i++)
            {
                Exp09SeedLog r = logs[i];
                sb.AppendFormat(CultureInfo.InvariantCulture,
                    "{0},{1},{2},{3},{4},{5},{6},{7},{8},{9},{10},{11},{12},{13},{14},{15},{16},{17},{18},{19},{20}\r\n",
                    r.TaskId, r.Hidden, r.Seed,
                    r.CanonicalSuccess ? 1 : 0, r.StructuralSuccess ? 1 : 0,
                    JsonNum(r.TaskId == "area" && r.CanonicalSuccess ? r.RecoveredK : double.NaN),
                    JsonNum(r.PiError),
                    JsonNum(r.CoeffA), JsonNum(r.CoeffB), JsonNum(r.RelAbDiff), JsonNum(r.EffectiveRadius),
                    JsonNum(r.TestMetric), JsonNum(r.DenseValLoss), JsonNum(r.FinalValLoss),
                    r.DenseNeurons, r.FinalNeurons, r.DenseConnections, r.FinalConnections,
                    r.PassEUsed ? 1 : 0,
                    r.RuntimeSeconds.ToString("G15", CultureInfo.InvariantCulture),
                    Escape(r.Reason).Replace(',', ';'));
            }
            File.WriteAllText(path, sb.ToString());
        }

        private static string BuildTable(List<Exp09SeedLog> logs, int[] sizes, bool doArea, bool doCircle)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("# Network size sensitivity");
            sb.AppendLine();
            sb.AppendLine("| Task | Hidden | n | Canon % | Struct % | Mean final n | Mean final e | Mean metric | PassE % | Mean t(s) |");
            sb.AppendLine("|------|--------|---|---------|----------|--------------|--------------|-------------|---------|-----------|");
            string[] tasks = doArea && doCircle ? new string[] { "area", "circle" }
                : doArea ? new string[] { "area" } : new string[] { "circle" };
            for (int t = 0; t < tasks.Length; t++)
            {
                for (int h = 0; h < sizes.Length; h++)
                {
                    int H = sizes[h];
                    int n = 0, can = 0, str = 0, pe = 0;
                    double sumNu = 0, sumE = 0, sumM = 0, sumT = 0;
                    for (int i = 0; i < logs.Count; i++)
                    {
                        if (logs[i].TaskId != tasks[t] || logs[i].Hidden != H) continue;
                        n++;
                        if (logs[i].CanonicalSuccess) can++;
                        if (logs[i].StructuralSuccess) str++;
                        if (logs[i].PassEUsed) pe++;
                        sumNu += logs[i].FinalNeurons;
                        sumE += logs[i].FinalConnections;
                        sumM += logs[i].TestMetric;
                        sumT += logs[i].RuntimeSeconds;
                    }
                    if (n == 0) continue;
                    sb.AppendFormat(CultureInfo.InvariantCulture,
                        "| {0} | {1} | {2} | {3:F0} | {4:F0} | {5:F1} | {6:F1} | {7:G4} | {8:F0} | {9:F2} |\n",
                        tasks[t], H, n, 100.0 * can / n, 100.0 * str / n,
                        sumNu / n, sumE / n, sumM / n, 100.0 * pe / n, sumT / n);
                }
            }
            return sb.ToString();
        }

        private static string BuildHeadline(List<Exp09SeedLog> logs, int[] sizes, bool doArea, bool doCircle)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendFormat(CultureInfo.InvariantCulture, "Exp09 total runs={0}.\r\n", logs.Count);
            sb.Append(BuildTable(logs, sizes, doArea, doCircle));
            return sb.ToString();
        }

        private static int[] ParseSizes(string csv)
        {
            string[] parts = csv.Split(new char[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            List<int> list = new List<int>();
            for (int i = 0; i < parts.Length; i++)
            {
                int v;
                if (int.TryParse(parts[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out v) && v >= 2)
                    list.Add(v);
            }
            return list.ToArray();
        }

        private static string JoinInts(int[] a)
        {
            if (a == null || a.Length == 0) return "";
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < a.Length; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append(a[i].ToString(CultureInfo.InvariantCulture));
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
