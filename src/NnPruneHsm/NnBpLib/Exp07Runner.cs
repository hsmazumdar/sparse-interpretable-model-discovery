using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;

namespace VnnBp.VnnBpLib
{
    public sealed class Exp07SeedLog
    {
        public string ModelId;
        public string ModelName;
        public int Seed;
        public bool DoPrune;
        public bool PassE;
        public bool StructuralSuccess;
        public bool CanonicalSuccess;
        public bool ExplicitR2;
        public double RecoveredK;
        public double InterceptB;
        public double PiError;
        public double TestRmse;
        public int DenseNeurons;
        public int FinalNeurons;
        public int DenseConnections;
        public int FinalConnections;
        public int EquationTerms;
        public int SquareLinearHidden;
        public double DenseValLoss;
        public double FinalValLoss;
        public double FidelityMax;
        public bool FidelityPass;
        public string Reason;
        public string Equation;
        public double RuntimeSeconds;
    }

    /// <summary>
    /// Experiment 07 — operator ablation on pixel-area data (no Math.PI in labels).
    /// A: lin/sig only + prune
    /// B: mixed (quad available) + prune
    /// C: square-linear only + prune
    /// D: mixed, no prune
    /// E: mixed + prune + train-OLS Pass E (canonical install if needed)
    /// </summary>
    public static class Exp07Runner
    {
        private struct ModelSpec
        {
            public string Id;
            public string Name;
            public bool DoPrune;
            public bool PassE;
            public char Bank; // A lin/sig, B/D/E mixed, C quad-only
        }

        public static int Run(string[] args)
        {
            PiOptions opt = PiOptions.Parse(args);
            opt.RunArea = true;
            opt.RunCirc = false;
            opt.AllowFallback = false;
            bool quick = false;
            bool seedsSet = false;
            if (args != null)
            {
                for (int i = 0; i < args.Length; i++)
                {
                    if (args[i] == "--quick") quick = true;
                    if (args[i] == "--seeds") seedsSet = true;
                }
            }
            if (!quick && !seedsSet) opt.SeedCount = 15;
            if (!quick && opt.RadiusStep == 10) opt.RadiusStep = 2;
            if (!quick && opt.DenseEpochs == 400) opt.DenseEpochs = 150;
            if (!quick && opt.RetrainCycles == 10000) opt.RetrainCycles = 2500;
            if (quick && opt.RadiusStep < 5) opt.RadiusStep = 10;

            string root = FindProjectRoot();
            if (string.IsNullOrEmpty(opt.ResultsDir) || opt.ResultsDir.IndexOf("PiExperiment", StringComparison.OrdinalIgnoreCase) >= 0)
                opt.ResultsDir = Path.Combine(root, "Results", "Exp07_OperatorAblation");
            Directory.CreateDirectory(opt.ResultsDir);
            GitHubMirrorPublisher.EnsureLayout(root);
            GitHubMirrorPublisher.CopySource(root);

            ModelSpec[] models = BuildModels();
            StringBuilder console = new StringBuilder();
            Log(console, "Experiment 07 — operator ablation (area law)");
            Log(console, "Results: " + opt.ResultsDir);
            Log(console, "Seeds: " + opt.SeedCount + "  hidden=" + opt.Hidden + "  epochs=" + opt.DenseEpochs);
            Log(console, "Radii: " + opt.RadiusLo + ".." + opt.RadiusHi + " step " + opt.RadiusStep);
            Log(console, "Models: A lin/sig+prune | B mixed+prune | C quad-only+prune | D mixed no-prune | E mixed+prune+PassE");
            Log(console, "π is not used to label data or accept pruning.");

            int[] radii = PiDataGenerator.RadiusRange(opt.RadiusLo, opt.RadiusHi, opt.RadiusStep);
            PiSample[] area = PiDataGenerator.GenerateAreaData(radii, opt.GridResolution);
            if (opt.MinRadius > 0)
                area = PiDataGenerator.FilterMinRadius(area, opt.MinRadius);
            PiDataGenerator.SaveCsv(Path.Combine(opt.ResultsDir, "area_data.csv"), area);
            Log(console, "Samples: " + area.Length);

            double meanA = 0;
            for (int i = 0; i < area.Length; i++) meanA += Math.Abs(area[i].Y);
            if (area.Length > 0) meanA /= area.Length;

            List<Exp07SeedLog> all = new List<Exp07SeedLog>();
            string status = GitHubMirrorPublisher.BuildStatusExp07("running", all, "");
            File.WriteAllText(Path.Combine(opt.ResultsDir, "STATUS.md"), status);

            for (int m = 0; m < models.Length; m++)
            {
                ModelSpec spec = models[m];
                string modelDir = Path.Combine(opt.ResultsDir, "Model_" + spec.Id);
                Directory.CreateDirectory(modelDir);
                Log(console, "");
                Log(console, "=== Model " + spec.Id + ": " + spec.Name + " ===");

                List<Exp07SeedLog> batch = new List<Exp07SeedLog>();
                for (int s = 1; s <= opt.SeedCount; s++)
                {
                    Log(console, "Model " + spec.Id + " seed " + s + "/" + opt.SeedCount + "...");
                    Stopwatch sw = Stopwatch.StartNew();
                    Exp07SeedLog row = RunOne(area, meanA, s, opt, spec, modelDir);
                    sw.Stop();
                    row.RuntimeSeconds = sw.Elapsed.TotalSeconds;
                    string seedDir = Path.Combine(modelDir, string.Format(CultureInfo.InvariantCulture, "seed_{0:00}", s));
                    File.WriteAllText(Path.Combine(seedDir, "runtime.txt"),
                        row.RuntimeSeconds.ToString("G15", CultureInfo.InvariantCulture) + " seconds");
                    batch.Add(row);
                    all.Add(row);

                    string headline = BuildHeadline(all, models);
                    bool more = !(m == models.Length - 1 && s == opt.SeedCount);
                    status = GitHubMirrorPublisher.BuildStatusExp07(
                        more ? ("running " + spec.Id + " " + s + "/" + opt.SeedCount) : "complete",
                        all, headline);
                    File.WriteAllText(Path.Combine(opt.ResultsDir, "STATUS.md"), status);
                    GitHubMirrorPublisher.PublishSeed(root, Path.Combine("Exp07_OperatorAblation", "Model_" + spec.Id), seedDir, status);

                    Log(console, string.Format(CultureInfo.InvariantCulture,
                        "  canon={0}  r2={1}  rmse={2:G4}  n {3}->{4}  e {5}->{6}  k={7:G6}",
                        row.CanonicalSuccess ? "YES" : "NO",
                        row.ExplicitR2 ? "YES" : "NO",
                        row.TestRmse,
                        row.DenseNeurons, row.FinalNeurons,
                        row.DenseConnections, row.FinalConnections,
                        row.CanonicalSuccess ? row.RecoveredK : double.NaN));
                }
                WriteSeedCsv(Path.Combine(modelDir, "seed_logs.csv"), batch);
                File.WriteAllText(Path.Combine(modelDir, "headline.txt"), BuildModelHeadline(batch, spec));
            }

            string table = BuildTableD(all, models);
            string headline2 = BuildHeadline(all, models);
            File.WriteAllText(Path.Combine(opt.ResultsDir, "TableD_Ablation.md"), table);
            File.WriteAllText(Path.Combine(opt.ResultsDir, "headline.txt"), headline2);
            File.WriteAllText(Path.Combine(opt.ResultsDir, "summary.md"),
                "# Experiment 07 — operator ablation\r\n\r\n" + headline2 + "\r\n" + table);
            WriteSeedCsv(Path.Combine(opt.ResultsDir, "seed_logs.csv"), all);
            status = GitHubMirrorPublisher.BuildStatusExp07("complete", all, headline2);
            File.WriteAllText(Path.Combine(GitHubMirrorPublisher.MirrorRoot(root), "STATUS.md"), status);
            File.WriteAllText(Path.Combine(opt.ResultsDir, "STATUS.md"), status);
            GitHubMirrorPublisher.PublishFile(root, "Exp07_OperatorAblation", Path.Combine(opt.ResultsDir, "seed_logs.csv"));
            GitHubMirrorPublisher.PublishFile(root, "Exp07_OperatorAblation", Path.Combine(opt.ResultsDir, "TableD_Ablation.md"));
            GitHubMirrorPublisher.PublishFile(root, "Exp07_OperatorAblation", Path.Combine(opt.ResultsDir, "headline.txt"));
            GitHubMirrorPublisher.PublishFile(root, "Exp07_OperatorAblation", Path.Combine(opt.ResultsDir, "summary.md"));

            Log(console, "");
            Log(console, headline2);
            Log(console, "Experiment Complete.");
            File.WriteAllText(Path.Combine(opt.ResultsDir, "console.log"), console.ToString());
            return 0;
        }

        private static ModelSpec[] BuildModels()
        {
            return new ModelSpec[]
            {
                Spec("A", "lin/sig only + prune", true, false, 'A'),
                Spec("B", "mixed (quad available) + prune", true, false, 'B'),
                Spec("C", "square-linear only + prune", true, false, 'C'),
                Spec("D", "mixed, no prune", false, false, 'D'),
                Spec("E", "mixed + prune + Pass E", true, true, 'E'),
            };
        }

        private static ModelSpec Spec(string id, string name, bool prune, bool passE, char bank)
        {
            ModelSpec s = new ModelSpec();
            s.Id = id; s.Name = name; s.DoPrune = prune; s.PassE = passE; s.Bank = bank;
            return s;
        }

        private static Exp07SeedLog RunOne(
            PiSample[] samples, double meanA, int seed, PiOptions opt, ModelSpec spec, string modelDir)
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

            string seedDir = Path.Combine(modelDir, string.Format(CultureInfo.InvariantCulture, "seed_{0:00}", seed));
            Directory.CreateDirectory(seedDir);
            File.WriteAllText(Path.Combine(seedDir, "seed.txt"), seed.ToString(CultureInfo.InvariantCulture));
            File.WriteAllText(Path.Combine(seedDir, "model.txt"), spec.Id + " " + spec.Name);

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
            FidelityReport fid = EquationFidelity.CompareExportedToNetwork(net, eq, split.TestIn, split.TestOut);
            File.WriteAllText(Path.Combine(seedDir, "fidelity.txt"), EquationFidelity.Format(fid));

            double testRmse = RmsePhysical(net, split.TestIn, split.TestOut, yScale);
            double pi = PiDataGenerator.ReferencePi();
            double piErr = can.CanonicalSuccess && NnMath.IsFinite(can.SlopeK)
                ? Math.Abs(can.SlopeK - pi) / pi
                : double.NaN;

            Exp07SeedLog row = new Exp07SeedLog();
            row.ModelId = spec.Id;
            row.ModelName = spec.Name;
            row.Seed = seed;
            row.DoPrune = spec.DoPrune;
            row.PassE = spec.PassE;
            row.StructuralSuccess = can.StructuralSuccess;
            row.CanonicalSuccess = can.CanonicalSuccess;
            row.ExplicitR2 = can.StructuralSuccess || can.CanonicalSuccess
                || PiNetworkBuilder.CountHiddenOfType(net, 7) > 0
                || PiNetworkBuilder.CountHiddenOfType(net, 4) > 0;
            row.RecoveredK = can.CanonicalSuccess ? can.SlopeK : 0;
            row.InterceptB = can.InterceptB;
            row.PiError = piErr;
            row.TestRmse = testRmse;
            row.DenseNeurons = denseN;
            row.FinalNeurons = net.maxNeurons;
            row.DenseConnections = denseC;
            row.FinalConnections = net.maxConnections;
            row.EquationTerms = CountEquationTerms(eq);
            row.SquareLinearHidden = PiNetworkBuilder.CountHiddenOfType(net, 7);
            row.DenseValLoss = denseVal;
            row.FinalValLoss = finalVal.Loss;
            row.FidelityMax = fid.MaxAbsDiff;
            row.FidelityPass = fid.Pass;
            row.Reason = can.Reason;
            row.Equation = can.CanonicalEquation;
            if (can.CanonicalSuccess)
                File.WriteAllText(Path.Combine(seedDir, "canonical_equation.txt"), can.CanonicalEquation);
            else
                File.WriteAllText(Path.Combine(seedDir, "canonical_equation.txt"), "non-canonical\n" + can.Reason);

            StringBuilder met = new StringBuilder();
            met.AppendLine("{");
            met.AppendFormat(CultureInfo.InvariantCulture, "  \"model\": \"{0}\",\n", spec.Id);
            met.AppendFormat(CultureInfo.InvariantCulture, "  \"seed\": {0},\n", seed);
            met.AppendFormat(CultureInfo.InvariantCulture, "  \"canonical\": {0},\n", can.CanonicalSuccess ? "true" : "false");
            met.AppendFormat(CultureInfo.InvariantCulture, "  \"explicit_r2\": {0},\n", row.ExplicitR2 ? "true" : "false");
            met.AppendFormat(CultureInfo.InvariantCulture, "  \"test_rmse\": {0},\n", JsonNum(testRmse));
            met.AppendFormat(CultureInfo.InvariantCulture, "  \"final_neurons\": {0},\n", net.maxNeurons);
            met.AppendFormat(CultureInfo.InvariantCulture, "  \"final_edges\": {0},\n", net.maxConnections);
            met.AppendFormat(CultureInfo.InvariantCulture, "  \"k\": {0}\n", JsonNum(can.CanonicalSuccess ? can.SlopeK : double.NaN));
            met.AppendLine("}");
            File.WriteAllText(Path.Combine(seedDir, "metrics.json"), met.ToString());
            return row;
        }

        private static VnnBpNet CreateBank(ModelSpec spec, int hidden, int seed)
        {
            string name = "Exp07_" + spec.Id;
            if (spec.Bank == 'A')
                return PiNetworkBuilder.CreateLinSigOnlyBank(name, hidden, seed);
            if (spec.Bank == 'C')
                return PiNetworkBuilder.CreateQuadOnlyBank(name, hidden, seed);
            return PiNetworkBuilder.CreateAreaDiscoveryBank(name, hidden, seed);
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

        private static void WriteSeedCsv(string path, List<Exp07SeedLog> logs)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("model,seed,prune,passe,canonical,explicit_r2,k,b,pi_error,test_rmse,dense_n,final_n,dense_e,final_e,terms,sqlin,val_loss,fid_max,reason");
            for (int i = 0; i < logs.Count; i++)
            {
                Exp07SeedLog r = logs[i];
                sb.AppendFormat(CultureInfo.InvariantCulture,
                    "{0},{1},{2},{3},{4},{5},{6},{7},{8},{9},{10},{11},{12},{13},{14},{15},{16},{17},{18}\r\n",
                    r.ModelId, r.Seed, r.DoPrune ? 1 : 0, r.PassE ? 1 : 0,
                    r.CanonicalSuccess ? 1 : 0, r.ExplicitR2 ? 1 : 0,
                    JsonNum(r.CanonicalSuccess ? r.RecoveredK : double.NaN),
                    JsonNum(r.InterceptB), JsonNum(r.PiError), JsonNum(r.TestRmse),
                    r.DenseNeurons, r.FinalNeurons, r.DenseConnections, r.FinalConnections,
                    r.EquationTerms, r.SquareLinearHidden, JsonNum(r.FinalValLoss), JsonNum(r.FidelityMax),
                    Escape(r.Reason).Replace(',', ';'));
            }
            File.WriteAllText(path, sb.ToString());
        }

        private static string BuildTableD(List<Exp07SeedLog> logs, ModelSpec[] models)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("# Table D — Operator ablation (area)");
            sb.AppendLine();
            sb.AppendLine("| Model | Description | n | Canon % | Explicit r² % | Mean test RMSE | Mean final neurons | Mean final edges | Mean terms | Mean |k-π|/π % |");
            sb.AppendLine("|-------|-------------|---|---------|---------------|----------------|--------------------|------------------|------------|----------------|");
            for (int m = 0; m < models.Length; m++)
            {
                ModelSpec spec = models[m];
                int n = 0, can = 0, r2 = 0;
                double sumRmse = 0, sumNu = 0, sumE = 0, sumT = 0, sumPi = 0;
                int nPi = 0;
                for (int i = 0; i < logs.Count; i++)
                {
                    if (logs[i].ModelId != spec.Id) continue;
                    n++;
                    if (logs[i].CanonicalSuccess) can++;
                    if (logs[i].ExplicitR2) r2++;
                    sumRmse += logs[i].TestRmse;
                    sumNu += logs[i].FinalNeurons;
                    sumE += logs[i].FinalConnections;
                    sumT += logs[i].EquationTerms;
                    if (logs[i].CanonicalSuccess && NnMath.IsFinite(logs[i].PiError))
                    {
                        sumPi += logs[i].PiError;
                        nPi++;
                    }
                }
                if (n == 0) continue;
                sb.AppendFormat(CultureInfo.InvariantCulture,
                    "| {0} | {1} | {2} | {3:F0} | {4:F0} | {5:G4} | {6:F1} | {7:F1} | {8:F1} | {9} |\n",
                    spec.Id, spec.Name, n,
                    100.0 * can / n, 100.0 * r2 / n,
                    sumRmse / n, sumNu / n, sumE / n, sumT / n,
                    nPi > 0 ? (100.0 * sumPi / nPi).ToString("F3", CultureInfo.InvariantCulture) : "—");
            }
            return sb.ToString();
        }

        private static string BuildModelHeadline(List<Exp07SeedLog> logs, ModelSpec spec)
        {
            int n = logs.Count, can = 0, r2 = 0;
            double sumRmse = 0;
            for (int i = 0; i < n; i++)
            {
                if (logs[i].CanonicalSuccess) can++;
                if (logs[i].ExplicitR2) r2++;
                sumRmse += logs[i].TestRmse;
            }
            return string.Format(CultureInfo.InvariantCulture,
                "Model {0}: canon {1}/{2}, r2 {3}/{2}, mean RMSE={4:G4}.\r\n",
                spec.Id, can, n, r2, n > 0 ? sumRmse / n : 0);
        }

        private static string BuildHeadline(List<Exp07SeedLog> logs, ModelSpec[] models)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendFormat(CultureInfo.InvariantCulture, "Exp07 total runs={0}.\r\n", logs.Count);
            sb.Append(BuildTableD(logs, models));
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
