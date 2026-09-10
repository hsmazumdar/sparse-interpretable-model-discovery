using System;
using System.Globalization;

namespace VnnBp.VnnBpLib
{
    public enum SyntheticKind
    {
        S1_Linear = 1,
        S2_PureQuadratic = 2,
        S3_MixedPoly = 3,
        S4_DiagonalQuad2D = 4,
        S5_Circle = 5
    }

    public sealed class SyntheticProblem
    {
        public SyntheticKind Kind;
        public string Id;
        public string Formula;
        public int InputDim;
        public double[] TrueCoeffs; // interpreted per kind
        public bool IsClassification;

        public static SyntheticProblem[] DefaultSuite()
        {
            return new SyntheticProblem[]
            {
                new SyntheticProblem
                {
                    Kind = SyntheticKind.S1_Linear, Id = "S1", InputDim = 1,
                    Formula = "y=2.5*x+1.2",
                    TrueCoeffs = new double[] { 1.2, 2.5, 0 }, // c0,c1,c2
                    IsClassification = false
                },
                new SyntheticProblem
                {
                    Kind = SyntheticKind.S2_PureQuadratic, Id = "S2", InputDim = 1,
                    Formula = "y=1.7*x^2+0.4",
                    TrueCoeffs = new double[] { 0.4, 0, 1.7 },
                    IsClassification = false
                },
                new SyntheticProblem
                {
                    Kind = SyntheticKind.S3_MixedPoly, Id = "S3", InputDim = 1,
                    Formula = "y=1.5*x+2.2*x^2-0.7",
                    TrueCoeffs = new double[] { -0.7, 1.5, 2.2 },
                    IsClassification = false
                },
                new SyntheticProblem
                {
                    Kind = SyntheticKind.S4_DiagonalQuad2D, Id = "S4", InputDim = 2,
                    Formula = "y=1.3*x1^2+0.8*x2^2+0.2",
                    TrueCoeffs = new double[] { 0.2, 1.3, 0.8 }, // c0, a, b
                    IsClassification = false
                },
                new SyntheticProblem
                {
                    Kind = SyntheticKind.S5_Circle, Id = "S5", InputDim = 2,
                    Formula = "(x-0.5)^2+(y-0.5)^2<=0.25^2",
                    TrueCoeffs = new double[] { 0.5, 0.5, 0.25 }, // cx,cy,R
                    IsClassification = true
                }
            };
        }

        public void Generate(int n, int seed, double noiseStd, out double[][] xin, out double[][] xout)
        {
            Random rnd = new Random(seed);
            xin = new double[n][];
            xout = new double[n][];
            for (int i = 0; i < n; i++)
            {
                if (InputDim == 1)
                {
                    double x = rnd.NextDouble(); // [0,1]
                    double y = Eval1D(x);
                    if (noiseStd > 0)
                        y += Gaussian(rnd) * noiseStd;
                    xin[i] = new double[] { x };
                    xout[i] = new double[] { y };
                }
                else if (Kind == SyntheticKind.S4_DiagonalQuad2D)
                {
                    double x1 = 2.0 * rnd.NextDouble() - 1.0; // [-1,1]
                    double x2 = 2.0 * rnd.NextDouble() - 1.0;
                    double y = TrueCoeffs[0] + TrueCoeffs[1] * x1 * x1 + TrueCoeffs[2] * x2 * x2;
                    if (noiseStd > 0)
                        y += Gaussian(rnd) * noiseStd;
                    xin[i] = new double[] { x1, x2 };
                    xout[i] = new double[] { y };
                }
                else // S5 circle
                {
                    double x = rnd.NextDouble();
                    double y = rnd.NextDouble();
                    double cx = TrueCoeffs[0], cy = TrueCoeffs[1], R = TrueCoeffs[2];
                    double d = (x - cx) * (x - cx) + (y - cy) * (y - cy);
                    double t = d <= R * R ? 1.0 : 0.0;
                    if (noiseStd > 0)
                    {
                        // label flip with small probability proportional to noise
                        if (rnd.NextDouble() < Math.Min(0.25, noiseStd))
                            t = 1.0 - t;
                    }
                    xin[i] = new double[] { x, y };
                    xout[i] = new double[] { t };
                }
            }
        }

        private double Eval1D(double x)
        {
            return TrueCoeffs[0] + TrueCoeffs[1] * x + TrueCoeffs[2] * x * x;
        }

        private static double Gaussian(Random rnd)
        {
            // Box-Muller without explicit 2π: use atan-based? Plan avoids 2π in labels;
            // Box-Muller uses 2π for sampling noise only (not labels). Use sum of uniforms.
            double s = 0;
            for (int k = 0; k < 12; k++) s += rnd.NextDouble();
            return s - 6.0;
        }

        public override string ToString()
        {
            return Id + ":" + Formula;
        }
    }
}
