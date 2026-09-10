using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;

namespace VnnBp.VnnBpLib
{
    public sealed class Exp10SeedLog
    {
        public string FactorId;
        public double PruneFactor;
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
        public int EquationTerms;
        public double Compression;
        public double RuntimeSeconds;
        public string Reason;
    }

    /// <summary>
    /// Experiment 10 — pruning validation-loss tolerance sensitivity (area).
    /// Sweeps prune factors (allowed L_val ≤ factor × dense L_val).
    /// Pass E off so Pareto isolates tolerance. Records performance, compression,
    /// equation complexity, and canonical rate.
    /// </summary>
    public static class Exp10Runner
    {
        private static readonly double[] DefaultFactors = new double[] { 1.0, 1.05, 1.1, 1.25, 1.5, 2.0 };

        public static int Run(string[] args)
        {
            PiOptions opt = PiOptions.Parse(args);
            opt.RunArea = true;
            opt.RunCirc = false;
            opt.AllowFallback = false;
            bool quick = false;
            bool seedsSet = false;
            double[] factors = DefaultFactors;
            if (args != null)
            {
                for (int i = 0; i < args.Length; i++)
                {
                    if (args[i] == "--quick") quick = true;
                    if (args[i] == "--seeds") seedsSet = true;
                    if (args[i] == "--factors" && i + 1 < args.Length)
                        factors = ParseFactors(args[++i]);
                }
            }
            if (!quick && !seedsSet) opt.SeedCount = 10;
            if (!quick && opt.RadiusStep == 10) opt.RadiusStep = 2;
            if (!quick && opt.DenseEpochs == 400) opt.DenseEpochs = 150;
            if (quick)
            {
                if (ReferenceEquals(factors, DefaultFactors))
                    factors = new double[] { 1.0, 1.1, 1.5 };
                if (opt.RadiusStep < 5) opt.RadiusStep = 10;
            }
            if (factors == null || factors.Length == 0)
                factors = DefaultFactors;

            string root = FindProjectRoot();
            if (string.IsNullOrEmpty(opt.ResultsDir) || opt.ResultsDir.IndexOf("PiExperiment", StringComparison.OrdinalIgnoreCase) >= 0)
                opt.ResultsDir = Path.Combine(root, "Results", "Exp10_PruneTolerance");
            Directory.CreateDirectory(opt.ResultsDir);
            GitHubMirrorPublisher.EnsureLayout(root);
            GitHubMirrorPublisher.CopySource(root);

            StringBuilder console = new StringBuilder();
            Log(console, "Experiment 10 — prune tolerance sensitivity (area)");
            Log(console, "Results: " + opt.ResultsDir);
            Log(console, "Seeds: " + opt.SeedCount + "  epochs=" + opt.DenseEpochs + "  retrain=" + opt.RetrainCycles);
            Log(console, "Factors: " + JoinDoubles(factors));
            Log(console, "Radii: " + opt.RadiusLo + ".." + opt.RadiusHi + " step " + opt.RadiusStep);
            Log(console, "Pass E disabled (isolates tolerance). π not an objective.");

            int[] radii = PiDataGenerator.RadiusRange(opt.RadiusLo, opt.RadiusHi, opt.RadiusStep);
            PiSample[] area = PiDataGenerator.GenerateAreaData(radii, opt.GridResolution);
            if (opt.MinRadius > 0)
                area = PiDataGenerator.FilterMinRadius(area, opt.MinRadius);
            PiDataGenerator.SaveCsv(Path.Combine(opt.ResultsDir, "area_data.csv"), area);
            Log(console, "Samples: " + area.Length);

            double meanA = 0;
            for (int i = 0; i < area.Length; i++) meanA += Math.Abs(area[i].Y);
            if (area.Length > 0) meanA /= area.Length;

            List<Exp10SeedLog> all = new List<Exp10SeedLog>();
            string status = GitHubMirrorPublisher.BuildStatusExp10("running", all, "");
            File.WriteAllText(Path.Combine(opt.ResultsDir, "STATUS.md"), status);

            for (int fi = 0; fi < factors.Length; fi++)
            {
                double factor = factors[fi];
                string fid = FactorId(factor);
                string setDir = Path.Combine(opt.ResultsDir, "Factor_" + fid);
                Directory.CreateDirectory(setDir);
                Log(console, "");
                Log(console, "=== factor=" + factor.ToString("G4", CultureInfo.InvariantCulture) + " (" + fid + ") ===");

                for (int s = 1; s <= opt.SeedCount; s++)
                {
                    Log(console, fid + " seed " + s + "/" + opt.SeedCount + "...");
                    Stopwatch sw = Stopwatch.StartNew();
                    Exp10SeedLog row = RunOne(area, meanA, s, factor, fid, opt, setDir);
                    sw.Stop();
                    row.RuntimeSeconds = sw.Elapsed.TotalSeconds;
                    all.Add(row);
                    File.WriteAllText(Path.Combine(setDir, string.Format(CultureInfo.InvariantCulture, "seed_{0:00}", s), "runtime.txt"),
                        row.RuntimeSeconds.ToString("G15", CultureInfo.InvariantCulture) + " seconds");

                    status = GitHubMirrorPublisher.BuildStatusExp10("running", all, BuildHeadline(all, factors));
                    File.WriteAllText(Path.Combine(opt.ResultsDir, "STATUS.md"), status);
                    GitHubMirrorPublisher.PublishSeed(root, "Exp10_PruneTolerance",
                        Path.Combine(setDir, string.Format(CultureInfo.InvariantCulture, "seed_{0:00}", s)), status);

                    Log(console, string.Format(CultureInfo.InvariantCulture,
                        "  canon={0}  Lval={1:G4}  rmse={2:G4}  n {3}->{4}  e {5}->{6}  comp={7:P1}  terms={8}  t={9:F1}s",
                        row.CanonicalSuccess ? "YES" : "NO",
                        row.FinalValLoss, row.TestRmse,
                        row.DenseNeurons, row.FinalNeurons,
                        row.DenseConnections, row.FinalConnections,
                        row.Compression, row.EquationTerms, row.RuntimeSeconds));
                }
            }

            WriteSeedCsv(Path.Combine(opt.ResultsDir, "seed_logs.csv"), all);
            string table = BuildTable(all, factors);
            string pareto = BuildParetoCsv(all, factors);
            File.WriteAllText(Path.Combine(opt.ResultsDir, "pareto_points.csv"), pareto);
            string headline2 = BuildHeadline(all, factors);
            File.WriteAllText(Path.Combine(opt.ResultsDir, "headline.txt"), headline2);
            File.WriteAllText(Path.Combine(opt.ResultsDir, "summary.md"),
                "# Experiment 10 — prune tolerance\r\n\r\n" + headline2 + "\r\n" + table
                + "\r\n## Pareto notes\r\n\r\n"
                + "See `pareto_points.csv`: mean test RMSE vs edge compression, and mean equation terms vs prune factor.\r\n");
            status = GitHubMirrorPublisher.BuildStatusExp10("complete", all, headline2);
            File.WriteAllText(Path.Combine(GitHubMirrorPublisher.MirrorRoot(root), "STATUS.md"), status);
            File.WriteAllText(Path.Combine(opt.ResultsDir, "STATUS.md"), status);
            GitHubMirrorPublisher.PublishFile(root, "Exp10_PruneTolerance", Path.Combine(opt.ResultsDir, "seed_logs.csv"));
            GitHubMirrorPublisher.PublishFile(root, "Exp10_PruneTolerance", Path.Combine(opt.ResultsDir, "pareto_points.csv"));
            GitHubMirrorPublisher.PublishFile(root, "Exp10_PruneTolerance", Path.Combine(opt.ResultsDir, "headline.txt"));
            GitHubMirrorPublisher.PublishFile(root, "Exp10_PruneTolerance", Path.Combine(opt.ResultsDir, "summary.md"));

            Log(console, "");
            Log(console, headline2);
            Log(console, "Experiment Complete.");
            File.WriteAllText(Path.Combine(opt.ResultsDir, "console.log"), console.ToString());
            return 0;
        }

        private static Exp10SeedLog RunOne(
            PiSample[] samples, double meanA, int seed, double factor, string fid, PiOptions opt, string setDir)
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
            File.WriteAllText(Path.Combine(seedDir, "factor.txt"),
                factor.ToString("G15", CultureInfo.InvariantCulture));

            VnnBpNet net = PiNetworkBuilder.CreateAreaDiscoveryBank("Exp10_" + fid, opt.Hidden, seed);
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

            // Same factor for prune and sigmoid-strip (do not widen strip — that would mix tolerances).
            PiExperimentRunner.PruneWithLockedBaseline(
                net, split, factor, opt.RetrainCycles, seed, logger, eta);
            int strip = opt.RetrainCycles;
            if (strip > 5000) strip = 5000;
            if (opt.RetrainCycles > 0)
                PiExperimentRunner.PruneSigmoidPathsForCanonical(
                    net, split, factor, strip, seed, logger, eta, denseVal);

            EvaluationResult finalVal = net.Evaluate(split.ValIn, split.ValOut);
            EvaluationResult finalTest = net.Evaluate(split.TestIn, split.TestOut);
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

            double compression = denseC > 0
                ? 1.0 - ((double)net.maxConnections / denseC)
                : 0.0;

            Exp10SeedLog row = new Exp10SeedLog();
            row.FactorId = fid;
            row.PruneFactor = factor;
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
            row.EquationTerms = CountEquationTerms(eq);
            row.Compression = compression;
            row.Reason = can.Reason;

            StringBuilder met = new StringBuilder();
            met.AppendLine("{");
            met.AppendFormat(CultureInfo.InvariantCulture, "  \"factor\": {0},\n", JsonNum(factor));
            met.AppendFormat(CultureInfo.InvariantCulture, "  \"canonical\": {0},\n", can.CanonicalSuccess ? "true" : "false");
            met.AppendFormat(CultureInfo.InvariantCulture, "  \"val_loss\": {0},\n", JsonNum(finalVal.Loss));
            met.AppendFormat(CultureInfo.InvariantCulture, "  \"test_rmse\": {0},\n", JsonNum(testRmse));
            met.AppendFormat(CultureInfo.InvariantCulture, "  \"compression\": {0},\n", JsonNum(compression));
            met.AppendFormat(CultureInfo.InvariantCulture, "  \"terms\": {0},\n", row.EquationTerms);
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

        private static void WriteSeedCsv(string path, List<Exp10SeedLog> logs)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("factor_id,factor,seed,canonical,structural,k,pi_error,dense_val,final_val,test_loss,test_rmse,dense_n,final_n,dense_e,final_e,compression,terms,runtime_s,reason");
            for (int i = 0; i < logs.Count; i++)
            {
                Exp10SeedLog r = logs[i];
                sb.AppendFormat(CultureInfo.InvariantCulture,
                    "{0},{1},{2},{3},{4},{5},{6},{7},{8},{9},{10},{11},{12},{13},{14},{15},{16},{17},{18}\r\n",
                    r.FactorId, JsonNum(r.PruneFactor), r.Seed,
                    r.CanonicalSuccess ? 1 : 0, r.StructuralSuccess ? 1 : 0,
                    JsonNum(r.CanonicalSuccess ? r.RecoveredK : double.NaN), JsonNum(r.PiError),
                    JsonNum(r.DenseValLoss), JsonNum(r.FinalValLoss), JsonNum(r.TestLoss), JsonNum(r.TestRmse),
                    r.DenseNeurons, r.FinalNeurons, r.DenseConnections, r.FinalConnections,
                    JsonNum(r.Compression), r.EquationTerms,
                    r.RuntimeSeconds.ToString("G15", CultureInfo.InvariantCulture),
                    Escape(r.Reason).Replace(',', ';'));
            }
            File.WriteAllText(path, sb.ToString());
        }

        private static string BuildParetoCsv(List<Exp10SeedLog> logs, double[] factors)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("factor,n,canon_pct,struct_pct,mean_compression,mean_test_rmse,mean_final_val,mean_terms,mean_final_n,mean_final_e,mean_runtime_s");
            for (int f = 0; f < factors.Length; f++)
            {
                double factor = factors[f];
                string fid = FactorId(factor);
                int n = 0, can = 0, str = 0;
                double sumC = 0, sumR = 0, sumV = 0, sumTm = 0, sumNu = 0, sumE = 0, sumT = 0;
                int nR = 0;
                for (int i = 0; i < logs.Count; i++)
                {
                    if (logs[i].FactorId != fid) continue;
                    n++;
                    if (logs[i].CanonicalSuccess) can++;
                    if (logs[i].StructuralSuccess) str++;
                    sumC += logs[i].Compression;
                    if (NnMath.IsFinite(logs[i].TestRmse)) { sumR += logs[i].TestRmse; nR++; }
                    sumV += logs[i].FinalValLoss;
                    sumTm += logs[i].EquationTerms;
                    sumNu += logs[i].FinalNeurons;
                    sumE += logs[i].FinalConnections;
                    sumT += logs[i].RuntimeSeconds;
                }
                if (n == 0) continue;
                sb.AppendFormat(CultureInfo.InvariantCulture,
                    "{0},{1},{2},{3},{4},{5},{6},{7},{8},{9},{10}\r\n",
                    JsonNum(factor), n,
                    (100.0 * can / n).ToString("G15", CultureInfo.InvariantCulture),
                    (100.0 * str / n).ToString("G15", CultureInfo.InvariantCulture),
                    JsonNum(sumC / n),
                    nR > 0 ? JsonNum(sumR / nR) : "null",
                    JsonNum(sumV / n),
                    JsonNum(sumTm / n),
                    JsonNum(sumNu / n),
                    JsonNum(sumE / n),
                    JsonNum(sumT / n));
            }
            return sb.ToString();
        }

        private static string BuildTable(List<Exp10SeedLog> logs, double[] factors)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("# Prune tolerance (area)");
            sb.AppendLine();
            sb.AppendLine("| Factor | n | Canon % | Struct % | Mean compress | Mean RMSE | Mean L_val | Mean terms | Mean n | Mean e | Mean t(s) |");
            sb.AppendLine("|--------|---|---------|----------|---------------|-----------|------------|------------|--------|--------|-----------|");
            for (int f = 0; f < factors.Length; f++)
            {
                double factor = factors[f];
                string fid = FactorId(factor);
                int n = 0, can = 0, str = 0;
                double sumC = 0, sumR = 0, sumV = 0, sumTm = 0, sumNu = 0, sumE = 0, sumT = 0;
                int nR = 0;
                for (int i = 0; i < logs.Count; i++)
                {
                    if (logs[i].FactorId != fid) continue;
                    n++;
                    if (logs[i].CanonicalSuccess) can++;
                    if (logs[i].StructuralSuccess) str++;
                    sumC += logs[i].Compression;
                    if (NnMath.IsFinite(logs[i].TestRmse)) { sumR += logs[i].TestRmse; nR++; }
                    sumV += logs[i].FinalValLoss;
                    sumTm += logs[i].EquationTerms;
                    sumNu += logs[i].FinalNeurons;
                    sumE += logs[i].FinalConnections;
                    sumT += logs[i].RuntimeSeconds;
                }
                if (n == 0) continue;
                sb.AppendFormat(CultureInfo.InvariantCulture,
                    "| {0:G4} | {1} | {2:F0} | {3:F0} | {4:P1} | {5:G4} | {6:G4} | {7:F1} | {8:F1} | {9:F1} | {10:F2} |\n",
                    factor, n, 100.0 * can / n, 100.0 * str / n,
                    sumC / n,
                    nR > 0 ? sumR / nR : double.NaN,
                    sumV / n, sumTm / n, sumNu / n, sumE / n, sumT / n);
            }
            return sb.ToString();
        }

        private static string BuildHeadline(List<Exp10SeedLog> logs, double[] factors)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendFormat(CultureInfo.InvariantCulture, "Exp10 total runs={0}.\r\n", logs.Count);
            sb.Append(BuildTable(logs, factors));
            return sb.ToString();
        }

        private static string FactorId(double factor)
        {
            // e.g. 1.0 -> f100, 1.05 -> f105, 1.1 -> f110, 1.25 -> f125
            int pct = (int)Math.Round(factor * 100.0);
            return "f" + pct.ToString(CultureInfo.InvariantCulture);
        }

        private static double[] ParseFactors(string csv)
        {
            string[] parts = csv.Split(new char[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            List<double> list = new List<double>();
            for (int i = 0; i < parts.Length; i++)
            {
                double v;
                if (double.TryParse(parts[i], NumberStyles.Float, CultureInfo.InvariantCulture, out v) && v > 0)
                    list.Add(v);
            }
            return list.ToArray();
        }

        private static string JoinDoubles(double[] a)
        {
            if (a == null || a.Length == 0) return "";
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < a.Length; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append(a[i].ToString("G4", CultureInfo.InvariantCulture));
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
