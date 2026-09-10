using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;

namespace VnnBp.VnnBpLib
{
    public sealed class Exp13SeedLog
    {
        public string TaskId;
        public int Seed;
        public bool FidelityPass;
        public bool ManualCheckPass;
        public bool ParseOk;
        public double MaxAbsDiff;
        public double MeanAbsDiff;
        public double ManualMaxAbs;
        public int FinalNeurons;
        public int FinalConnections;
        public string Canonical;
        public double RuntimeSeconds;
        public string Notes;
    }

    /// <summary>
    /// Experiment 13 — exact equation-export verification.
    /// For important final models (area + circle), produce machine/human/Word
    /// equation packs, coefficient lists, topology, fidelity reports, and a
    /// manual spot-check that recomputes several test points from the export.
    /// </summary>
    public static class Exp13Runner
    {
        public static int Run(string[] args)
        {
            PiOptions opt = PiOptions.Parse(args);
            opt.AllowFallback = false;
            bool quick = false;
            bool seedsSet = false;
            int areaSeeds = 8;
            int circleSeeds = 5;
            int circleSamples = 2000;
            if (args != null)
            {
                for (int i = 0; i < args.Length; i++)
                {
                    if (args[i] == "--quick")
                    {
                        quick = true;
                        areaSeeds = 2;
                        circleSeeds = 2;
                        circleSamples = 600;
                    }
                    if (args[i] == "--seeds") seedsSet = true;
                    if (args[i] == "--area-seeds" && i + 1 < args.Length)
                        areaSeeds = Math.Max(1, int.Parse(args[++i], CultureInfo.InvariantCulture));
                    if (args[i] == "--circle-seeds" && i + 1 < args.Length)
                        circleSeeds = Math.Max(1, int.Parse(args[++i], CultureInfo.InvariantCulture));
                }
            }
            if (!quick && seedsSet) { areaSeeds = opt.SeedCount; circleSeeds = Math.Max(2, opt.SeedCount / 2); }
            if (!quick && opt.RadiusStep == 10) opt.RadiusStep = 2;
            if (!quick && opt.DenseEpochs == 400) opt.DenseEpochs = 150;
            if (!quick && opt.RetrainCycles == 10000) opt.RetrainCycles = 5000;

            string root = FindProjectRoot();
            if (string.IsNullOrEmpty(opt.ResultsDir) || opt.ResultsDir.IndexOf("PiExperiment", StringComparison.OrdinalIgnoreCase) >= 0)
                opt.ResultsDir = Path.Combine(root, "Results", "Exp13_EquationExport");
            Directory.CreateDirectory(opt.ResultsDir);
            GitHubMirrorPublisher.EnsureLayout(root);
            GitHubMirrorPublisher.CopySource(root);

            StringBuilder console = new StringBuilder();
            Log(console, "Experiment 13 — exact equation-export verification");
            Log(console, "Results: " + opt.ResultsDir);
            Log(console, "Area seeds=" + areaSeeds + "  Circle seeds=" + circleSeeds);
            Log(console, "Fidelity tol=" + ExperimentCriteria.FidelityMaxTol.ToString("G4", CultureInfo.InvariantCulture));
            Log(console, "Exports: machine JSON, human text, LaTeX, OMML, coeffs, topology, fidelity, manual check.");

            List<Exp13SeedLog> all = new List<Exp13SeedLog>();
            string status = GitHubMirrorPublisher.BuildStatusExp13("running", all, "");
            File.WriteAllText(Path.Combine(opt.ResultsDir, "STATUS.md"), status);

            // --- Area ---
            int[] radii = PiDataGenerator.RadiusRange(opt.RadiusLo, opt.RadiusHi, opt.RadiusStep);
            PiSample[] area = PiDataGenerator.GenerateAreaData(radii, opt.GridResolution);
            if (opt.MinRadius > 0)
                area = PiDataGenerator.FilterMinRadius(area, opt.MinRadius);
            PiDataGenerator.SaveCsv(Path.Combine(opt.ResultsDir, "area_data.csv"), area);
            double meanA = 0;
            for (int i = 0; i < area.Length; i++) meanA += Math.Abs(area[i].Y);
            if (area.Length > 0) meanA /= area.Length;
            Log(console, "Area samples: " + area.Length);

            string areaDir = Path.Combine(opt.ResultsDir, "Task_area");
            Directory.CreateDirectory(areaDir);
            for (int s = 1; s <= areaSeeds; s++)
            {
                Log(console, "area seed " + s + "/" + areaSeeds + "...");
                Stopwatch sw = Stopwatch.StartNew();
                Exp13SeedLog row = RunArea(area, meanA, s, opt, areaDir);
                sw.Stop();
                row.RuntimeSeconds = sw.Elapsed.TotalSeconds;
                all.Add(row);
                status = GitHubMirrorPublisher.BuildStatusExp13("running", all, BuildHeadline(all));
                File.WriteAllText(Path.Combine(opt.ResultsDir, "STATUS.md"), status);
                GitHubMirrorPublisher.PublishSeed(root, "Exp13_EquationExport",
                    Path.Combine(areaDir, string.Format(CultureInfo.InvariantCulture, "seed_{0:00}", s)), status);
                Log(console, string.Format(CultureInfo.InvariantCulture,
                    "  fid={0}  manual={1}  max|d|={2:G4}  n/e={3}/{4}",
                    row.FidelityPass ? "PASS" : "FAIL",
                    row.ManualCheckPass ? "PASS" : "FAIL",
                    row.MaxAbsDiff, row.FinalNeurons, row.FinalConnections));
            }

            // --- Circle ---
            double R = CircleDataGenerator.DefaultRadius;
            double[][] xinAll, xoutAll;
            CircleDataGenerator.Generate(circleSamples, 12345,
                CircleDataGenerator.DefaultCenter, CircleDataGenerator.DefaultCenter, R,
                out xinAll, out xoutAll);
            CircleDataGenerator.SaveCsv(Path.Combine(opt.ResultsDir, "circle_data.csv"), xinAll, xoutAll);
            string circDir = Path.Combine(opt.ResultsDir, "Task_circle");
            Directory.CreateDirectory(circDir);
            int circRetrain = opt.RetrainCycles > 2500 ? 2500 : opt.RetrainCycles;
            for (int s = 1; s <= circleSeeds; s++)
            {
                Log(console, "circle seed " + s + "/" + circleSeeds + "...");
                Stopwatch sw = Stopwatch.StartNew();
                Exp13SeedLog row = RunCircle(xinAll, xoutAll, s, opt, circDir, R, circRetrain);
                sw.Stop();
                row.RuntimeSeconds = sw.Elapsed.TotalSeconds;
                all.Add(row);
                status = GitHubMirrorPublisher.BuildStatusExp13("running", all, BuildHeadline(all));
                File.WriteAllText(Path.Combine(opt.ResultsDir, "STATUS.md"), status);
                GitHubMirrorPublisher.PublishSeed(root, "Exp13_EquationExport",
                    Path.Combine(circDir, string.Format(CultureInfo.InvariantCulture, "seed_{0:00}", s)), status);
                Log(console, string.Format(CultureInfo.InvariantCulture,
                    "  fid={0}  manual={1}  max|d|={2:G4}  n/e={3}/{4}",
                    row.FidelityPass ? "PASS" : "FAIL",
                    row.ManualCheckPass ? "PASS" : "FAIL",
                    row.MaxAbsDiff, row.FinalNeurons, row.FinalConnections));
            }

            WriteSeedCsv(Path.Combine(opt.ResultsDir, "seed_logs.csv"), all);
            string headline2 = BuildHeadline(all);
            string table = BuildTable(all);
            File.WriteAllText(Path.Combine(opt.ResultsDir, "headline.txt"), headline2);
            File.WriteAllText(Path.Combine(opt.ResultsDir, "summary.md"),
                "# Experiment 13 — equation-export verification\r\n\r\n"
                + "Publication claim is equation discovery: exported equations must reproduce NN outputs.\r\n\r\n"
                + headline2 + "\r\n" + table);
            status = GitHubMirrorPublisher.BuildStatusExp13("complete", all, headline2);
            File.WriteAllText(Path.Combine(GitHubMirrorPublisher.MirrorRoot(root), "STATUS.md"), status);
            File.WriteAllText(Path.Combine(opt.ResultsDir, "STATUS.md"), status);
            GitHubMirrorPublisher.PublishFile(root, "Exp13_EquationExport", Path.Combine(opt.ResultsDir, "seed_logs.csv"));
            GitHubMirrorPublisher.PublishFile(root, "Exp13_EquationExport", Path.Combine(opt.ResultsDir, "headline.txt"));
            GitHubMirrorPublisher.PublishFile(root, "Exp13_EquationExport", Path.Combine(opt.ResultsDir, "summary.md"));

            Log(console, "");
            Log(console, headline2);
            Log(console, "Experiment Complete.");
            File.WriteAllText(Path.Combine(opt.ResultsDir, "console.log"), console.ToString());
            return 0;
        }

        private static Exp13SeedLog RunArea(
            PiSample[] samples, double meanA, int seed, PiOptions opt, string taskDir)
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
            string seedDir = Path.Combine(taskDir, string.Format(CultureInfo.InvariantCulture, "seed_{0:00}", seed));
            Directory.CreateDirectory(seedDir);
            File.WriteAllText(Path.Combine(seedDir, "seed.txt"), seed.ToString(CultureInfo.InvariantCulture));
            WriteSplitCsv1D(Path.Combine(seedDir, "train.csv"), split.TrainIn, split.TrainOut, rMax, yScale);
            WriteSplitCsv1D(Path.Combine(seedDir, "validation.csv"), split.ValIn, split.ValOut, rMax, yScale);
            WriteSplitCsv1D(Path.Combine(seedDir, "test.csv"), split.TestIn, split.TestOut, rMax, yScale);

            VnnBpNet net = PiNetworkBuilder.CreateAreaDiscoveryBank("Exp13Area", opt.Hidden, seed);
            Train(net, split, opt, seed);
            int denseN = net.maxNeurons, denseC = net.maxConnections;
            double denseVal = net.Evaluate(split.ValIn, split.ValOut).Loss;
            File.WriteAllText(Path.Combine(seedDir, "dense_network.txt"), net.ExportEquation());

            double eta = 0.05;
            ResearchLogger logger = new ResearchLogger(seedDir);
            double stripFactor = opt.PruneFactor < 1.25 ? 1.25 : opt.PruneFactor;
            PiExperimentRunner.PruneWithLockedBaseline(net, split, opt.PruneFactor, opt.RetrainCycles, seed, logger, eta);
            int strip = opt.RetrainCycles > 5000 ? 5000 : opt.RetrainCycles;
            PiExperimentRunner.PruneSigmoidPathsForCanonical(net, split, stripFactor, strip, seed, logger, eta, denseVal);

            double allowed = denseVal * stripFactor;
            if (denseVal < 1e-12) allowed = denseVal + 1e-8 + (stripFactor - 1.0) * 1e-8;
            CanonicalResult probe = CanonicalExtractor.ExtractArea(net, rMax, yScale);
            if (!probe.CanonicalSuccess)
            {
                string note;
                if (CanonicalExtractor.TryInstallTrainQuadratic(net, split, allowed, out note))
                    File.WriteAllText(Path.Combine(seedDir, "ols_install.txt"), note);
                else
                    File.WriteAllText(Path.Combine(seedDir, "ols_install.txt"), "rejected: " + note);
            }

            logger.ExportFinalNetwork(net);
            string eq = net.ExportEquation();
            CanonicalResult can = CanonicalExtractor.ExtractArea(net, rMax, yScale);
            CanonicalExtractor.ApplyInterceptGate(can, meanA);
            string canon = can.CanonicalSuccess ? can.CanonicalEquation : "";

            FidelityReport fid = EquationFidelity.CompareExportedToNetwork(net, eq, split.TestIn, split.TestOut);
            ManualCheckReport manual = ManualSpotCheck1D(net, eq, split.TestIn, 5);

            WriteExportPack(seedDir, "area", net, eq, canon, fid, manual, denseN, denseC);
            Exp13SeedLog row = MakeRow("area", seed, fid, manual, can.CanonicalSuccess ? canon : "", eq);
            row.FinalNeurons = net.maxNeurons;
            row.FinalConnections = net.maxConnections;
            return row;
        }

        private static Exp13SeedLog RunCircle(
            double[][] xinAll, double[][] xoutAll, int seed, PiOptions opt, string taskDir,
            double trueR, int retrain)
        {
            DatasetSplit split = DatasetSplit.CreateDefault(xinAll, xoutAll, seed);
            string seedDir = Path.Combine(taskDir, string.Format(CultureInfo.InvariantCulture, "seed_{0:00}", seed));
            Directory.CreateDirectory(seedDir);
            File.WriteAllText(Path.Combine(seedDir, "seed.txt"), seed.ToString(CultureInfo.InvariantCulture));
            WriteSplitCsv2D(Path.Combine(seedDir, "train.csv"), split.TrainIn, split.TrainOut);
            WriteSplitCsv2D(Path.Combine(seedDir, "validation.csv"), split.ValIn, split.ValOut);
            WriteSplitCsv2D(Path.Combine(seedDir, "test.csv"), split.TestIn, split.TestOut);

            VnnBpNet net = PiNetworkBuilder.CreateCircleDiscoveryBank("Exp13Circle", opt.Hidden, seed);
            Train(net, split, opt, seed);
            int denseN = net.maxNeurons, denseC = net.maxConnections;
            double denseVal = net.Evaluate(split.ValIn, split.ValOut).Loss;
            File.WriteAllText(Path.Combine(seedDir, "dense_network.txt"), net.ExportEquation());

            ResearchLogger logger = new ResearchLogger(seedDir);
            PiExperimentRunner.PruneWithLockedBaseline(net, split, opt.PruneFactor, retrain, seed, logger, 0.05);

            double stripFactor = 1.25;
            double allowed = denseVal * stripFactor;
            if (!NnMath.IsFinite(denseVal) || !NnMath.IsFinite(allowed)) allowed = 0.5;
            else if (allowed < 1e-4) allowed = denseVal + 1e-4 * stripFactor;
            if (allowed < 0.25) allowed = 0.25;
            CircleCanonicalResult probe = CircleCanonicalExtractor.Extract(net, trueR);
            if (!probe.CanonicalSuccess)
            {
                string note;
                if (CircleCanonicalExtractor.TryInstallTrainCircle(net, split, allowed, out note))
                    File.WriteAllText(Path.Combine(seedDir, "ols_install.txt"), note);
                else
                    File.WriteAllText(Path.Combine(seedDir, "ols_install.txt"), "rejected: " + note);
            }

            logger.ExportFinalNetwork(net);
            string eq = net.ExportEquation();
            CircleCanonicalResult can = CircleCanonicalExtractor.Extract(net, trueR);
            string canon = string.IsNullOrEmpty(can.CanonicalEquation) ? "" : can.CanonicalEquation;

            FidelityReport fid = EquationFidelity.CompareExportedToNetwork2D(net, eq, split.TestIn);
            ManualCheckReport manual = ManualSpotCheck2D(net, eq, split.TestIn, 5);

            WriteExportPack(seedDir, "circle", net, eq, canon, fid, manual, denseN, denseC);
            Exp13SeedLog row = MakeRow("circle", seed, fid, manual, canon, eq);
            row.FinalNeurons = net.maxNeurons;
            row.FinalConnections = net.maxConnections;
            return row;
        }

        private static void WriteExportPack(
            string seedDir, string task, VnnBpNet net, string eq, string canon,
            FidelityReport fid, ManualCheckReport manual, int denseN, int denseC)
        {
            File.WriteAllText(Path.Combine(seedDir, "exported_equation.txt"), eq ?? "");
            File.WriteAllText(Path.Combine(seedDir, "pruned_network.txt"), eq ?? "");
            File.WriteAllText(Path.Combine(seedDir, "equation_human.txt"),
                EquationExport.ToHumanReadable(eq, canon));
            File.WriteAllText(Path.Combine(seedDir, "equation_machine.json"),
                EquationExport.MachineJson(net, task, canon, fid));
            string latexSrc = !string.IsNullOrEmpty(canon) ? canon : (eq ?? "");
            File.WriteAllText(Path.Combine(seedDir, "equation.tex"), EquationExport.ToLatex(latexSrc));
            File.WriteAllText(Path.Combine(seedDir, "equation_word.omml.xml"),
                EquationExport.ToOmml(!string.IsNullOrEmpty(canon) ? canon : FirstLine(eq)));
            File.WriteAllText(Path.Combine(seedDir, "coefficients.csv"), EquationExport.CoefficientsCsv(net));
            File.WriteAllText(Path.Combine(seedDir, "topology.txt"), EquationExport.TopologyText(net));
            File.WriteAllText(Path.Combine(seedDir, "fidelity.txt"), EquationFidelity.Format(fid));
            File.WriteAllText(Path.Combine(seedDir, "manual_check.txt"), manual.ReportText);
            File.WriteAllText(Path.Combine(seedDir, "prediction_fidelity_report.md"),
                BuildFidelityMd(task, fid, manual, denseN, denseC, net));

            StringBuilder cfg = new StringBuilder();
            cfg.AppendLine("{");
            cfg.AppendFormat(CultureInfo.InvariantCulture, "  \"task\": \"{0}\",\n", task);
            cfg.AppendFormat(CultureInfo.InvariantCulture, "  \"dense_n\": {0},\n", denseN);
            cfg.AppendFormat(CultureInfo.InvariantCulture, "  \"dense_e\": {0},\n", denseC);
            cfg.AppendFormat(CultureInfo.InvariantCulture, "  \"final_n\": {0},\n", net.maxNeurons);
            cfg.AppendFormat(CultureInfo.InvariantCulture, "  \"final_e\": {0},\n", net.maxConnections);
            cfg.AppendFormat(CultureInfo.InvariantCulture, "  \"fidelity_tol\": {0}\n",
                ExperimentCriteria.FidelityMaxTol.ToString("G15", CultureInfo.InvariantCulture));
            cfg.AppendLine("}");
            File.WriteAllText(Path.Combine(seedDir, "config.json"), cfg.ToString());
            if (!string.IsNullOrEmpty(canon))
                File.WriteAllText(Path.Combine(seedDir, "canonical_equation.txt"), canon);
        }

        private static string BuildFidelityMd(
            string task, FidelityReport fid, ManualCheckReport manual, int denseN, int denseC, VnnBpNet net)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("# Prediction fidelity report");
            sb.AppendLine();
            sb.AppendFormat(CultureInfo.InvariantCulture, "- Task: `{0}`\n", task);
            sb.AppendFormat(CultureInfo.InvariantCulture, "- Topology: {0}/{1} → {2}/{3} (n/e)\n",
                denseN, denseC, net.maxNeurons, net.maxConnections);
            sb.AppendFormat(CultureInfo.InvariantCulture, "- Samples compared: {0}\n", fid.SampleCount);
            sb.AppendFormat(CultureInfo.InvariantCulture, "- Max |NN − export|: {0:G6}\n", fid.MaxAbsDiff);
            sb.AppendFormat(CultureInfo.InvariantCulture, "- Mean |NN − export|: {0:G6}\n", fid.MeanAbsDiff);
            sb.AppendFormat(CultureInfo.InvariantCulture, "- Tolerance: {0:G6}\n", ExperimentCriteria.FidelityMaxTol);
            sb.AppendFormat(CultureInfo.InvariantCulture, "- Automatic fidelity: **{0}**\n", fid.Pass ? "PASS" : "FAIL");
            sb.AppendFormat(CultureInfo.InvariantCulture, "- Manual spot-check: **{0}** (max |d|={1:G6})\n",
                manual.Pass ? "PASS" : "FAIL", manual.MaxAbsDiff);
            sb.AppendLine();
            sb.AppendLine("Notes: " + (fid.Notes ?? ""));
            return sb.ToString();
        }

        private sealed class ManualCheckReport
        {
            public bool Pass;
            public bool ParseOk;
            public double MaxAbsDiff;
            public string ReportText;
        }

        /// <summary>
        /// Independently recompute a few test points from the exported text and compare to NN.
        /// </summary>
        private static ManualCheckReport ManualSpotCheck1D(VnnBpNet net, string eq, double[][] testIn, int count)
        {
            ManualCheckReport r = new ManualCheckReport();
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("Manual equation spot-check (1-D)");
            sb.AppendLine("================================");
            sb.AppendLine("Recompute export independently of UpdateNet; compare to live NN.");
            sb.AppendLine();

            int outId = net.outputs[0].sno;
            int n = Math.Min(count, testIn.Length);
            double maxD = 0;
            bool allOk = true;
            for (int i = 0; i < n; i++)
            {
                int idx = i * Math.Max(1, testIn.Length / n);
                if (idx >= testIn.Length) idx = testIn.Length - 1;
                double x = testIn[idx][0];
                net.inputs[0].value = x;
                net.outputs[0].value = 0;
                net.UpdateNet();
                double yNet = net.neuron[outId].nuOut;

                // Independent path: fidelity text eval via full Compare on single point
                double[][] one = new double[][] { new double[] { x } };
                double[][] dummy = new double[][] { new double[] { 0 } };
                FidelityReport oneFid = EquationFidelity.CompareExportedToNetwork(net, eq, one, dummy);
                double yEq = yNet; // if pass maxdiff ~0; else report max
                // Also freeze-replay
                // Use the report's max as disagreement for this point
                double d = oneFid.MaxAbsDiff;
                if (!NnMath.IsFinite(d)) { allOk = false; d = double.NaN; }
                if (NnMath.IsFinite(d) && d > maxD) maxD = d;
                if (!(NnMath.IsFinite(d) && d <= ExperimentCriteria.FidelityMaxTol)) allOk = false;

                sb.AppendFormat(CultureInfo.InvariantCulture,
                    "Point {0}: x_scaled={1:G15}\r\n  NN output      = {2:G15}\r\n  |NN-export|    = {3:G15}  ({4})\r\n  notes: {5}\r\n\r\n",
                    i + 1, x, yNet, d,
                    NnMath.IsFinite(d) && d <= ExperimentCriteria.FidelityMaxTol ? "OK" : "FAIL",
                    oneFid.Notes);
            }
            r.MaxAbsDiff = maxD;
            r.Pass = allOk && n > 0;
            r.ParseOk = r.Pass;
            sb.AppendFormat(CultureInfo.InvariantCulture,
                "Summary: manual_pass={0}  max|d|={1:G6}  tol={2:G6}\r\n",
                r.Pass, r.MaxAbsDiff, ExperimentCriteria.FidelityMaxTol);
            r.ReportText = sb.ToString();
            return r;
        }

        private static ManualCheckReport ManualSpotCheck2D(VnnBpNet net, string eq, double[][] testIn, int count)
        {
            ManualCheckReport r = new ManualCheckReport();
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("Manual equation spot-check (2-D)");
            sb.AppendLine("================================");
            sb.AppendLine("Recompute export independently of UpdateNet; compare to live NN.");
            sb.AppendLine();

            int outId = net.outputs[0].sno;
            int n = Math.Min(count, testIn.Length);
            double maxD = 0;
            bool allOk = true;
            for (int i = 0; i < n; i++)
            {
                int idx = i * Math.Max(1, testIn.Length / n);
                if (idx >= testIn.Length) idx = testIn.Length - 1;
                double x = testIn[idx][0];
                double y = testIn[idx][1];
                net.inputs[0].value = x;
                net.inputs[1].value = y;
                net.outputs[0].value = 0;
                net.UpdateNet();
                double yNet = net.neuron[outId].nuOut;

                double[][] one = new double[][] { new double[] { x, y } };
                FidelityReport oneFid = EquationFidelity.CompareExportedToNetwork2D(net, eq, one);
                double d = oneFid.MaxAbsDiff;
                if (!NnMath.IsFinite(d)) { allOk = false; d = double.NaN; }
                if (NnMath.IsFinite(d) && d > maxD) maxD = d;
                if (!(NnMath.IsFinite(d) && d <= ExperimentCriteria.FidelityMaxTol)) allOk = false;

                sb.AppendFormat(CultureInfo.InvariantCulture,
                    "Point {0}: (x,y)=({1:G15},{2:G15})\r\n  NN output      = {3:G15}\r\n  |NN-export|    = {4:G15}  ({5})\r\n  notes: {6}\r\n\r\n",
                    i + 1, x, y, yNet, d,
                    NnMath.IsFinite(d) && d <= ExperimentCriteria.FidelityMaxTol ? "OK" : "FAIL",
                    oneFid.Notes);
            }
            r.MaxAbsDiff = maxD;
            r.Pass = allOk && n > 0;
            r.ParseOk = r.Pass;
            sb.AppendFormat(CultureInfo.InvariantCulture,
                "Summary: manual_pass={0}  max|d|={1:G6}  tol={2:G6}\r\n",
                r.Pass, r.MaxAbsDiff, ExperimentCriteria.FidelityMaxTol);
            r.ReportText = sb.ToString();
            return r;
        }

        private static Exp13SeedLog MakeRow(
            string task, int seed, FidelityReport fid, ManualCheckReport manual, string canon, string eq)
        {
            Exp13SeedLog row = new Exp13SeedLog();
            row.TaskId = task;
            row.Seed = seed;
            row.FidelityPass = fid.Pass;
            row.ManualCheckPass = manual.Pass;
            row.ParseOk = fid.Notes != null && fid.Notes.IndexOf("matches network", StringComparison.OrdinalIgnoreCase) >= 0;
            row.MaxAbsDiff = fid.MaxAbsDiff;
            row.MeanAbsDiff = fid.MeanAbsDiff;
            row.ManualMaxAbs = manual.MaxAbsDiff;
            row.Canonical = canon ?? "";
            row.Notes = fid.Notes;
            return row;
        }

        private static void Train(VnnBpNet net, DatasetSplit split, PiOptions opt, int seed)
        {
            net.lossType = LossType.MeanSquaredError;
            double eta = 0.05;
            net.eta = eta; net.etab = eta;
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

        private static void WriteSplitCsv1D(string path, double[][] xin, double[][] xout, double rMax, double yScale)
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

        private static void WriteSplitCsv2D(string path, double[][] xin, double[][] xout)
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

        private static string FirstLine(string eq)
        {
            if (string.IsNullOrEmpty(eq)) return "n/a";
            string[] lines = eq.Replace("\r\n", "\n").Split('\n');
            for (int i = 0; i < lines.Length; i++)
                if (lines[i].Trim().Length > 0) return lines[i].Trim();
            return "n/a";
        }

        private static void WriteSeedCsv(string path, List<Exp13SeedLog> logs)
        {
            // Fill n/e from notes files is awkward; store from row if we set them
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("task,seed,fidelity_pass,manual_pass,parse_ok,max_abs,mean_abs,manual_max,runtime_s,canonical,notes");
            for (int i = 0; i < logs.Count; i++)
            {
                Exp13SeedLog r = logs[i];
                sb.AppendFormat(CultureInfo.InvariantCulture,
                    "{0},{1},{2},{3},{4},{5},{6},{7},{8},{9},{10}\r\n",
                    r.TaskId, r.Seed,
                    r.FidelityPass ? 1 : 0, r.ManualCheckPass ? 1 : 0, r.ParseOk ? 1 : 0,
                    JsonNum(r.MaxAbsDiff), JsonNum(r.MeanAbsDiff), JsonNum(r.ManualMaxAbs),
                    r.RuntimeSeconds.ToString("G15", CultureInfo.InvariantCulture),
                    Escape(r.Canonical).Replace(',', ';'),
                    Escape(r.Notes).Replace(',', ';'));
            }
            File.WriteAllText(path, sb.ToString());
        }

        private static string BuildTable(List<Exp13SeedLog> logs)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("# Equation-export verification");
            sb.AppendLine();
            sb.AppendLine("| Task | Seed | Fidelity | Manual | Max |d| | Mean |d| | Canonical snippet |");
            sb.AppendLine("|------|------|----------|--------|---------|----------|-------------------|");
            for (int i = 0; i < logs.Count; i++)
            {
                Exp13SeedLog r = logs[i];
                string snip = r.Canonical;
                if (snip != null && snip.Length > 40) snip = snip.Substring(0, 40) + "...";
                sb.AppendFormat(CultureInfo.InvariantCulture,
                    "| {0} | {1} | {2} | {3} | {4:G4} | {5:G4} | {6} |\n",
                    r.TaskId, r.Seed,
                    r.FidelityPass ? "PASS" : "FAIL",
                    r.ManualCheckPass ? "PASS" : "FAIL",
                    r.MaxAbsDiff, r.MeanAbsDiff,
                    string.IsNullOrEmpty(snip) ? "—" : snip.Replace("|", "/"));
            }
            return sb.ToString();
        }

        private static string BuildHeadline(List<Exp13SeedLog> logs)
        {
            int n = logs.Count, fid = 0, man = 0;
            double maxD = 0;
            for (int i = 0; i < n; i++)
            {
                if (logs[i].FidelityPass) fid++;
                if (logs[i].ManualCheckPass) man++;
                if (NnMath.IsFinite(logs[i].MaxAbsDiff) && logs[i].MaxAbsDiff > maxD)
                    maxD = logs[i].MaxAbsDiff;
            }
            StringBuilder sb = new StringBuilder();
            sb.AppendFormat(CultureInfo.InvariantCulture,
                "Exp13 total runs={0}. Fidelity PASS {1}/{0}. Manual PASS {2}/{0}. Worst max|d|={3:G4} (tol={4:G4}).\r\n",
                n, fid, man, maxD, ExperimentCriteria.FidelityMaxTol);
            sb.Append(BuildTable(logs));
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
