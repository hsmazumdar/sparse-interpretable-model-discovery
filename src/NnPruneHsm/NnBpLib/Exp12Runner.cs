using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;

namespace VnnBp.VnnBpLib
{
    public sealed class Exp12SeedLog
    {
        public string ProblemId;
        public string Formula;
        public bool ExpectsExactRecovery;
        public int Seed;
        public bool StructureRecovered;
        public bool ExplicitCrossProduct;
        public bool LimitationConfirmed;
        public double TestRmse;
        public double DiagOnlyRmse;
        public double FullQuadRmse;
        public double DiagOnlyR2;
        public double FullQuadR2;
        public double CoeffC0;
        public double CoeffA;
        public double CoeffB;
        public double CoeffCxy;
        public int DenseNeurons;
        public int FinalNeurons;
        public int DenseConnections;
        public int FinalConnections;
        public double RuntimeSeconds;
        public string Reason;
        public string Equation;
    }

    /// <summary>
    /// Experiment 12 — cross-product limitation (honest negative result).
    /// Type-4/7 quadratics are diagonal only: z = b + w1 x1^2 + w2 x2^2 (no x1*x2).
    /// Positive control: recover diagonal y = c0 + a x1^2 + b x2^2.
    /// Negative test: do NOT exactly recover y = c0 + c x1 x2.
    /// </summary>
    public static class Exp12Runner
    {
        private struct Problem
        {
            public string Id;
            public string Formula;
            public bool ExpectsExact;
            public double C0, A, B, Cxy; // y = C0 + A x1^2 + B x2^2 + Cxy x1 x2
        }

        public static int Run(string[] args)
        {
            PiOptions opt = PiOptions.Parse(args);
            opt.AllowFallback = false;
            bool quick = false;
            bool seedsSet = false;
            int samples = 800;
            if (args != null)
            {
                for (int i = 0; i < args.Length; i++)
                {
                    if (args[i] == "--quick") { quick = true; samples = 300; }
                    if (args[i] == "--seeds") seedsSet = true;
                    if (args[i] == "--samples" && i + 1 < args.Length)
                        samples = Math.Max(100, int.Parse(args[++i], CultureInfo.InvariantCulture));
                }
            }
            if (!quick && !seedsSet) opt.SeedCount = 10;
            if (!quick && opt.DenseEpochs == 400) opt.DenseEpochs = 150;
            if (!quick && opt.RetrainCycles == 10000) opt.RetrainCycles = 5000;

            string root = FindProjectRoot();
            if (string.IsNullOrEmpty(opt.ResultsDir) || opt.ResultsDir.IndexOf("PiExperiment", StringComparison.OrdinalIgnoreCase) >= 0)
                opt.ResultsDir = Path.Combine(root, "Results", "Exp12_CrossProduct");
            Directory.CreateDirectory(opt.ResultsDir);
            GitHubMirrorPublisher.EnsureLayout(root);
            GitHubMirrorPublisher.CopySource(root);

            Problem[] problems = new Problem[]
            {
                P("diag", "y=0.2+1.3*x1^2+0.8*x2^2", true, 0.2, 1.3, 0.8, 0.0),
                P("cross", "y=0.5+2.0*x1*x2", false, 0.5, 0.0, 0.0, 2.0),
                P("mixed", "y=0.1+1.0*x1^2+0.7*x2^2+1.5*x1*x2", false, 0.1, 1.0, 0.7, 1.5),
            };

            StringBuilder console = new StringBuilder();
            Log(console, "Experiment 12 — cross-product limitation (diagonal quadratic only)");
            Log(console, "Results: " + opt.ResultsDir);
            Log(console, "Seeds: " + opt.SeedCount + "  epochs=" + opt.DenseEpochs + "  samples=" + samples);
            Log(console, "Operators cannot form x1*x2. Expect diag recovery; cross/mixed NOT exact.");
            Log(console, "Honest negative result — do not hide limitation.");

            List<Exp12SeedLog> all = new List<Exp12SeedLog>();
            string status = GitHubMirrorPublisher.BuildStatusExp12("running", all, "");
            File.WriteAllText(Path.Combine(opt.ResultsDir, "STATUS.md"), status);

            for (int pi = 0; pi < problems.Length; pi++)
            {
                Problem prob = problems[pi];
                string pdir = Path.Combine(opt.ResultsDir, "Problem_" + prob.Id);
                Directory.CreateDirectory(pdir);
                Log(console, "");
                Log(console, "=== " + prob.Id + ": " + prob.Formula + " (expect exact=" + prob.ExpectsExact + ") ===");

                // Shared geometry seed for data; split still varies by seed.
                double[][] xinAll, xoutAll;
                Generate(prob, samples, 4242, out xinAll, out xoutAll);
                WriteDataCsv(Path.Combine(pdir, "data.csv"), xinAll, xoutAll);

                for (int s = 1; s <= opt.SeedCount; s++)
                {
                    Log(console, prob.Id + " seed " + s + "/" + opt.SeedCount + "...");
                    Stopwatch sw = Stopwatch.StartNew();
                    Exp12SeedLog row = RunOne(prob, xinAll, xoutAll, s, opt, pdir);
                    sw.Stop();
                    row.RuntimeSeconds = sw.Elapsed.TotalSeconds;
                    all.Add(row);
                    File.WriteAllText(Path.Combine(pdir, string.Format(CultureInfo.InvariantCulture, "seed_{0:00}", s), "runtime.txt"),
                        row.RuntimeSeconds.ToString("G15", CultureInfo.InvariantCulture) + " seconds");

                    status = GitHubMirrorPublisher.BuildStatusExp12("running", all, BuildHeadline(all, problems));
                    File.WriteAllText(Path.Combine(opt.ResultsDir, "STATUS.md"), status);
                    GitHubMirrorPublisher.PublishSeed(root, "Exp12_CrossProduct",
                        Path.Combine(pdir, string.Format(CultureInfo.InvariantCulture, "seed_{0:00}", s)), status);

                    Log(console, string.Format(CultureInfo.InvariantCulture,
                        "  struct={0}  crossTerm={1}  limitOK={2}  rmse={3:G4}  diagR2={4:F3}  fullR2={5:F3}  e {6}->{7}",
                        row.StructureRecovered ? "YES" : "NO",
                        row.ExplicitCrossProduct ? "Y" : "N",
                        row.LimitationConfirmed ? "Y" : "N",
                        row.TestRmse, row.DiagOnlyR2, row.FullQuadR2,
                        row.DenseConnections, row.FinalConnections));
                }
            }

            WriteSeedCsv(Path.Combine(opt.ResultsDir, "seed_logs.csv"), all);
            string headline2 = BuildHeadline(all, problems);
            string table = BuildTable(all, problems);
            File.WriteAllText(Path.Combine(opt.ResultsDir, "headline.txt"), headline2);
            File.WriteAllText(Path.Combine(opt.ResultsDir, "summary.md"),
                "# Experiment 12 — cross-product limitation\r\n\r\n"
                + "Type-4/7 neurons implement **diagonal** quadratics only "
                + "(no x1*x2 product). This experiment records that boundary honestly.\r\n\r\n"
                + headline2 + "\r\n" + table);
            status = GitHubMirrorPublisher.BuildStatusExp12("complete", all, headline2);
            File.WriteAllText(Path.Combine(GitHubMirrorPublisher.MirrorRoot(root), "STATUS.md"), status);
            File.WriteAllText(Path.Combine(opt.ResultsDir, "STATUS.md"), status);
            GitHubMirrorPublisher.PublishFile(root, "Exp12_CrossProduct", Path.Combine(opt.ResultsDir, "seed_logs.csv"));
            GitHubMirrorPublisher.PublishFile(root, "Exp12_CrossProduct", Path.Combine(opt.ResultsDir, "headline.txt"));
            GitHubMirrorPublisher.PublishFile(root, "Exp12_CrossProduct", Path.Combine(opt.ResultsDir, "summary.md"));

            Log(console, "");
            Log(console, headline2);
            Log(console, "Experiment Complete.");
            File.WriteAllText(Path.Combine(opt.ResultsDir, "console.log"), console.ToString());
            return 0;
        }

        private static Problem P(string id, string formula, bool expect, double c0, double a, double b, double cxy)
        {
            Problem p = new Problem();
            p.Id = id; p.Formula = formula; p.ExpectsExact = expect;
            p.C0 = c0; p.A = a; p.B = b; p.Cxy = cxy;
            return p;
        }

        private static void Generate(Problem prob, int n, int seed, out double[][] xin, out double[][] xout)
        {
            Random rnd = new Random(seed);
            xin = new double[n][];
            xout = new double[n][];
            for (int i = 0; i < n; i++)
            {
                double x1 = 2.0 * rnd.NextDouble() - 1.0;
                double x2 = 2.0 * rnd.NextDouble() - 1.0;
                double y = prob.C0 + prob.A * x1 * x1 + prob.B * x2 * x2 + prob.Cxy * x1 * x2;
                xin[i] = new double[] { x1, x2 };
                xout[i] = new double[] { y };
            }
        }

        private static Exp12SeedLog RunOne(
            Problem prob, double[][] xinAll, double[][] xoutAll, int seed, PiOptions opt, string pdir)
        {
            DatasetSplit split = DatasetSplit.CreateDefault(xinAll, xoutAll, seed);
            string seedDir = Path.Combine(pdir, string.Format(CultureInfo.InvariantCulture, "seed_{0:00}", seed));
            Directory.CreateDirectory(seedDir);
            File.WriteAllText(Path.Combine(seedDir, "seed.txt"), seed.ToString(CultureInfo.InvariantCulture));
            File.WriteAllText(Path.Combine(seedDir, "problem.txt"), prob.Id + "\n" + prob.Formula);
            WriteDataCsv(Path.Combine(seedDir, "train.csv"), split.TrainIn, split.TrainOut);
            WriteDataCsv(Path.Combine(seedDir, "validation.csv"), split.ValIn, split.ValOut);
            WriteDataCsv(Path.Combine(seedDir, "test.csv"), split.TestIn, split.TestOut);

            // Oracle baselines on test labels (no NN): how well diagonal vs full quadratic fit the truth.
            double dRmse, dR2, fRmse, fR2;
            double oc0, oa, ob, ocxy;
            FitFullQuad(split.TestIn, split.TestOut, out oc0, out oa, out ob, out ocxy, out fRmse, out fR2);
            FitDiag(split.TestIn, split.TestOut, out oc0, out oa, out ob, out dRmse, out dR2);

            int H = opt.Hidden > 0 ? opt.Hidden : 22;
            // Favour square-linear (funno 7) for uncentered x in [-1,1].
            int lin = Math.Max(2, H / 6);
            int sig = Math.Max(2, H / 6);
            int q4 = Math.Max(2, H / 6);
            int q7 = H - lin - sig - q4;
            if (q7 < 4) { q7 = 4; int rest = H - q7; lin = rest / 3; sig = rest / 3; q4 = rest - lin - sig; }
            VnnBpNet net = PiNetworkBuilder.Create2DOperatorBank(
                "Exp12_" + prob.Id, H, lin, sig, q4, q7, seed, false);

            net.lossType = LossType.MeanSquaredError;
            double eta = 0.05;
            net.eta = eta; net.etab = eta;
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

            PiExperimentRunner.PruneWithLockedBaseline(net, split, opt.PruneFactor, opt.RetrainCycles, seed, logger, eta);

            // Pass E: only install diagonal OLS when the problem is purely diagonal.
            // Never invent an x1*x2 operator — that would hide the limitation.
            if (prob.ExpectsExact && Math.Abs(prob.Cxy) < 1e-12)
            {
                double allowed = FiniteAllowed(denseVal, 1.25);
                double c0, a, b;
                FitDiag(split.TrainIn, split.TrainOut, out c0, out a, out b, out dRmse, out dR2);
                string note;
                if (TryInstallDiag2D(net, split, c0, a, b, allowed, out note))
                    File.WriteAllText(Path.Combine(seedDir, "ols_install.txt"), note);
                else
                    File.WriteAllText(Path.Combine(seedDir, "ols_install.txt"), "rejected: " + note);
            }
            else
            {
                File.WriteAllText(Path.Combine(seedDir, "ols_install.txt"),
                    "skipped: Pass E does not install cross-product (operator unavailable).");
            }

            logger.ExportFinalNetwork(net);
            string eq = net.ExportEquation();
            File.WriteAllText(Path.Combine(seedDir, "exported_equation.txt"), eq);
            File.WriteAllText(Path.Combine(seedDir, "pruned_network.txt"), eq);

            bool explicitCross = EquationHasCrossProduct(eq);
            // Fit network predictions on test to full/diag bases.
            double[][] yhat = Predict(net, split.TestIn);
            double nc0, na, nb, ncxy, nFrmse, nFr2, nDrmse, nDr2;
            FitFullQuad(split.TestIn, yhat, out nc0, out na, out nb, out ncxy, out nFrmse, out nFr2);
            double dc0, da, db;
            FitDiag(split.TestIn, yhat, out dc0, out da, out db, out nDrmse, out nDr2);
            // Prefer full-quad coeffs for reporting (includes cxy); diag used for R2 only.

            double testRmse = Rmse(net, split.TestIn, split.TestOut);

            bool structure;
            if (prob.ExpectsExact)
            {
                // Diagonal recovery: both squared coeffs significant and close to truth.
                double ea = Math.Abs(na - prob.A) / Math.Max(Math.Abs(prob.A), 1e-6);
                double eb = Math.Abs(nb - prob.B) / Math.Max(Math.Abs(prob.B), 1e-6);
                structure = Math.Abs(na) > 0.05 && Math.Abs(nb) > 0.05 && ea < 0.25 && eb < 0.25
                    && Math.Abs(ncxy) < 0.2 * Math.Max(Math.Abs(prob.A), Math.Abs(prob.B));
            }
            else
            {
                // Exact recovery of x1*x2 requires an explicit product operator in the export.
                // Low RMSE via many sigmoids is approximation, not structure recovery — report RMSE separately.
                structure = explicitCross;
            }

            bool limitationOk = prob.ExpectsExact ? structure : !structure;

            Exp12SeedLog row = new Exp12SeedLog();
            row.ProblemId = prob.Id;
            row.Formula = prob.Formula;
            row.ExpectsExactRecovery = prob.ExpectsExact;
            row.Seed = seed;
            row.StructureRecovered = structure;
            row.ExplicitCrossProduct = explicitCross;
            row.LimitationConfirmed = limitationOk;
            row.TestRmse = testRmse;
            row.DiagOnlyRmse = dRmse;
            row.FullQuadRmse = fRmse;
            row.DiagOnlyR2 = dR2;
            row.FullQuadR2 = fR2;
            row.CoeffC0 = nc0;
            row.CoeffA = na;
            row.CoeffB = nb;
            row.CoeffCxy = ncxy;
            row.DenseNeurons = denseN;
            row.FinalNeurons = net.maxNeurons;
            row.DenseConnections = denseC;
            row.FinalConnections = net.maxConnections;
            row.Equation = eq;
            row.Reason = structure
                ? (prob.ExpectsExact ? "diagonal structure recovered" : "UNEXPECTED: cross-like recovery")
                : (prob.ExpectsExact ? "diagonal miss" : "expected: no exact x1*x2 recovery");

            StringBuilder met = new StringBuilder();
            met.AppendLine("{");
            met.AppendFormat(CultureInfo.InvariantCulture, "  \"problem\": \"{0}\",\n", prob.Id);
            met.AppendFormat(CultureInfo.InvariantCulture, "  \"expects_exact\": {0},\n", prob.ExpectsExact ? "true" : "false");
            met.AppendFormat(CultureInfo.InvariantCulture, "  \"structure\": {0},\n", structure ? "true" : "false");
            met.AppendFormat(CultureInfo.InvariantCulture, "  \"explicit_cross\": {0},\n", explicitCross ? "true" : "false");
            met.AppendFormat(CultureInfo.InvariantCulture, "  \"limitation_ok\": {0},\n", limitationOk ? "true" : "false");
            met.AppendFormat(CultureInfo.InvariantCulture, "  \"test_rmse\": {0},\n", JsonNum(testRmse));
            met.AppendFormat(CultureInfo.InvariantCulture, "  \"diag_oracle_r2\": {0},\n", JsonNum(dR2));
            met.AppendFormat(CultureInfo.InvariantCulture, "  \"full_oracle_r2\": {0},\n", JsonNum(fR2));
            met.AppendFormat(CultureInfo.InvariantCulture, "  \"fit_cxy\": {0}\n", JsonNum(ncxy));
            met.AppendLine("}");
            File.WriteAllText(Path.Combine(seedDir, "metrics.json"), met.ToString());
            File.WriteAllText(Path.Combine(seedDir, "canonical_equation.txt"), row.Reason + "\n" + eq);
            return row;
        }

        private static bool EquationHasCrossProduct(string eq)
        {
            if (string.IsNullOrEmpty(eq)) return false;
            // Current exporters never emit n_i*n_j; keep detector for future multiply ops.
            string s = eq.Replace(" ", "");
            if (s.IndexOf("*n", StringComparison.Ordinal) < 0) return false;
            // Look for patterns like n0*n1 or (n0)*(n1) without ^2
            for (int i = 0; i < s.Length - 3; i++)
            {
                if (s[i] != 'n') continue;
                int j = i + 1;
                while (j < s.Length && char.IsDigit(s[j])) j++;
                if (j >= s.Length || s[j] != '*') continue;
                if (j + 1 < s.Length && s[j + 1] == 'n')
                {
                    // nA*nB product
                    int k = j + 2;
                    while (k < s.Length && char.IsDigit(s[k])) k++;
                    if (k > j + 2) return true;
                }
            }
            return false;
        }

        private static double FiniteAllowed(double denseVal, double factor)
        {
            if (!NnMath.IsFinite(denseVal)) return 0.5;
            if (denseVal < 1e-12) return denseVal + 1e-8 + (factor - 1.0) * 1e-8;
            return denseVal * factor;
        }

        private static double[][] Predict(VnnBpNet net, double[][] xin)
        {
            double[][] y = new double[xin.Length][];
            int oi = net.outputs[0].sno;
            for (int i = 0; i < xin.Length; i++)
            {
                for (int k = 0; k < net.maxInputs; k++)
                    net.inputs[k].value = xin[i][k];
                net.outputs[0].value = 0;
                net.UpdateNet();
                y[i] = new double[] { net.neuron[oi].nuOut };
            }
            return y;
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
                if (!NnMath.IsFinite(d)) return double.NaN;
                s += d * d;
            }
            return Math.Sqrt(s / xin.Length);
        }

        private static double Std(double[][] xout)
        {
            if (xout == null || xout.Length == 0) return 1.0;
            double m = 0;
            for (int i = 0; i < xout.Length; i++) m += xout[i][0];
            m /= xout.Length;
            double s = 0;
            for (int i = 0; i < xout.Length; i++)
            {
                double d = xout[i][0] - m;
                s += d * d;
            }
            return Math.Sqrt(s / xout.Length);
        }

        private static void FitDiag(
            double[][] xin, double[][] xout,
            out double c0, out double a, out double b,
            out double rmse, out double r2)
        {
            // Design: [1, x1^2, x2^2]
            int n = xin.Length;
            double[,] AtA = new double[3, 3];
            double[] Atb = new double[3];
            for (int i = 0; i < n; i++)
            {
                double x1 = xin[i][0], x2 = xin[i][1], y = xout[i][0];
                double[] phi = new double[] { 1.0, x1 * x1, x2 * x2 };
                for (int r = 0; r < 3; r++)
                {
                    Atb[r] += phi[r] * y;
                    for (int c = 0; c < 3; c++)
                        AtA[r, c] += phi[r] * phi[c];
                }
            }
            double[] coef = Solve3(AtA, Atb);
            c0 = coef[0]; a = coef[1]; b = coef[2];
            Stats(xin, xout, coef, false, out rmse, out r2);
        }

        private static void FitFullQuad(
            double[][] xin, double[][] xout,
            out double c0, out double a, out double b, out double cxy,
            out double rmse, out double r2)
        {
            // Design: [1, x1^2, x2^2, x1*x2]
            int n = xin.Length;
            double[,] AtA = new double[4, 4];
            double[] Atb = new double[4];
            for (int i = 0; i < n; i++)
            {
                double x1 = xin[i][0], x2 = xin[i][1], y = xout[i][0];
                double[] phi = new double[] { 1.0, x1 * x1, x2 * x2, x1 * x2 };
                for (int r = 0; r < 4; r++)
                {
                    Atb[r] += phi[r] * y;
                    for (int c = 0; c < 4; c++)
                        AtA[r, c] += phi[r] * phi[c];
                }
            }
            double[] coef = Solve4(AtA, Atb);
            c0 = coef[0]; a = coef[1]; b = coef[2]; cxy = coef[3];
            Stats(xin, xout, coef, true, out rmse, out r2);
        }

        private static void Stats(double[][] xin, double[][] xout, double[] coef, bool full, out double rmse, out double r2)
        {
            int n = xin.Length;
            double sse = 0, sst = 0, mean = 0;
            for (int i = 0; i < n; i++) mean += xout[i][0];
            mean /= Math.Max(1, n);
            for (int i = 0; i < n; i++)
            {
                double x1 = xin[i][0], x2 = xin[i][1], y = xout[i][0];
                double pred = coef[0] + coef[1] * x1 * x1 + coef[2] * x2 * x2;
                if (full) pred += coef[3] * x1 * x2;
                double d = y - pred;
                sse += d * d;
                double dm = y - mean;
                sst += dm * dm;
            }
            rmse = Math.Sqrt(sse / Math.Max(1, n));
            r2 = sst < 1e-18 ? 1.0 : 1.0 - sse / sst;
        }

        private static double[] Solve3(double[,] A, double[] b)
        {
            return Gauss(A, b, 3);
        }

        private static double[] Solve4(double[,] A, double[] b)
        {
            return Gauss(A, b, 4);
        }

        private static double[] Gauss(double[,] A0, double[] b0, int n)
        {
            double[,] A = new double[n, n];
            double[] b = new double[n];
            for (int i = 0; i < n; i++)
            {
                b[i] = b0[i];
                for (int j = 0; j < n; j++) A[i, j] = A0[i, j];
            }
            for (int k = 0; k < n; k++)
            {
                int piv = k;
                for (int i = k + 1; i < n; i++)
                    if (Math.Abs(A[i, k]) > Math.Abs(A[piv, k])) piv = i;
                for (int j = 0; j < n; j++)
                {
                    double tmp = A[k, j]; A[k, j] = A[piv, j]; A[piv, j] = tmp;
                }
                { double tmp = b[k]; b[k] = b[piv]; b[piv] = tmp; }
                double diag = A[k, k];
                if (Math.Abs(diag) < 1e-18) diag = 1e-18;
                for (int j = k; j < n; j++) A[k, j] /= diag;
                b[k] /= diag;
                for (int i = 0; i < n; i++)
                {
                    if (i == k) continue;
                    double f = A[i, k];
                    for (int j = k; j < n; j++) A[i, j] -= f * A[k, j];
                    b[i] -= f * b[k];
                }
            }
            return b;
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

        private static void WriteDataCsv(string path, double[][] xin, double[][] xout)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("x1,x2,y");
            for (int i = 0; i < xin.Length; i++)
            {
                sb.AppendFormat(CultureInfo.InvariantCulture,
                    "{0:G15},{1:G15},{2:G15}\r\n", xin[i][0], xin[i][1], xout[i][0]);
            }
            File.WriteAllText(path, sb.ToString());
        }

        private static void WriteSeedCsv(string path, List<Exp12SeedLog> logs)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("problem,expects_exact,seed,structure,explicit_cross,limitation_ok,test_rmse,diag_oracle_rmse,full_oracle_rmse,diag_r2,full_r2,c0,a,b,cxy,dense_n,final_n,dense_e,final_e,runtime_s,reason");
            for (int i = 0; i < logs.Count; i++)
            {
                Exp12SeedLog r = logs[i];
                sb.AppendFormat(CultureInfo.InvariantCulture,
                    "{0},{1},{2},{3},{4},{5},{6},{7},{8},{9},{10},{11},{12},{13},{14},{15},{16},{17},{18},{19},{20}\r\n",
                    r.ProblemId, r.ExpectsExactRecovery ? 1 : 0, r.Seed,
                    r.StructureRecovered ? 1 : 0, r.ExplicitCrossProduct ? 1 : 0, r.LimitationConfirmed ? 1 : 0,
                    JsonNum(r.TestRmse), JsonNum(r.DiagOnlyRmse), JsonNum(r.FullQuadRmse),
                    JsonNum(r.DiagOnlyR2), JsonNum(r.FullQuadR2),
                    JsonNum(r.CoeffC0), JsonNum(r.CoeffA), JsonNum(r.CoeffB), JsonNum(r.CoeffCxy),
                    r.DenseNeurons, r.FinalNeurons, r.DenseConnections, r.FinalConnections,
                    r.RuntimeSeconds.ToString("G15", CultureInfo.InvariantCulture),
                    Escape(r.Reason).Replace(',', ';'));
            }
            File.WriteAllText(path, sb.ToString());
        }

        private static string BuildTable(List<Exp12SeedLog> logs, Problem[] problems)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("# Cross-product limitation");
            sb.AppendLine();
            sb.AppendLine("| Problem | Expect exact | n | Struct % | Explicit xy % | Limitation OK % | Mean RMSE | Oracle diag R² | Oracle full R² |");
            sb.AppendLine("|---------|--------------|---|----------|---------------|-----------------|-----------|----------------|----------------|");
            for (int p = 0; p < problems.Length; p++)
            {
                Problem prob = problems[p];
                int n = 0, st = 0, xy = 0, lim = 0;
                double sumR = 0, sumDr = 0, sumFr = 0;
                int nR = 0;
                for (int i = 0; i < logs.Count; i++)
                {
                    if (logs[i].ProblemId != prob.Id) continue;
                    n++;
                    if (logs[i].StructureRecovered) st++;
                    if (logs[i].ExplicitCrossProduct) xy++;
                    if (logs[i].LimitationConfirmed) lim++;
                    if (NnMath.IsFinite(logs[i].TestRmse)) { sumR += logs[i].TestRmse; nR++; }
                    sumDr += logs[i].DiagOnlyR2;
                    sumFr += logs[i].FullQuadR2;
                }
                if (n == 0) continue;
                sb.AppendFormat(CultureInfo.InvariantCulture,
                    "| {0} | {1} | {2} | {3:F0} | {4:F0} | {5:F0} | {6:G4} | {7:F3} | {8:F3} |\n",
                    prob.Id, prob.ExpectsExact ? "yes" : "no", n,
                    100.0 * st / n, 100.0 * xy / n, 100.0 * lim / n,
                    nR > 0 ? sumR / nR : double.NaN, sumDr / n, sumFr / n);
            }
            return sb.ToString();
        }

        private static string BuildHeadline(List<Exp12SeedLog> logs, Problem[] problems)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendFormat(CultureInfo.InvariantCulture, "Exp12 total runs={0}.\r\n", logs.Count);
            sb.Append(BuildTable(logs, problems));
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
