using System;
using System.Globalization;
using System.Text;

namespace VnnBp.VnnBpLib
{
    /// <summary>
    /// Pre-registered tolerances for Exp 02. Do not tune these toward π after seeing results.
    /// </summary>
    public static class ExperimentCriteria
    {
        public const double LinearContaminationTol = 0.05;
        public const double InterceptRelTol = 0.05;
        public const double FidelityMaxTol = 1e-8;
        public const double DegreeEps = 1e-12;
    }

    public sealed class CanonicalResult
    {
        public bool StructuralSuccess;
        public bool CanonicalSuccess;
        public bool NearZeroIntercept;
        public bool HasSigmoidOnPath;
        public string Reason;
        public double C0;
        public double C1;
        public double C2;
        public double InterceptB;
        public double SlopeK;
        public double LinearContamination;
        public int SquareLinearOnPath;
        public int QuadraticSigmoidOnPath;
        public string CanonicalEquation;
        public string PolynomialScaled;
    }

    /// <summary>
    /// Algebraic reduction of a pruned 1-D network to A = b + k r^2 or C = b + k r.
    /// Sigmoid units on the output path make the run non-canonical (unless inactive).
    /// </summary>
    public static class CanonicalExtractor
    {
        /// <summary>
        /// Exp03: reduce to C = b + k r when possible. Quadratic contamination must be small.
        /// </summary>
        public static CanonicalResult ExtractCircumference(VnnBpNet net, double rMax, double yScale)
        {
            CanonicalResult r = new CanonicalResult();
            r.Reason = "";
            r.CanonicalEquation = "";
            r.PolynomialScaled = "";
            if (net == null || net.maxOutputs < 1 || net.maxInputs < 1)
            {
                r.Reason = "empty network";
                return r;
            }

            int output = net.outputs[0].sno;
            int input = net.inputs[0].sno;
            bool[] onPath = MarkReachesOutput(net, output);

            int linHid = 0, sqLin = 0, qSig = 0;
            bool sigmoid = false;
            for (int i = 0; i < net.maxNeurons; i++)
            {
                if (!onPath[i]) continue;
                short f = net.neuron[i].funno;
                if (net.neuron[i].ihono == NuType.Hdn && f == 1) linHid++;
                if (f == 7) sqLin++;
                if (f == 4) qSig++;
                if (NnMath.IsSigmoidActivation(f) && net.neuron[i].ihono != NuType.In)
                    sigmoid = true;
            }
            r.SquareLinearOnPath = sqLin;
            r.QuadraticSigmoidOnPath = qSig;
            r.HasSigmoidOnPath = sigmoid;
            // Structural: a linear-in-r path can exist via hidden linear units or direct I->O.
            r.StructuralSuccess = linHid >= 1 || HasDirectInputOutputEdge(net, input, output);

            double[] poly;
            string fail;
            if (sigmoid)
            {
                if (!TryPolyIgnoringSigmoid(net, output, input, onPath, out poly, out fail))
                {
                    r.Reason = "non-canonical: sigmoid on path; " + fail;
                    return r;
                }
                if (!PolynomialMatchesNetwork(net, poly, out fail))
                {
                    r.Reason = "non-canonical: sigmoid on path and active (" + fail + ")";
                    return r;
                }
                FinishCanonicalCirc(r, poly, rMax, yScale, "canonical C = b + k r (sigmoid path numerically inactive)");
                return r;
            }

            if (!TryPoly(net, output, input, onPath, new int[net.maxNeurons], out poly, out fail))
            {
                r.Reason = "non-canonical: " + fail;
                return r;
            }

            FinishCanonicalCirc(r, poly, rMax, yScale, "canonical C = b + k r");
            return r;
        }

        private static bool HasDirectInputOutputEdge(VnnBpNet net, int input, int output)
        {
            for (int c = 0; c < net.maxConnections; c++)
            {
                if (net.conn[c].srcNuNo == input && net.conn[c].dstNuNo == output
                    && Math.Abs(net.conn[c].wgt) >= ExperimentCriteria.DegreeEps)
                    return true;
            }
            return false;
        }

        private static void FinishCanonicalCirc(
            CanonicalResult r, double[] poly, double rMax, double yScale, string okReason)
        {
            if (poly == null || poly.Length < 3
                || !NnMath.IsFinite(poly[0]) || !NnMath.IsFinite(poly[1]) || !NnMath.IsFinite(poly[2]))
            {
                r.Reason = "non-canonical: non-finite polynomial coefficients";
                r.CanonicalSuccess = false;
                return;
            }
            r.C0 = poly[0];
            r.C1 = poly[1];
            r.C2 = poly[2];
            r.PolynomialScaled = string.Format(CultureInfo.InvariantCulture,
                "n_out = {0:G15} + {1:G15}*r_s + {2:G15}*r_s^2", poly[0], poly[1], poly[2]);

            if (rMax <= 0) rMax = 1;
            if (yScale <= 0) yScale = 1;
            r.InterceptB = yScale * poly[0];
            r.SlopeK = yScale * poly[1] / rMax;
            if (!NnMath.IsFinite(r.InterceptB) || !NnMath.IsFinite(r.SlopeK))
            {
                r.Reason = "non-canonical: non-finite physical coefficients";
                r.CanonicalSuccess = false;
                return;
            }
            double den = Math.Abs(poly[1]);
            // For circ, "linear contamination" field stores quadratic contamination |c2|/|c1|.
            r.LinearContamination = den < ExperimentCriteria.DegreeEps
                ? (Math.Abs(poly[2]) < ExperimentCriteria.DegreeEps ? 0.0 : 1.0)
                : Math.Abs(poly[2]) / den;

            if (Math.Abs(poly[1]) < ExperimentCriteria.DegreeEps)
            {
                r.Reason = "non-canonical: no linear-r term (c1~0)";
                r.CanonicalSuccess = false;
                return;
            }
            if (r.LinearContamination > ExperimentCriteria.LinearContaminationTol)
            {
                r.Reason = string.Format(CultureInfo.InvariantCulture,
                    "non-canonical: quadratic contamination {0:G6} > {1}",
                    r.LinearContamination, ExperimentCriteria.LinearContaminationTol);
                r.CanonicalSuccess = false;
                return;
            }

            r.CanonicalSuccess = true;
            r.CanonicalEquation = string.Format(CultureInfo.InvariantCulture,
                "C = {0:G15} + {1:G15} * r", r.InterceptB, r.SlopeK);
            r.Reason = okReason;
            r.StructuralSuccess = true;
        }

        public static CanonicalResult ExtractArea(VnnBpNet net, double rMax, double yScale)
        {
            CanonicalResult r = new CanonicalResult();
            r.Reason = "";
            r.CanonicalEquation = "";
            r.PolynomialScaled = "";
            if (net == null || net.maxOutputs < 1 || net.maxInputs < 1)
            {
                r.Reason = "empty network";
                return r;
            }

            int output = net.outputs[0].sno;
            int input = net.inputs[0].sno;
            bool[] onPath = MarkReachesOutput(net, output);

            int sqLin = 0, qSig = 0;
            bool sigmoid = false;
            for (int i = 0; i < net.maxNeurons; i++)
            {
                if (!onPath[i]) continue;
                short f = net.neuron[i].funno;
                if (f == 7) sqLin++;
                if (f == 4) qSig++;
                if (NnMath.IsSigmoidActivation(f) && net.neuron[i].ihono != NuType.In)
                    sigmoid = true;
            }
            r.SquareLinearOnPath = sqLin;
            r.QuadraticSigmoidOnPath = qSig;
            r.HasSigmoidOnPath = sigmoid;
            r.StructuralSuccess = (sqLin + qSig) >= 1;

            double[] poly;
            string fail;
            if (sigmoid)
            {
                // Attempt polynomial reduction on the non-sigmoid subgraph.
                // Accept only if that polynomial matches the live network to fidelity tol
                // (sigmoid path numerically inactive). Never uses π.
                if (!TryPolyIgnoringSigmoid(net, output, input, onPath, out poly, out fail))
                {
                    r.Reason = "non-canonical: sigmoid on path; " + fail;
                    return r;
                }
                if (!PolynomialMatchesNetwork(net, poly, out fail))
                {
                    r.Reason = "non-canonical: sigmoid on path and active (" + fail + ")";
                    return r;
                }
                FinishCanonical(r, poly, rMax, yScale, "canonical A = b + k r^2 (sigmoid path numerically inactive)");
                return r;
            }

            if (!TryPoly(net, output, input, onPath, new int[net.maxNeurons], out poly, out fail))
            {
                r.Reason = "non-canonical: " + fail;
                return r;
            }

            FinishCanonical(r, poly, rMax, yScale, "canonical A = b + k r^2");
            return r;
        }

        private static void FinishCanonical(
            CanonicalResult r, double[] poly, double rMax, double yScale, string okReason)
        {
            r.C0 = poly[0];
            r.C1 = poly[1];
            r.C2 = poly[2];
            r.PolynomialScaled = string.Format(CultureInfo.InvariantCulture,
                "n_out = {0:G15} + {1:G15}*r_s + {2:G15}*r_s^2", poly[0], poly[1], poly[2]);

            if (rMax <= 0) rMax = 1;
            if (yScale <= 0) yScale = 1;
            r.InterceptB = yScale * poly[0];
            r.SlopeK = yScale * poly[2] / (rMax * rMax);
            double den = Math.Abs(poly[2]);
            r.LinearContamination = den < ExperimentCriteria.DegreeEps
                ? (Math.Abs(poly[1]) < ExperimentCriteria.DegreeEps ? 0.0 : 1.0)
                : Math.Abs(poly[1]) / den;

            if (Math.Abs(poly[2]) < ExperimentCriteria.DegreeEps)
            {
                r.Reason = "non-canonical: no r^2 term (c2~0)";
                r.CanonicalSuccess = false;
                return;
            }
            if (r.LinearContamination > ExperimentCriteria.LinearContaminationTol)
            {
                r.Reason = string.Format(CultureInfo.InvariantCulture,
                    "non-canonical: linear-r contamination {0:G6} > {1}",
                    r.LinearContamination, ExperimentCriteria.LinearContaminationTol);
                r.CanonicalSuccess = false;
                return;
            }

            r.CanonicalSuccess = true;
            r.CanonicalEquation = string.Format(CultureInfo.InvariantCulture,
                "A = {0:G15} + {1:G15} * r^2", r.InterceptB, r.SlopeK);
            r.Reason = okReason;
        }

        /// <summary>
        /// Compose polynomial while skipping any edge whose source or destination is sigmoid.
        /// </summary>
        private static bool TryPolyIgnoringSigmoid(
            VnnBpNet net, int output, int input, bool[] onPath,
            out double[] poly, out string fail)
        {
            // Clone connectivity conceptually: zero weights on sigmoid edges, then TryPoly
            // on a temporary weight overlay is awkward; instead DFS that refuses sigmoid nodes
            // by treating them as absent sources (contribution 0).
            return TryPolySkipSigmoid(net, output, input, onPath, new int[net.maxNeurons], out poly, out fail);
        }

        private static bool TryPolySkipSigmoid(
            VnnBpNet net, int index, int inputIndex, bool[] onPath, int[] color,
            out double[] poly, out string fail)
        {
            poly = new double[3];
            fail = "";
            if (index < 0 || index >= net.maxNeurons)
            {
                fail = "bad index";
                return false;
            }
            if (NnMath.IsSigmoidActivation(net.neuron[index].funno) && net.neuron[index].ihono != NuType.In)
            {
                // Absent from polynomial subgraph
                fail = "hit sigmoid n" + index.ToString();
                return false;
            }
            if (color[index] == 1)
            {
                fail = "cycle";
                return false;
            }
            if (net.neuron[index].ihono == NuType.In || index == inputIndex)
            {
                poly[1] = 1.0;
                color[index] = 2;
                return true;
            }

            short f = net.neuron[index].funno;
            if (f != 1 && f != 7)
            {
                fail = "unsupported funno " + f.ToString() + " at n" + index.ToString();
                return false;
            }

            color[index] = 1;
            double[] acc = new double[3];
            acc[0] = net.neuron[index].bias;

            for (int c = 0; c < net.maxConnections; c++)
            {
                if (net.conn[c].dstNuNo != index) continue;
                int src = net.conn[c].srcNuNo;
                if (src < 0 || src >= net.maxNeurons) continue;
                if (Math.Abs(net.conn[c].wgt) < ExperimentCriteria.DegreeEps) continue;
                if (src == index) continue;
                if (NnMath.IsSigmoidActivation(net.neuron[src].funno) && net.neuron[src].ihono != NuType.In)
                    continue; // drop sigmoid source edges
                double[] sp;
                string innerFail;
                if (!TryPolySkipSigmoid(net, src, inputIndex, onPath, color, out sp, out innerFail))
                {
                    fail = innerFail;
                    color[index] = 0;
                    return false;
                }
                double w = net.conn[c].wgt;
                if (f == 7)
                {
                    double[] sq;
                    if (!SquarePoly(sp, out sq, out innerFail))
                    {
                        fail = innerFail;
                        color[index] = 0;
                        return false;
                    }
                    AddScaled(acc, sq, w);
                }
                else
                    AddScaled(acc, sp, w);
            }

            color[index] = 2;
            poly = acc;
            return true;
        }

        private static bool PolynomialMatchesNetwork(VnnBpNet net, double[] poly, out string fail)
        {
            fail = "";
            int oi = net.outputs[0].sno;
            double maxAbs = 0;
            for (int i = 0; i <= 40; i++)
            {
                double rs = i / 40.0;
                net.inputs[0].value = rs;
                if (net.outputs != null && net.outputs.Length > 0)
                    net.outputs[0].value = 0;
                net.UpdateNet();
                double yNet = net.neuron[oi].nuOut;
                double yPoly = poly[0] + poly[1] * rs + poly[2] * rs * rs;
                if (!NnMath.IsFinite(yNet) || !NnMath.IsFinite(yPoly))
                {
                    fail = "non-finite y_nn or y_poly";
                    return false;
                }
                double d = Math.Abs(yNet - yPoly);
                if (d > maxAbs) maxAbs = d;
            }
            if (maxAbs > 1e-5)
            {
                fail = string.Format(CultureInfo.InvariantCulture, "max|y_nn-y_poly|={0:G6}", maxAbs);
                return false;
            }
            return true;
        }

        public static void ApplyInterceptGate(CanonicalResult r, double meanAbsArea)
        {
            if (r == null) return;
            if (!r.CanonicalSuccess)
            {
                r.NearZeroIntercept = false;
                return;
            }
            double scale = Math.Abs(meanAbsArea);
            if (scale < 1.0) scale = 1.0;
            r.NearZeroIntercept = Math.Abs(r.InterceptB) / scale <= ExperimentCriteria.InterceptRelTol;
        }

        /// <summary>
        /// Fit C_scaled = c0 + c1 r_s on the training split (no 2π). Build a direct
        /// linear I->O net. Accept only if validation loss ≤ allowedLoss.
        /// </summary>
        public static bool TryInstallTrainLinear(
            VnnBpNet net,
            DatasetSplit split,
            double allowedLoss,
            out string note)
        {
            note = "";
            if (net == null || split == null || split.TrainIn == null || split.TrainIn.Length < 2)
            {
                note = "insufficient train data";
                return false;
            }

            double c0, c1;
            FitScaledLinear(split.TrainIn, split.TrainOut, out c0, out c1);
            if (Math.Abs(c1) < ExperimentCriteria.DegreeEps)
            {
                note = "OLS c1~0";
                return false;
            }

            VnnBpNet cand = BuildMinimalLinearNet(c0, c1);
            double val = cand.Evaluate(split.ValIn, split.ValOut).Loss;
            if (!(val <= allowedLoss))
            {
                note = string.Format(CultureInfo.InvariantCulture,
                    "OLS linear rejected: Lval={0:G6} > allowed={1:G6}", val, allowedLoss);
                return false;
            }

            net.RestoreCompleteSnapshot(cand.CreateCompleteSnapshot());
            note = string.Format(CultureInfo.InvariantCulture,
                "installed train-OLS linear (c0={0:G6}, c1={1:G6}, Lval={2:G6})", c0, c1, val);
            return true;
        }

        private static void FitScaledLinear(double[][] xin, double[][] xout, out double c0, out double c1)
        {
            int n = xin.Length;
            double s1 = 0, sx = 0, sy = 0, sxx = 0, sxy = 0;
            for (int i = 0; i < n; i++)
            {
                double x = xin[i][0];
                double y = xout[i][0];
                s1 += 1.0;
                sx += x;
                sy += y;
                sxx += x * x;
                sxy += x * y;
            }
            double det = s1 * sxx - sx * sx;
            if (Math.Abs(det) < 1e-18)
            {
                c0 = n > 0 ? sy / n : 0;
                c1 = 0;
                return;
            }
            c0 = (sy * sxx - sx * sxy) / det;
            c1 = (s1 * sxy - sx * sy) / det;
        }

        private static VnnBpNet BuildMinimalLinearNet(double c0, double c1)
        {
            // 1-0-1: direct input to output (no hidden). Constructor needs hidden≥1 in some builds;
            // use 1 hidden linear with identity, or zero-hidden if supported.
            VnnBpNet net = new VnnBpNet("olsLin,1,1,Fully Conn,1");
            net.lossType = LossType.MeanSquaredError;
            int input = -1, hidden = -1, output = -1;
            for (int i = 0; i < net.maxNeurons; i++)
            {
                if (net.neuron[i].ihono == NuType.In)
                {
                    input = i;
                    net.neuron[i].funno = 1;
                    net.neuron[i].bias = 0;
                }
                else if (net.neuron[i].ihono == NuType.Hdn)
                {
                    hidden = i;
                    net.neuron[i].funno = 1;
                    net.neuron[i].bias = 0;
                }
                else if (net.neuron[i].ihono == NuType.Out)
                {
                    output = i;
                    net.neuron[i].funno = 1;
                    net.neuron[i].bias = c0;
                }
            }
            for (int c = 0; c < net.maxConnections; c++)
            {
                if (net.conn[c].srcNuNo == input && net.conn[c].dstNuNo == output)
                    net.conn[c].wgt = c1;
                else if (net.conn[c].srcNuNo == input && net.conn[c].dstNuNo == hidden)
                    net.conn[c].wgt = 0.0;
                else if (net.conn[c].srcNuNo == hidden && net.conn[c].dstNuNo == output)
                    net.conn[c].wgt = 0.0;
                else
                    net.conn[c].wgt = 0.0;
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

        /// <summary>
        /// Fit C_scaled = c0 + c1 r_s + c2 r_s^2 on train; install linear+type-7 realizing it.
        /// </summary>
        public static bool TryInstallTrainMixed(
            VnnBpNet net,
            DatasetSplit split,
            double allowedLoss,
            out string note)
        {
            note = "";
            if (net == null || split == null || split.TrainIn == null || split.TrainIn.Length < 3)
            {
                note = "insufficient train data";
                return false;
            }
            double c0, c1, c2;
            FitScaledMixed(split.TrainIn, split.TrainOut, out c0, out c1, out c2);
            VnnBpNet cand = BuildMinimalMixedNet(c0, c1, c2);
            double val = cand.Evaluate(split.ValIn, split.ValOut).Loss;
            if (!(val <= allowedLoss))
            {
                note = string.Format(CultureInfo.InvariantCulture,
                    "OLS mixed rejected: Lval={0:G6} > allowed={1:G6}", val, allowedLoss);
                return false;
            }
            net.RestoreCompleteSnapshot(cand.CreateCompleteSnapshot());
            note = string.Format(CultureInfo.InvariantCulture,
                "installed train-OLS mixed (c0={0:G6}, c1={1:G6}, c2={2:G6}, Lval={3:G6})", c0, c1, c2, val);
            return true;
        }

        private static void FitScaledMixed(double[][] xin, double[][] xout, out double c0, out double c1, out double c2)
        {
            // Normal equations for [1, x, x^2]
            int n = xin.Length;
            double s0 = 0, sx = 0, sx2 = 0, sx3 = 0, sx4 = 0, sy = 0, sxy = 0, sx2y = 0;
            for (int i = 0; i < n; i++)
            {
                double x = xin[i][0];
                double y = xout[i][0];
                double x2 = x * x;
                s0 += 1; sx += x; sx2 += x2; sx3 += x2 * x; sx4 += x2 * x2;
                sy += y; sxy += x * y; sx2y += x2 * y;
            }
            // Solve 3x3 via Cramer's / elimination
            double[,] A = new double[3, 3];
            double[] b = new double[3];
            A[0, 0] = s0; A[0, 1] = sx; A[0, 2] = sx2; b[0] = sy;
            A[1, 0] = sx; A[1, 1] = sx2; A[1, 2] = sx3; b[1] = sxy;
            A[2, 0] = sx2; A[2, 1] = sx3; A[2, 2] = sx4; b[2] = sx2y;
            double[] sol;
            if (!Solve3(A, b, out sol))
            {
                c0 = n > 0 ? sy / n : 0; c1 = 0; c2 = 0;
                return;
            }
            c0 = sol[0]; c1 = sol[1]; c2 = sol[2];
        }

        private static bool Solve3(double[,] A, double[] b, out double[] x)
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

        private static VnnBpNet BuildMinimalMixedNet(double c0, double c1, double c2)
        {
            // 1-2-1: hidden0 linear (identity path), hidden1 type-7 square
            VnnBpNet net = new VnnBpNet("olsMix,1,1,Fully Conn,2");
            net.lossType = LossType.MeanSquaredError;
            int input = -1, hLin = -1, hSq = -1, output = -1;
            int hSeen = 0;
            for (int i = 0; i < net.maxNeurons; i++)
            {
                if (net.neuron[i].ihono == NuType.In) { input = i; net.neuron[i].funno = 1; net.neuron[i].bias = 0; }
                else if (net.neuron[i].ihono == NuType.Out) { output = i; net.neuron[i].funno = 1; net.neuron[i].bias = c0; }
                else if (net.neuron[i].ihono == NuType.Hdn)
                {
                    if (hSeen == 0) { hLin = i; net.neuron[i].funno = 1; net.neuron[i].bias = 0; }
                    else { hSq = i; net.neuron[i].funno = 7; net.neuron[i].bias = 0; }
                    hSeen++;
                }
            }
            for (int c = 0; c < net.maxConnections; c++)
            {
                int s = net.conn[c].srcNuNo, d = net.conn[c].dstNuNo;
                if (s == input && d == hLin) net.conn[c].wgt = 1.0;
                else if (s == hLin && d == output) net.conn[c].wgt = c1;
                else if (s == input && d == hSq) net.conn[c].wgt = 1.0;
                else if (s == hSq && d == output) net.conn[c].wgt = c2;
                else if (s == input && d == output) net.conn[c].wgt = 0;
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

        /// <summary>
        /// 1-D polynomial extraction in physical units (xScale, yScale).
        /// </summary>
        public static CanonicalResult ExtractPoly1D(VnnBpNet net, double xScale, double yScale)
        {
            CanonicalResult r = new CanonicalResult();
            r.Reason = "";
            if (net == null || net.maxOutputs < 1 || net.maxInputs < 1)
            {
                r.Reason = "empty";
                return r;
            }
            int output = net.outputs[0].sno;
            int input = net.inputs[0].sno;
            bool[] onPath = MarkReachesOutput(net, output);
            bool sigmoid = false;
            int sq = 0;
            for (int i = 0; i < net.maxNeurons; i++)
            {
                if (!onPath[i]) continue;
                if (net.neuron[i].funno == 7) sq++;
                if (NnMath.IsSigmoidActivation(net.neuron[i].funno) && net.neuron[i].ihono != NuType.In)
                    sigmoid = true;
            }
            r.SquareLinearOnPath = sq;
            r.StructuralSuccess = true;
            r.HasSigmoidOnPath = sigmoid;

            double[] poly;
            string fail;
            if (sigmoid)
            {
                if (!TryPolyIgnoringSigmoid(net, output, input, onPath, out poly, out fail)
                    || !PolynomialMatchesNetwork(net, poly, out fail))
                {
                    r.Reason = "non-canonical: " + fail;
                    return r;
                }
            }
            else if (!TryPoly(net, output, input, onPath, new int[net.maxNeurons], out poly, out fail))
            {
                r.Reason = "non-canonical: " + fail;
                return r;
            }

            if (xScale <= 0) xScale = 1;
            if (yScale <= 0) yScale = 1;
            // n_out = p0 + p1 x_s + p2 x_s^2; x = x_s * xScale
            // y = yScale * n_out = yScale*p0 + (yScale*p1/xScale)*x + (yScale*p2/xScale^2)*x^2
            r.C0 = yScale * poly[0];
            r.C1 = yScale * poly[1] / xScale;
            r.C2 = yScale * poly[2] / (xScale * xScale);
            r.InterceptB = r.C0;
            r.SlopeK = r.C2; // for quadratic-focused callers
            r.PolynomialScaled = string.Format(CultureInfo.InvariantCulture,
                "n_out={0:G15}+{1:G15}*x_s+{2:G15}*x_s^2", poly[0], poly[1], poly[2]);
            r.CanonicalEquation = string.Format(CultureInfo.InvariantCulture,
                "y = {0:G15} + {1:G15}*x + {2:G15}*x^2", r.C0, r.C1, r.C2);
            if (!NnMath.IsFinite(r.C0) || !NnMath.IsFinite(r.C1) || !NnMath.IsFinite(r.C2))
            {
                r.Reason = "non-finite coeffs";
                return r;
            }
            r.CanonicalSuccess = true;
            r.Reason = "polynomial reduced";
            return r;
        }

        public static bool StructureMatches(CanonicalResult r, SyntheticKind kind)
        {
            if (r == null || !r.CanonicalSuccess) return false;
            double a0 = Math.Abs(r.C0), a1 = Math.Abs(r.C1), a2 = Math.Abs(r.C2);
            double scale = Math.Max(a1, a2);
            if (scale < ExperimentCriteria.DegreeEps) scale = Math.Max(a0, 1.0);
            switch (kind)
            {
                case SyntheticKind.S1_Linear:
                    return a1 > 0.05 * Math.Max(a0, 1.0) && a2 <= 0.05 * Math.Max(a1, ExperimentCriteria.DegreeEps);
                case SyntheticKind.S2_PureQuadratic:
                    return a2 > 0.05 * Math.Max(a0, 1.0) && a1 <= 0.05 * Math.Max(a2, ExperimentCriteria.DegreeEps);
                case SyntheticKind.S3_MixedPoly:
                    return a1 > 0.05 * scale && a2 > 0.05 * scale;
                default:
                    return r.CanonicalSuccess;
            }
        }

        /// <summary>
        /// Fit A_scaled = c0 + c2 r_s^2 on the training split (no π). Build a 1-hidden
        /// type-7 network that realises that polynomial. If its validation loss stays
        /// within allowedLoss vs the locked dense baseline, replace net in-place.
        /// </summary>
        public static bool TryInstallTrainQuadratic(
            VnnBpNet net,
            DatasetSplit split,
            double allowedLoss,
            out string note)
        {
            note = "";
            if (net == null || split == null || split.TrainIn == null || split.TrainIn.Length < 2)
            {
                note = "insufficient train data";
                return false;
            }

            double c0, c2;
            FitScaledQuadratic(split.TrainIn, split.TrainOut, out c0, out c2);
            if (Math.Abs(c2) < ExperimentCriteria.DegreeEps)
            {
                note = "OLS c2~0";
                return false;
            }

            VnnBpNet cand = BuildMinimalSquareNet(c0, c2);
            double val = cand.Evaluate(split.ValIn, split.ValOut).Loss;
            if (!(val <= allowedLoss))
            {
                note = string.Format(CultureInfo.InvariantCulture,
                    "OLS quadratic rejected: Lval={0:G6} > allowed={1:G6}", val, allowedLoss);
                return false;
            }

            net.RestoreCompleteSnapshot(cand.CreateCompleteSnapshot());
            note = string.Format(CultureInfo.InvariantCulture,
                "installed train-OLS quadratic (c0={0:G6}, c2={1:G6}, Lval={2:G6})", c0, c2, val);
            return true;
        }

        private static void FitScaledQuadratic(double[][] xin, double[][] xout, out double c0, out double c2)
        {
            // y = c0 + c2 * r_s^2  (no linear term)
            int n = xin.Length;
            double s1 = 0, sx = 0, sy = 0, sxx = 0, sxy = 0;
            for (int i = 0; i < n; i++)
            {
                double rs = xin[i][0];
                double x = rs * rs;
                double y = xout[i][0];
                s1 += 1.0;
                sx += x;
                sy += y;
                sxx += x * x;
                sxy += x * y;
            }
            double det = s1 * sxx - sx * sx;
            if (Math.Abs(det) < 1e-18)
            {
                c0 = n > 0 ? sy / n : 0;
                c2 = 0;
                return;
            }
            c0 = (sy * sxx - sx * sxy) / det;
            c2 = (s1 * sxy - sx * sy) / det;
        }

        private static VnnBpNet BuildMinimalSquareNet(double c0, double c2)
        {
            VnnBpNet net = new VnnBpNet("olsQuad,1,1,Fully Conn,1");
            net.lossType = LossType.MeanSquaredError;
            int input = -1, hidden = -1, output = -1;
            for (int i = 0; i < net.maxNeurons; i++)
            {
                if (net.neuron[i].ihono == NuType.In)
                {
                    input = i;
                    net.neuron[i].funno = 1;
                    net.neuron[i].bias = 0;
                }
                else if (net.neuron[i].ihono == NuType.Hdn)
                {
                    hidden = i;
                    net.neuron[i].funno = 7;
                    net.neuron[i].bias = 0;
                }
                else if (net.neuron[i].ihono == NuType.Out)
                {
                    output = i;
                    net.neuron[i].funno = 1;
                    net.neuron[i].bias = c0;
                }
            }
            for (int c = 0; c < net.maxConnections; c++)
            {
                if (net.conn[c].srcNuNo == input && net.conn[c].dstNuNo == hidden)
                    net.conn[c].wgt = 1.0;
                else if (net.conn[c].srcNuNo == hidden && net.conn[c].dstNuNo == output)
                    net.conn[c].wgt = c2;
                else if (net.conn[c].srcNuNo == input && net.conn[c].dstNuNo == output)
                    net.conn[c].wgt = 0.0;
                else
                    net.conn[c].wgt = 0.0;
            }
            // Drop zero-weight extras so extractor sees a clean path.
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

        /// <summary>Known-weight 1-hidden type-7 net must reduce to k = w_ho * w_ih.</summary>
        public static string SelfTest()
        {
            VnnBpNet net = new VnnBpNet("canon,1,1,Fully Conn,1");
            int input = -1, hidden = -1, output = -1;
            for (int i = 0; i < net.maxNeurons; i++)
            {
                if (net.neuron[i].ihono == NuType.In) { input = i; net.neuron[i].funno = 1; net.neuron[i].bias = 0; }
                if (net.neuron[i].ihono == NuType.Hdn) { hidden = i; net.neuron[i].funno = 7; net.neuron[i].bias = 0.0; }
                if (net.neuron[i].ihono == NuType.Out) { output = i; net.neuron[i].funno = 1; net.neuron[i].bias = 0.1; }
            }
            for (int c = 0; c < net.maxConnections; c++)
            {
                if (net.conn[c].srcNuNo == input && net.conn[c].dstNuNo == hidden)
                    net.conn[c].wgt = 2.0;
                else if (net.conn[c].srcNuNo == hidden && net.conn[c].dstNuNo == output)
                    net.conn[c].wgt = 1.5;
                else
                    net.conn[c].wgt = 0.0;
            }
            // Drop zero weights so the extractor does not see extra paths.
            for (int c = 0; c < net.maxConnections; c++)
            {
                if (Math.Abs(net.conn[c].wgt) < 1e-18)
                {
                    net.conn[c].srcNuNo = 9999;
                    net.conn[c].dstNuNo = 9999;
                }
            }
            net.sort_connections();

            double rMax = 10.0;
            double yScale = 100.0;
            CanonicalResult got = ExtractArea(net, rMax, yScale);
            // n_h = 2 r_s^2, n_o = 0.1 + 1.5 n_h = 0.1 + 3 r_s^2
            // physical: A = 0.1*100 + 3 r^2 = 10 + 3 r^2
            bool ok = got.CanonicalSuccess
                && Math.Abs(got.SlopeK - 3.0) < 1e-9
                && Math.Abs(got.InterceptB - 10.0) < 1e-9;
            return string.Format(CultureInfo.InvariantCulture,
                "canonical_selftest,{0},{1},{2},{3}",
                got.SlopeK, 3.0,
                Math.Abs(got.SlopeK - 3.0),
                ok ? "PASS" : "FAIL");
        }

        /// <summary>Known-weight direct linear net must reduce to k = w_io * yScale / rMax.</summary>
        public static string SelfTestCirc()
        {
            VnnBpNet net = BuildMinimalLinearNet(0.2, 1.5);
            double rMax = 10.0;
            double yScale = 10.0;
            CanonicalResult got = ExtractCircumference(net, rMax, yScale);
            // n_o = 0.2 + 1.5 r_s; C = 0.2*10 + 1.5 r = 2 + 1.5 r
            bool ok = got.CanonicalSuccess
                && Math.Abs(got.SlopeK - 1.5) < 1e-9
                && Math.Abs(got.InterceptB - 2.0) < 1e-9;
            return string.Format(CultureInfo.InvariantCulture,
                "canonical_circ_selftest,{0},{1},{2},{3}",
                got.SlopeK, 1.5,
                Math.Abs(got.SlopeK - 1.5),
                ok ? "PASS" : "FAIL");
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

        /// <param name="color">0=white, 1=grey, 2=black (cycle detection).</param>
        private static bool TryPoly(
            VnnBpNet net, int index, int inputIndex, bool[] onPath, int[] color,
            out double[] poly, out string fail)
        {
            poly = new double[3];
            fail = "";
            if (index < 0 || index >= net.maxNeurons)
            {
                fail = "bad index";
                return false;
            }
            if (color[index] == 1)
            {
                fail = "cycle";
                return false;
            }
            if (color[index] == 2)
            {
                // stored in... we don't cache; recompute is fine for tiny graphs
            }
            if (net.neuron[index].ihono == NuType.In || index == inputIndex)
            {
                poly[1] = 1.0;
                color[index] = 2;
                return true;
            }

            short f = net.neuron[index].funno;
            if (NnMath.IsSigmoidActivation(f))
            {
                fail = "sigmoid neuron n" + index.ToString();
                return false;
            }
            if (f != 1 && f != 7 && net.neuron[index].ihono != NuType.In)
            {
                fail = "unsupported funno " + f.ToString() + " at n" + index.ToString();
                return false;
            }

            color[index] = 1;
            double[] acc = new double[3];
            acc[0] = net.neuron[index].bias;

            for (int c = 0; c < net.maxConnections; c++)
            {
                if (net.conn[c].dstNuNo != index)
                    continue;
                int src = net.conn[c].srcNuNo;
                if (src < 0 || src >= net.maxNeurons)
                    continue;
                if (Math.Abs(net.conn[c].wgt) < ExperimentCriteria.DegreeEps)
                    continue;
                if (src == index)
                    continue;
                double[] sp;
                string innerFail;
                if (!TryPoly(net, src, inputIndex, onPath, color, out sp, out innerFail))
                {
                    fail = innerFail;
                    color[index] = 0;
                    return false;
                }
                double w = net.conn[c].wgt;
                if (f == 7)
                {
                    double[] sq;
                    if (!SquarePoly(sp, out sq, out innerFail))
                    {
                        fail = innerFail;
                        color[index] = 0;
                        return false;
                    }
                    AddScaled(acc, sq, w);
                }
                else
                    AddScaled(acc, sp, w);
            }

            color[index] = 2;
            poly = acc;
            return true;
        }

        private static void AddScaled(double[] dst, double[] src, double w)
        {
            dst[0] += w * src[0];
            dst[1] += w * src[1];
            dst[2] += w * src[2];
        }

        private static bool SquarePoly(double[] p, out double[] q, out string fail)
        {
            q = new double[3];
            fail = "";
            // (a0 + a1 x + a2 x^2)^2 has x^3,x^4 unless a2=0
            if (Math.Abs(p[2]) > ExperimentCriteria.DegreeEps)
            {
                fail = "square of degree>1 polynomial";
                return false;
            }
            q[0] = p[0] * p[0];
            q[1] = 2.0 * p[0] * p[1];
            q[2] = p[1] * p[1];
            return true;
        }
    }
}
