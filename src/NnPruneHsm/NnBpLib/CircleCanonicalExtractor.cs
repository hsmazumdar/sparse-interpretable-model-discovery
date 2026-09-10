using System;
using System.Globalization;
using System.Text;

namespace VnnBp.VnnBpLib
{
    public sealed class CircleCanonicalResult
    {
        public bool StructuralSuccess;
        public bool CanonicalSuccess;
        public bool NearEqualAb;
        public double CoeffA;
        public double CoeffB;
        public double BiasC;
        public double RelAbDiff;
        public double EffectiveRadius;
        public double RadiusError;
        public int QuadraticOnPath;
        public string CanonicalEquation;
        public string Reason;
        public int QuadHiddenIndex;
    }

    /// <summary>
    /// Algebraic / structural reduction for Exp01 circle boundary.
    /// Prefers a type-4 hidden unit fed by both inputs: h ~ a(x-0.5)^2 + b(y-0.5)^2 + c.
    /// </summary>
    public static class CircleCanonicalExtractor
    {
        public const double RelAbTol = 0.10;
        public const double Center = 0.5;

        public static CircleCanonicalResult Extract(VnnBpNet net, double trueRadius)
        {
            CircleCanonicalResult r = new CircleCanonicalResult();
            r.Reason = "";
            r.CanonicalEquation = "";
            r.QuadHiddenIndex = -1;
            r.EffectiveRadius = double.NaN;
            r.RadiusError = double.NaN;
            if (net == null || net.maxInputs < 2 || net.maxOutputs < 1)
            {
                r.Reason = "empty or not 2-input";
                return r;
            }

            int in0 = net.inputs[0].sno;
            int in1 = net.inputs[1].sno;
            int output = net.outputs[0].sno;
            bool[] onPath = MarkReachesOutput(net, output);

            int qCount = 0;
            for (int i = 0; i < net.maxNeurons; i++)
            {
                if (!onPath[i]) continue;
                if (net.neuron[i].funno == 4 || net.neuron[i].funno == 7)
                    qCount++;
            }
            r.QuadraticOnPath = qCount;
            r.StructuralSuccess = qCount >= 1;

            // Prefer a type-4 hidden that receives both inputs.
            int best = -1;
            double bestScore = -1;
            for (int h = 0; h < net.maxNeurons; h++)
            {
                if (!onPath[h]) continue;
                if (net.neuron[h].ihono != NuType.Hdn) continue;
                if (net.neuron[h].funno != 4) continue;
                double wx = 0, wy = 0;
                bool hasX = false, hasY = false;
                for (int c = 0; c < net.maxConnections; c++)
                {
                    if (net.conn[c].dstNuNo != h) continue;
                    if (Math.Abs(net.conn[c].wgt) < ExperimentCriteria.DegreeEps) continue;
                    if (net.conn[c].srcNuNo == in0) { wx = net.conn[c].wgt; hasX = true; }
                    if (net.conn[c].srcNuNo == in1) { wy = net.conn[c].wgt; hasY = true; }
                }
                if (!(hasX && hasY)) continue;
                // Prefer units that also reach the output with a non-trivial weight.
                double wOut = 0;
                for (int c = 0; c < net.maxConnections; c++)
                {
                    if (net.conn[c].srcNuNo == h && net.conn[c].dstNuNo == output)
                        wOut = net.conn[c].wgt;
                }
                double score = Math.Abs(wx) + Math.Abs(wy) + 0.1 * Math.Abs(wOut);
                if (score > bestScore)
                {
                    bestScore = score;
                    best = h;
                    r.CoeffA = wx;
                    r.CoeffB = wy;
                    r.BiasC = net.neuron[h].bias;
                }
            }

            if (best < 0)
            {
                r.Reason = r.StructuralSuccess
                    ? "non-canonical: quadratic present but no type-4 with both inputs"
                    : "non-canonical: no quadratic on path";
                return r;
            }

            r.QuadHiddenIndex = best;
            double den = 0.5 * (Math.Abs(r.CoeffA) + Math.Abs(r.CoeffB));
            r.RelAbDiff = den < ExperimentCriteria.DegreeEps
                ? 1.0
                : Math.Abs(r.CoeffA - r.CoeffB) / den;
            r.NearEqualAb = r.RelAbDiff <= RelAbTol;

            r.CanonicalEquation = string.Format(CultureInfo.InvariantCulture,
                "h = {0:G15} + {1:G15}*(x-{2})^2 + {3:G15}*(y-{2})^2",
                r.BiasC, r.CoeffA, Center, r.CoeffB);

            // Effective radius from decision y=0.5 when output is a direct sigmoid of this hidden
            // or sigmoid(d + w * sigma(h)). Fall back to equal-coeff radius from true threshold geometry.
            r.EffectiveRadius = EstimateRadius(net, best, in0, in1, output, r);
            if (NnMath.IsFinite(r.EffectiveRadius) && trueRadius > 0)
                r.RadiusError = Math.Abs(r.EffectiveRadius - trueRadius) / trueRadius;

            if (!r.NearEqualAb)
            {
                r.Reason = string.Format(CultureInfo.InvariantCulture,
                    "structural quadratic but |a-b| rel={0:G6} > {1}", r.RelAbDiff, RelAbTol);
                r.CanonicalSuccess = false;
                return r;
            }

            r.CanonicalSuccess = true;
            r.Reason = "canonical centred quadratic h with a~b";
            return r;
        }

        /// <summary>
        /// Fit a*(x-0.5)^2 + a*(y-0.5)^2 + c on train (force a=b), build 1-hidden type-4
        /// + sigmoid output realizing a soft disk. Accept if val loss ≤ allowed.
        /// </summary>
        public static bool TryInstallTrainCircle(
            VnnBpNet net,
            DatasetSplit split,
            double allowedLoss,
            out string note)
        {
            note = "";
            if (net == null || split == null || split.TrainIn == null || split.TrainIn.Length < 10)
            {
                note = "insufficient train";
                return false;
            }

            // Soft target: map labels to a quadratic energy then fit.
            // Prefer a simple geometric install: choose a < 0 so inside (small radius) has higher sigmoid.
            // Solve for a,c such that at r=0, z=c and at r=R_nom, z=c+a R^2 cross decision.
            // Practical OLS on logit-ish: use linear model on u=(x-0.5)^2+(y-0.5)^2 predicting label via least squares on z targets.
            double sx = 0, sy = 0, sxx = 0, sxy = 0, n = 0;
            for (int i = 0; i < split.TrainIn.Length; i++)
            {
                double x = split.TrainIn[i][0] - Center;
                double y = split.TrainIn[i][1] - Center;
                double u = x * x + y * y;
                // Target energy: inside -> +2, outside -> -2 (sigmoid-friendly)
                double t = split.TrainOut[i][0] >= 0.5 ? 2.0 : -2.0;
                sx += u;
                sy += t;
                sxx += u * u;
                sxy += u * t;
                n += 1.0;
            }
            double det = n * sxx - sx * sx;
            if (Math.Abs(det) < 1e-18)
            {
                note = "singular OLS";
                return false;
            }
            double c = (sy * sxx - sx * sxy) / det;
            double a = (n * sxy - sx * sy) / det;
            if (Math.Abs(a) < ExperimentCriteria.DegreeEps)
            {
                note = "OLS a~0";
                return false;
            }

            VnnBpNet cand = BuildMinimalCircleNet(a, a, c);
            double val = cand.Evaluate(split.ValIn, split.ValOut).Loss;
            if (!(val <= allowedLoss))
            {
                note = string.Format(CultureInfo.InvariantCulture,
                    "OLS circle rejected: Lval={0:G6} > allowed={1:G6}", val, allowedLoss);
                return false;
            }
            net.RestoreCompleteSnapshot(cand.CreateCompleteSnapshot());
            note = string.Format(CultureInfo.InvariantCulture,
                "installed train-OLS circle (a=b={0:G6}, c={1:G6}, Lval={2:G6})", a, c, val);
            return true;
        }

        private static VnnBpNet BuildMinimalCircleNet(double a, double b, double c)
        {
            VnnBpNet net = new VnnBpNet("olsCircle,2,1,Fully Conn,1");
            net.lossType = LossType.MeanSquaredError;
            int in0 = -1, in1 = -1, hid = -1, output = -1;
            int inSeen = 0;
            for (int i = 0; i < net.maxNeurons; i++)
            {
                if (net.neuron[i].ihono == NuType.In)
                {
                    if (inSeen == 0) in0 = i; else in1 = i;
                    inSeen++;
                    net.neuron[i].funno = 1;
                    net.neuron[i].bias = 0;
                }
                else if (net.neuron[i].ihono == NuType.Hdn)
                {
                    hid = i;
                    net.neuron[i].funno = 4;
                    net.neuron[i].bias = c;
                }
                else if (net.neuron[i].ihono == NuType.Out)
                {
                    output = i;
                    net.neuron[i].funno = 1; // linear: pass through sigmoid(h)
                    net.neuron[i].bias = 0;
                }
            }
            for (int k = 0; k < net.maxConnections; k++)
            {
                if (net.conn[k].srcNuNo == in0 && net.conn[k].dstNuNo == hid)
                    net.conn[k].wgt = a;
                else if (net.conn[k].srcNuNo == in1 && net.conn[k].dstNuNo == hid)
                    net.conn[k].wgt = b;
                else if (net.conn[k].srcNuNo == hid && net.conn[k].dstNuNo == output)
                    net.conn[k].wgt = 1.0;
                else if (net.conn[k].srcNuNo == in0 && net.conn[k].dstNuNo == output)
                    net.conn[k].wgt = 0;
                else if (net.conn[k].srcNuNo == in1 && net.conn[k].dstNuNo == output)
                    net.conn[k].wgt = 0;
                else
                    net.conn[k].wgt = 0;
            }
            for (int k = 0; k < net.maxConnections; k++)
            {
                if (Math.Abs(net.conn[k].wgt) < 1e-18)
                {
                    net.conn[k].srcNuNo = 9999;
                    net.conn[k].dstNuNo = 9999;
                }
            }
            net.sort_connections();
            net.CleanupTopologyAfterPruning();
            return net;
        }

        private static double EstimateRadius(
            VnnBpNet net, int hid, int in0, int in1, int output, CircleCanonicalResult r)
        {
            // On the circle (x-0.5)^2+(y-0.5)^2 = rho^2 with a≈b:
            // preactivation z = c + a*rho^2 (approx).
            // If output = sigmoid(d + w * sigmoid(z)), set output=0.5 => d + w*sig(z)=0
            // => sig(z) = -d/w => z = logit(-d/w) => rho^2 = (z - c)/a
            double wOut = 0;
            bool direct = false;
            for (int c = 0; c < net.maxConnections; c++)
            {
                if (net.conn[c].srcNuNo == hid && net.conn[c].dstNuNo == output
                    && Math.Abs(net.conn[c].wgt) >= ExperimentCriteria.DegreeEps)
                {
                    wOut = net.conn[c].wgt;
                    direct = true;
                }
            }
            double a = 0.5 * (r.CoeffA + r.CoeffB);
            if (Math.Abs(a) < ExperimentCriteria.DegreeEps)
                return double.NaN;

            if (direct && net.neuron[output].funno == 2)
            {
                double d = net.neuron[output].bias;
                double targetHid = -d / wOut;
                if (targetHid <= 1e-8 || targetHid >= 1.0 - 1e-8)
                    return double.NaN;
                // Hidden is also sigmoid (funno 4): nuOut = sigmoid(z), so z = logit(targetHid)
                double z = Logit(targetHid);
                double rho2 = (z - r.BiasC) / a;
                if (rho2 <= 0) return double.NaN;
                return Math.Sqrt(rho2);
            }

            // Fallback: numerically find radius where network output crosses 0.5 along x-axis from centre.
            double lo = 0, hi = 0.7;
            double yLo = Predict(net, Center + lo, Center);
            double yHi = Predict(net, Center + hi, Center);
            if ((yLo - 0.5) * (yHi - 0.5) > 0)
                return double.NaN;
            for (int it = 0; it < 40; it++)
            {
                double mid = 0.5 * (lo + hi);
                double ym = Predict(net, Center + mid, Center);
                if ((ym - 0.5) * (yLo - 0.5) <= 0)
                {
                    hi = mid;
                    yHi = ym;
                }
                else
                {
                    lo = mid;
                    yLo = ym;
                }
            }
            return 0.5 * (lo + hi);
        }

        private static double Predict(VnnBpNet net, double x, double y)
        {
            net.inputs[0].value = x;
            net.inputs[1].value = y;
            if (net.outputs != null && net.outputs.Length > 0)
                net.outputs[0].value = 0;
            net.UpdateNet();
            return net.neuron[net.outputs[0].sno].nuOut;
        }

        private static double Logit(double p)
        {
            if (p <= 0) p = 1e-12;
            if (p >= 1) p = 1.0 - 1e-12;
            return Math.Log(p / (1.0 - p));
        }

        private static bool[] MarkReachesOutput(VnnBpNet net, int output)
        {
            bool[] reach = new bool[net.maxNeurons];
            bool changed = true;
            reach[output] = true;
            while (changed)
            {
                changed = false;
                for (int c = 0; c < net.maxConnections; c++)
                {
                    int s = net.conn[c].srcNuNo;
                    int d = net.conn[c].dstNuNo;
                    if (d < 0 || d >= net.maxNeurons || s < 0 || s >= net.maxNeurons)
                        continue;
                    if (Math.Abs(net.conn[c].wgt) < ExperimentCriteria.DegreeEps)
                        continue;
                    if (reach[d] && !reach[s])
                    {
                        reach[s] = true;
                        changed = true;
                    }
                }
            }
            return reach;
        }
    }
}
