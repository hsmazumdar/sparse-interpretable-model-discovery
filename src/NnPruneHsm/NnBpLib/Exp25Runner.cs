using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;

namespace VnnBp.VnnBpLib
{
    /// <summary>
    /// Experiment 25. Adds a product neuron and a power neuron to the Exp22 bank.
    /// Prune factor, validation strip, retrain budget, and Pass E are unchanged.
    /// Gradient clipping is off unless a cell has more than 5/20 non-finite seeds.
    /// </summary>
    public static class Exp25Runner
    {
        private const double KeplerADiv = 40.0;
        private const double KeplerPDiv = 100.0;

        public static int Run(string[] args)
        {
            if (Has(args, "--selfcheck"))
                return SelfCheck() ? 0 : 1;

            PiOptions opt = PiOptions.Parse(args);
            opt.AllowFallback = false;
            bool epochsSet = false;
            bool retrainSet = false;
            bool seedsSet = false;
            string bank = "product";
            string part = "all";
            string lawsArg = null;
            if (args != null)
            {
                for (int i = 0; i < args.Length; i++)
                {
                    if (args[i] == "--seeds") seedsSet = true;
                    if (args[i] == "--epochs") epochsSet = true;
                    if (args[i] == "--retrain" && i + 1 < args.Length)
                    {
                        opt.RetrainCycles = Math.Max(1, int.Parse(args[++i], CultureInfo.InvariantCulture));
                        retrainSet = true;
                    }
                    if (args[i] == "--bank" && i + 1 < args.Length)
                        bank = args[++i].Trim().ToLowerInvariant();
                    if (args[i] == "--part" && i + 1 < args.Length)
                        part = args[++i].Trim().ToLowerInvariant();
                    if (args[i] == "--laws" && i + 1 < args.Length)
                        lawsArg = args[++i];
                }
            }
            if (bank != "product" && bank != "power" && bank != "both")
            {
                Console.WriteLine("bank must be product, power, or both");
                return 2;
            }
            if (!seedsSet) opt.SeedCount = 20;
            if (!epochsSet && opt.DenseEpochs == 400) opt.DenseEpochs = 150;
            if (!retrainSet) opt.RetrainCycles = 5000;

            string root = Exp22Runner.ProjectRoot();
            string outRoot = Path.Combine(root, "Results", "Exp25_OperatorExtension");
            Directory.CreateDirectory(outRoot);
            File.WriteAllText(Path.Combine(outRoot, "PROTOCOL.md"), ProtocolText());

            int nProd = (bank == "power") ? 0 : 2;
            int nPow = (bank == "product") ? 0 : 2;
            Console.WriteLine("Exp25 bank=" + bank + " seeds=" + opt.SeedCount
                + " epochs=" + opt.DenseEpochs + " retrain=" + opt.RetrainCycles);

            if (part == "all" || part == "laws")
                RunLaws(opt, root, outRoot, bank, nProd, nPow, lawsArg);
            if (part == "all" || part == "kepler")
                RunKepler(opt, root, outRoot, bank, nProd, nPow);
            return 0;
        }

        public static bool SelfCheck()
        {
            double dpk, dpa;
            double p = NnMath.PowerSigned(4.0, 1.5, 1.0, 1.0, out dpk, out dpa);
            if (Math.Abs(p - 8.0) > 1e-9)
            {
                Console.WriteLine("power value " + p.ToString("G", CultureInfo.InvariantCulture));
                return false;
            }
            VnnBpNet net = PiNetworkBuilder.Create2DOperatorBank("exp25check", 6, 1, 1, 1, 3, 1, false);
            PiNetworkBuilder.InstallOperatorExtension(net, 1, 1);
            net.eta = 0.05;
            net.etab = 0.05;
            net.lossType = LossType.MeanSquaredError;
            net.inputs[0].value = 0.5;
            net.inputs[1].value = -0.4;
            net.UpdateNet();
            bool sawProd = false;
            for (int i = 0; i < net.maxNeurons; i++)
            {
                if (net.neuron[i].funno != 8) continue;
                sawProd = true;
                if (Math.Abs(net.neuron[i].nuOut - (0.5 * -0.4)) > 1e-12)
                {
                    Console.WriteLine("product output " + net.neuron[i].nuOut.ToString("G", CultureInfo.InvariantCulture));
                    return false;
                }
            }
            if (!sawProd) { Console.WriteLine("no product neuron"); return false; }

            int pow = -1;
            for (int i = 0; i < net.maxNeurons; i++)
                if (net.neuron[i].funno == 9) pow = i;
            if (pow < 0) { Console.WriteLine("no power neuron"); return false; }
            int src, connK;
            double k;
            net.PowerSource(pow, out src, out k, out connK);
            net.conn[connK].wgt = 1.5;
            int output = net.outputs[0].sno;
            for (int c = 0; c < net.maxConnections; c++)
                if (net.conn[c].dstNuNo == output) net.conn[c].wgt = 0.0;
            net.neuron[output].bias = 0.0;
            PiNetworkBuilder.SetEdgesFromFunno(net, 9, 1.0);
            net.inputs[0].value = 4.0;
            net.inputs[1].value = 0.0;
            net.outputs[0].value = 0.0;
            net.UpdateNet();
            if (Math.Abs(net.neuron[pow].nuOut - 8.0) > 1e-8)
            {
                Console.WriteLine("power neuron " + net.neuron[pow].nuOut.ToString("G", CultureInfo.InvariantCulture));
                return false;
            }
            if (Math.Abs(net.neuron[output].nuOut - 8.0) > 1e-8)
            {
                Console.WriteLine("power path output " + net.neuron[output].nuOut.ToString("G", CultureInfo.InvariantCulture));
                return false;
            }
            string eq = net.ExportEquation();
            if (eq.IndexOf("n0*n1", StringComparison.Ordinal) < 0 || eq.IndexOf("a^k", StringComparison.Ordinal) < 0)
            {
                Console.WriteLine("export missing operator name");
                Console.WriteLine(eq);
                return false;
            }
            double kBefore = 1.5;
            net.ReinforceConnections();
            net.PowerSource(pow, out src, out k, out connK);
            double expect = kBefore - 0.05 * 8.0 * (8.0 * Math.Log(4.0));
            if (Math.Abs(k - expect) > 1e-6)
            {
                Console.WriteLine("k " + k.ToString("G17", CultureInfo.InvariantCulture)
                    + " expected " + expect.ToString("G17", CultureInfo.InvariantCulture));
                return false;
            }
            Console.WriteLine("Exp25 self-check passed");
            return true;
        }

        private static void RunLaws(PiOptions opt, string root, string outRoot, string bank, int nProd, int nPow, string lawsArg)
        {
            string srcRoot = Path.Combine(root, "Results", "Exp22_KnownLaws");
            string[] laws = new string[] { "L1", "L2", "L3", "L4" };
            if (!string.IsNullOrEmpty(lawsArg))
                laws = lawsArg.Split(',');
            for (int li = 0; li < laws.Length; li++)
            {
                string law = laws[li].Trim();
                string lawDir = Path.Combine(srcRoot, law);
                if (!Directory.Exists(lawDir)) continue;
                string[] noises = Directory.GetDirectories(lawDir);
                Array.Sort(noises, StringComparer.OrdinalIgnoreCase);
                for (int ni = 0; ni < noises.Length; ni++)
                {
                    string noiseName = Path.GetFileName(noises[ni]);
                    string cell = Path.Combine(outRoot, bank, law, noiseName);
                    Directory.CreateDirectory(cell);
                    bool passE = law == "L1";
                    int bad = RunLawSeeds(opt, noises[ni], cell, bank, nProd, nPow, passE, 0.0, false);
                    string clipPath = Path.Combine(cell, "CLIP.txt");
                    if (!File.Exists(clipPath) && bad > 5)
                    {
                        File.WriteAllText(clipPath, "gradient_clip=0.5\r\nreason=nonfinite " + bad.ToString(CultureInfo.InvariantCulture) + " of " + opt.SeedCount.ToString(CultureInfo.InvariantCulture) + "\r\n");
                        Console.WriteLine("CLIP " + bank + " " + law + " " + noiseName + " bad=" + bad);
                        RunLawSeeds(opt, noises[ni], cell, bank, nProd, nPow, passE, 0.5, true);
                    }
                }
            }
        }

        private static int RunLawSeeds(
            PiOptions opt, string srcNoise, string cell, string bank, int nProd, int nPow,
            bool passE, double clip, bool force)
        {
            int bad = 0;
            for (int s = 1; s <= opt.SeedCount; s++)
            {
                string tag = s.ToString("00", CultureInfo.InvariantCulture);
                string src = Path.Combine(srcNoise, "seed_" + tag);
                string dst = Path.Combine(cell, "seed_" + tag);
                Directory.CreateDirectory(dst);
                string done = Path.Combine(dst, "pred_test.csv");
                if (force)
                    ClearSeed(dst);
                else if (File.Exists(done) && File.Exists(Path.Combine(dst, "pred_grid.csv")) && File.Exists(Path.Combine(dst, "exported_equation.txt")))
                {
                    if (File.Exists(Path.Combine(dst, "diverged.txt"))) bad++;
                    continue;
                }
                if (!File.Exists(Path.Combine(src, "train.csv")))
                {
                    Console.WriteLine("missing " + src);
                    bad++;
                    continue;
                }
                double[][] trX, trY, vaX, vaY, teX, teY;
                ReadXy(Path.Combine(src, "train.csv"), out trX, out trY);
                ReadXy(Path.Combine(src, "validation.csv"), out vaX, out vaY);
                ReadXy(Path.Combine(src, "test.csv"), out teX, out teY);
                bool diverged = TrainLaw(trX, trY, vaX, vaY, teX, teY, s, opt, dst, nProd, nPow, passE, clip);
                if (diverged) bad++;
                Console.WriteLine(bank + " " + Path.GetFileName(Path.GetDirectoryName(cell)) + " "
                    + Path.GetFileName(cell) + " seed " + tag + (diverged ? " NONFINITE" : " ok")
                    + (clip > 0 ? " clip" : ""));
            }
            return bad;
        }

        private static bool TrainLaw(
            double[][] trX, double[][] trY, double[][] vaX, double[][] vaY, double[][] teX, double[][] teY,
            int seed, PiOptions opt, string dst, int nProd, int nPow, bool passE, double clip)
        {
            Stopwatch sw = Stopwatch.StartNew();
            DatasetSplit split = new DatasetSplit();
            split.TrainIn = trX; split.TrainOut = trY;
            split.ValIn = vaX; split.ValOut = vaY;
            split.TestIn = teX; split.TestOut = teY;
            VnnBpNet net = MakeNet(opt.Hidden > 0 ? opt.Hidden : 22, seed, nProd, nPow, clip, false);
            TrainDense(net, split, opt, seed);
            double denseVal = net.Evaluate(split.ValIn, split.ValOut).Loss;
            ResearchLogger logger = new ResearchLogger(dst);
            PiExperimentRunner.PruneWithLockedBaseline(net, split, opt.PruneFactor, opt.RetrainCycles, seed, logger, net.eta);
            string passNote = "skipped";
            if (passE)
            {
                double c0, a, b, rmse, r2;
                Exp22Runner.FitDiag(split.TrainIn, split.TrainOut, out c0, out a, out b, out rmse, out r2);
                double allowed = Exp22Runner.FiniteAllowed(denseVal, 1.25);
                if (Exp22Runner.TryInstallDiag2D(net, split, c0, a, b, allowed, out passNote))
                    passNote = "installed " + passNote;
                else
                    passNote = "rejected " + passNote;
            }
            File.WriteAllText(Path.Combine(dst, "ols_install.txt"), passNote);
            Finish(net, dst, teX, teY, null, 0, 0, 0, 0, false, sw);
            return File.Exists(Path.Combine(dst, "diverged.txt"));
        }

        private static void RunKepler(PiOptions opt, string root, string outRoot, string bank, int nProd, int nPow)
        {
            string srcRoot = Path.Combine(root, "Results", "Exp23_Kepler");
            string gridPath = Path.Combine(srcRoot, "grid.csv");
            if (!File.Exists(gridPath))
            {
                Console.WriteLine("missing Kepler grid");
                return;
            }
            double[][] gridX, gridY;
            ReadXy(gridPath, out gridX, out gridY);
            string cell = Path.Combine(outRoot, bank, "kepler");
            Directory.CreateDirectory(cell);
            bool rawPower = bank != "product";
            int bad = RunKeplerSeeds(opt, srcRoot, cell, gridX, nProd, nPow, rawPower, 0.0, false);
            string clipPath = Path.Combine(cell, "CLIP.txt");
            if (!File.Exists(clipPath) && bad > 5)
            {
                File.WriteAllText(clipPath, "gradient_clip=0.5\r\nreason=nonfinite " + bad.ToString(CultureInfo.InvariantCulture) + " of " + opt.SeedCount.ToString(CultureInfo.InvariantCulture) + "\r\n");
                Console.WriteLine("CLIP " + bank + " kepler bad=" + bad);
                RunKeplerSeeds(opt, srcRoot, cell, gridX, nProd, nPow, rawPower, 0.5, true);
            }
        }

        private static int RunKeplerSeeds(
            PiOptions opt, string srcRoot, string cell, double[][] grid, int nProd, int nPow,
            bool rawPower, double clip, bool force)
        {
            int bad = 0;
            for (int s = 1; s <= opt.SeedCount; s++)
            {
                string tag = s.ToString("00", CultureInfo.InvariantCulture);
                string src = Path.Combine(srcRoot, "seed_" + tag);
                string dst = Path.Combine(cell, "seed_" + tag);
                Directory.CreateDirectory(dst);
                if (force) ClearSeed(dst);
                else if (File.Exists(Path.Combine(dst, "pred_test.csv")) && File.Exists(Path.Combine(dst, "pred_grid.csv")))
                {
                    if (File.Exists(Path.Combine(dst, "diverged.txt"))) bad++;
                    continue;
                }
                bool diverged = TrainKepler(src, dst, grid, s, opt, nProd, nPow, rawPower, clip);
                if (diverged) bad++;
                Console.WriteLine("kepler " + Path.GetFileName(Path.GetDirectoryName(cell)) + " seed " + tag
                    + (diverged ? " NONFINITE" : " ok") + (clip > 0 ? " clip" : ""));
            }
            return bad;
        }

        private static bool TrainKepler(
            string src, string dst, double[][] grid, int seed, PiOptions opt,
            int nProd, int nPow, bool rawPower, double clip)
        {
            Stopwatch sw = Stopwatch.StartNew();
            double[][] trX, trY, vaX, vaY, teX, teY;
            ReadXy(Path.Combine(src, "train.csv"), out trX, out trY);
            ReadXy(Path.Combine(src, "validation.csv"), out vaX, out vaY);
            ReadXy(Path.Combine(src, "test.csv"), out teX, out teY);
            DatasetSplit split = new DatasetSplit();
            double meanA = 0, stdA = 1, meanP = 0, stdP = 1;
            if (rawPower)
            {
                split.TrainIn = ScaleFixed(trX, KeplerADiv);
                split.ValIn = ScaleFixed(vaX, KeplerADiv);
                split.TestIn = ScaleFixed(teX, KeplerADiv);
                split.TrainOut = ScaleFixedY(trY, KeplerPDiv);
                split.ValOut = ScaleFixedY(vaY, KeplerPDiv);
                split.TestOut = ScaleFixedY(teY, KeplerPDiv);
                File.WriteAllText(Path.Combine(dst, "scale.txt"),
                    "mode=fixed\nADiv=40\nPDiv=100\npowerInputGain=40\npowerOutputDiv=100\n");
            }
            else
            {
                MeanStd(trX, trY, out meanA, out stdA, out meanP, out stdP);
                split.TrainIn = ScaleZ(trX, meanA, stdA);
                split.ValIn = ScaleZ(vaX, meanA, stdA);
                split.TestIn = ScaleZ(teX, meanA, stdA);
                split.TrainOut = ScaleZy(trY, meanP, stdP);
                split.ValOut = ScaleZy(vaY, meanP, stdP);
                split.TestOut = ScaleZy(teY, meanP, stdP);
                File.WriteAllText(Path.Combine(dst, "scale.txt"),
                    string.Format(CultureInfo.InvariantCulture, "mode=zscore\nmeanA={0:G15}\nstdA={1:G15}\nmeanP={2:G15}\nstdP={3:G15}\n",
                        meanA, stdA, meanP, stdP));
            }
            VnnBpNet net = MakeNet(12, seed, nProd, nPow, clip, true);
            if (rawPower)
            {
                net.powerInputGain = KeplerADiv;
                net.powerOutputDiv = KeplerPDiv;
                PiNetworkBuilder.SetEdgesFromFunno(net, 9, 1.0);
            }
            int retrain = opt.RetrainCycles;
            TrainDense(net, split, opt, seed);
            ResearchLogger logger = new ResearchLogger(dst);
            PiExperimentRunner.PruneWithLockedBaseline(net, split, opt.PruneFactor, retrain, seed, logger, net.eta);
            File.WriteAllText(Path.Combine(dst, "ols_install.txt"), "skipped: Kepler is not a diagonal square");
            Finish(net, dst, teX, teY, grid, meanA, stdA, meanP, stdP, rawPower, sw);
            return File.Exists(Path.Combine(dst, "diverged.txt"));
        }

        private static VnnBpNet MakeNet(int H, int seed, int nProd, int nPow, double clip, bool kepler)
        {
            int lin, sig, q4, q7;
            if (kepler)
            {
                lin = 3; sig = 3; q4 = 2; q7 = 4;
            }
            else
            {
                lin = Math.Max(2, H / 6);
                sig = Math.Max(2, H / 6);
                q4 = Math.Max(2, H / 6);
                q7 = H - lin - sig - q4;
                if (q7 < 4)
                {
                    q7 = 4;
                    int rest = H - q7;
                    lin = rest / 3;
                    sig = rest / 3;
                    q4 = rest - lin - sig;
                }
            }
            VnnBpNet net = PiNetworkBuilder.Create2DOperatorBank("Exp25", H, lin, sig, q4, q7, seed, false);
            PiNetworkBuilder.InstallOperatorExtension(net, nProd, nPow);
            net.lossType = LossType.MeanSquaredError;
            net.eta = 0.05;
            net.etab = 0.05;
            net.gradClip = clip;
            return net;
        }

        private static void TrainDense(VnnBpNet net, DatasetSplit split, PiOptions opt, int seed)
        {
            TrainingOptions tr = new TrainingOptions();
            tr.LearningRate = net.eta;
            tr.LossType = LossType.MeanSquaredError;
            tr.ShuffleEachEpoch = true;
            for (int e = 0; e < opt.DenseEpochs; e++)
            {
                tr.RandomSeed = seed * 1000 + e;
                net.TrainEpoch(split.TrainIn, split.TrainOut, tr);
            }
        }

        private static void Finish(
            VnnBpNet net, string dst, double[][] teX, double[][] teY, double[][] grid,
            double meanA, double stdA, double meanP, double stdP, bool rawPower, Stopwatch sw)
        {
            string eq = net.ExportEquation();
            File.WriteAllText(Path.Combine(dst, "exported_equation.txt"), eq);
            double fid = Fidelity(net, teX, teY, rawPower, meanA, stdA);
            File.WriteAllText(Path.Combine(dst, "fidelity.txt"), fid.ToString("G6", CultureInfo.InvariantCulture));
            bool bad = !NnMath.IsFinite(fid) || fid > 1e-6;
            if (grid == null)
                bad = WriteLawPred(net, dst, teX, teY) || bad;
            else
                bad = WriteKeplerPred(net, dst, teX, teY, grid, meanA, stdA, meanP, stdP, rawPower) || bad;
            bad = bad || WeightsBad(net);
            if (bad)
                File.WriteAllText(Path.Combine(dst, "diverged.txt"), "1\r\n");
            else if (File.Exists(Path.Combine(dst, "diverged.txt")))
                File.Delete(Path.Combine(dst, "diverged.txt"));
            sw.Stop();
            File.WriteAllText(Path.Combine(dst, "runtime.txt"),
                (sw.ElapsedMilliseconds / 1000.0).ToString("G", CultureInfo.InvariantCulture));
        }

        private static double Fidelity(VnnBpNet net, double[][] rawX, double[][] rawY, bool rawPower, double meanA, double stdA)
        {
            double max = 0.0;
            int n = rawX.Length < 32 ? rawX.Length : 32;
            for (int s = 0; s < n; s++)
            {
                double x1 = rawX[s][0];
                double x2 = rawX.Length > 0 && rawX[s].Length > 1 ? rawX[s][1] : 0.0;
                if (rawPower)
                {
                    net.inputs[0].value = x1 / KeplerADiv;
                    if (net.inputs.Length > 1) net.inputs[1].value = 0.0;
                }
                else if (stdA > 0 && PathUsesZ(net))
                {
                    net.inputs[0].value = (x1 - meanA) / stdA;
                    if (net.inputs.Length > 1) net.inputs[1].value = 0.0;
                }
                else
                {
                    net.inputs[0].value = x1;
                    if (net.inputs.Length > 1) net.inputs[1].value = x2;
                }
                net.UpdateNet();
                for (int i = 0; i < net.maxNeurons; i++)
                {
                    if (net.neuron[i].funno == 8)
                    {
                        int a, b;
                        net.ProductSources(i, out a, out b);
                        double expect = (a >= 0 && b >= 0) ? net.neuron[a].nuOut * net.neuron[b].nuOut : 0.0;
                        double d = Math.Abs(net.neuron[i].nuOut - expect);
                        if (d > max) max = d;
                    }
                    else if (net.neuron[i].funno == 9)
                    {
                        int src, connK;
                        double k;
                        net.PowerSource(i, out src, out k, out connK);
                        double aval = src >= 0 ? net.neuron[src].nuOut : 0.0;
                        double dpk, dpa;
                        double expect = NnMath.PowerSigned(aval, k, net.powerInputGain, net.powerOutputDiv, out dpk, out dpa);
                        double d = Math.Abs(net.neuron[i].nuOut - expect);
                        if (d > max) max = d;
                    }
                }
            }
            return max;
        }

        private static bool PathUsesZ(VnnBpNet net)
        {
            return false;
        }

        private static bool WriteLawPred(VnnBpNet net, string dst, double[][] teX, double[][] teY)
        {
            bool bad = false;
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("x1,x2,y,yhat");
            for (int i = 0; i < teX.Length; i++)
            {
                double yhat = Predict(net, teX[i][0], teX[i][1]);
                if (!NnMath.IsFinite(yhat)) bad = true;
                sb.AppendFormat(CultureInfo.InvariantCulture, "{0:G15},{1:G15},{2:G15},{3:G15}\r\n",
                    teX[i][0], teX[i][1], teY[i][0], yhat);
            }
            File.WriteAllText(Path.Combine(dst, "pred_test.csv"), sb.ToString());
            sb.Length = 0;
            sb.AppendLine("x1,x2,yhat");
            const int n = 21;
            for (int i = 0; i < n; i++)
            {
                double x1 = -1.0 + 2.0 * i / 20.0;
                for (int j = 0; j < n; j++)
                {
                    double x2 = -1.0 + 2.0 * j / 20.0;
                    double yhat = Predict(net, x1, x2);
                    if (!NnMath.IsFinite(yhat)) bad = true;
                    sb.AppendFormat(CultureInfo.InvariantCulture, "{0:G15},{1:G15},{2:G15}\r\n", x1, x2, yhat);
                }
            }
            File.WriteAllText(Path.Combine(dst, "pred_grid.csv"), sb.ToString());
            return bad;
        }

        private static bool WriteKeplerPred(
            VnnBpNet net, string dst, double[][] teX, double[][] teY, double[][] grid,
            double meanA, double stdA, double meanP, double stdP, bool rawPower)
        {
            bool bad = false;
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("x1,x2,y,yhat");
            for (int i = 0; i < teX.Length; i++)
            {
                double yhat = PredictYears(net, teX[i][0], meanA, stdA, meanP, stdP, rawPower);
                if (!NnMath.IsFinite(yhat)) bad = true;
                sb.AppendFormat(CultureInfo.InvariantCulture, "{0:G15},0,{1:G15},{2:G15}\r\n", teX[i][0], teY[i][0], yhat);
            }
            File.WriteAllText(Path.Combine(dst, "pred_test.csv"), sb.ToString());
            sb.Length = 0;
            sb.AppendLine("x1,x2,yhat");
            for (int i = 0; i < grid.Length; i++)
            {
                double yhat = PredictYears(net, grid[i][0], meanA, stdA, meanP, stdP, rawPower);
                if (!NnMath.IsFinite(yhat)) bad = true;
                sb.AppendFormat(CultureInfo.InvariantCulture, "{0:G15},0,{1:G15}\r\n", grid[i][0], yhat);
            }
            File.WriteAllText(Path.Combine(dst, "pred_grid.csv"), sb.ToString());
            return bad;
        }

        private static double Predict(VnnBpNet net, double x1, double x2)
        {
            net.inputs[0].value = x1;
            if (net.inputs.Length > 1) net.inputs[1].value = x2;
            net.UpdateNet();
            return net.neuron[net.outputs[0].sno].nuOut;
        }

        private static double PredictYears(VnnBpNet net, double a, double meanA, double stdA, double meanP, double stdP, bool rawPower)
        {
            if (rawPower)
                net.inputs[0].value = a / KeplerADiv;
            else
                net.inputs[0].value = (a - meanA) / stdA;
            if (net.inputs.Length > 1) net.inputs[1].value = 0.0;
            net.UpdateNet();
            double z = net.neuron[net.outputs[0].sno].nuOut;
            if (rawPower) return z * KeplerPDiv;
            return z * stdP + meanP;
        }

        private static bool WeightsBad(VnnBpNet net)
        {
            for (int i = 0; i < net.maxNeurons; i++)
                if (!NnMath.IsFinite(net.neuron[i].bias)) return true;
            for (int c = 0; c < net.maxConnections; c++)
                if (!NnMath.IsFinite(net.conn[c].wgt)) return true;
            return false;
        }

        private static void ClearSeed(string dst)
        {
            string[] names = new string[] {
                "pred_test.csv", "pred_grid.csv", "exported_equation.txt", "fidelity.txt",
                "diverged.txt", "runtime.txt", "ols_install.txt", "scale.txt"
            };
            for (int i = 0; i < names.Length; i++)
            {
                string p = Path.Combine(dst, names[i]);
                if (File.Exists(p)) File.Delete(p);
            }
        }

        private static double[][] ScaleFixed(double[][] x, double div)
        {
            double[][] z = new double[x.Length][];
            for (int i = 0; i < x.Length; i++)
                z[i] = new double[] { x[i][0] / div, 0.0 };
            return z;
        }

        private static double[][] ScaleFixedY(double[][] y, double div)
        {
            double[][] z = new double[y.Length][];
            for (int i = 0; i < y.Length; i++)
                z[i] = new double[] { y[i][0] / div };
            return z;
        }

        private static void MeanStd(double[][] x, double[][] y, out double meanA, out double stdA, out double meanP, out double stdP)
        {
            meanA = 0; meanP = 0;
            for (int i = 0; i < x.Length; i++) { meanA += x[i][0]; meanP += y[i][0]; }
            meanA /= x.Length; meanP /= y.Length;
            double va = 0, vp = 0;
            for (int i = 0; i < x.Length; i++)
            {
                double da = x[i][0] - meanA;
                double dp = y[i][0] - meanP;
                va += da * da; vp += dp * dp;
            }
            stdA = Math.Sqrt(va / x.Length);
            stdP = Math.Sqrt(vp / y.Length);
            if (stdA < 1e-8) stdA = 1;
            if (stdP < 1e-8) stdP = 1;
        }

        private static double[][] ScaleZ(double[][] x, double meanA, double stdA)
        {
            double[][] z = new double[x.Length][];
            for (int i = 0; i < x.Length; i++)
                z[i] = new double[] { (x[i][0] - meanA) / stdA, 0.0 };
            return z;
        }

        private static double[][] ScaleZy(double[][] y, double meanP, double stdP)
        {
            double[][] z = new double[y.Length][];
            for (int i = 0; i < y.Length; i++)
                z[i] = new double[] { (y[i][0] - meanP) / stdP };
            return z;
        }

        private static void ReadXy(string path, out double[][] x, out double[][] y)
        {
            string[] lines = File.ReadAllLines(path);
            int n = 0;
            for (int i = 1; i < lines.Length; i++)
                if (lines[i].Trim().Length > 0) n++;
            x = new double[n][];
            y = new double[n][];
            int r = 0;
            for (int i = 1; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (line.Length == 0) continue;
                string[] p = line.Split(',');
                double x1 = double.Parse(p[0], CultureInfo.InvariantCulture);
                double x2 = p.Length > 2 ? double.Parse(p[1], CultureInfo.InvariantCulture) : 0.0;
                double yy = p.Length > 2 ? double.Parse(p[2], CultureInfo.InvariantCulture) : 0.0;
                if (p.Length == 2)
                {
                    x2 = double.Parse(p[1], CultureInfo.InvariantCulture);
                    yy = 0.0;
                }
                x[r] = new double[] { x1, x2 };
                y[r] = new double[] { yy };
                r++;
            }
        }

        private static bool Has(string[] args, string flag)
        {
            if (args == null) return false;
            for (int i = 0; i < args.Length; i++)
                if (args[i] == flag) return true;
            return false;
        }

        private static string ProtocolText()
        {
            return "# Experiment 25 protocol\r\n\r\n"
                + "Operator bank extensions, trained on the Exp22 and Exp23 splits already on disk.\r\n"
                + "Seeds, noise draws, and holdouts are not regenerated. No seed is dropped.\r\n"
                + "Dense epochs 150, prune factor 1.1, retrain 5000 sample-updates, rollback outside the validation strip.\r\n"
                + "Pass E may install a diagonal square only on L1, and only if validation loss stays within 1.25 times the dense baseline.\r\n\r\n"
                + "New units, taken from the trailing square-linear slots so the hidden count stays 22 (laws) or 12 (Kepler):\r\n"
                + "- product (funno 8): p = y_a * y_b of the two live incoming signals. Incoming weights are fixed at 1 on the two inputs and are not trained. The downstream weight is the coefficient. Export text is nA*nB.\r\n"
                + "- power (funno 9): y = sign(a)*|a|^k with k the trained incoming weight. The exponent k*log|a| is clipped to [-8, 8] so the exponential stays finite. Export text contains a^k.\r\n\r\n"
                + "Banks: product (2 product units), power (2 power units), both (2+2). The original bank is not retrained; its rows are copied from Exp22 and Exp23.\r\n\r\n"
                + "Kepler product bank uses the Exp23 train-fold z-score of a and P, and predictions are mapped back to years.\r\n"
                + "Kepler power and both banks use fixed scales chosen before fitting: network input a/40, training target P/100, powerInputGain=40, powerOutputDiv=100, so the power unit emits a^k/100. Predictions are multiplied by 100 to return years. The edge from each power unit to the output starts at 1. These scales are not fit to the exponent.\r\n\r\n"
                + "Gradient clipping is off. If a law-noise cell or the Kepler cell has more than 5 non-finite seeds out of 20, that cell is rerun with a maximum step of 0.5 and CLIP.txt records that the clip was used.\r\n\r\n"
                + "Term recovery uses the Exp22 rules (25% relative, 0.05 absolute, 5% residual) and, for a product law, requires nA*nB in the export. Kepler uses the Exp23 rule: on the fixed log-spaced grid, |exponent-1.5|/1.5 <= 0.05 and |const-1| <= 0.05, predictions positive.\r\n"
                + "A non-finite network stays in the denominator.\r\n";
        }
    }
}
