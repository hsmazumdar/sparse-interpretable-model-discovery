using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;

namespace VnnBp.VnnBpLib
{
    public sealed class Exp08SeedLog
    {
        public string SettingId;
        public string SettingName;
        public int RetrainCycles;
        public bool DoPrune;
        public int Seed;
        public bool CanonicalSuccess;
        public bool StructuralSuccess;
        public double RecoveredK;
        public double PiError;
        public double DenseValLoss;
        public double FinalValLoss;
        public double TestRmse;
        public double TestLoss;
        public int DenseNeurons;
        public int FinalNeurons;
        public int DenseConnections;
        public int FinalConnections;
        public double RuntimeSeconds;
        public string Reason;
    }

    /// <summary>
    /// Experiment 08 — pruning / post-prune retrain ablation on area data.
    /// Settings: no prune; prune + 0 / 1k / 10k / 50k sample updates.
    /// Pass E is disabled so results isolate the retrain budget.
    /// </summary>
    public static class Exp08Runner
    {
        private struct Setting
        {
            public string Id;
            public string Name;
            public bool DoPrune;
            public int RetrainCycles;
        }

        public static int Run(string[] args)
        {
            PiOptions opt = PiOptions.Parse(args);
            opt.RunArea = true;
            opt.RunCirc = false;
            opt.AllowFallback = false;
            bool quick = false;
            bool seedsSet = false;
            bool include50k = true;
            if (args != null)
            {
                for (int i = 0; i < args.Length; i++)
                {
                    if (args[i] == "--quick") quick = true;
                    if (args[i] == "--seeds") seedsSet = true;
                    if (args[i] == "--no-50k") include50k = false;
                }
            }
            if (!quick && !seedsSet) opt.SeedCount = 12;
            if (!quick && opt.RadiusStep == 10) opt.RadiusStep = 2;
            if (!quick && opt.DenseEpochs == 400) opt.DenseEpochs = 150;
            if (quick)
            {
                include50k = false;
                if (opt.RadiusStep < 5) opt.RadiusStep = 10;
            }

            string root = FindProjectRoot();
            if (string.IsNullOrEmpty(opt.ResultsDir) || opt.ResultsDir.IndexOf("PiExperiment", StringComparison.OrdinalIgnoreCase) >= 0)
                opt.ResultsDir = Path.Combine(root, "Results", "Exp08_PruningAblation");
            Directory.CreateDirectory(opt.ResultsDir);
            GitHubMirrorPublisher.EnsureLayout(root);
            GitHubMirrorPublisher.CopySource(root);

            Setting[] settings = BuildSettings(include50k);
            StringBuilder console = new StringBuilder();
            Log(console, "Experiment 08 — pruning / retrain ablation (area)");
            Log(console, "Results: " + opt.ResultsDir);
            Log(console, "Seeds: " + opt.SeedCount + "  epochs=" + opt.DenseEpochs);
            Log(console, "Radii: " + opt.RadiusLo + ".." + opt.RadiusHi + " step " + opt.RadiusStep);
            Log(console, "Pass E disabled (isolates retrain budget). π not an objective.");

            int[] radii = PiDataGenerator.RadiusRange(opt.RadiusLo, opt.RadiusHi, opt.RadiusStep);
            PiSample[] area = PiDataGenerator.GenerateAreaData(radii, opt.GridResolution);
            if (opt.MinRadius > 0)
                area = PiDataGenerator.FilterMinRadius(area, opt.MinRadius);
            PiDataGenerator.SaveCsv(Path.Combine(opt.ResultsDir, "area_data.csv"), area);
            Log(console, "Samples: " + area.Length);

            double meanA = 0;
            for (int i = 0; i < area.Length; i++) meanA += Math.Abs(area[i].Y);
            if (area.Length > 0) meanA /= area.Length;

            List<Exp08SeedLog> all = new List<Exp08SeedLog>();
            string status = GitHubMirrorPublisher.BuildStatusExp08("running", all, "");
            File.WriteAllText(Path.Combine(opt.ResultsDir, "STATUS.md"), status);

            for (int si = 0; si < settings.Length; si++)
            {
                Setting set = settings[si];
                string setDir = Path.Combine(opt.ResultsDir, "Setting_" + set.Id);
                Directory.CreateDirectory(setDir);
                Log(console, "");
                Log(console, "=== " + set.Id + ": " + set.Name + " ===");

                List<Exp08SeedLog> batch = new List<Exp08SeedLog>();
                for (int s = 1; s <= opt.SeedCount; s++)
                {
                    Log(console, set.Id + " seed " + s + "/" + opt.SeedCount + "...");
                    Stopwatch sw = Stopwatch.StartNew();
                    Exp08SeedLog row = RunOne(area, meanA, s, opt, set, setDir);
                    sw.Stop();
                    row.RuntimeSeconds = sw.Elapsed.TotalSeconds;
                    string seedDir = Path.Combine(setDir, string.Format(CultureInfo.InvariantCulture, "seed_{0:00}", s));
                    File.WriteAllText(Path.Combine(seedDir, "runtime.txt"),
                        row.RuntimeSeconds.ToString("G15", CultureInfo.InvariantCulture) + " seconds");
                    batch.Add(row);
                    all.Add(row);

                    string headline = BuildHeadline(all, settings);
                    bool more = !(si == settings.Length - 1 && s == opt.SeedCount);
                    status = GitHubMirrorPublisher.BuildStatusExp08(
                        more ? ("running " + set.Id + " " + s + "/" + opt.SeedCount) : "complete",
                        all, headline);
                    File.WriteAllText(Path.Combine(opt.ResultsDir, "STATUS.md"), status);
                    GitHubMirrorPublisher.PublishSeed(root,
                        Path.Combine("Exp08_PruningAblation", "Setting_" + set.Id), seedDir, status);

                    Log(console, string.Format(CultureInfo.InvariantCulture,
                        "  canon={0}  Lval={1:G4}  rmse={2:G4}  n {3}->{4}  e {5}->{6}  t={7:F1}s",
                        row.CanonicalSuccess ? "YES" : "NO",
                        row.FinalValLoss, row.TestRmse,
                        row.DenseNeurons, row.FinalNeurons,
                        row.DenseConnections, row.FinalConnections,
                        row.RuntimeSeconds));
                }
                WriteSeedCsv(Path.Combine(setDir, "seed_logs.csv"), batch);
                File.WriteAllText(Path.Combine(setDir, "headline.txt"), BuildSettingHeadline(batch, set));
            }

            string table = BuildTable(all, settings);
            string headline2 = BuildHeadline(all, settings);
            File.WriteAllText(Path.Combine(opt.ResultsDir, "Table_PruneAblation.md"), table);
            File.WriteAllText(Path.Combine(opt.ResultsDir, "headline.txt"), headline2);
            File.WriteAllText(Path.Combine(opt.ResultsDir, "summary.md"),
                "# Experiment 08 — pruning ablation\r\n\r\n" + headline2 + "\r\n" + table);
            WriteSeedCsv(Path.Combine(opt.ResultsDir, "seed_logs.csv"), all);
            status = GitHubMirrorPublisher.BuildStatusExp08("complete", all, headline2);
            File.WriteAllText(Path.Combine(GitHubMirrorPublisher.MirrorRoot(root), "STATUS.md"), status);
            File.WriteAllText(Path.Combine(opt.ResultsDir, "STATUS.md"), status);
            GitHubMirrorPublisher.PublishFile(root, "Exp08_PruningAblation", Path.Combine(opt.ResultsDir, "seed_logs.csv"));
            GitHubMirrorPublisher.PublishFile(root, "Exp08_PruningAblation", Path.Combine(opt.ResultsDir, "Table_PruneAblation.md"));
            GitHubMirrorPublisher.PublishFile(root, "Exp08_PruningAblation", Path.Combine(opt.ResultsDir, "headline.txt"));
            GitHubMirrorPublisher.PublishFile(root, "Exp08_PruningAblation", Path.Combine(opt.ResultsDir, "summary.md"));

            Log(console, "");
            Log(console, headline2);
            Log(console, "Experiment Complete.");
            File.WriteAllText(Path.Combine(opt.ResultsDir, "console.log"), console.ToString());
            return 0;
        }

        private static Setting[] BuildSettings(bool include50k)
        {
            List<Setting> list = new List<Setting>();
            list.Add(S("none", "no pruning", false, 0));
            list.Add(S("r0", "prune, 0 retrain cycles", true, 0));
            list.Add(S("r1k", "prune, 1,000 sample updates", true, 1000));
            list.Add(S("r10k", "prune, 10,000 sample updates", true, 10000));
            if (include50k)
                list.Add(S("r50k", "prune, 50,000 sample updates", true, 50000));
            return list.ToArray();
        }

        private static Setting S(string id, string name, bool prune, int cycles)
        {
            Setting s = new Setting();
            s.Id = id; s.Name = name; s.DoPrune = prune; s.RetrainCycles = cycles;
            return s;
        }

        private static Exp08SeedLog RunOne(
            PiSample[] samples, double meanA, int seed, PiOptions opt, Setting set, string setDir)
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
            File.WriteAllText(Path.Combine(seedDir, "setting.txt"), set.Id + " cycles=" + set.RetrainCycles);

            VnnBpNet net = PiNetworkBuilder.CreateAreaDiscoveryBank("Exp08_" + set.Id, opt.Hidden, seed);
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

            if (set.DoPrune)
            {
                PiExperimentRunner.PruneWithLockedBaseline(
                    net, split, opt.PruneFactor, set.RetrainCycles, seed, logger, eta);
                // Light sigmoid strip using the same retrain budget (0 stays 0).
                int strip = set.RetrainCycles;
                if (strip > 3000) strip = 3000;
                if (set.RetrainCycles > 0)
                    PiExperimentRunner.PruneSigmoidPathsForCanonical(
                        net, split, Math.Max(opt.PruneFactor, 1.25), strip, seed, logger, eta, denseVal);
            }

            EvaluationResult finalVal = net.Evaluate(split.ValIn, split.ValOut);
            EvaluationResult finalTest = net.Evaluate(split.TestIn, split.TestOut);
            logger.ExportFinalNetwork(net);
            string eq = net.ExportEquation();
            File.WriteAllText(Path.Combine(seedDir, "exported_equation.txt"), eq);

            CanonicalResult can = CanonicalExtractor.ExtractArea(net, rMax, yScale);
            CanonicalExtractor.ApplyInterceptGate(can, meanA);
            double testRmse = RmsePhysical(net, split.TestIn, split.TestOut, yScale);
            double pi = PiDataGenerator.ReferencePi();
            double piErr = can.CanonicalSuccess && NnMath.IsFinite(can.SlopeK)
                ? Math.Abs(can.SlopeK - pi) / pi : double.NaN;

            Exp08SeedLog row = new Exp08SeedLog();
            row.SettingId = set.Id;
            row.SettingName = set.Name;
            row.RetrainCycles = set.RetrainCycles;
            row.DoPrune = set.DoPrune;
            row.Seed = seed;
            row.CanonicalSuccess = can.CanonicalSuccess;
            row.StructuralSuccess = can.StructuralSuccess;
            row.RecoveredK = can.CanonicalSuccess ? can.SlopeK : 0;
            row.PiError = piErr;
            row.DenseValLoss = denseVal;
            row.FinalValLoss = finalVal.Loss;
            row.TestRmse = testRmse;
            row.TestLoss = finalTest.Loss;
            row.DenseNeurons = denseN;
            row.FinalNeurons = net.maxNeurons;
            row.DenseConnections = denseC;
            row.FinalConnections = net.maxConnections;
            row.Reason = can.Reason;

            StringBuilder met = new StringBuilder();
            met.AppendLine("{");
            met.AppendFormat(CultureInfo.InvariantCulture, "  \"setting\": \"{0}\",\n", set.Id);
            met.AppendFormat(CultureInfo.InvariantCulture, "  \"retrain_cycles\": {0},\n", set.RetrainCycles);
            met.AppendFormat(CultureInfo.InvariantCulture, "  \"canonical\": {0},\n", can.CanonicalSuccess ? "true" : "false");
            met.AppendFormat(CultureInfo.InvariantCulture, "  \"val_loss\": {0},\n", JsonNum(finalVal.Loss));
            met.AppendFormat(CultureInfo.InvariantCulture, "  \"test_rmse\": {0},\n", JsonNum(testRmse));
            met.AppendFormat(CultureInfo.InvariantCulture, "  \"final_edges\": {0}\n", net.maxConnections);
            met.AppendLine("}");
            File.WriteAllText(Path.Combine(seedDir, "metrics.json"), met.ToString());
            if (can.CanonicalSuccess)
                File.WriteAllText(Path.Combine(seedDir, "canonical_equation.txt"), can.CanonicalEquation);
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
                double d = net.neuron[oi].nuOut * yScale - xout[i][0] * yScale;
                s += d * d;
            }
            return Math.Sqrt(s / xin.Length);
        }

        private static void WriteSeedCsv(string path, List<Exp08SeedLog> logs)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("setting,seed,retrain,prune,canonical,structural,k,pi_error,dense_val,final_val,test_loss,test_rmse,dense_n,final_n,dense_e,final_e,runtime_s,reason");
            for (int i = 0; i < logs.Count; i++)
            {
                Exp08SeedLog r = logs[i];
                sb.AppendFormat(CultureInfo.InvariantCulture,
                    "{0},{1},{2},{3},{4},{5},{6},{7},{8},{9},{10},{11},{12},{13},{14},{15},{16},{17}\r\n",
                    r.SettingId, r.Seed, r.RetrainCycles, r.DoPrune ? 1 : 0,
                    r.CanonicalSuccess ? 1 : 0, r.StructuralSuccess ? 1 : 0,
                    JsonNum(r.CanonicalSuccess ? r.RecoveredK : double.NaN), JsonNum(r.PiError),
                    JsonNum(r.DenseValLoss), JsonNum(r.FinalValLoss), JsonNum(r.TestLoss), JsonNum(r.TestRmse),
                    r.DenseNeurons, r.FinalNeurons, r.DenseConnections, r.FinalConnections,
                    r.RuntimeSeconds.ToString("G15", CultureInfo.InvariantCulture),
                    Escape(r.Reason).Replace(',', ';'));
            }
            File.WriteAllText(path, sb.ToString());
        }

        private static string BuildTable(List<Exp08SeedLog> logs, Setting[] settings)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("# Pruning ablation (area)");
            sb.AppendLine();
            sb.AppendLine("| Setting | Retrain | n | Canon % | Struct % | Mean L_val | Mean test RMSE | Mean neurons | Mean edges | Mean runtime s |");
            sb.AppendLine("|---------|---------|---|---------|----------|------------|----------------|--------------|------------|----------------|");
            for (int s = 0; s < settings.Length; s++)
            {
                Setting set = settings[s];
                int n = 0, can = 0, str = 0;
                double sumV = 0, sumR = 0, sumNu = 0, sumE = 0, sumT = 0;
                for (int i = 0; i < logs.Count; i++)
                {
                    if (logs[i].SettingId != set.Id) continue;
                    n++;
                    if (logs[i].CanonicalSuccess) can++;
                    if (logs[i].StructuralSuccess) str++;
                    sumV += logs[i].FinalValLoss;
                    sumR += logs[i].TestRmse;
                    sumNu += logs[i].FinalNeurons;
                    sumE += logs[i].FinalConnections;
                    sumT += logs[i].RuntimeSeconds;
                }
                if (n == 0) continue;
                sb.AppendFormat(CultureInfo.InvariantCulture,
                    "| {0} | {1} | {2} | {3:F0} | {4:F0} | {5:G4} | {6:G4} | {7:F1} | {8:F1} | {9:F2} |\n",
                    set.Name, set.DoPrune ? set.RetrainCycles.ToString(CultureInfo.InvariantCulture) : "—",
                    n, 100.0 * can / n, 100.0 * str / n,
                    sumV / n, sumR / n, sumNu / n, sumE / n, sumT / n);
            }
            return sb.ToString();
        }

        private static string BuildSettingHeadline(List<Exp08SeedLog> logs, Setting set)
        {
            int n = logs.Count, can = 0;
            double sumR = 0, sumE = 0;
            for (int i = 0; i < n; i++)
            {
                if (logs[i].CanonicalSuccess) can++;
                sumR += logs[i].TestRmse;
                sumE += logs[i].FinalConnections;
            }
            return string.Format(CultureInfo.InvariantCulture,
                "{0}: canon {1}/{2}, mean RMSE={3:G4}, mean edges={4:F1}.\r\n",
                set.Id, can, n, n > 0 ? sumR / n : 0, n > 0 ? sumE / n : 0);
        }

        private static string BuildHeadline(List<Exp08SeedLog> logs, Setting[] settings)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendFormat(CultureInfo.InvariantCulture, "Exp08 total runs={0}.\r\n", logs.Count);
            sb.Append(BuildTable(logs, settings));
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
