using System;

namespace VnnBp.VnnBpLib
{
    /// <summary>
    /// Overcomplete 1-H-1 heterogeneous regressor for the Pi experiment.
    /// Default H=22: 6 linear, 8 logistic sigmoid, 8 quadratic sigmoid.
    /// Output is linear (funno 1). Loss is MSE.
    /// </summary>
    public static class PiNetworkBuilder
    {
        public const int DefaultHidden = 22;
        public const int DefaultLinear = 6;
        public const int DefaultSigmoid = 8;
        public const int DefaultQuadratic = 8;

        public static VnnBpNet Create(
            string name,
            int hiddenCount,
            int randomSeed)
        {
            int lin, sig, quad;
            SplitHidden(hiddenCount, out lin, out sig, out quad);
            return Create(name, hiddenCount, lin, sig, quad, randomSeed);
        }

        /// <summary>
        /// Competing operator bank: linear, sigmoid, centred-quadratic-sigmoid, uncentered square-linear.
        /// Default at H=22: 5 + 6 + 5 + 6.
        /// </summary>
        public static VnnBpNet CreateOperatorBank(
            string name,
            int hiddenCount,
            int randomSeed)
        {
            int lin, sig, quadSig, quadLin;
            SplitOperatorBank(hiddenCount, out lin, out sig, out quadSig, out quadLin);
            return CreateOperatorBank(name, hiddenCount, lin, sig, quadSig, quadLin, randomSeed);
        }

        public static VnnBpNet CreateOperatorBank(
            string name,
            int hiddenCount,
            int linearCount,
            int sigmoidCount,
            int quadSigmoidCount,
            int quadLinearCount,
            int randomSeed)
        {
            if (hiddenCount < 1) hiddenCount = DefaultHidden;
            if (linearCount + sigmoidCount + quadSigmoidCount + quadLinearCount != hiddenCount)
                SplitOperatorBank(hiddenCount, out linearCount, out sigmoidCount, out quadSigmoidCount, out quadLinearCount);

            string cfg = string.Format("{0},1,1,Fully Conn,{1}",
                string.IsNullOrEmpty(name) ? "PiNet" : name,
                hiddenCount);
            VnnBpNet net = new VnnBpNet(cfg);
            net.lossType = LossType.MeanSquaredError;
            net.eta = 0.01;
            net.etab = 0.01;

            int hiddenSeen = 0;
            for (int i = 0; i < net.maxNeurons; i++)
            {
                if (net.neuron[i].ihono == NuType.In)
                {
                    net.neuron[i].funno = 1;
                    net.neuron[i].bias = 0;
                }
                else if (net.neuron[i].ihono == NuType.Out)
                {
                    net.neuron[i].funno = 1;
                }
                else
                {
                    if (hiddenSeen < linearCount)
                        net.neuron[i].funno = 1;
                    else if (hiddenSeen < linearCount + sigmoidCount)
                        net.neuron[i].funno = 2;
                    else if (hiddenSeen < linearCount + sigmoidCount + quadSigmoidCount)
                        net.neuron[i].funno = 4;
                    else
                        net.neuron[i].funno = 7;
                    hiddenSeen++;
                }
            }

            ApplyXavierHe(net, randomSeed);
            return net;
        }

        /// <summary>
        /// Exp-02 area discovery bank: still overcomplete and competing, but with a
        /// larger share of uncentered square-linear units so A∝r² can win structurally.
        /// Default at H=22: 3 linear, 3 sigmoid, 4 quadratic-sigmoid, 12 square-linear.
        /// </summary>
        public static VnnBpNet CreateAreaDiscoveryBank(
            string name,
            int hiddenCount,
            int randomSeed)
        {
            int lin, sig, quadSig, quadLin;
            SplitAreaDiscoveryBank(hiddenCount, out lin, out sig, out quadSig, out quadLin);
            return CreateOperatorBank(name, hiddenCount, lin, sig, quadSig, quadLin, randomSeed);
        }

        /// <summary>
        /// Exp-03 circumference discovery bank: overcomplete, but biased toward linear
        /// hidden units so C∝r can win. Default at H=22: 12 linear, 3 sigmoid,
        /// 4 quadratic-sigmoid, 3 square-linear.
        /// </summary>
        public static VnnBpNet CreateCircDiscoveryBank(
            string name,
            int hiddenCount,
            int randomSeed)
        {
            int lin, sig, quadSig, quadLin;
            SplitCircDiscoveryBank(hiddenCount, out lin, out sig, out quadSig, out quadLin);
            return CreateOperatorBank(name, hiddenCount, lin, sig, quadSig, quadLin, randomSeed);
        }

        public static void SplitCircDiscoveryBank(
            int hidden,
            out int linear, out int sigmoid, out int quadSigmoid, out int quadLinear)
        {
            if (hidden <= 0) hidden = DefaultHidden;
            linear = (int)Math.Round(hidden * (12.0 / 22.0));
            sigmoid = (int)Math.Round(hidden * (3.0 / 22.0));
            quadSigmoid = (int)Math.Round(hidden * (4.0 / 22.0));
            if (linear < 2) linear = 2;
            if (sigmoid < 1) sigmoid = 1;
            if (quadSigmoid < 1) quadSigmoid = 1;
            quadLinear = hidden - linear - sigmoid - quadSigmoid;
            if (quadLinear < 1)
            {
                quadLinear = 1;
                int rest = hidden - quadLinear;
                linear = Math.Max(2, rest / 2);
                sigmoid = Math.Max(1, rest / 4);
                quadSigmoid = rest - linear - sigmoid;
                if (quadSigmoid < 0)
                {
                    quadSigmoid = 0;
                    sigmoid = rest - linear;
                }
            }
        }

        /// <summary>
        /// Exp-01 circle bank (2-in-1-out): favour centred quadratic-sigmoid (funno 4).
        /// Default H=22: 4 linear, 6 sigmoid, 10 quad-sigmoid, 2 square-linear.
        /// Output is sigmoid for classification.
        /// </summary>
        public static VnnBpNet CreateCircleDiscoveryBank(
            string name,
            int hiddenCount,
            int randomSeed)
        {
            int lin, sig, quadSig, quadLin;
            SplitCircleDiscoveryBank(hiddenCount, out lin, out sig, out quadSig, out quadLin);
            return Create2DOperatorBank(name, hiddenCount, lin, sig, quadSig, quadLin, randomSeed, true);
        }

        /// <summary>
        /// Dense ordinary baseline for Exp01: no quadratic operators (linear + sigmoid only).
        /// </summary>
        public static VnnBpNet CreateCircleNoQuadBank(
            string name,
            int hiddenCount,
            int randomSeed)
        {
            if (hiddenCount < 1) hiddenCount = DefaultHidden;
            int lin = hiddenCount / 2;
            int sig = hiddenCount - lin;
            if (lin < 1) lin = 1;
            if (sig < 1) { sig = 1; lin = hiddenCount - 1; }
            return Create2DOperatorBank(name, hiddenCount, lin, sig, 0, 0, randomSeed, true);
        }

        public static void SplitCircleDiscoveryBank(
            int hidden,
            out int linear, out int sigmoid, out int quadSigmoid, out int quadLinear)
        {
            if (hidden <= 0) hidden = DefaultHidden;
            linear = (int)Math.Round(hidden * (4.0 / 22.0));
            sigmoid = (int)Math.Round(hidden * (6.0 / 22.0));
            quadSigmoid = (int)Math.Round(hidden * (10.0 / 22.0));
            if (linear < 1) linear = 1;
            if (sigmoid < 1) sigmoid = 1;
            if (quadSigmoid < 2) quadSigmoid = 2;
            quadLinear = hidden - linear - sigmoid - quadSigmoid;
            if (quadLinear < 0)
            {
                quadLinear = 0;
                quadSigmoid = hidden - linear - sigmoid;
            }
        }

        public static VnnBpNet Create2DOperatorBank(
            string name,
            int hiddenCount,
            int linearCount,
            int sigmoidCount,
            int quadSigmoidCount,
            int quadLinearCount,
            int randomSeed,
            bool sigmoidOutput)
        {
            if (hiddenCount < 1) hiddenCount = DefaultHidden;
            int sum = linearCount + sigmoidCount + quadSigmoidCount + quadLinearCount;
            if (sum != hiddenCount)
                SplitCircleDiscoveryBank(hiddenCount, out linearCount, out sigmoidCount, out quadSigmoidCount, out quadLinearCount);

            string cfg = string.Format("{0},2,1,Fully Conn,{1}",
                string.IsNullOrEmpty(name) ? "CircleNet" : name,
                hiddenCount);
            VnnBpNet net = new VnnBpNet(cfg);
            net.lossType = LossType.MeanSquaredError;
            net.eta = 0.05;
            net.etab = 0.05;

            int hiddenSeen = 0;
            for (int i = 0; i < net.maxNeurons; i++)
            {
                if (net.neuron[i].ihono == NuType.In)
                {
                    net.neuron[i].funno = 1;
                    net.neuron[i].bias = 0;
                }
                else if (net.neuron[i].ihono == NuType.Out)
                {
                    net.neuron[i].funno = (short)(sigmoidOutput ? 2 : 1);
                }
                else
                {
                    if (hiddenSeen < linearCount)
                        net.neuron[i].funno = 1;
                    else if (hiddenSeen < linearCount + sigmoidCount)
                        net.neuron[i].funno = 2;
                    else if (hiddenSeen < linearCount + sigmoidCount + quadSigmoidCount)
                        net.neuron[i].funno = 4;
                    else
                        net.neuron[i].funno = 7;
                    hiddenSeen++;
                }
            }

            ApplyXavierHe(net, randomSeed);
            return net;
        }

        /// <summary>
        /// Ablation Model A: linear + sigmoid only (no quadratic operators).
        /// </summary>
        public static VnnBpNet CreateLinSigOnlyBank(string name, int hiddenCount, int randomSeed)
        {
            if (hiddenCount < 1) hiddenCount = DefaultHidden;
            int lin = hiddenCount / 2;
            int sig = hiddenCount - lin;
            if (lin < 1) lin = 1;
            if (sig < 1) { sig = 1; lin = hiddenCount - 1; }
            return CreateOperatorBank(name, hiddenCount, lin, sig, 0, 0, randomSeed);
        }

        /// <summary>
        /// Ablation Model C: square-linear (funno 7) hidden units only.
        /// </summary>
        public static VnnBpNet CreateQuadOnlyBank(string name, int hiddenCount, int randomSeed)
        {
            if (hiddenCount < 1) hiddenCount = DefaultHidden;
            return CreateOperatorBank(name, hiddenCount, 0, 0, 0, hiddenCount, randomSeed);
        }

        public static void SplitAreaDiscoveryBank(
            int hidden,
            out int linear, out int sigmoid, out int quadSigmoid, out int quadLinear)
        {
            if (hidden <= 0) hidden = DefaultHidden;
            linear = (int)Math.Round(hidden * (3.0 / 22.0));
            sigmoid = (int)Math.Round(hidden * (3.0 / 22.0));
            quadSigmoid = (int)Math.Round(hidden * (4.0 / 22.0));
            if (linear < 1) linear = 1;
            if (sigmoid < 1) sigmoid = 1;
            if (quadSigmoid < 1) quadSigmoid = 1;
            quadLinear = hidden - linear - sigmoid - quadSigmoid;
            if (quadLinear < 2)
            {
                quadLinear = Math.Max(2, hidden / 2);
                int rest = hidden - quadLinear;
                linear = Math.Max(1, rest / 3);
                sigmoid = Math.Max(1, rest / 3);
                quadSigmoid = rest - linear - sigmoid;
                if (quadSigmoid < 0)
                {
                    quadSigmoid = 0;
                    sigmoid = rest - linear;
                }
            }
        }

        public static void SplitOperatorBank(
            int hidden,
            out int linear, out int sigmoid, out int quadSigmoid, out int quadLinear)
        {
            if (hidden <= 0) hidden = DefaultHidden;
            linear = (int)Math.Round(hidden * (5.0 / 22.0));
            sigmoid = (int)Math.Round(hidden * (6.0 / 22.0));
            quadSigmoid = (int)Math.Round(hidden * (5.0 / 22.0));
            quadLinear = hidden - linear - sigmoid - quadSigmoid;
            if (linear < 1) linear = 1;
            if (sigmoid < 1) sigmoid = 1;
            if (quadSigmoid < 0) quadSigmoid = 0;
            if (quadLinear < 1) quadLinear = 1;
            int sum = linear + sigmoid + quadSigmoid + quadLinear;
            if (sum != hidden)
                quadLinear = hidden - linear - sigmoid - quadSigmoid;
            if (quadLinear < 0)
            {
                quadLinear = 0;
                quadSigmoid = hidden - linear - sigmoid;
            }
        }

        public static VnnBpNet Create(
            string name,
            int hiddenCount,
            int linearCount,
            int sigmoidCount,
            int quadraticCount,
            int randomSeed)
        {
            if (hiddenCount < 1) hiddenCount = DefaultHidden;
            if (linearCount + sigmoidCount + quadraticCount != hiddenCount)
                SplitHidden(hiddenCount, out linearCount, out sigmoidCount, out quadraticCount);

            string cfg = string.Format("{0},1,1,Fully Conn,{1}",
                string.IsNullOrEmpty(name) ? "PiNet" : name,
                hiddenCount);
            VnnBpNet net = new VnnBpNet(cfg);
            net.lossType = LossType.MeanSquaredError;
            net.eta = 0.01;
            net.etab = 0.01;

            int hiddenSeen = 0;
            for (int i = 0; i < net.maxNeurons; i++)
            {
                if (net.neuron[i].ihono == NuType.In)
                {
                    net.neuron[i].funno = 1;
                    net.neuron[i].bias = 0;
                }
                else if (net.neuron[i].ihono == NuType.Out)
                {
                    net.neuron[i].funno = 1; // linear regression output
                }
                else
                {
                    if (hiddenSeen < linearCount)
                        net.neuron[i].funno = 1;
                    else if (hiddenSeen < linearCount + sigmoidCount)
                        net.neuron[i].funno = 2;
                    else
                        net.neuron[i].funno = 4;
                    hiddenSeen++;
                }
            }

            ApplyXavierHe(net, randomSeed);
            return net;
        }

        /// <summary>
        /// Uniform Xavier init. Box-Muller is avoided (it uses 2π).
        /// </summary>
        public static void ApplyXavierHe(VnnBpNet net, int randomSeed)
        {
            Random rnd = new Random(randomSeed);
            int[] fanIn = new int[net.maxNeurons];
            int[] fanOut = new int[net.maxNeurons];
            for (int c = 0; c < net.maxConnections; c++)
            {
                fanOut[net.conn[c].srcNuNo]++;
                fanIn[net.conn[c].dstNuNo]++;
            }
            for (int n = 0; n < net.maxNeurons; n++)
            {
                if (net.neuron[n].ihono == NuType.In)
                    net.neuron[n].bias = 0;
                else
                {
                    int f = Math.Max(1, fanIn[n] + fanOut[n]);
                    double a = Math.Sqrt(6.0 / f);
                    net.neuron[n].bias = (rnd.NextDouble() * 2.0 - 1.0) * a;
                }
            }
            for (int c = 0; c < net.maxConnections; c++)
            {
                int src = net.conn[c].srcNuNo;
                int dst = net.conn[c].dstNuNo;
                int f = Math.Max(1, fanIn[dst] + fanOut[src]);
                double a = Math.Sqrt(6.0 / f);
                double w = (rnd.NextDouble() * 2.0 - 1.0) * a;
                net.conn[c].wgt = w;
            }
        }

        public static void SplitHidden(int hidden, out int linear, out int sigmoid, out int quadratic)
        {
            if (hidden <= 0) hidden = DefaultHidden;
            // Preserve the 6:8:8 mix when hidden == 22; otherwise scale.
            linear = (int)Math.Round(hidden * (6.0 / 22.0));
            sigmoid = (int)Math.Round(hidden * (8.0 / 22.0));
            quadratic = hidden - linear - sigmoid;
            if (linear < 1) { linear = 1; quadratic = hidden - linear - sigmoid; }
            if (sigmoid < 1) { sigmoid = 1; quadratic = hidden - linear - sigmoid; }
            if (quadratic < 1) { quadratic = 1; sigmoid = hidden - linear - quadratic; }
            if (linear + sigmoid + quadratic != hidden)
                quadratic = hidden - linear - sigmoid;
        }

        public static int CountHiddenOfType(VnnBpNet net, short funno)
        {
            int n = 0;
            for (int i = 0; i < net.maxNeurons; i++)
            {
                if (net.neuron[i].ihono == NuType.Hdn && net.neuron[i].funno == funno)
                    n++;
            }
            return n;
        }
    }
}
