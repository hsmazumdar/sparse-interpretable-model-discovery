using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;

namespace VnnBp.VnnBpLib
{
    /// <summary>
    /// Experiment 22 — four known laws, several noise levels, 20 seeds.
    /// Experiment 23 — Kepler leave-in splits prepared by exp23_kepler.py.
    /// Same prune–retrain–rollback graph as Exp12. No Math.PI in labels.
    /// Pass E installs a diagonal square only for the pure axis-aligned law (L1).
    /// </summary>
    public static class Exp22Runner
    {
        private enum LawKind { Axis, Shift, Product, Trig }

        private struct Law
        {
            public string Id;
            public string Formula;
            public LawKind Kind;
        }

        public static int Run(string[] args)
        {
            PiOptions opt = PiOptions.Parse(args);
            opt.AllowFallback = false;
            bool quick = false;
            bool seedsSet = false;
            bool epochsSet = false;
            bool retrainSet = false;
            int samples = 800;
            string lawsArg = null;
            string noiseArg = null;
            if (args != null)
            {
                for (int i = 0; i < args.Length; i++)
                {
                    if (args[i] == "--quick") quick = true;
                    if (args[i] == "--seeds") seedsSet = true;
                    if (args[i] == "--epochs") epochsSet = true;
                    if (args[i] == "--retrain" && i + 1 < args.Length)
                    {
                        opt.RetrainCycles = Math.Max(1, int.Parse(args[++i], CultureInfo.InvariantCulture));
                        retrainSet = true;
                    }
                    if (args[i] == "--samples" && i + 1 < args.Length)
                        samples = Math.Max(80, int.Parse(args[++i], CultureInfo.InvariantCulture));
                    if (args[i] == "--laws" && i + 1 < args.Length)
                        lawsArg = args[++i];
                    if (args[i] == "--noise" && i + 1 < args.Length)
                        noiseArg = args[++i];
                }
            }
            if (!seedsSet) opt.SeedCount = quick ? 1 : 20;
            if (!epochsSet && opt.DenseEpochs == 400) opt.DenseEpochs = quick ? 40 : 150;
            if (!retrainSet) opt.RetrainCycles = quick ? 400 : 5000;

            string root = ProjectRoot();
            bool defaultOut = string.IsNullOrEmpty(opt.ResultsDir)
                || opt.ResultsDir.IndexOf("PiExperiment", StringComparison.OrdinalIgnoreCase) >= 0;
            if (defaultOut)
                opt.ResultsDir = Path.Combine(root, "Results", "Exp22_KnownLaws");
            Directory.CreateDirectory(opt.ResultsDir);

            Law[] allLaws = new Law[]
            {
                L("L1", "y=x1^2+x2^2", LawKind.Axis),
                L("L2", "y=(x1-0.4)^2+(x2+0.25)^2", LawKind.Shift),
                L("L3", "y=x1^2+2*x1*x2+4*x2^2", LawKind.Product),
                L("L4", "y=sin(x1)+x2^2", LawKind.Trig),
            };
            Law[] laws = FilterLaws(allLaws, lawsArg);
            double[] noises = ParseNoises(noiseArg, quick);

            StringBuilder console = new StringBuilder();
            Log(console, "Experiment 22 — known laws (axis, shifted quadratic, product, sine)");
            Log(console, "Results: " + opt.ResultsDir);
            Log(console, "Seeds: " + opt.SeedCount + "  epochs=" + opt.DenseEpochs
                + "  retrain=" + opt.RetrainCycles + "  samples=" + samples);
            Log(console, "Noise std (absolute, same generator as Exp05): " + JoinD(noises));
            Log(console, "Pass E diagonal install only for L1. No x1*x2 operator. No sine operator.");

            string protocol = ProtocolText();
            File.WriteAllText(Path.Combine(opt.ResultsDir, "PROTOCOL.md"), protocol);

            List<string> rows = new List<string>();
            rows.Add("law,formula,noise,seed,test_rmse_noisy,runtime_s,pass_e,dense_e,final_e,dense_n,final_n");

            for (int li = 0; li < laws.Length; li++)
            {
                for (int ni = 0; ni < noises.Length; ni++)
                {
                    Law law = laws[li];
                    double noise = noises[ni];
                    string tag = NoiseTag(noise);
                    string pdir = Path.Combine(opt.ResultsDir, law.Id, "noise_" + tag);
                    Directory.CreateDirectory(pdir);
                    File.WriteAllText(Path.Combine(pdir, "problem.txt"),
                        law.Id + "\n" + law.Formula + "\nnoise=" + noise.ToString("G", CultureInfo.InvariantCulture));

                    int dataSeed = 88000 + LawIndex(law.Id) * 100 + ni;
                    double[][] xin;
                    double[][] yNoisy;
                    Generate(law, samples, dataSeed, noise, out xin, out yNoisy);
                    Log(console, "");
                    Log(console, "=== " + law.Id + " noise=" + noise.ToString("G", CultureInfo.InvariantCulture) + " ===");

                    for (int s = 1; s <= opt.SeedCount; s++)
                    {
                        string seedDir = Path.Combine(pdir, string.Format(CultureInfo.InvariantCulture, "seed_{0:00}", s));
                        Directory.CreateDirectory(seedDir);
                        string predTest = Path.Combine(seedDir, "pred_test.csv");
                        string rowPath = Path.Combine(seedDir, "hsm_row.txt");
                        if (File.Exists(predTest) && File.Exists(rowPath) && File.Exists(Path.Combine(seedDir, "pred_grid.csv")))
                        {
                            rows.Add(File.ReadAllText(rowPath).Trim());
                            Log(console, law.Id + " n=" + tag + " seed " + s + " resume");
                            continue;
                        }
                        Log(console, law.Id + " noise " + tag + " seed " + s + "/" + opt.SeedCount + "...");
                        Stopwatch sw = Stopwatch.StartNew();
                        string row = RunOne(law, noise, xin, yNoisy, s, opt, seedDir);
                        sw.Stop();
                        // runtime is the last field already written inside RunOne via placeholder; replace file
                        File.WriteAllText(Path.Combine(seedDir, "runtime.txt"),
                            sw.Elapsed.TotalSeconds.ToString("G15", CultureInfo.InvariantCulture));
                        row = row.Replace("RUNTIME", sw.Elapsed.TotalSeconds.ToString("G15", CultureInfo.InvariantCulture));
                        File.WriteAllText(rowPath, row);
                        rows.Add(row);
                        Log(console, "  " + row);
                    }
                }
            }

            File.WriteAllText(Path.Combine(opt.ResultsDir, "hsm_seed_logs.csv"), string.Join("\r\n", rows.ToArray()) + "\r\n");
            Log(console, "Experiment 22 network runs finished. Score with exp22_score.py.");
            File.WriteAllText(Path.Combine(opt.ResultsDir, "console.log"), console.ToString());
            return 0;
        }

        private static string RunOne(
            Law law, double noise, double[][] xin, double[][] yNoisy, int seed, PiOptions opt, string seedDir)
        {
            DatasetSplit split = DatasetSplit.CreateDefault(xin, yNoisy, seed);
            WriteXy(Path.Combine(seedDir, "train.csv"), split.TrainIn, split.TrainOut);
            WriteXy(Path.Combine(seedDir, "validation.csv"), split.ValIn, split.ValOut);
            WriteXy(Path.Combine(seedDir, "test.csv"), split.TestIn, split.TestOut);
            File.WriteAllText(Path.Combine(seedDir, "seed.txt"), seed.ToString(CultureInfo.InvariantCulture));
            File.WriteAllText(Path.Combine(seedDir, "problem.txt"), law.Id + "\n" + law.Formula);

            int H = opt.Hidden > 0 ? opt.Hidden : 22;
            int lin = Math.Max(2, H / 6);
            int sig = Math.Max(2, H / 6);
            int q4 = Math.Max(2, H / 6);
            int q7 = H - lin - sig - q4;
            if (q7 < 4)
            {
                q7 = 4;
                int rest = H - q7;
                lin = rest / 3;
                sig = rest / 3;
                q4 = rest - lin - sig;
            }
            VnnBpNet net = PiNetworkBuilder.Create2DOperatorBank(
                "Exp22_" + law.Id, H, lin, sig, q4, q7, seed, false);
            net.lossType = LossType.MeanSquaredError;
            double eta = 0.05;
            net.eta = eta;
            net.etab = eta;
            TrainingOptions tr = new TrainingOptions();
            tr.LearningRate = eta;
            tr.LossType = LossType.MeanSquaredError;
            tr.ShuffleEachEpoch = true;
            for (int e = 0; e < opt.DenseEpochs; e++)
            {
                tr.RandomSeed = seed * 1000 + e;
                net.TrainEpoch(split.TrainIn, split.TrainOut, tr);
            }
            int denseN = net.maxNeurons;
            int denseC = net.maxConnections;
            double denseVal = net.Evaluate(split.ValIn, split.ValOut).Loss;
            ResearchLogger logger = new ResearchLogger(seedDir);
            PiExperimentRunner.PruneWithLockedBaseline(
                net, split, opt.PruneFactor, opt.RetrainCycles, seed, logger, eta);

            string passNote = "skipped";
            if (law.Kind == LawKind.Axis)
            {
                double c0, a, b, rmse, r2;
                FitDiag(split.TrainIn, split.TrainOut, out c0, out a, out b, out rmse, out r2);
                double allowed = FiniteAllowed(denseVal, 1.25);
                if (TryInstallDiag2D(net, split, c0, a, b, allowed, out passNote))
                    passNote = "installed " + passNote;
                else
                    passNote = "rejected " + passNote;
            }
            else
            {
                passNote = "skipped: Pass E installs a diagonal square only for L1";
            }
            File.WriteAllText(Path.Combine(seedDir, "ols_install.txt"), passNote);

            string eq = net.ExportEquation();
            File.WriteAllText(Path.Combine(seedDir, "exported_equation.txt"), eq);
            logger.ExportFinalNetwork(net);

            WritePred(Path.Combine(seedDir, "pred_train.csv"), net, split.TrainIn, split.TrainOut);
            WritePred(Path.Combine(seedDir, "pred_test.csv"), net, split.TestIn, split.TestOut);
            WriteGrid(Path.Combine(seedDir, "pred_grid.csv"), net);

            double testRmse = Rmse(net, split.TestIn, split.TestOut);
            int passFlag = passNote.StartsWith("installed", StringComparison.Ordinal) ? 1 : 0;
            return string.Format(CultureInfo.InvariantCulture,
                "{0},{1},{2},{3},{4},RUNTIME,{5},{6},{7},{8},{9}",
                law.Id,
                law.Formula.Replace(',', ';'),
                noise.ToString("G", CultureInfo.InvariantCulture),
                seed,
                Num(testRmse),
                passFlag,
                denseC,
                net.maxConnections,
                denseN,
                net.maxNeurons);
        }

        private static void Generate(Law law, int n, int seed, double noise, out double[][] xin, out double[][] yout)
        {
            Random rnd = new Random(seed);
            xin = new double[n][];
            yout = new double[n][];
            for (int i = 0; i < n; i++)
            {
                double x1 = 2.0 * rnd.NextDouble() - 1.0;
                double x2 = 2.0 * rnd.NextDouble() - 1.0;
                double y = Eval(law.Kind, x1, x2);
                if (noise > 0)
                    y += Gauss(rnd) * noise;
                xin[i] = new double[] { x1, x2 };
                yout[i] = new double[] { y };
            }
        }

        private static double Eval(LawKind kind, double x1, double x2)
        {
            if (kind == LawKind.Axis)
                return x1 * x1 + x2 * x2;
            if (kind == LawKind.Shift)
                return (x1 - 0.4) * (x1 - 0.4) + (x2 + 0.25) * (x2 + 0.25);
            if (kind == LawKind.Product)
                return x1 * x1 + 2.0 * x1 * x2 + 4.0 * x2 * x2;
            return Math.Sin(x1) + x2 * x2;
        }

        private static void WriteGrid(string path, VnnBpNet net)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("x1,x2,yhat");
            const int n = 21;
            for (int i = 0; i < n; i++)
            {
                double x1 = -1.0 + 2.0 * i / 20.0;
                for (int j = 0; j < n; j++)
                {
                    double x2 = -1.0 + 2.0 * j / 20.0;
                    double yhat = Predict1(net, x1, x2);
                    sb.AppendFormat(CultureInfo.InvariantCulture, "{0:G15},{1:G15},{2:G15}\r\n", x1, x2, yhat);
                }
            }
            File.WriteAllText(path, sb.ToString());
        }

        private static string ProtocolText()
        {
            return "# Experiment 22 protocol\r\n\r\n"
                + "Laws (inputs uniform on [-1,1]^2, independent of the split seed):\r\n\r\n"
                + "- L1 axis-aligned squares: y = x1^2 + x2^2\r\n"
                + "- L2 shifted quadratic: y = (x1 - 0.4)^2 + (x2 + 0.25)^2\r\n"
                + "- L3 product: y = x1^2 + 2 x1 x2 + 4 x2^2\r\n"
                + "- L4 trigonometric: y = sin(x1) + x2^2\r\n\r\n"
                + "Noise is additive and absolute, std in {0, 0.02, 0.05}, "
                + "using the Exp05 sum-of-12-uniforms generator. "
                + "The same 800-point draw is split 70/15/15 by seed, matching DatasetSplit.\r\n\r\n"
                + "The graph is the Exp12 operator bank (linear, sigmoid, centred quadratic-sigmoid, "
                + "uncentred square-linear). Dense training 150 epochs, prune factor 1.1, "
                + "5000 retrain sample-updates, rollback outside the validation strip. "
                + "Pass E may replace the graph by a diagonal square fit only on L1, "
                + "and only if validation loss stays within 1.25 times the dense baseline. "
                + "It does not install a product or a sine.\r\n\r\n"
                + "Scoring is in exp22_score.py (written before reading these runs): "
                + "term recovery if every true coefficient is within 25% relative "
                + "and every other readout coefficient is at most 0.05 in absolute value, "
                + "and, for the network and gplearn, the function lies in the poly-3 + sin(x1) span "
                + "(residual <= 5% of the clean-target standard deviation). "
                + "SINDy is sequential-thresholded least squares on the full degree-3 polynomial library "
                + "(10 monomials). That library is not the true support. It has no sine column.\r\n";
        }

        private static Law L(string id, string formula, LawKind kind)
        {
            Law law = new Law();
            law.Id = id;
            law.Formula = formula;
            law.Kind = kind;
            return law;
        }

        private static int LawIndex(string id)
        {
            if (id == "L1") return 1;
            if (id == "L2") return 2;
            if (id == "L3") return 3;
            return 4;
        }

        private static Law[] FilterLaws(Law[] all, string arg)
        {
            if (string.IsNullOrEmpty(arg)) return all;
            string[] parts = arg.Split(',');
            List<Law> keep = new List<Law>();
            for (int i = 0; i < all.Length; i++)
            {
                for (int j = 0; j < parts.Length; j++)
                {
                    if (string.Equals(all[i].Id, parts[j].Trim(), StringComparison.OrdinalIgnoreCase))
                        keep.Add(all[i]);
                }
            }
            if (keep.Count == 0) return all;
            return keep.ToArray();
        }

        private static double[] ParseNoises(string arg, bool quick)
        {
            if (string.IsNullOrEmpty(arg))
                return quick ? new double[] { 0.0 } : new double[] { 0.0, 0.02, 0.05 };
            string[] parts = arg.Split(',');
            double[] v = new double[parts.Length];
            for (int i = 0; i < parts.Length; i++)
                v[i] = double.Parse(parts[i].Trim(), CultureInfo.InvariantCulture);
            return v;
        }

        private static string NoiseTag(double noise)
        {
            return noise.ToString("G", CultureInfo.InvariantCulture).Replace('.', 'p');
        }

        private static double Gauss(Random rnd)
        {
            double s = 0;
            for (int k = 0; k < 12; k++) s += rnd.NextDouble();
            return s - 6.0;
        }

        private static void WriteXy(string path, double[][] xin, double[][] xout)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("x1,x2,y");
            for (int i = 0; i < xin.Length; i++)
            {
                sb.AppendFormat(CultureInfo.InvariantCulture, "{0:G15},{1:G15},{2:G15}\r\n",
                    xin[i][0], xin[i][1], xout[i][0]);
            }
            File.WriteAllText(path, sb.ToString());
        }

        private static void WritePred(string path, VnnBpNet net, double[][] xin, double[][] xout)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("x1,x2,y,yhat");
            for (int i = 0; i < xin.Length; i++)
            {
                double yhat = Predict1(net, xin[i][0], xin[i][1]);
                sb.AppendFormat(CultureInfo.InvariantCulture, "{0:G15},{1:G15},{2:G15},{3:G15}\r\n",
                    xin[i][0], xin[i][1], xout[i][0], yhat);
            }
            File.WriteAllText(path, sb.ToString());
        }

        private static double Predict1(VnnBpNet net, double x1, double x2)
        {
            int oi = net.outputs[0].sno;
            net.inputs[0].value = x1;
            if (net.maxInputs > 1) net.inputs[1].value = x2;
            net.outputs[0].value = 0;
            net.UpdateNet();
            return net.neuron[oi].nuOut;
        }

        private static double Rmse(VnnBpNet net, double[][] xin, double[][] xout)
        {
            double s = 0;
            for (int i = 0; i < xin.Length; i++)
            {
                double d = Predict1(net, xin[i][0], xin[i][1]) - xout[i][0];
                s += d * d;
            }
            return Math.Sqrt(s / Math.Max(1, xin.Length));
        }

        internal static double FiniteAllowed(double denseVal, double factor)
        {
            if (denseVal < 1e-12) return denseVal + 1e-8 + (factor - 1.0) * 1e-8;
            return denseVal * factor;
        }

        internal static void FitDiag(
            double[][] xin, double[][] xout,
            out double c0, out double a, out double b, out double rmse, out double r2)
        {
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
            double[] coef = GaussSolve(AtA, Atb, 3);
            c0 = coef[0]; a = coef[1]; b = coef[2];
            double sse = 0, sst = 0, mean = 0;
            for (int i = 0; i < n; i++) mean += xout[i][0];
            mean /= Math.Max(1, n);
            for (int i = 0; i < n; i++)
            {
                double x1 = xin[i][0], x2 = xin[i][1], y = xout[i][0];
                double pred = c0 + a * x1 * x1 + b * x2 * x2;
                double d = y - pred;
                sse += d * d;
                double dm = y - mean;
                sst += dm * dm;
            }
            rmse = Math.Sqrt(sse / Math.Max(1, n));
            r2 = sst < 1e-18 ? 1.0 : 1.0 - sse / sst;
        }

        private static double[] GaussSolve(double[,] A0, double[] b0, int n)
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

        internal static bool TryInstallDiag2D(
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
                "diag OLS c0={0:G6} a={1:G6} b={2:G6} Lval={3:G6}", c0, a, b, val);
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

        private static string Num(double v)
        {
            if (double.IsNaN(v) || double.IsInfinity(v)) return "";
            return v.ToString("G15", CultureInfo.InvariantCulture);
        }

        private static string JoinD(double[] v)
        {
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < v.Length; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append(v[i].ToString("G", CultureInfo.InvariantCulture));
            }
            return sb.ToString();
        }

        private static void Log(StringBuilder sb, string line)
        {
            sb.AppendLine(line);
            Console.WriteLine(line);
        }

        public static string ProjectRoot()
        {
            string dir = AppDomain.CurrentDomain.BaseDirectory;
            for (int i = 0; i < 6 && !string.IsNullOrEmpty(dir); i++)
            {
                if (File.Exists(Path.Combine(dir, "NnPruneHsm.sln"))
                    || Directory.Exists(Path.Combine(dir, "NnPruneHsm")) && File.Exists(Path.Combine(dir, "exp11_sklearn.py")))
                    return dir;
                DirectoryInfo parent = Directory.GetParent(dir);
                if (parent == null) break;
                dir = parent.FullName;
            }
            return AppDomain.CurrentDomain.BaseDirectory;
        }
    }

    /// <summary>
    /// Experiment 23 — quadratic graph on the NASA planetary table prepared by exp23_kepler.py.
    /// Inputs are standardized from the training fold only. Predictions are returned in years.
    /// </summary>
    public static class Exp23Runner
    {
        public static int Run(string[] args)
        {
            PiOptions opt = PiOptions.Parse(args);
            opt.AllowFallback = false;
            bool epochsSet = false;
            bool retrainSet = false;
            if (args != null)
            {
                for (int i = 0; i < args.Length; i++)
                {
                    if (args[i] == "--epochs") epochsSet = true;
                    if (args[i] == "--retrain" && i + 1 < args.Length)
                    {
                        opt.RetrainCycles = Math.Max(1, int.Parse(args[++i], CultureInfo.InvariantCulture));
                        retrainSet = true;
                    }
                }
            }
            if (!epochsSet && opt.DenseEpochs == 400) opt.DenseEpochs = 150;
            if (!retrainSet) opt.RetrainCycles = 5000;

            string root = Exp22Runner.ProjectRoot();
            string dir = Path.Combine(root, "Results", "Exp23_Kepler");
            if (!Directory.Exists(dir))
            {
                Console.WriteLine("ERROR: missing " + dir + " (run exp23_kepler.py first)");
                return 1;
            }
            string[] seeds = Directory.GetDirectories(dir, "seed_*");
            Array.Sort(seeds, StringComparer.OrdinalIgnoreCase);
            Console.WriteLine("Experiment 23 — Kepler folds: " + seeds.Length);
            double[][] grid = ReadXy(Path.Combine(dir, "grid.csv"), false);
            for (int s = 0; s < seeds.Length; s++)
            {
                string seedDir = seeds[s];
                if (File.Exists(Path.Combine(seedDir, "pred_grid.csv")))
                {
                    Console.WriteLine(Path.GetFileName(seedDir) + " resume");
                    continue;
                }
                Console.WriteLine(Path.GetFileName(seedDir) + "...");
                Stopwatch sw = Stopwatch.StartNew();
                RunFold(seedDir, grid, opt, s + 1);
                sw.Stop();
                File.WriteAllText(Path.Combine(seedDir, "hsm_runtime.txt"),
                    sw.Elapsed.TotalSeconds.ToString("G15", CultureInfo.InvariantCulture));
                Console.WriteLine("  " + sw.Elapsed.TotalSeconds.ToString("F2", CultureInfo.InvariantCulture) + " s");
            }
            Console.WriteLine("Experiment 23 network runs finished.");
            return 0;
        }

        private static void RunFold(string seedDir, double[][] grid, PiOptions opt, int seed)
        {
            double[][] trX, trY, vaX, vaY, teX, teY;
            ReadSplit(Path.Combine(seedDir, "train.csv"), out trX, out trY);
            ReadSplit(Path.Combine(seedDir, "validation.csv"), out vaX, out vaY);
            ReadSplit(Path.Combine(seedDir, "test.csv"), out teX, out teY);

            double meanA = 0, meanP = 0;
            for (int i = 0; i < trX.Length; i++)
            {
                meanA += trX[i][0];
                meanP += trY[i][0];
            }
            meanA /= trX.Length;
            meanP /= trY.Length;
            double varA = 0, varP = 0;
            for (int i = 0; i < trX.Length; i++)
            {
                double da = trX[i][0] - meanA;
                double dp = trY[i][0] - meanP;
                varA += da * da;
                varP += dp * dp;
            }
            double stdA = Math.Sqrt(varA / trX.Length);
            double stdP = Math.Sqrt(varP / trY.Length);
            if (stdA < 1e-8) stdA = 1;
            if (stdP < 1e-8) stdP = 1;
            File.WriteAllText(Path.Combine(seedDir, "scale.txt"),
                string.Format(CultureInfo.InvariantCulture, "meanA={0:G15}\nstdA={1:G15}\nmeanP={2:G15}\nstdP={3:G15}\n",
                    meanA, stdA, meanP, stdP));

            double[][] trXs = ScaleIn(trX, meanA, stdA);
            double[][] vaXs = ScaleIn(vaX, meanA, stdA);
            double[][] teXs = ScaleIn(teX, meanA, stdA);
            double[][] trYs = ScaleOut(trY, meanP, stdP);
            double[][] vaYs = ScaleOut(vaY, meanP, stdP);

            DatasetSplit split = new DatasetSplit();
            split.TrainIn = trXs; split.TrainOut = trYs;
            split.ValIn = vaXs; split.ValOut = vaYs;
            split.TestIn = teXs; split.TestOut = ScaleOut(teY, meanP, stdP);

            int H = 12;
            int lin = 3, sig = 3, q4 = 2, q7 = 4;
            VnnBpNet net = PiNetworkBuilder.Create2DOperatorBank(
                "Exp23_Kepler", H, lin, sig, q4, q7, seed, false);
            net.lossType = LossType.MeanSquaredError;
            double eta = 0.05;
            net.eta = eta; net.etab = eta;
            TrainingOptions trOpt = new TrainingOptions();
            trOpt.LearningRate = eta;
            trOpt.LossType = LossType.MeanSquaredError;
            trOpt.ShuffleEachEpoch = true;
            for (int e = 0; e < opt.DenseEpochs; e++)
            {
                trOpt.RandomSeed = seed * 1000 + e;
                net.TrainEpoch(split.TrainIn, split.TrainOut, trOpt);
            }
            ResearchLogger logger = new ResearchLogger(seedDir);
            PiExperimentRunner.PruneWithLockedBaseline(
                net, split, opt.PruneFactor, opt.RetrainCycles, seed, logger, eta);
            File.WriteAllText(Path.Combine(seedDir, "exported_equation.txt"), net.ExportEquation());
            File.WriteAllText(Path.Combine(seedDir, "ols_install.txt"),
                "skipped: Kepler is not a diagonal square in semi-major axis");

            WriteOrigPred(Path.Combine(seedDir, "pred_test.csv"), net, teX, teY, meanA, stdA, meanP, stdP);
            WriteGridPred(Path.Combine(seedDir, "pred_grid.csv"), net, grid, meanA, stdA, meanP, stdP);
        }

        private static double[][] ScaleIn(double[][] x, double meanA, double stdA)
        {
            double[][] z = new double[x.Length][];
            for (int i = 0; i < x.Length; i++)
                z[i] = new double[] { (x[i][0] - meanA) / stdA, 0.0 };
            return z;
        }

        private static double[][] ScaleOut(double[][] y, double meanP, double stdP)
        {
            double[][] z = new double[y.Length][];
            for (int i = 0; i < y.Length; i++)
                z[i] = new double[] { (y[i][0] - meanP) / stdP };
            return z;
        }

        private static void WriteOrigPred(
            string path, VnnBpNet net, double[][] x, double[][] y,
            double meanA, double stdA, double meanP, double stdP)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("x1,x2,y,yhat");
            for (int i = 0; i < x.Length; i++)
            {
                double yhat = PredictYears(net, x[i][0], meanA, stdA, meanP, stdP);
                sb.AppendFormat(CultureInfo.InvariantCulture, "{0:G15},{1:G15},{2:G15},{3:G15}\r\n",
                    x[i][0], 0.0, y[i][0], yhat);
            }
            File.WriteAllText(path, sb.ToString());
        }

        private static void WriteGridPred(
            string path, VnnBpNet net, double[][] grid,
            double meanA, double stdA, double meanP, double stdP)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("x1,x2,yhat");
            for (int i = 0; i < grid.Length; i++)
            {
                double yhat = PredictYears(net, grid[i][0], meanA, stdA, meanP, stdP);
                sb.AppendFormat(CultureInfo.InvariantCulture, "{0:G15},0,{1:G15}\r\n", grid[i][0], yhat);
            }
            File.WriteAllText(path, sb.ToString());
        }

        private static double PredictYears(VnnBpNet net, double a, double meanA, double stdA, double meanP, double stdP)
        {
            int oi = net.outputs[0].sno;
            net.inputs[0].value = (a - meanA) / stdA;
            if (net.maxInputs > 1) net.inputs[1].value = 0;
            net.outputs[0].value = 0;
            net.UpdateNet();
            return net.neuron[oi].nuOut * stdP + meanP;
        }

        private static void ReadSplit(string path, out double[][] xin, out double[][] yout)
        {
            double[][] raw = ReadXy(path, true);
            xin = new double[raw.Length][];
            yout = new double[raw.Length][];
            for (int i = 0; i < raw.Length; i++)
            {
                xin[i] = new double[] { raw[i][0], raw[i][1] };
                yout[i] = new double[] { raw[i][2] };
            }
        }

        private static double[][] ReadXy(string path, bool hasY)
        {
            string[] lines = File.ReadAllLines(path);
            List<double[]> rows = new List<double[]>();
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (line.Length == 0) continue;
                if (i == 0 && (line.StartsWith("x1", StringComparison.OrdinalIgnoreCase) || line.StartsWith("a", StringComparison.OrdinalIgnoreCase)))
                    continue;
                string[] p = line.Split(',');
                if (hasY)
                    rows.Add(new double[] {
                        double.Parse(p[0], CultureInfo.InvariantCulture),
                        double.Parse(p[1], CultureInfo.InvariantCulture),
                        double.Parse(p[2], CultureInfo.InvariantCulture) });
                else
                    rows.Add(new double[] { double.Parse(p[0], CultureInfo.InvariantCulture), 0 });
            }
            return rows.ToArray();
        }
    }
}
