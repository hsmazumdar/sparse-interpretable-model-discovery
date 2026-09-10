using System;
using System.Globalization;
using System.Text;

namespace VnnBp.VnnBpLib
{
    /// <summary>
    /// Recovers slope k from a pruned network by evaluating the live (exported)
    /// model on the geometric samples and fitting:
    ///   Area:           y = a + k r^2
    ///   Circumference:  y = a + k r
    /// The intercept a is required so an output bias cannot be mistaken for 2π vs π.
    /// </summary>
    public sealed class PiRecoveredConstant
    {
        public double SlopeK;
        public double Intercept;
        public double RelativeError;
        public double TargetConstant;
        public string Equation;
        public int HiddenNeurons;
        public int Connections;
        public int QuadraticHidden;
        public int LinearHidden;
        public bool UsedFactorTwoCorrection;
        public string Notes;
    }

    public static class PiEquationParser
    {
        public static PiRecoveredConstant Recover(
            VnnBpNet net,
            PiSample[] samples,
            bool areaTask)
        {
            return Recover(net, samples, areaTask, 1.0, 1.0);
        }

        /// <param name="inputScale">Network input is r / inputScale (use Rmax). Parser unscales.</param>
        /// <param name="outputScale">Network target is y / outputScale. Parser unscales predictions.</param>
        public static PiRecoveredConstant Recover(
            VnnBpNet net,
            PiSample[] samples,
            bool areaTask,
            double inputScale,
            double outputScale)
        {
            if (inputScale == 0.0) inputScale = 1.0;
            if (outputScale == 0.0) outputScale = 1.0;
            PiRecoveredConstant rec = new PiRecoveredConstant();
            rec.Equation = net != null ? net.ExportEquation() : "";
            rec.HiddenNeurons = CountHidden(net);
            rec.Connections = net != null ? net.maxConnections : 0;
            rec.QuadraticHidden = PiNetworkBuilder.CountHiddenOfType(net, 4);
            rec.LinearHidden = PiNetworkBuilder.CountHiddenOfType(net, 1);
            rec.TargetConstant = areaTask ? PiDataGenerator.ReferencePi() : PiDataGenerator.ReferenceTwoPi();
            rec.UsedFactorTwoCorrection = false;
            rec.Notes = "";

            if (net == null || samples == null || samples.Length < 2)
            {
                rec.Notes = "insufficient data";
                rec.RelativeError = double.NaN;
                return rec;
            }

            double[] x = new double[samples.Length];
            double[] y = new double[samples.Length];
            for (int i = 0; i < samples.Length; i++)
            {
                double r = samples[i].R;
                x[i] = areaTask ? (r * r) : r;
                double pred = Predict(net, r / inputScale);
                y[i] = pred * outputScale;
                if (!NnMath.IsFinite(y[i]))
                    y[i] = 0;
            }

            double intercept, slope;
            OrdinaryLeastSquares(x, y, out intercept, out slope);
            rec.Intercept = intercept;
            rec.SlopeK = slope;

            // Fallback: if area slope is closer to 2π than to π, the linear
            // output may have absorbed a factor of two.
            if (areaTask)
            {
                double pi = PiDataGenerator.ReferencePi();
                double twoPi = PiDataGenerator.ReferenceTwoPi();
                double ePi = RelErr(slope, pi);
                double e2 = RelErr(slope, twoPi);
                if (e2 < ePi && e2 < 0.25)
                {
                    rec.SlopeK = slope / 2.0;
                    rec.UsedFactorTwoCorrection = true;
                    rec.Notes = "divided slope by 2 (closer to 2π than π before correction)";
                }
            }

            rec.RelativeError = RelErr(rec.SlopeK, rec.TargetConstant);
            if (!NnMath.IsFinite(rec.SlopeK) || !NnMath.IsFinite(rec.RelativeError))
            {
                rec.SlopeK = 0;
                rec.RelativeError = 1.0;
                rec.Notes = (rec.Notes + " non-finite prediction; k set to 0").Trim();
            }
            return rec;
        }

        public static string Format(PiRecoveredConstant rec)
        {
            if (rec == null) return "";
            StringBuilder sb = new StringBuilder();
            sb.AppendFormat(CultureInfo.InvariantCulture,
                "k={0:G15}  intercept={1:G15}  target={2:G15}  rel_err={3:G15}  conn={4}  hidden={5}  quad={6}\r\n",
                rec.SlopeK, rec.Intercept, rec.TargetConstant, rec.RelativeError,
                rec.Connections, rec.HiddenNeurons, rec.QuadraticHidden);
            if (!string.IsNullOrEmpty(rec.Notes))
                sb.AppendLine(rec.Notes);
            sb.AppendLine("--- exported equation ---");
            sb.Append(rec.Equation);
            return sb.ToString();
        }

        private static double Predict(VnnBpNet net, double r)
        {
            net.inputs[0].value = r;
            if (net.outputs != null && net.outputs.Length > 0)
                net.outputs[0].value = 0;
            net.UpdateNet();
            int oi = net.outputs[0].sno;
            return net.neuron[oi].nuOut;
        }

        private static void OrdinaryLeastSquares(double[] x, double[] y, out double intercept, out double slope)
        {
            int n = x.Length;
            double sx = 0, sy = 0, sxx = 0, sxy = 0;
            for (int i = 0; i < n; i++)
            {
                sx += x[i];
                sy += y[i];
                sxx += x[i] * x[i];
                sxy += x[i] * y[i];
            }
            double d = n * sxx - sx * sx;
            if (Math.Abs(d) < 1e-18)
            {
                intercept = n > 0 ? sy / n : 0;
                slope = 0;
                return;
            }
            slope = (n * sxy - sx * sy) / d;
            intercept = (sy - slope * sx) / n;
        }

        private static double RelErr(double k, double target)
        {
            double den = Math.Abs(target);
            if (den < 1e-18) return Math.Abs(k);
            return Math.Abs(k - target) / den;
        }

        private static int CountHidden(VnnBpNet net)
        {
            if (net == null) return 0;
            int n = 0;
            for (int i = 0; i < net.maxNeurons; i++)
            {
                if (net.neuron[i].ihono == NuType.Hdn)
                    n++;
            }
            return n;
        }
    }
}
