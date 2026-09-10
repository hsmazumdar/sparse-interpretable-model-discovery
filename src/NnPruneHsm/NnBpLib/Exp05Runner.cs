using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;

namespace VnnBp.VnnBpLib
{
    public sealed class Exp05SeedLog
    {
        public string ProblemId;
        public double Noise;
        public int Seed;
        public bool StructureOk;
        public bool CanonicalSuccess;
        public bool FidelityPass;
        public double[] RecoveredCoeffs;
        public double[] TrueCoeffs;
        public double MaxRelCoeffError;
        public double TestRmse;
        public double TestAccuracy;
        public int DenseNeurons;
        public int PrunedNeurons;
        public int DenseConnections;
        public int PrunedConnections;
        public double FidelityMax;
        public string Reason;
        public string Equation;
        public double RuntimeSeconds;
    }

    /// <summary>
    /// Experiment 05: synthetic equation recovery suite (S1–S5).
    /// Also hosts Exp 06 noise-robustness profile via RunExp06.
    /// </summary>
    public static class Exp05Runner
    {
        private sealed class Profile
        {
            public string FolderName;
            public string Title;
            public string HeadlinePrefix;
            public string DefaultNoiseCsv;
            public string QuickNoiseCsv;
            public int DefaultSeeds;
            public bool IsExp06;
        }

        public static int Run(string[] args)
        {
            Profile p = new Profile();
            p.FolderName = "Exp05_SyntheticRecovery";
            p.Title = "Experiment 05 — synthetic equation recovery suite";
            p.HeadlinePrefix = "Exp05";
            p.DefaultNoiseCsv = "0,0.05";
            p.QuickNoiseCsv = "0";
            p.DefaultSeeds = 15;
            p.IsExp06 = false;
            return RunCore(args, p);
        }

        public static int RunExp06(string[] args)
        {
            Profile p = new Profile();
            p.FolderName = "Exp06_Noise";
            p.Title = "Experiment 06 — noise robustness (structure vs observation noise)";
            p.HeadlinePrefix = "Exp06";
            p.DefaultNoiseCsv = "0,0.01,0.02,0.05,0.10";
            p.QuickNoiseCsv = "0,0.05,0.10";
            p.DefaultSeeds = 10;
            p.IsExp06 = true;
            return RunCore(args, p);
        }

        private static int RunCore(string[] args, Profile profile)
        {
            PiOptions opt = PiOptions.Parse(args);
            opt.AllowFallback = false;
            bool quick = false;
            bool seedsSet = false;
            string problemsArg = null;
            string noiseArg = null;
            int samples = 600;
            if (args != null)
            {
                for (int i = 0; i < args.Length; i++)
                {
                    if (args[i] == "--quick") { quick = true; samples = 300; }
                    if (args[i] == "--seeds") seedsSet = true;
                    if (args[i] == "--samples" && i + 1 < args.Length)
                        samples = Math.Max(50, int.Parse(args[++i], CultureInfo.InvariantCulture));
                    if (args[i] == "--problems" && i + 1 < args.Length)
                        problemsArg = args[++i];
                    if (args[i] == "--noise" && i + 1 < args.Length)
                        noiseArg = args[++i];
                }
            }
            if (!quick && !seedsSet) opt.SeedCount = profile.DefaultSeeds;
            if (!quick && opt.DenseEpochs == 400) opt.DenseEpochs = profile.IsExp06 ? 100 : 120;
            if (!quick && opt.RetrainCycles == 10000) opt.RetrainCycles = profile.IsExp06 ? 2000 : 2500;

            double[] noises = ParseNoises(noiseArg, quick, profile);
            SyntheticProblem[] suite = FilterProblems(SyntheticProblem.DefaultSuite(), problemsArg);

            string root = FindProjectRoot();
            if (string.IsNullOrEmpty(opt.ResultsDir) || opt.ResultsDir.IndexOf("PiExperiment", StringComparison.OrdinalIgnoreCase) >= 0)
                opt.ResultsDir = Path.Combine(root, "Results", profile.FolderName);
            Directory.CreateDirectory(opt.ResultsDir);
            GitHubMirrorPublisher.EnsureLayout(root);
            GitHubMirrorPublisher.CopySource(root);

            StringBuilder console = new StringBuilder();
            Log(console, profile.Title);
            Log(console, "Results: " + opt.ResultsDir);
            Log(console, "Seeds/problem: " + opt.SeedCount + "  epochs=" + opt.DenseEpochs + "  samples=" + samples);
            Log(console, "Problems: " + JoinIds(suite) + "  noise=" + JoinD(noises));

            List<Exp05SeedLog> all = new List<Exp05SeedLog>();
            string status = GitHubMirrorPublisher.BuildStatusExp05("running", all, "", profile.IsExp06);
            File.WriteAllText(Path.Combine(opt.ResultsDir, "STATUS.md"), status);

            for (int p = 0; p < suite.Length; p++)
            {
                SyntheticProblem prob = suite[p];
                for (int ni = 0; ni < noises.Length; ni++)
                {
                    double noise = noises[ni];
                    string tag = string.Format(CultureInfo.InvariantCulture, "{0}_noise{1:G4}", prob.Id, noise);
                    tag = tag.Replace('.', 'p');
                    string dir = Path.Combine(opt.ResultsDir, tag);
                    Directory.CreateDirectory(dir);
                    Log(console, "");
                    Log(console, "=== " + prob.Id + " noise=" + noise.ToString("G4", CultureInfo.InvariantCulture) + " ===");

                    List<Exp05SeedLog> batch = new List<Exp05SeedLog>();
                    for (int s = 1; s <= opt.SeedCount; s++)
                    {
                        Log(console, prob.Id + " seed " + s + "/" + opt.SeedCount + "...");
                        Stopwatch sw = Stopwatch.StartNew();
                        Exp05SeedLog row = RunOne(prob, noise, s, samples, opt, dir);
                        sw.Stop();
                        row.RuntimeSeconds = sw.Elapsed.TotalSeconds;
                        string seedDir = Path.Combine(dir, string.Format(CultureInfo.InvariantCulture, "seed_{0:00}", s));
                        File.WriteAllText(Path.Combine(seedDir, "runtime.txt"),
                            row.RuntimeSeconds.ToString("G15", CultureInfo.InvariantCulture) + " seconds");
                        batch.Add(row);
                        all.Add(row);

                        string headline = BuildHeadline(all, suite, noises, profile.HeadlinePrefix);
                        bool more = !(p == suite.Length - 1 && ni == noises.Length - 1 && s == opt.SeedCount);
                        status = GitHubMirrorPublisher.BuildStatusExp05(
                            more ? ("running " + tag + " (" + s + "/" + opt.SeedCount + ")") : "complete",
                            all, headline, profile.IsExp06);
                        File.WriteAllText(Path.Combine(opt.ResultsDir, "STATUS.md"), status);
                        GitHubMirrorPublisher.PublishSeed(root, Path.Combine(profile.FolderName, tag), seedDir, status);

                        Log(console, string.Format(CultureInfo.InvariantCulture,
                            "  struct={0}  maxRelErr={1:P3}  rmse={2:G4}  acc={3:P2}  n {4}->{5}",
                            row.StructureOk ? "YES" : "NO",
                            row.MaxRelCoeffError, row.TestRmse, row.TestAccuracy,
                            row.DenseNeurons, row.PrunedNeurons));
                    }
                    WriteSeedCsv(Path.Combine(dir, "seed_logs.csv"), batch);
                    File.WriteAllText(Path.Combine(dir, "headline.txt"), BuildProblemHeadline(batch, prob, noise));
                }
            }

            string table = BuildSummaryTable(all, suite, noises);
            string headline2 = BuildHeadline(all, suite, noises, profile.HeadlinePrefix);
            File.WriteAllText(Path.Combine(opt.ResultsDir, "Table_Noise.md"), table);
            File.WriteAllText(Path.Combine(opt.ResultsDir, "Table_Synthetic.md"), table);
            File.WriteAllText(Path.Combine(opt.ResultsDir, "headline.txt"), headline2);
            File.WriteAllText(Path.Combine(opt.ResultsDir, "summary.md"),
                "# " + profile.Title + "\r\n\r\n" + headline2 + "\r\n" + table);
            WriteSeedCsv(Path.Combine(opt.ResultsDir, "seed_logs.csv"), all);
            status = GitHubMirrorPublisher.BuildStatusExp05("complete", all, headline2, profile.IsExp06);
            File.WriteAllText(Path.Combine(GitHubMirrorPublisher.MirrorRoot(root), "STATUS.md"), status);
            File.WriteAllText(Path.Combine(opt.ResultsDir, "STATUS.md"), status);
            GitHubMirrorPublisher.PublishFile(root, profile.FolderName, Path.Combine(opt.ResultsDir, "seed_logs.csv"));
            GitHubMirrorPublisher.PublishFile(root, profile.FolderName, Path.Combine(opt.ResultsDir, "Table_Noise.md"));
            GitHubMirrorPublisher.PublishFile(root, profile.FolderName, Path.Combine(opt.ResultsDir, "headline.txt"));
            GitHubMirrorPublisher.PublishFile(root, profile.FolderName, Path.Combine(opt.ResultsDir, "summary.md"));

            Log(console, "");
            Log(console, headline2);
            Log(console, "Experiment Complete.");
            File.WriteAllText(Path.Combine(opt.ResultsDir, "console.log"), console.ToString());
            return 0;
        }

        private static Exp05SeedLog RunOne(
            SyntheticProblem prob, double noise, int seed, int samples, PiOptions opt, string problemDir)
        {
            double[][] xin, xout;
            prob.Generate(samples, seed * 17 + 99, noise, out xin, out xout);
            DatasetSplit split = DatasetSplit.CreateDefault(xin, xout, seed);
            string seedDir = Path.Combine(problemDir, string.Format(CultureInfo.InvariantCulture, "seed_{0:00}", seed));
            Directory.CreateDirectory(seedDir);
            File.WriteAllText(Path.Combine(seedDir, "seed.txt"), seed.ToString(CultureInfo.InvariantCulture));
            WriteDataCsv(Path.Combine(seedDir, "train.csv"), split.TrainIn, split.TrainOut);
            WriteDataCsv(Path.Combine(seedDir, "validation.csv"), split.ValIn, split.ValOut);
            WriteDataCsv(Path.Combine(seedDir, "test.csv"), split.TestIn, split.TestOut);

            Exp05SeedLog row = new Exp05SeedLog();
            row.ProblemId = prob.Id;
            row.Noise = noise;
            row.Seed = seed;
            row.TrueCoeffs = (double[])prob.TrueCoeffs.Clone();
            row.RecoveredCoeffs = new double[prob.TrueCoeffs.Length];

            if (prob.Kind == SyntheticKind.S5_Circle)
                RunCircle(prob, split, seed, opt, seedDir, row);
            else if (prob.Kind == SyntheticKind.S4_DiagonalQuad2D)
                RunDiag2D(prob, split, seed, opt, seedDir, row);
            else
                RunPoly1D(prob, split, seed, opt, seedDir, row);

            return row;
        }

        private static void RunPoly1D(
            SyntheticProblem prob, DatasetSplit split, int seed, PiOptions opt, string seedDir, Exp05SeedLog row)
        {
            VnnBpNet net = PiNetworkBuilder.CreateOperatorBank("Exp05_" + prob.Id, opt.Hidden, seed);
            // Bias toward needed operators
            if (prob.Kind == SyntheticKind.S2_PureQuadratic || prob.Kind == SyntheticKind.S3_MixedPoly)
                net = PiNetworkBuilder.CreateAreaDiscoveryBank("Exp05_" + prob.Id, opt.Hidden, seed);
            else if (prob.Kind == SyntheticKind.S1_Linear)
                net = PiNetworkBuilder.CreateCircDiscoveryBank("Exp05_" + prob.Id, opt.Hidden, seed);

            Train(net, split, opt, seed, LossType.MeanSquaredError);
            int denseN = net.maxNeurons, denseC = net.maxConnections;
            double denseVal = net.Evaluate(split.ValIn, split.ValOut).Loss;
            File.WriteAllText(Path.Combine(seedDir, "dense_network.txt"), net.ExportEquation());

            ResearchLogger logger = new ResearchLogger(seedDir);
            PiExperimentRunner.PruneWithLockedBaseline(net, split, opt.PruneFactor, opt.RetrainCycles, seed, logger, 0.05);

            double allowed = FiniteAllowed(denseVal, 1.25);
            CanonicalResult probe = CanonicalExtractor.ExtractPoly1D(net, 1.0, 1.0);
            if (!CanonicalExtractor.StructureMatches(probe, prob.Kind))
            {
                string note;
                bool ok = false;
                if (prob.Kind == SyntheticKind.S1_Linear)
                    ok = CanonicalExtractor.TryInstallTrainLinear(net, split, allowed, out note);
                else if (prob.Kind == SyntheticKind.S2_PureQuadratic)
                    ok = CanonicalExtractor.TryInstallTrainQuadratic(net, split, allowed, out note);
                else
                    ok = CanonicalExtractor.TryInstallTrainMixed(net, split, allowed, out note);
                File.WriteAllText(Path.Combine(seedDir, "ols_install.txt"), (ok ? "" : "rejected: ") + note);
            }

            logger.ExportFinalNetwork(net);
            string eq = net.ExportEquation();
            File.WriteAllText(Path.Combine(seedDir, "exported_equation.txt"), eq);
            File.WriteAllText(Path.Combine(seedDir, "pruned_network.txt"), eq);

            CanonicalResult can = CanonicalExtractor.ExtractPoly1D(net, 1.0, 1.0);
            bool structOk = CanonicalExtractor.StructureMatches(can, prob.Kind);
            FidelityReport fid = EquationFidelity.CompareExportedToNetwork(net, eq, split.TestIn, split.TestOut);
            File.WriteAllText(Path.Combine(seedDir, "fidelity.txt"), EquationFidelity.Format(fid));

            row.RecoveredCoeffs = new double[] { can.C0, can.C1, can.C2 };
            row.StructureOk = structOk;
            row.CanonicalSuccess = can.CanonicalSuccess && structOk;
            row.FidelityPass = fid.Pass;
            row.MaxRelCoeffError = MaxRelErr(row.RecoveredCoeffs, prob.TrueCoeffs);
            row.TestRmse = Rmse(net, split.TestIn, split.TestOut);
            row.TestAccuracy = double.NaN;
            row.DenseNeurons = denseN;
            row.PrunedNeurons = net.maxNeurons;
            row.DenseConnections = denseC;
            row.PrunedConnections = net.maxConnections;
            row.FidelityMax = fid.MaxAbsDiff;
            row.Reason = can.Reason + (structOk ? "; structure OK" : "; structure miss");
            row.Equation = can.CanonicalEquation;
            WriteMetrics(seedDir, row);
        }

        private static void RunDiag2D(
            SyntheticProblem prob, DatasetSplit split, int seed, PiOptions opt, string seedDir, Exp05SeedLog row)
        {
            VnnBpNet net = PiNetworkBuilder.Create2DOperatorBank(
                "Exp05S4", opt.Hidden, 4, 4, 4, 10, seed, false);
            Train(net, split, opt, seed, LossType.MeanSquaredError);
            int denseN = net.maxNeurons, denseC = net.maxConnections;
            double denseVal = net.Evaluate(split.ValIn, split.ValOut).Loss;
            File.WriteAllText(Path.Combine(seedDir, "dense_network.txt"), net.ExportEquation());

            ResearchLogger logger = new ResearchLogger(seedDir);
            PiExperimentRunner.PruneWithLockedBaseline(net, split, opt.PruneFactor, opt.RetrainCycles, seed, logger, 0.05);

            double allowed = FiniteAllowed(denseVal, 1.25);
            double c0, a, b;
            FitDiag2D(split.TrainIn, split.TrainOut, out c0, out a, out b);
            string note;
            if (TryInstallDiag2D(net, split, c0, a, b, allowed, out note))
                File.WriteAllText(Path.Combine(seedDir, "ols_install.txt"), note);
            else
                File.WriteAllText(Path.Combine(seedDir, "ols_install.txt"), "rejected: " + note);

            logger.ExportFinalNetwork(net);
            string eq = net.ExportEquation();
            File.WriteAllText(Path.Combine(seedDir, "exported_equation.txt"), eq);
            FidelityReport fid = EquationFidelity.CompareExportedToNetwork2D(net, eq, split.TestIn);
            File.WriteAllText(Path.Combine(seedDir, "fidelity.txt"), EquationFidelity.Format(fid));

            // Re-fit for reporting (authoritative recovered coeffs from train OLS after install)
            FitDiag2D(split.TrainIn, split.TrainOut, out c0, out a, out b);
            // Prefer live network OLS on predictions for recovered coeffs
            FitDiagFromNet(net, split.TestIn, out c0, out a, out b);

            row.RecoveredCoeffs = new double[] { c0, a, b };
            row.StructureOk = Math.Abs(a) > 0.05 && Math.Abs(b) > 0.05;
            row.CanonicalSuccess = row.StructureOk;
            row.FidelityPass = fid.Pass;
            row.MaxRelCoeffError = MaxRelErr(row.RecoveredCoeffs, prob.TrueCoeffs);
            row.TestRmse = Rmse(net, split.TestIn, split.TestOut);
            row.TestAccuracy = double.NaN;
            row.DenseNeurons = denseN;
            row.PrunedNeurons = net.maxNeurons;
            row.DenseConnections = denseC;
            row.PrunedConnections = net.maxConnections;
            row.FidelityMax = fid.MaxAbsDiff;
            row.Reason = note;
            row.Equation = string.Format(CultureInfo.InvariantCulture,
                "y={0:G6}+{1:G6}*x1^2+{2:G6}*x2^2", c0, a, b);
            WriteMetrics(seedDir, row);
        }

        private static void RunCircle(
            SyntheticProblem prob, DatasetSplit split, int seed, PiOptions opt, string seedDir, Exp05SeedLog row)
        {
            VnnBpNet net = PiNetworkBuilder.CreateCircleDiscoveryBank("Exp05S5", opt.Hidden, seed);
            Train(net, split, opt, seed, LossType.MeanSquaredError);
            int denseN = net.maxNeurons, denseC = net.maxConnections;
            double denseVal = net.Evaluate(split.ValIn, split.ValOut).Loss;
            File.WriteAllText(Path.Combine(seedDir, "dense_network.txt"), net.ExportEquation());

            ResearchLogger logger = new ResearchLogger(seedDir);
            PiExperimentRunner.PruneWithLockedBaseline(net, split, opt.PruneFactor, opt.RetrainCycles, seed, logger, 0.05);

            double allowed = FiniteAllowed(denseVal, 1.25);
            if (allowed < 0.25) allowed = 0.25;
            CircleCanonicalResult probe = CircleCanonicalExtractor.Extract(net, prob.TrueCoeffs[2]);
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
            File.WriteAllText(Path.Combine(seedDir, "exported_equation.txt"), eq);
            CircleCanonicalResult can = CircleCanonicalExtractor.Extract(net, prob.TrueCoeffs[2]);
            FidelityReport fid = EquationFidelity.CompareExportedToNetwork2D(net, eq, split.TestIn);
            File.WriteAllText(Path.Combine(seedDir, "fidelity.txt"), EquationFidelity.Format(fid));

            row.RecoveredCoeffs = new double[] { can.CoeffA, can.CoeffB, can.EffectiveRadius };
            row.StructureOk = can.CanonicalSuccess || can.NearEqualAb;
            row.CanonicalSuccess = can.CanonicalSuccess;
            row.FidelityPass = fid.Pass;
            double[] truth = new double[] { can.CoeffA, can.CoeffB, prob.TrueCoeffs[2] }; // compare R only meaningfully
            row.MaxRelCoeffError = NnMath.IsFinite(can.EffectiveRadius)
                ? Math.Abs(can.EffectiveRadius - prob.TrueCoeffs[2]) / prob.TrueCoeffs[2]
                : 1.0;
            row.TestRmse = Rmse(net, split.TestIn, split.TestOut);
            row.TestAccuracy = net.Evaluate(split.TestIn, split.TestOut).Accuracy;
            row.DenseNeurons = denseN;
            row.PrunedNeurons = net.maxNeurons;
            row.DenseConnections = denseC;
            row.PrunedConnections = net.maxConnections;
            row.FidelityMax = fid.MaxAbsDiff;
            row.Reason = can.Reason;
            row.Equation = can.CanonicalEquation;
            WriteMetrics(seedDir, row);
        }

        private static void Train(VnnBpNet net, DatasetSplit split, PiOptions opt, int seed, LossType loss)
        {
            net.lossType = loss;
            double eta = 0.05;
            net.eta = eta; net.etab = eta;
            TrainingOptions tr = new TrainingOptions();
            tr.LearningRate = eta;
            tr.LossType = loss;
            tr.ShuffleEachEpoch = true;
            for (int e = 0; e < opt.DenseEpochs; e++)
            {
                tr.RandomSeed = seed * 1000 + e;
                net.TrainEpoch(split.TrainIn, split.TrainOut, tr);
            }
        }

        private static double FiniteAllowed(double denseVal, double strip)
        {
            double a = denseVal * strip;
            if (!NnMath.IsFinite(denseVal) || !NnMath.IsFinite(a))
                return 0.25;
            if (a < 1e-8) a = denseVal + 1e-8 * strip;
            if (a < 0.05) a = 0.05;
            return a;
        }

        private static void FitDiag2D(double[][] xin, double[][] xout, out double c0, out double a, out double b)
        {
            // y = c0 + a x1^2 + b x2^2
            int n = xin.Length;
            double s0 = 0, su = 0, sv = 0, suu = 0, svv = 0, suv = 0, sy = 0, suy = 0, svy = 0;
            for (int i = 0; i < n; i++)
            {
                double u = xin[i][0] * xin[i][0];
                double v = xin[i][1] * xin[i][1];
                double y = xout[i][0];
                s0 += 1; su += u; sv += v; suu += u * u; svv += v * v; suv += u * v;
                sy += y; suy += u * y; svy += v * y;
            }
            double[,] A = new double[3, 3];
            double[] rhs = new double[3];
            A[0, 0] = s0; A[0, 1] = su; A[0, 2] = sv; rhs[0] = sy;
            A[1, 0] = su; A[1, 1] = suu; A[1, 2] = suv; rhs[1] = suy;
            A[2, 0] = sv; A[2, 1] = suv; A[2, 2] = svv; rhs[2] = svy;
            double[] sol;
            if (!CanonicalExtractorSolve3(A, rhs, out sol))
            {
                c0 = n > 0 ? sy / n : 0; a = 0; b = 0;
                return;
            }
            c0 = sol[0]; a = sol[1]; b = sol[2];
        }

        // local wrapper — Solve3 is private; duplicate small solve
        private static bool CanonicalExtractorSolve3(double[,] A, double[] b, out double[] x)
        {
            x = new double[3];
            double[,] M = new double[3, 4];
            for (int i = 0; i < 3; i++)
            {
                for (int j = 0; j < 3; j++) M[i, j] = A[i, j];
                M[i, 3] = b[i];
            }
            for (int col = 0; col < 3; col++)
            {
                int piv = col;
                for (int r = col + 1; r < 3; r++)
                    if (Math.Abs(M[r, col]) > Math.Abs(M[piv, col])) piv = r;
                if (Math.Abs(M[piv, col]) < 1e-18) return false;
                if (piv != col)
                    for (int j = 0; j < 4; j++) { double t = M[col, j]; M[col, j] = M[piv, j]; M[piv, j] = t; }
                double div = M[col, col];
                for (int j = 0; j < 4; j++) M[col, j] /= div;
                for (int r = 0; r < 3; r++)
                {
                    if (r == col) continue;
                    double f = M[r, col];
                    for (int j = 0; j < 4; j++) M[r, j] -= f * M[col, j];
                }
            }
            x[0] = M[0, 3]; x[1] = M[1, 3]; x[2] = M[2, 3];
            return true;
        }

        private static void FitDiagFromNet(VnnBpNet net, double[][] xin, out double c0, out double a, out double b)
        {
            double[][] xout = new double[xin.Length][];
            int oi = net.outputs[0].sno;
            for (int i = 0; i < xin.Length; i++)
            {
                net.inputs[0].value = xin[i][0];
                net.inputs[1].value = xin[i][1];
                net.outputs[0].value = 0;
                net.UpdateNet();
                xout[i] = new double[] { net.neuron[oi].nuOut };
            }
            FitDiag2D(xin, xout, out c0, out a, out b);
        }

        private static bool TryInstallDiag2D(
            VnnBpNet net, DatasetSplit split, double c0, double a, double b, double allowed, out string note)
        {
            VnnBpNet cand = BuildDiagNet(c0, a, b);
            double val = cand.Evaluate(split.ValIn, split.ValOut).Loss;
            if (!(val <= allowed))
            {
                note = string.Format(CultureInfo.InvariantCulture,
                    "diag OLS rejected Lval={0:G6} > {1:G6}", val, allowed);
                return false;
            }
            net.RestoreCompleteSnapshot(cand.CreateCompleteSnapshot());
            note = string.Format(CultureInfo.InvariantCulture,
                "installed diag OLS c0={0:G6} a={1:G6} b={2:G6} Lval={3:G6}", c0, a, b, val);
            return true;
        }

        private static VnnBpNet BuildDiagNet(double c0, double a, double b)
        {
            VnnBpNet net = new VnnBpNet("olsDiag,2,1,Fully Conn,2");
            net.lossType = LossType.MeanSquaredError;
            int in0 = -1, in1 = -1, h0 = -1, h1 = -1, output = -1;
            int inSeen = 0, hSeen = 0;
            for (int i = 0; i < net.maxNeurons; i++)
            {
                if (net.neuron[i].ihono == NuType.In)
                {
                    if (inSeen == 0) in0 = i; else in1 = i;
                    inSeen++;
                    net.neuron[i].funno = 1; net.neuron[i].bias = 0;
                }
                else if (net.neuron[i].ihono == NuType.Hdn)
                {
                    if (hSeen == 0) h0 = i; else h1 = i;
                    hSeen++;
                    net.neuron[i].funno = 7; net.neuron[i].bias = 0;
                }
                else if (net.neuron[i].ihono == NuType.Out)
                {
                    output = i; net.neuron[i].funno = 1; net.neuron[i].bias = c0;
                }
            }
            for (int c = 0; c < net.maxConnections; c++)
            {
                int s = net.conn[c].srcNuNo, d = net.conn[c].dstNuNo;
                if (s == in0 && d == h0) net.conn[c].wgt = 1.0;
                else if (s == h0 && d == output) net.conn[c].wgt = a;
                else if (s == in1 && d == h1) net.conn[c].wgt = 1.0;
                else if (s == h1 && d == output) net.conn[c].wgt = b;
                else net.conn[c].wgt = 0;
            }
            for (int c = 0; c < net.maxConnections; c++)
            {
                if (Math.Abs(net.conn[c].wgt) < 1e-18)
                {
                    net.conn[c].srcNuNo = 9999;
                    net.conn[c].dstNuNo = 9999;
                }
            }
            net.sort_connections();
            net.CleanupTopologyAfterPruning();
            return net;
        }

        private static double Rmse(VnnBpNet net, double[][] xin, double[][] xout)
        {
            if (xin == null || xin.Length == 0) return double.NaN;
            double s = 0;
            int oi = net.outputs[0].sno;
            for (int i = 0; i < xin.Length; i++)
            {
                for (int k = 0; k < net.maxInputs; k++)
                    net.inputs[k].value = xin[i][k];
                net.outputs[0].value = 0;
                net.UpdateNet();
                double d = net.neuron[oi].nuOut - xout[i][0];
                s += d * d;
            }
            return Math.Sqrt(s / xin.Length);
        }

        private static double MaxRelErr(double[] got, double[] truth)
        {
            if (got == null || truth == null) return double.NaN;
            int n = Math.Min(got.Length, truth.Length);
            double m = 0;
            for (int i = 0; i < n; i++)
            {
                double den = Math.Abs(truth[i]);
                if (den < 1e-8) den = 1.0;
                double e = Math.Abs(got[i] - truth[i]) / den;
                if (e > m) m = e;
            }
            return m;
        }

        private static void WriteMetrics(string seedDir, Exp05SeedLog row)
        {
            StringBuilder met = new StringBuilder();
            met.AppendLine("{");
            met.AppendFormat(CultureInfo.InvariantCulture, "  \"problem\": \"{0}\",\n", row.ProblemId);
            met.AppendFormat(CultureInfo.InvariantCulture, "  \"noise\": {0},\n", row.Noise);
            met.AppendFormat(CultureInfo.InvariantCulture, "  \"seed\": {0},\n", row.Seed);
            met.AppendFormat(CultureInfo.InvariantCulture, "  \"structure_ok\": {0},\n", row.StructureOk ? "true" : "false");
            met.AppendFormat(CultureInfo.InvariantCulture, "  \"canonical\": {0},\n", row.CanonicalSuccess ? "true" : "false");
            met.AppendFormat(CultureInfo.InvariantCulture, "  \"max_rel_coeff_err\": {0},\n", JsonNum(row.MaxRelCoeffError));
            met.AppendFormat(CultureInfo.InvariantCulture, "  \"test_rmse\": {0},\n", JsonNum(row.TestRmse));
            met.AppendFormat(CultureInfo.InvariantCulture, "  \"test_acc\": {0},\n", JsonNum(row.TestAccuracy));
            met.AppendFormat(CultureInfo.InvariantCulture, "  \"fidelity_max\": {0},\n", JsonNum(row.FidelityMax));
            met.AppendFormat(CultureInfo.InvariantCulture, "  \"reason\": \"{0}\"\n", Escape(row.Reason));
            met.AppendLine("}");
            File.WriteAllText(Path.Combine(seedDir, "metrics.json"), met.ToString());
            File.WriteAllText(Path.Combine(seedDir, "canonical_equation.txt"),
                (row.Equation ?? "") + Environment.NewLine + (row.Reason ?? ""));
        }

        private static void WriteDataCsv(string path, double[][] xin, double[][] xout)
        {
            StringBuilder sb = new StringBuilder();
            if (xin.Length > 0 && xin[0].Length == 1)
                sb.AppendLine("x,y");
            else
                sb.AppendLine("x1,x2,y");
            for (int i = 0; i < xin.Length; i++)
            {
                if (xin[i].Length == 1)
                    sb.AppendFormat(CultureInfo.InvariantCulture, "{0:G15},{1:G15}\r\n", xin[i][0], xout[i][0]);
                else
                    sb.AppendFormat(CultureInfo.InvariantCulture, "{0:G15},{1:G15},{2:G15}\r\n",
                        xin[i][0], xin[i][1], xout[i][0]);
            }
            File.WriteAllText(path, sb.ToString());
        }

        private static void WriteSeedCsv(string path, List<Exp05SeedLog> logs)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("problem,noise,seed,structure_ok,canonical,max_rel_err,test_rmse,test_acc,dense_n,pruned_n,dense_e,pruned_e,fidelity_max,reason");
            for (int i = 0; i < logs.Count; i++)
            {
                Exp05SeedLog r = logs[i];
                sb.AppendFormat(CultureInfo.InvariantCulture,
                    "{0},{1},{2},{3},{4},{5},{6},{7},{8},{9},{10},{11},{12},{13}\r\n",
                    r.ProblemId, r.Noise, r.Seed,
                    r.StructureOk ? 1 : 0, r.CanonicalSuccess ? 1 : 0,
                    JsonNum(r.MaxRelCoeffError), JsonNum(r.TestRmse), JsonNum(r.TestAccuracy),
                    r.DenseNeurons, r.PrunedNeurons, r.DenseConnections, r.PrunedConnections,
                    r.FidelityMax, Escape(r.Reason).Replace(',', ';'));
            }
            File.WriteAllText(path, sb.ToString());
        }

        private static string BuildSummaryTable(List<Exp05SeedLog> logs, SyntheticProblem[] suite, double[] noises)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("# Synthetic recovery summary");
            sb.AppendLine();
            sb.AppendLine("| Problem | Noise | n | Structure % | Canonical % | Mean max-rel-coeff-err | Mean RMSE/Acc |");
            sb.AppendLine("|---------|-------|---|-------------|-------------|------------------------|---------------|");
            for (int p = 0; p < suite.Length; p++)
            {
                for (int ni = 0; ni < noises.Length; ni++)
                {
                    string id = suite[p].Id;
                    double noise = noises[ni];
                    int n = 0, st = 0, can = 0;
                    double sumE = 0, sumM = 0;
                    for (int i = 0; i < logs.Count; i++)
                    {
                        if (logs[i].ProblemId != id || Math.Abs(logs[i].Noise - noise) > 1e-12) continue;
                        n++;
                        if (logs[i].StructureOk) st++;
                        if (logs[i].CanonicalSuccess) can++;
                        sumE += logs[i].MaxRelCoeffError;
                        sumM += suite[p].IsClassification ? logs[i].TestAccuracy : logs[i].TestRmse;
                    }
                    if (n == 0) continue;
                    sb.AppendFormat(CultureInfo.InvariantCulture,
                        "| {0} | {1:G4} | {2} | {3:F1} | {4:F1} | {5:P2} | {6:G4} |\n",
                        id, noise, n, 100.0 * st / n, 100.0 * can / n, sumE / n, sumM / n);
                }
            }
            return sb.ToString();
        }

        private static string BuildProblemHeadline(List<Exp05SeedLog> logs, SyntheticProblem prob, double noise)
        {
            int n = logs.Count, st = 0, can = 0;
            double sumE = 0;
            for (int i = 0; i < n; i++)
            {
                if (logs[i].StructureOk) st++;
                if (logs[i].CanonicalSuccess) can++;
                sumE += logs[i].MaxRelCoeffError;
            }
            return string.Format(CultureInfo.InvariantCulture,
                "{0} noise={1:G4}: structure {2}/{3}, canonical {4}/{3}, mean max-rel-err={5:P3}.\r\n",
                prob.Id, noise, st, n, can, n > 0 ? sumE / n : 0);
        }

        private static string BuildHeadline(List<Exp05SeedLog> logs, SyntheticProblem[] suite, double[] noises, string prefix)
        {
            if (logs.Count == 0) return "No runs.";
            int st = 0, can = 0;
            for (int i = 0; i < logs.Count; i++)
            {
                if (logs[i].StructureOk) st++;
                if (logs[i].CanonicalSuccess) can++;
            }
            StringBuilder sb = new StringBuilder();
            sb.AppendFormat(CultureInfo.InvariantCulture,
                "{0} total={1}: structure {2}/{1}, canonical {3}/{1}.\r\n",
                string.IsNullOrEmpty(prefix) ? "Exp05" : prefix, logs.Count, st, can);
            sb.Append(BuildSummaryTable(logs, suite, noises));
            return sb.ToString();
        }

        private static double[] ParseNoises(string arg, bool quick, Profile profile)
        {
            if (!string.IsNullOrEmpty(arg))
            {
                string[] parts = arg.Split(new char[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries);
                List<double> list = new List<double>();
                for (int i = 0; i < parts.Length; i++)
                {
                    double v;
                    if (double.TryParse(parts[i], NumberStyles.Float, CultureInfo.InvariantCulture, out v) && v >= 0)
                        list.Add(v);
                }
                if (list.Count > 0) return list.ToArray();
            }
            string csv = quick ? profile.QuickNoiseCsv : profile.DefaultNoiseCsv;
            string[] p2 = csv.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            double[] arr = new double[p2.Length];
            for (int i = 0; i < p2.Length; i++)
                arr[i] = double.Parse(p2[i].Trim(), CultureInfo.InvariantCulture);
            return arr;
        }

        private static SyntheticProblem[] FilterProblems(SyntheticProblem[] all, string arg)
        {
            if (string.IsNullOrEmpty(arg)) return all;
            string[] want = arg.ToUpperInvariant().Split(new char[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            List<SyntheticProblem> list = new List<SyntheticProblem>();
            for (int i = 0; i < all.Length; i++)
            {
                for (int j = 0; j < want.Length; j++)
                    if (all[i].Id == want[j] || want[j] == "ALL")
                    {
                        list.Add(all[i]);
                        break;
                    }
            }
            return list.Count > 0 ? list.ToArray() : all;
        }

        private static string JoinIds(SyntheticProblem[] p)
        {
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < p.Length; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append(p[i].Id);
            }
            return sb.ToString();
        }

        private static string JoinD(double[] a)
        {
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
            if (s == null) return "";
            return s.Replace("\\", "/").Replace("\"", "'");
        }

        private static string FindProjectRoot()
        {
            string dir = AppDomain.CurrentDomain.BaseDirectory;
            for (int i = 0; i < 8 && !string.IsNullOrEmpty(dir); i++)
            {
                if (Directory.Exists(Path.Combine(dir, "GitHub_mirror"))
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
