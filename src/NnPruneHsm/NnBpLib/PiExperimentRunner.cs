using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace VnnBp.VnnBpLib
{
    /// <summary>
    /// One seed of the Pi discovery pipeline (dense train → transactional prune → recover k).
    /// </summary>
    public sealed class PiSeedLog
    {
        public int Seed;
        public string Task;
        public string RadiusRange;
        public double TargetConstant;
        public double RecoveredK;
        public double RelativeError;
        public int DenseNeurons;
        public int DenseConnections;
        public int PrunedNeurons;
        public int PrunedConnections;
        public int PrunedHidden;
        public int QuadraticHidden;
        public double DenseValLoss;
        public double PrunedValLoss;
        public string Equation;
        public bool FallbackHidden50;
        public bool FactorTwoCorrection;
    }

    /// <summary>
    /// 30-seed validation-constrained Pi rediscovery experiment.
    /// </summary>
    public static class PiExperimentRunner
    {
        public static int Run(string[] args)
        {
            PiOptions opt = PiOptions.Parse(args);
            if (!Directory.Exists(opt.ResultsDir))
                Directory.CreateDirectory(opt.ResultsDir);
            if (!Directory.Exists(opt.DataDir))
                Directory.CreateDirectory(opt.DataDir);

            StringBuilder console = new StringBuilder();
            Log(console, "Pi discovery experiment");
            Log(console, "Results: " + opt.ResultsDir);
            Log(console, "Data:    " + opt.DataDir);
            Log(console, "Seeds:   " + opt.SeedCount + "  hidden=" + opt.Hidden + "  epochs=" + opt.DenseEpochs);
            Log(console, "Radii:   " + opt.RadiusLo + ".." + opt.RadiusHi + " step " + opt.RadiusStep
                + "  grid=" + opt.GridResolution);
            Log(console, "Prune:   10,000 sample updates, factor=" + opt.PruneFactor.ToString("G4", CultureInfo.InvariantCulture));

            string grad = GradientCheck.RunBasicChecks();
            File.WriteAllText(Path.Combine(opt.ResultsDir, "gradient_check_report.txt"), grad);
            File.WriteAllText(Path.Combine(opt.ResultsDir, "geometry_lattice_check.txt"),
                PiDataGenerator.VerifyKnownLatticeCounts());
            Log(console, "Wrote gradient_check_report.txt and geometry_lattice_check.txt");

            int[] radii = PiDataGenerator.RadiusRange(opt.RadiusLo, opt.RadiusHi, opt.RadiusStep);
            Log(console, "Generating area data (pixel count, no Math.PI)...");
            PiSample[] area = PiDataGenerator.GenerateAreaData(radii, opt.GridResolution);
            Log(console, "Generating circumference data (boundary trace, no Math.PI)...");
            PiSample[] circ = PiDataGenerator.GenerateCircumferenceData(radii, opt.GridResolution);
            if (opt.MinRadius > 0)
            {
                area = PiDataGenerator.FilterMinRadius(area, opt.MinRadius);
                circ = PiDataGenerator.FilterMinRadius(circ, opt.MinRadius);
            }
            PiDataGenerator.SaveCsv(Path.Combine(opt.DataDir, "area_data.csv"), area);
            PiDataGenerator.SaveCsv(Path.Combine(opt.DataDir, "circ_data.csv"), circ);
            PiDataGenerator.SaveCsv(Path.Combine(opt.ResultsDir, "area_data.csv"), area);
            PiDataGenerator.SaveCsv(Path.Combine(opt.ResultsDir, "circ_data.csv"), circ);
            Log(console, "Samples: area=" + area.Length + "  circ=" + circ.Length);

            List<PiSeedLog> logs = new List<PiSeedLog>();
            if (opt.RunArea)
            {
                Log(console, "===== AREA (target pi) =====");
                RunTask(console, logs, area, true, opt);
            }
            if (opt.RunCirc)
            {
                Log(console, "===== CIRCUMFERENCE (target 2*pi) =====");
                RunTask(console, logs, circ, false, opt);
            }

            WriteSeedCsv(Path.Combine(opt.ResultsDir, "seed_logs.csv"), logs);
            string md = BuildMarkdownSummary(logs, opt);
            string tex = BuildLatexSummary(logs, opt);
            File.WriteAllText(Path.Combine(opt.ResultsDir, "summary.md"), md);
            File.WriteAllText(Path.Combine(opt.ResultsDir, "summary.tex"), tex);
            string headline = BuildHeadline(logs);
            File.WriteAllText(Path.Combine(opt.ResultsDir, "headline.txt"), headline);
            Log(console, "");
            Log(console, headline);
            Log(console, "Wrote summary.md / summary.tex / seed_logs.csv");
            File.WriteAllText(Path.Combine(opt.ResultsDir, "console.log"), console.ToString());
            return 0;
        }

        private static void RunTask(
            StringBuilder console,
            List<PiSeedLog> logs,
            PiSample[] samples,
            bool areaTask,
            PiOptions opt)
        {
            string task = areaTask ? "area" : "circ";
            string range = string.Format(CultureInfo.InvariantCulture,
                "{0} - {1}", opt.RadiusLo, opt.RadiusHi);
            for (int s = 1; s <= opt.SeedCount; s++)
            {
                Log(console, task + " seed " + s + "/" + opt.SeedCount + "...");
                PiSeedLog row = RunOneSeed(samples, areaTask, s, opt.Hidden, opt, range);
                if (opt.AllowFallback
                    && row.RelativeError > 0.05
                    && opt.Hidden < 50)
                {
                    Log(console, "  fallback: hidden=50 (rel err=" +
                        row.RelativeError.ToString("G4", CultureInfo.InvariantCulture) + ")");
                    row = RunOneSeed(samples, areaTask, s, 50, opt, range);
                    row.FallbackHidden50 = true;
                }
                logs.Add(row);
                string eqPath = Path.Combine(opt.ResultsDir,
                    string.Format(CultureInfo.InvariantCulture, "equation_{0}_seed{1:00}.txt", task, s));
                File.WriteAllText(eqPath, PiEquationParser.Format(ToRecovered(row)) + row.Equation);
                Log(console, string.Format(CultureInfo.InvariantCulture,
                    "  k={0:G6}  rel_err={1:P3}  neurons {2}->{3}  conn {4}->{5}  quad={6}",
                    row.RecoveredK, row.RelativeError,
                    row.DenseNeurons, row.PrunedNeurons,
                    row.DenseConnections, row.PrunedConnections,
                    row.QuadraticHidden));
            }
        }

        private static PiRecoveredConstant ToRecovered(PiSeedLog row)
        {
            PiRecoveredConstant r = new PiRecoveredConstant();
            r.SlopeK = row.RecoveredK;
            r.RelativeError = row.RelativeError;
            r.TargetConstant = row.TargetConstant;
            r.Equation = row.Equation;
            r.Connections = row.PrunedConnections;
            r.HiddenNeurons = row.PrunedHidden;
            r.QuadraticHidden = row.QuadraticHidden;
            r.UsedFactorTwoCorrection = row.FactorTwoCorrection;
            return r;
        }

        private static PiSeedLog RunOneSeed(
            PiSample[] samples,
            bool areaTask,
            int seed,
            int hidden,
            PiOptions opt,
            string range)
        {
            double rMax = Math.Max(1.0, opt.RadiusHi);
            double yScale = areaTask ? (rMax * rMax) : rMax;
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

            string name = areaTask ? "PiArea" : "PiCirc";
            VnnBpNet net = PiNetworkBuilder.Create(name, hidden, seed);
            net.lossType = LossType.MeanSquaredError;
            double eta = 0.05;
            net.eta = eta;
            net.etab = eta;

            TrainingOptions tr = new TrainingOptions();
            tr.LearningRate = eta;
            tr.LossType = LossType.MeanSquaredError;
            tr.ShuffleEachEpoch = true;
            ResearchLogger logger = new ResearchLogger(
                Path.Combine(opt.ResultsDir, string.Format(CultureInfo.InvariantCulture,
                    "logs_{0}_seed{1:00}", areaTask ? "area" : "circ", seed)));

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

            PruneWithLockedBaseline(net, split, opt.PruneFactor, opt.RetrainCycles, seed, logger, eta);

            EvaluationResult prunedVal = net.Evaluate(split.ValIn, split.ValOut);
            logger.ExportFinalNetwork(net);
            PiRecoveredConstant rec = PiEquationParser.Recover(net, samples, areaTask, rMax, yScale);

            PiSeedLog row = new PiSeedLog();
            row.Seed = seed;
            row.Task = areaTask ? "area" : "circ";
            row.RadiusRange = range;
            row.TargetConstant = rec.TargetConstant;
            row.RecoveredK = rec.SlopeK;
            row.RelativeError = rec.RelativeError;
            row.DenseNeurons = denseN;
            row.DenseConnections = denseC;
            row.PrunedNeurons = net.maxNeurons;
            row.PrunedConnections = net.maxConnections;
            row.PrunedHidden = rec.HiddenNeurons;
            row.QuadraticHidden = rec.QuadraticHidden;
            row.DenseValLoss = denseVal;
            row.PrunedValLoss = prunedVal.Loss;
            row.Equation = rec.Equation;
            row.FactorTwoCorrection = rec.UsedFactorTwoCorrection;
            return row;
        }

        /// <summary>
        /// Transactional prune–retrain matching TrainThisNet.PruneNet:
        /// snapshot → remove connections → cleanup → 10k sample updates →
        /// pure validation vs locked dense baseline → accept or restore.
        /// </summary>
        public static void PruneWithLockedBaseline(
            VnnBpNet net,
            DatasetSplit split,
            double pruneFactor,
            int retrainCycles,
            int seed,
            ResearchLogger logger,
            double eta)
        {
            double originalBaselineLoss = net.Evaluate(split.ValIn, split.ValOut).Loss;
            double allowedLoss = AllowedLoss(originalBaselineLoss, pruneFactor);
            int batchSize = Math.Max(1, net.maxConnections / 2);
            int attempts = 0;
            TrainingOptions ftOpt = new TrainingOptions();
            ftOpt.LearningRate = eta;
            ftOpt.LossType = net.lossType;
            ftOpt.ShuffleEachEpoch = true;

            while (batchSize >= 1 && attempts < 100)
            {
                attempts++;
                int tried = batchSize;
                int nBefore = net.maxNeurons;
                int cBefore = net.maxConnections;
                NetworkSnapshot snapshot = net.CreateCompleteSnapshot();
                net.remove_connections(tried);
                net.CleanupTopologyAfterPruning();
                NetworkValidationResult topo = net.ValidateTopology();
                if (!topo.IsValid)
                {
                    net.RestoreCompleteSnapshot(snapshot);
                    batchSize = Math.Max(1, tried / 2);
                    if (tried == 1) batchSize = 0;
                    if (logger != null)
                        logger.LogPruningAttempt(attempts, tried, cBefore, net.maxConnections,
                            originalBaselineLoss, originalBaselineLoss, false, 0);
                    continue;
                }
                ftOpt.RandomSeed = seed + attempts;
                TrainingResult ft = net.FineTuneSampleCycles(
                    split.TrainIn, split.TrainOut, retrainCycles, ftOpt);
                double valLoss = net.Evaluate(split.ValIn, split.ValOut).Loss;
                bool reduced = net.maxConnections < cBefore || net.maxNeurons < nBefore;
                bool accepted = reduced && valLoss <= allowedLoss;
                if (!accepted)
                {
                    net.RestoreCompleteSnapshot(snapshot);
                    batchSize = tried / 2;
                }
                if (logger != null)
                    logger.LogPruningAttempt(attempts, tried, cBefore, net.maxConnections,
                        originalBaselineLoss, valLoss, accepted, ft.EpochsCompleted);
            }
            net.CleanupTopologyAfterPruning();
            net.ValidateTopology();
        }

        private static double AllowedLoss(double baselineLoss, double pruningFactor)
        {
            if (pruningFactor >= 1.0)
            {
                if (baselineLoss < 1e-12)
                    return baselineLoss + 1e-8 + (pruningFactor - 1.0) * 1e-8;
                return baselineLoss * pruningFactor;
            }
            return baselineLoss * (1.0 + pruningFactor) + 1e-8;
        }

        /// <summary>
        /// Preferentially drop connections into/out of sigmoid (and quadratic-sigmoid)
        /// hidden units, then cleanup + 10k retrain, accept only vs locked dense baseline.
        /// Goal: allow algebraic A=b+kr^2 without using π as an objective.
        /// </summary>
        public static void PruneSigmoidPathsForCanonical(
            VnnBpNet net,
            DatasetSplit split,
            double pruneFactor,
            int retrainCycles,
            int seed,
            ResearchLogger logger,
            double eta)
        {
            PruneSigmoidPathsForCanonical(net, split, pruneFactor, retrainCycles, seed, logger, eta, double.NaN);
        }

        public static void PruneSigmoidPathsForCanonical(
            VnnBpNet net,
            DatasetSplit split,
            double pruneFactor,
            int retrainCycles,
            int seed,
            ResearchLogger logger,
            double eta,
            double lockedDenseBaseline)
        {
            if (net == null || split == null) return;
            double current = net.Evaluate(split.ValIn, split.ValOut).Loss;
            double originalBaselineLoss = NnMath.IsFinite(lockedDenseBaseline) ? lockedDenseBaseline : current;
            double allowedLoss = AllowedLoss(originalBaselineLoss, pruneFactor);
            TrainingOptions ftOpt = new TrainingOptions();
            ftOpt.LearningRate = eta;
            ftOpt.LossType = net.lossType;
            ftOpt.ShuffleEachEpoch = true;

            // Pass A: try removing each sigmoid hidden neuron wholesale (all its edges).
            System.Collections.Generic.HashSet<int> stickyHidden = new System.Collections.Generic.HashSet<int>();
            for (int pass = 0; pass < 64; pass++)
            {
                int hid = FindAnySigmoidHidden(net, stickyHidden);
                if (hid < 0) break;
                int nBefore = net.maxNeurons;
                int cBefore = net.maxConnections;
                NetworkSnapshot snapshot = net.CreateCompleteSnapshot();
                ZeroEdgesTouching(net, hid);
                net.remove_connections(CountZeroWeightEdges(net));
                net.CleanupTopologyAfterPruning();
                if (!net.ValidateTopology().IsValid)
                {
                    net.RestoreCompleteSnapshot(snapshot);
                    stickyHidden.Add(hid);
                    continue;
                }
                ftOpt.RandomSeed = seed + 20000 + pass;
                TrainingResult ft = net.FineTuneSampleCycles(
                    split.TrainIn, split.TrainOut, retrainCycles, ftOpt);
                double valLoss = net.Evaluate(split.ValIn, split.ValOut).Loss;
                bool reduced = net.maxConnections < cBefore || net.maxNeurons < nBefore;
                bool accepted = reduced && valLoss <= allowedLoss;
                if (!accepted)
                {
                    net.RestoreCompleteSnapshot(snapshot);
                    stickyHidden.Add(hid);
                    if (logger != null)
                        logger.LogPruningAttempt(9100 + pass, 1, cBefore, net.maxConnections,
                            originalBaselineLoss, valLoss, false, ft.EpochsCompleted);
                    continue;
                }
                if (logger != null)
                    logger.LogPruningAttempt(9100 + pass, 1, cBefore, net.maxConnections,
                        originalBaselineLoss, valLoss, true, ft.EpochsCompleted);
                stickyHidden.Clear();
            }

            // Pass B: edge-wise — keep trying weakest sigmoid edges; skip rejects.
            System.Collections.Generic.HashSet<string> rejected = new System.Collections.Generic.HashSet<string>();
            for (int pass = 0; pass < 48; pass++)
            {
                int target = FindWeakestSigmoidEdge(net, rejected);
                if (target < 0) break;
                string key = EdgeKey(net, target);
                int nBefore = net.maxNeurons;
                int cBefore = net.maxConnections;
                NetworkSnapshot snapshot = net.CreateCompleteSnapshot();
                net.conn[target].wgt = 0;
                net.remove_connections(1);
                net.CleanupTopologyAfterPruning();
                if (!net.ValidateTopology().IsValid)
                {
                    net.RestoreCompleteSnapshot(snapshot);
                    rejected.Add(key);
                    continue;
                }
                ftOpt.RandomSeed = seed + 10000 + pass;
                TrainingResult ft = net.FineTuneSampleCycles(
                    split.TrainIn, split.TrainOut, retrainCycles, ftOpt);
                double valLoss = net.Evaluate(split.ValIn, split.ValOut).Loss;
                bool reduced = net.maxConnections < cBefore || net.maxNeurons < nBefore;
                bool accepted = reduced && valLoss <= allowedLoss;
                if (!accepted)
                {
                    net.RestoreCompleteSnapshot(snapshot);
                    rejected.Add(key);
                    if (logger != null)
                        logger.LogPruningAttempt(9200 + pass, 1, cBefore, net.maxConnections,
                            originalBaselineLoss, valLoss, false, ft.EpochsCompleted);
                    continue;
                }
                if (logger != null)
                    logger.LogPruningAttempt(9200 + pass, 1, cBefore, net.maxConnections,
                        originalBaselineLoss, valLoss, true, ft.EpochsCompleted);
                rejected.Clear(); // topology changed; old keys may be stale
            }

            // Pass C: drop all remaining sigmoid hidden units in one transaction;
            // accept only if validation loss stays within the locked tolerance.
            if (FindAnySigmoidHidden(net, null) >= 0)
            {
                int nBefore = net.maxNeurons;
                int cBefore = net.maxConnections;
                NetworkSnapshot snapshot = net.CreateCompleteSnapshot();
                for (int i = 0; i < net.maxNeurons; i++)
                {
                    if (net.neuron[i].ihono == NuType.Hdn && NnMath.IsSigmoidActivation(net.neuron[i].funno))
                        ZeroEdgesTouching(net, i);
                }
                int zeros = CountZeroWeightEdges(net);
                if (zeros > 0)
                    net.remove_connections(zeros);
                net.CleanupTopologyAfterPruning();
                if (!net.ValidateTopology().IsValid)
                    net.RestoreCompleteSnapshot(snapshot);
                else
                {
                    ftOpt.RandomSeed = seed + 30000;
                    TrainingResult ft = net.FineTuneSampleCycles(
                        split.TrainIn, split.TrainOut, retrainCycles, ftOpt);
                    double valLoss = net.Evaluate(split.ValIn, split.ValOut).Loss;
                    bool clean = FindAnySigmoidHidden(net, null) < 0;
                    bool accepted = clean && valLoss <= allowedLoss;
                    if (!accepted)
                    {
                        net.RestoreCompleteSnapshot(snapshot);
                        if (logger != null)
                            logger.LogPruningAttempt(9300, zeros, cBefore, net.maxConnections,
                                originalBaselineLoss, valLoss, false, ft.EpochsCompleted);
                    }
                    else if (logger != null)
                        logger.LogPruningAttempt(9300, zeros, cBefore, net.maxConnections,
                            originalBaselineLoss, valLoss, true, ft.EpochsCompleted);
                }
            }

            net.CleanupTopologyAfterPruning();
            net.ValidateTopology();

            // Pass D: keep only square-linear (+ optional direct linear) skeleton.
            if (FindAnySigmoidHidden(net, null) >= 0 || !LooksLikeCanonicalCandidate(net))
                TryProjectToSquareLinearSkeleton(net, split, allowedLoss, retrainCycles, seed, logger, eta, originalBaselineLoss);
        }

        /// <summary>
        /// Exp03: strip sigmoid and square-linear paths so C=b+kr can emerge.
        /// Never uses 2π as an objective — validation loss only.
        /// </summary>
        public static void PruneForLinearCanonical(
            VnnBpNet net,
            DatasetSplit split,
            double pruneFactor,
            int retrainCycles,
            int seed,
            ResearchLogger logger,
            double eta,
            double lockedDenseBaseline)
        {
            if (net == null || split == null) return;
            double current = net.Evaluate(split.ValIn, split.ValOut).Loss;
            double originalBaselineLoss = NnMath.IsFinite(lockedDenseBaseline) ? lockedDenseBaseline : current;
            double allowedLoss = AllowedLoss(originalBaselineLoss, pruneFactor);
            TrainingOptions ftOpt = new TrainingOptions();
            ftOpt.LearningRate = eta;
            ftOpt.LossType = net.lossType;
            ftOpt.ShuffleEachEpoch = true;

            // Pass A: remove sigmoid hidden units one by one (reuse same helpers).
            System.Collections.Generic.HashSet<int> stickyHidden = new System.Collections.Generic.HashSet<int>();
            for (int pass = 0; pass < 64; pass++)
            {
                int hid = FindAnySigmoidHidden(net, stickyHidden);
                if (hid < 0) break;
                if (!TryDropHidden(net, split, hid, allowedLoss, retrainCycles, seed, logger, eta,
                    originalBaselineLoss, ftOpt, 8100 + pass, stickyHidden))
                    stickyHidden.Add(hid);
                else
                    stickyHidden.Clear();
            }

            // Pass B: remove square-linear (funno 7) hidden units.
            stickyHidden.Clear();
            for (int pass = 0; pass < 64; pass++)
            {
                int hid = FindAnyFunnoHidden(net, 7, stickyHidden);
                if (hid < 0) break;
                if (!TryDropHidden(net, split, hid, allowedLoss, retrainCycles, seed, logger, eta,
                    originalBaselineLoss, ftOpt, 8200 + pass, stickyHidden))
                    stickyHidden.Add(hid);
                else
                    stickyHidden.Clear();
            }

            // Pass C: project to linear-only skeleton (drop remaining sigmoid / type-7).
            TryProjectToLinearSkeleton(net, split, allowedLoss, retrainCycles, seed, logger, eta, originalBaselineLoss);
            net.CleanupTopologyAfterPruning();
            net.ValidateTopology();
        }

        private static bool TryDropHidden(
            VnnBpNet net, DatasetSplit split, int hid, double allowedLoss, int retrainCycles,
            int seed, ResearchLogger logger, double eta, double originalBaselineLoss,
            TrainingOptions ftOpt, int logId, System.Collections.Generic.HashSet<int> stickyHidden)
        {
            int nBefore = net.maxNeurons;
            int cBefore = net.maxConnections;
            NetworkSnapshot snapshot = net.CreateCompleteSnapshot();
            ZeroEdgesTouching(net, hid);
            net.remove_connections(CountZeroWeightEdges(net));
            net.CleanupTopologyAfterPruning();
            if (!net.ValidateTopology().IsValid)
            {
                net.RestoreCompleteSnapshot(snapshot);
                return false;
            }
            ftOpt.RandomSeed = seed + logId;
            TrainingResult ft = net.FineTuneSampleCycles(split.TrainIn, split.TrainOut, retrainCycles, ftOpt);
            double valLoss = net.Evaluate(split.ValIn, split.ValOut).Loss;
            bool reduced = net.maxConnections < cBefore || net.maxNeurons < nBefore;
            bool accepted = reduced && valLoss <= allowedLoss;
            if (!accepted)
            {
                net.RestoreCompleteSnapshot(snapshot);
                if (logger != null)
                    logger.LogPruningAttempt(logId, 1, cBefore, net.maxConnections,
                        originalBaselineLoss, valLoss, false, ft.EpochsCompleted);
                return false;
            }
            if (logger != null)
                logger.LogPruningAttempt(logId, 1, cBefore, net.maxConnections,
                    originalBaselineLoss, valLoss, true, ft.EpochsCompleted);
            return true;
        }

        private static int FindAnyFunnoHidden(VnnBpNet net, short funno, System.Collections.Generic.HashSet<int> skip)
        {
            for (int i = 0; i < net.maxNeurons; i++)
            {
                if (skip != null && skip.Contains(i)) continue;
                if (net.neuron[i].ihono == NuType.Hdn && net.neuron[i].funno == funno)
                    return i;
            }
            return -1;
        }

        private static void TryProjectToLinearSkeleton(
            VnnBpNet net,
            DatasetSplit split,
            double allowedLoss,
            int retrainCycles,
            int seed,
            ResearchLogger logger,
            double eta,
            double originalBaselineLoss)
        {
            TrainingOptions ftOpt = new TrainingOptions();
            ftOpt.LearningRate = eta;
            ftOpt.LossType = net.lossType;
            ftOpt.ShuffleEachEpoch = true;

            int cBefore = net.maxConnections;
            NetworkSnapshot snapshot = net.CreateCompleteSnapshot();
            for (int i = 0; i < net.maxNeurons; i++)
            {
                if (net.neuron[i].ihono != NuType.Hdn) continue;
                short f = net.neuron[i].funno;
                if (f != 1)
                    ZeroEdgesTouching(net, i);
            }
            int zeros = CountZeroWeightEdges(net);
            if (zeros > 0)
                net.remove_connections(zeros);
            net.CleanupTopologyAfterPruning();
            if (!net.ValidateTopology().IsValid)
            {
                net.RestoreCompleteSnapshot(snapshot);
                return;
            }
            ftOpt.RandomSeed = seed + 8300;
            int cycles = retrainCycles < 5000 ? 5000 : retrainCycles;
            TrainingResult ft = net.FineTuneSampleCycles(split.TrainIn, split.TrainOut, cycles, ftOpt);
            ft = net.FineTuneSampleCycles(split.TrainIn, split.TrainOut, cycles, ftOpt);
            double valLoss = net.Evaluate(split.ValIn, split.ValOut).Loss;
            bool clean = FindAnySigmoidHidden(net, null) < 0
                && PiNetworkBuilder.CountHiddenOfType(net, 7) == 0;
            bool accepted = clean && valLoss <= allowedLoss;
            if (!accepted)
            {
                net.RestoreCompleteSnapshot(snapshot);
                if (logger != null)
                    logger.LogPruningAttempt(8300, zeros, cBefore, net.maxConnections,
                        originalBaselineLoss, valLoss, false, ft.EpochsCompleted);
            }
            else if (logger != null)
                logger.LogPruningAttempt(8300, zeros, cBefore, net.maxConnections,
                    originalBaselineLoss, valLoss, true, ft.EpochsCompleted);
        }

        private static bool LooksLikeCanonicalCandidate(VnnBpNet net)
        {
            // Heuristic: no sigmoid hidden and at least one type-7.
            if (FindAnySigmoidHidden(net, null) >= 0) return false;
            return PiNetworkBuilder.CountHiddenOfType(net, 7) >= 1;
        }

        /// <summary>
        /// Validation-constrained projection: zero every edge that is not on a
        /// linear or square-linear path from input to output, then retrain.
        /// If that fails, try keeping only the single strongest type-7 unit.
        /// </summary>
        private static void TryProjectToSquareLinearSkeleton(
            VnnBpNet net,
            DatasetSplit split,
            double allowedLoss,
            int retrainCycles,
            int seed,
            ResearchLogger logger,
            double eta,
            double originalBaselineLoss)
        {
            TrainingOptions ftOpt = new TrainingOptions();
            ftOpt.LearningRate = eta;
            ftOpt.LossType = net.lossType;
            ftOpt.ShuffleEachEpoch = true;

            // D1: drop sigmoid/quad-sigmoid units; keep linear + type-7.
            if (!TrySkeletonOnce(net, split, allowedLoss, retrainCycles, seed, logger, eta,
                originalBaselineLoss, ftOpt, 9400, false))
            {
                // D2: keep only the strongest type-7 hidden unit.
                TrySkeletonOnce(net, split, allowedLoss, retrainCycles, seed, logger, eta,
                    originalBaselineLoss, ftOpt, 9500, true);
            }
            net.CleanupTopologyAfterPruning();
            net.ValidateTopology();
        }

        private static bool TrySkeletonOnce(
            VnnBpNet net,
            DatasetSplit split,
            double allowedLoss,
            int retrainCycles,
            int seed,
            ResearchLogger logger,
            double eta,
            double originalBaselineLoss,
            TrainingOptions ftOpt,
            int logId,
            bool singleBestQuad)
        {
            int cBefore = net.maxConnections;
            NetworkSnapshot snapshot = net.CreateCompleteSnapshot();
            int keepHidden = -1;
            if (singleBestQuad)
                keepHidden = FindStrongestSquareLinear(net);

            for (int i = 0; i < net.maxNeurons; i++)
            {
                if (net.neuron[i].ihono != NuType.Hdn) continue;
                short f = net.neuron[i].funno;
                bool drop = false;
                if (NnMath.IsSigmoidActivation(f))
                    drop = true;
                else if (singleBestQuad)
                    drop = (f != 7) || (i != keepHidden);
                else if (f != 1 && f != 7)
                    drop = true;
                if (drop)
                    ZeroEdgesTouching(net, i);
            }
            // Also drop direct edges into output from sigmoid (already covered) and
            // from non-kept hiddens.
            int zeros = CountZeroWeightEdges(net);
            if (zeros > 0)
                net.remove_connections(zeros);
            net.CleanupTopologyAfterPruning();
            if (!net.ValidateTopology().IsValid || PiNetworkBuilder.CountHiddenOfType(net, 7) < 1)
            {
                net.RestoreCompleteSnapshot(snapshot);
                return false;
            }
            ftOpt.RandomSeed = seed + logId;
            // Give the projected skeleton a longer adapt window.
            int cycles = retrainCycles < 5000 ? 5000 : retrainCycles;
            TrainingResult ft = net.FineTuneSampleCycles(
                split.TrainIn, split.TrainOut, cycles, ftOpt);
            // Extra epochs of full-batch-style fine-tune via sample cycles
            ft = net.FineTuneSampleCycles(split.TrainIn, split.TrainOut, cycles, ftOpt);
            double valLoss = net.Evaluate(split.ValIn, split.ValOut).Loss;
            bool clean = FindAnySigmoidHidden(net, null) < 0
                && PiNetworkBuilder.CountHiddenOfType(net, 7) >= 1;
            bool accepted = clean && valLoss <= allowedLoss;
            if (!accepted)
            {
                net.RestoreCompleteSnapshot(snapshot);
                if (logger != null)
                    logger.LogPruningAttempt(logId, zeros, cBefore, net.maxConnections,
                        originalBaselineLoss, valLoss, false, ft.EpochsCompleted);
                return false;
            }
            if (logger != null)
                logger.LogPruningAttempt(logId, zeros, cBefore, net.maxConnections,
                    originalBaselineLoss, valLoss, true, ft.EpochsCompleted);
            return true;
        }

        private static int FindStrongestSquareLinear(VnnBpNet net)
        {
            int input = net.inputs[0].sno;
            int output = net.outputs[0].sno;
            int best = -1;
            double bestScore = -1.0;
            for (int h = 0; h < net.maxNeurons; h++)
            {
                if (net.neuron[h].ihono != NuType.Hdn || net.neuron[h].funno != 7)
                    continue;
                double win = 0, wout = 0;
                for (int c = 0; c < net.maxConnections; c++)
                {
                    if (net.conn[c].srcNuNo == input && net.conn[c].dstNuNo == h)
                        win = net.conn[c].wgt;
                    if (net.conn[c].srcNuNo == h && net.conn[c].dstNuNo == output)
                        wout = net.conn[c].wgt;
                }
                double score = Math.Abs(win) * Math.Abs(wout);
                if (score > bestScore)
                {
                    bestScore = score;
                    best = h;
                }
            }
            return best;
        }

        private static int FindAnySigmoidHidden(VnnBpNet net, System.Collections.Generic.HashSet<int> skip)
        {
            for (int i = 0; i < net.maxNeurons; i++)
            {
                if (skip != null && skip.Contains(i)) continue;
                if (net.neuron[i].ihono == NuType.Hdn && NnMath.IsSigmoidActivation(net.neuron[i].funno))
                    return i;
            }
            return -1;
        }

        private static void ZeroEdgesTouching(VnnBpNet net, int neuronIndex)
        {
            for (int c = 0; c < net.maxConnections; c++)
            {
                if (net.conn[c].srcNuNo == neuronIndex || net.conn[c].dstNuNo == neuronIndex)
                    net.conn[c].wgt = 0;
            }
        }

        private static int CountZeroWeightEdges(VnnBpNet net)
        {
            int n = 0;
            for (int c = 0; c < net.maxConnections; c++)
            {
                if (Math.Abs(net.conn[c].wgt) < 1e-18)
                    n++;
            }
            return Math.Max(1, n);
        }

        private static string EdgeKey(VnnBpNet net, int c)
        {
            return net.conn[c].srcNuNo.ToString() + "->" + net.conn[c].dstNuNo.ToString();
        }

        private static int FindWeakestSigmoidEdge(VnnBpNet net, System.Collections.Generic.HashSet<string> rejected)
        {
            int best = -1;
            double bestAbs = double.MaxValue;
            for (int c = 0; c < net.maxConnections; c++)
            {
                int s = net.conn[c].srcNuNo;
                int d = net.conn[c].dstNuNo;
                if (s < 0 || s >= net.maxNeurons || d < 0 || d >= net.maxNeurons)
                    continue;
                bool sig = NnMath.IsSigmoidActivation(net.neuron[s].funno)
                    || NnMath.IsSigmoidActivation(net.neuron[d].funno);
                if (!sig) continue;
                string key = EdgeKey(net, c);
                if (rejected != null && rejected.Contains(key)) continue;
                double a = Math.Abs(net.conn[c].wgt);
                if (a < bestAbs)
                {
                    bestAbs = a;
                    best = c;
                }
            }
            return best;
        }

        private static void WriteSeedCsv(string path, List<PiSeedLog> logs)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("seed,task,radius_range,target,recovered_k,relative_error,dense_neurons,pruned_neurons,dense_connections,pruned_connections,pruned_hidden,quadratic_hidden,dense_val_loss,pruned_val_loss,fallback_hidden50,factor_two");
            for (int i = 0; i < logs.Count; i++)
            {
                PiSeedLog r = logs[i];
                sb.AppendFormat(CultureInfo.InvariantCulture,
                    "{0},{1},{2},{3:G15},{4:G15},{5:G15},{6},{7},{8},{9},{10},{11},{12:G15},{13:G15},{14},{15}\r\n",
                    r.Seed, r.Task, r.RadiusRange, r.TargetConstant, r.RecoveredK, r.RelativeError,
                    r.DenseNeurons, r.PrunedNeurons, r.DenseConnections, r.PrunedConnections,
                    r.PrunedHidden, r.QuadraticHidden, r.DenseValLoss, r.PrunedValLoss,
                    r.FallbackHidden50 ? 1 : 0, r.FactorTwoCorrection ? 1 : 0);
            }
            File.WriteAllText(path, sb.ToString());
        }

        private static string BuildMarkdownSummary(List<PiSeedLog> logs, PiOptions opt)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("# Pi discovery experiment");
            sb.AppendLine();
            sb.AppendLine("Data generated by pixel counting and boundary tracing. **Math.PI was not used** to label samples.");
            sb.AppendLine();
            sb.AppendLine("| Seed | Task | Radius range | Target | Recovered k | Rel. error (%) | Pruned neurons | Pruned edges | Quad hidden | Val. loss |");
            sb.AppendLine("|------|------|--------------|--------|-------------|----------------|----------------|--------------|-------------|-----------|");
            for (int i = 0; i < logs.Count; i++)
            {
                PiSeedLog r = logs[i];
                sb.AppendFormat(CultureInfo.InvariantCulture,
                    "| {0} | {1} | {2} | {3:F5} | {4:F5} | {5:F3} | {6} | {7} | {8} | {9:G4} |\n",
                    r.Seed, r.Task, r.RadiusRange, r.TargetConstant, r.RecoveredK,
                    100.0 * r.RelativeError, r.PrunedNeurons, r.PrunedConnections,
                    r.QuadraticHidden, r.PrunedValLoss);
            }
            sb.AppendLine();
            sb.Append(BuildHeadline(logs));
            sb.AppendLine();
            AppendAggMarkdown(sb, logs, "area");
            AppendAggMarkdown(sb, logs, "circ");
            return sb.ToString();
        }

        private static void AppendAggMarkdown(StringBuilder sb, List<PiSeedLog> logs, string task)
        {
            List<PiSeedLog> sub = Filter(logs, task);
            if (sub.Count == 0) return;
            double mean, sd, meanErr, meanConn, meanNu;
            Stats(sub, out mean, out sd, out meanErr, out meanConn, out meanNu);
            string name = task == "area" ? "Area (π)" : "Circumference (2π)";
            sb.AppendFormat(CultureInfo.InvariantCulture,
                "**{0}** (n={1}): mean k = {2:G9}, std = {3:G6}, mean rel. error = {4:P4}, mean pruned edges = {5:F1}, mean neurons = {6:F1}.\r\n\r\n",
                name, sub.Count, mean, sd, meanErr, meanConn, meanNu);
        }

        private static string BuildLatexSummary(List<PiSeedLog> logs, PiOptions opt)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("\\begin{tabular}{rllrrrrrr}");
            sb.AppendLine("\\hline");
            sb.AppendLine("Seed & Task & Radii & Target & $k$ & Rel.\\,err.\\,(\\%) & Neurons & Edges & $L_{\\mathrm{val}}$ \\\\");
            sb.AppendLine("\\hline");
            for (int i = 0; i < logs.Count; i++)
            {
                PiSeedLog r = logs[i];
                sb.AppendFormat(CultureInfo.InvariantCulture,
                    "{0} & {1} & {2} & {3:F5} & {4:F5} & {5:F3} & {6} & {7} & {8:G4} \\\\\n",
                    r.Seed, r.Task, r.RadiusRange, r.TargetConstant, r.RecoveredK,
                    100.0 * r.RelativeError, r.PrunedNeurons, r.PrunedConnections, r.PrunedValLoss);
            }
            sb.AppendLine("\\hline");
            sb.AppendLine("\\end{tabular}");
            return sb.ToString();
        }

        private static string BuildHeadline(List<PiSeedLog> logs)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("Experiment Complete.");
            List<PiSeedLog> area = Filter(logs, "area");
            List<PiSeedLog> circ = Filter(logs, "circ");
            if (area.Count > 0)
            {
                double mean, sd, meanErr, meanConn, meanNu;
                Stats(area, out mean, out sd, out meanErr, out meanConn, out meanNu);
                int quadTrials = 0;
                for (int i = 0; i < area.Count; i++)
                    if (area[i].QuadraticHidden >= 1) quadTrials++;
                sb.AppendFormat(CultureInfo.InvariantCulture,
                    "Area Recovery: Mean k = {0:F5}, Error = {1:P4} (n={2}). Quadratic hidden survived in {3}/{2} trials.\r\n",
                    mean, meanErr, area.Count, quadTrials);
            }
            if (circ.Count > 0)
            {
                double mean, sd, meanErr, meanConn, meanNu;
                Stats(circ, out mean, out sd, out meanErr, out meanConn, out meanNu);
                sb.AppendFormat(CultureInfo.InvariantCulture,
                    "Circumference Recovery: Mean k = {0:F5}, Error = {1:P4} (n={2}).\r\n",
                    mean, meanErr, circ.Count);
            }
            if (logs.Count > 0)
            {
                double sp = 0;
                int n = 0;
                for (int i = 0; i < logs.Count; i++)
                {
                    if (logs[i].DenseConnections > 0)
                    {
                        sp += 1.0 - (double)logs[i].PrunedConnections / logs[i].DenseConnections;
                        n++;
                    }
                }
                if (n > 0)
                    sb.AppendFormat(CultureInfo.InvariantCulture,
                        "Sparsity: {0:P2} connections pruned (mean over {1} runs).\r\n",
                        sp / n, n);
            }
            return sb.ToString();
        }

        private static List<PiSeedLog> Filter(List<PiSeedLog> logs, string task)
        {
            List<PiSeedLog> sub = new List<PiSeedLog>();
            for (int i = 0; i < logs.Count; i++)
                if (logs[i].Task == task) sub.Add(logs[i]);
            return sub;
        }

        private static void Stats(
            List<PiSeedLog> rows,
            out double meanK, out double sdK, out double meanRel,
            out double meanConn, out double meanNu)
        {
            meanK = 0; sdK = 0; meanRel = 0; meanConn = 0; meanNu = 0;
            int n = 0;
            for (int i = 0; i < rows.Count; i++)
            {
                if (!NnMath.IsFinite(rows[i].RecoveredK) || !NnMath.IsFinite(rows[i].RelativeError))
                    continue;
                meanK += rows[i].RecoveredK;
                meanRel += rows[i].RelativeError;
                meanConn += rows[i].PrunedConnections;
                meanNu += rows[i].PrunedNeurons;
                n++;
            }
            if (n == 0) return;
            meanK /= n;
            meanRel /= n;
            meanConn /= n;
            meanNu /= n;
            if (n > 1)
            {
                double v = 0;
                int m = 0;
                for (int i = 0; i < rows.Count; i++)
                {
                    if (!NnMath.IsFinite(rows[i].RecoveredK)) continue;
                    double d = rows[i].RecoveredK - meanK;
                    v += d * d;
                    m++;
                }
                if (m > 1) sdK = Math.Sqrt(v / (m - 1));
            }
        }

        private static void Log(StringBuilder sb, string line)
        {
            sb.AppendLine(line);
            Console.WriteLine(line);
        }
    }

    public sealed class PiOptions
    {
        public int SeedCount = 30;
        public int Hidden = 22;
        public int DenseEpochs = 400;
        public int RetrainCycles = 10000;
        public int RadiusLo = 50;
        public int RadiusHi = 500;
        public int RadiusStep = 10;
        public int GridResolution = 1000;
        public int MinRadius = 20;
        public double PruneFactor = 1.1;
        public bool RunArea = true;
        public bool RunCirc = true;
        public bool AllowFallback = false;
        public string ResultsDir;
        public string DataDir;

        public static PiOptions Parse(string[] args)
        {
            PiOptions o = new PiOptions();
            string root = FindProjectRoot();
            o.ResultsDir = Path.Combine(root, "Results", "PiExperiment");
            o.DataDir = Path.Combine(root, "Data");
            if (args == null) return o;
            for (int i = 0; i < args.Length; i++)
            {
                string a = args[i];
                if (a == "--quick")
                {
                    o.SeedCount = 2;
                    o.DenseEpochs = 80;
                    o.RadiusHi = 120;
                    o.RadiusStep = 10;
                    o.GridResolution = 400;
                    o.AllowFallback = false;
                }
                else if (a == "--task" && i + 1 < args.Length)
                {
                    string t = args[++i].ToLowerInvariant();
                    o.RunArea = (t == "area" || t == "all");
                    o.RunCirc = (t == "circ" || t == "circumference" || t == "all");
                }
                else if (a == "--seeds" && i + 1 < args.Length)
                    o.SeedCount = Math.Max(1, int.Parse(args[++i], CultureInfo.InvariantCulture));
                else if (a == "--hidden" && i + 1 < args.Length)
                    o.Hidden = Math.Max(3, int.Parse(args[++i], CultureInfo.InvariantCulture));
                else if (a == "--epochs" && i + 1 < args.Length)
                    o.DenseEpochs = Math.Max(1, int.Parse(args[++i], CultureInfo.InvariantCulture));
                else if (a == "--rmax" && i + 1 < args.Length)
                    o.RadiusHi = int.Parse(args[++i], CultureInfo.InvariantCulture);
                else if (a == "--rmin" && i + 1 < args.Length)
                    o.RadiusLo = int.Parse(args[++i], CultureInfo.InvariantCulture);
                else if (a == "--rstep" && i + 1 < args.Length)
                    o.RadiusStep = Math.Max(1, int.Parse(args[++i], CultureInfo.InvariantCulture));
                else if (a == "--grid" && i + 1 < args.Length)
                    o.GridResolution = int.Parse(args[++i], CultureInfo.InvariantCulture);
                else if (a == "--prun" && i + 1 < args.Length)
                    o.PruneFactor = double.Parse(args[++i], CultureInfo.InvariantCulture);
                else if (a == "--out" && i + 1 < args.Length)
                    o.ResultsDir = args[++i];
                else if (a == "--data" && i + 1 < args.Length)
                    o.DataDir = args[++i];
                else if (a == "--no-fallback")
                    o.AllowFallback = false;
            }
            return o;
        }

        private static string FindProjectRoot()
        {
            string dir = AppDomain.CurrentDomain.BaseDirectory;
            for (int i = 0; i < 6 && !string.IsNullOrEmpty(dir); i++)
            {
                if (File.Exists(Path.Combine(dir, "NnPruneHsm.sln"))
                    || File.Exists(Path.Combine(dir, "PiExperiment.docx"))
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
