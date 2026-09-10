using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace VnnBp.VnnBpLib
{
    /// <summary>
    /// One (radius, measurement) pair. Y is raw pixel count (area) or
    /// Euclidean boundary length (circumference). Never derived from Math.PI.
    /// </summary>
    public sealed class PiSample
    {
        public double R;
        public double Y;
    }

    /// <summary>
    /// Pixel-counting and boundary-tracing geometry. No Math.PI, no trig-based
    /// circle parameterisation of the target.
    /// </summary>
    public static class PiDataGenerator
    {
        /// <summary>
        /// Machin formula used only when scoring recovered k. Not used to label data.
        /// </summary>
        public static double ReferencePi()
        {
            return 16.0 * Math.Atan(0.2) - 4.0 * Math.Atan(1.0 / 239.0);
        }

        public static double ReferenceTwoPi()
        {
            return 2.0 * ReferencePi();
        }

        /// <summary>
        /// Integer radii from inclusive lo to hi in steps.
        /// </summary>
        public static int[] RadiusRange(int lo, int hi, int step)
        {
            if (step < 1) step = 1;
            if (hi < lo) { int t = lo; lo = hi; hi = t; }
            List<int> list = new List<int>();
            for (int r = lo; r <= hi; r += step)
                list.Add(r);
            if (list.Count == 0) list.Add(lo);
            return list.ToArray();
        }

        /// <summary>
        /// Count pixels whose centres satisfy (x-c)^2 + (y-c)^2 &lt;= r^2.
        /// Target is the raw count; it is not multiplied by a pixel-area factor.
        /// </summary>
        public static PiSample[] GenerateAreaData(int[] radii, int gridResolution)
        {
            if (radii == null) throw new ArgumentNullException("radii");
            if (gridResolution < 8)
                throw new ArgumentException("gridResolution must be at least 8.");
            PiSample[] samples = new PiSample[radii.Length];
            double c = gridResolution / 2.0;
            for (int i = 0; i < radii.Length; i++)
            {
                int r = radii[i];
                if (r < 0) r = 0;
                double r2 = (double)r * (double)r;
                int count = 0;
                for (int y = 0; y < gridResolution; y++)
                {
                    double dy = (y + 0.5) - c;
                    double dy2 = dy * dy;
                    for (int x = 0; x < gridResolution; x++)
                    {
                        double dx = (x + 0.5) - c;
                        if (dx * dx + dy2 <= r2)
                            count++;
                    }
                }
                samples[i] = new PiSample();
                samples[i].R = r;
                samples[i].Y = count;
            }
            return samples;
        }

        /// <summary>
        /// Boundary pixels: inside the disk, with at least one 4-neighbour outside.
        /// Circumference is the closed Euclidean polyline through those pixels
        /// ordered by Atan2 about the centre (ordering only; the length is Euclidean).
        /// This is the raw / digitised perimeter (Exp03 Step 3.1).
        /// </summary>
        public static PiSample[] GenerateCircumferenceData(int[] radii, int gridResolution)
        {
            return GenerateCircumferenceRaw(radii, gridResolution);
        }

        public static PiSample[] GenerateCircumferenceRaw(int[] radii, int gridResolution)
        {
            if (radii == null) throw new ArgumentNullException("radii");
            if (gridResolution < 8)
                throw new ArgumentException("gridResolution must be at least 8.");
            PiSample[] samples = new PiSample[radii.Length];
            double c = gridResolution / 2.0;
            for (int i = 0; i < radii.Length; i++)
            {
                int r = radii[i];
                if (r < 0) r = 0;
                double r2 = (double)r * (double)r;
                List<double> bx = new List<double>();
                List<double> by = new List<double>();
                for (int y = 0; y < gridResolution; y++)
                {
                    for (int x = 0; x < gridResolution; x++)
                    {
                        if (!Inside(x, y, c, r2, gridResolution))
                            continue;
                        if (Inside(x - 1, y, c, r2, gridResolution)
                            && Inside(x + 1, y, c, r2, gridResolution)
                            && Inside(x, y - 1, c, r2, gridResolution)
                            && Inside(x, y + 1, c, r2, gridResolution))
                            continue;
                        bx.Add(x + 0.5);
                        by.Add(y + 0.5);
                    }
                }
                samples[i] = new PiSample();
                samples[i].R = r;
                samples[i].Y = ClosedPolylineLength(bx, by, c, c);
            }
            return samples;
        }

        /// <summary>
        /// Improved perimeter (Exp03 Step 3.2): marching-squares contour length on the
        /// binary disk (edge midpoints). Approximates Euclidean perimeter without
        /// using Math.PI or 2πr as a label.
        /// </summary>
        public static PiSample[] GenerateCircumferenceImproved(int[] radii, int gridResolution)
        {
            if (radii == null) throw new ArgumentNullException("radii");
            if (gridResolution < 8)
                throw new ArgumentException("gridResolution must be at least 8.");
            PiSample[] samples = new PiSample[radii.Length];
            double c = gridResolution / 2.0;
            for (int i = 0; i < radii.Length; i++)
            {
                int r = radii[i];
                if (r < 0) r = 0;
                double r2 = (double)r * (double)r;
                bool[,] mask = new bool[gridResolution, gridResolution];
                for (int y = 0; y < gridResolution; y++)
                    for (int x = 0; x < gridResolution; x++)
                        mask[x, y] = Inside(x, y, c, r2, gridResolution);
                samples[i] = new PiSample();
                samples[i].R = r;
                samples[i].Y = MarchingSquaresPerimeter(mask, gridResolution);
            }
            return samples;
        }

        /// <summary>
        /// Sum lengths of linear segments between midpoints of inside/outside edges
        /// on each 2×2 cell (standard binary marching squares, no π).
        /// </summary>
        private static double MarchingSquaresPerimeter(bool[,] mask, int res)
        {
            double len = 0;
            for (int y = 0; y < res - 1; y++)
            {
                for (int x = 0; x < res - 1; x++)
                {
                    int v0 = mask[x, y] ? 1 : 0;
                    int v1 = mask[x + 1, y] ? 1 : 0;
                    int v2 = mask[x + 1, y + 1] ? 1 : 0;
                    int v3 = mask[x, y + 1] ? 1 : 0;
                    int code = v0 | (v1 << 1) | (v2 << 2) | (v3 << 3);
                    if (code == 0 || code == 15) continue;
                    // Edge midpoints of the unit cell [x,x+1]×[y,y+1]
                    // bottom, right, top, left
                    double bx = x + 0.5, by = y;
                    double rx = x + 1, ry = y + 0.5;
                    double tx = x + 0.5, ty = y + 1;
                    double lx = x, ly = y + 0.5;
                    switch (code)
                    {
                        case 1: case 14: len += Dist(bx, by, lx, ly); break;
                        case 2: case 13: len += Dist(bx, by, rx, ry); break;
                        case 3: case 12: len += Dist(lx, ly, rx, ry); break;
                        case 4: case 11: len += Dist(rx, ry, tx, ty); break;
                        case 6: case 9: len += Dist(bx, by, tx, ty); break;
                        case 7: case 8: len += Dist(lx, ly, tx, ty); break;
                        case 5: // saddle: bottom-left + top-right
                            len += Dist(bx, by, lx, ly) + Dist(rx, ry, tx, ty);
                            break;
                        case 10: // saddle: bottom-right + top-left
                            len += Dist(bx, by, rx, ry) + Dist(lx, ly, tx, ty);
                            break;
                    }
                }
            }
            return len;
        }

        private static double Dist(double x0, double y0, double x1, double y1)
        {
            double dx = x1 - x0;
            double dy = y1 - y0;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        /// <summary>
        /// Stochastic (Monte Carlo) area estimate for each radius.
        /// Sample N points uniformly in the square [-r,r]^2; estimate area as
        /// (hits/N) * (2r)^2. Never uses Math.PI.
        /// </summary>
        public static PiSample[] GenerateStochasticAreaData(
            int[] radii, int observationCount, int randomSeed)
        {
            if (radii == null) throw new ArgumentNullException("radii");
            if (observationCount < 1) observationCount = 1;
            PiSample[] samples = new PiSample[radii.Length];
            for (int i = 0; i < radii.Length; i++)
            {
                int r = radii[i];
                if (r < 0) r = 0;
                // Deterministic stream per (seed, N, r) so depths are comparable.
                Random rnd = new Random(unchecked(randomSeed * 1000003 + observationCount * 9176 + r * 97));
                int hits = 0;
                double rr = (double)r;
                double r2 = rr * rr;
                for (int n = 0; n < observationCount; n++)
                {
                    double x = (2.0 * rnd.NextDouble() - 1.0) * rr;
                    double y = (2.0 * rnd.NextDouble() - 1.0) * rr;
                    if (x * x + y * y <= r2)
                        hits++;
                }
                samples[i] = new PiSample();
                samples[i].R = r;
                // Square area = (2r)^2 = 4 r^2; no π involved.
                samples[i].Y = observationCount > 0
                    ? (hits * 4.0 * r2) / observationCount
                    : 0;
            }
            return samples;
        }

        public static PiSample[] FilterMinRadius(PiSample[] samples, double minR)
        {
            if (samples == null) return new PiSample[0];
            List<PiSample> keep = new List<PiSample>();
            for (int i = 0; i < samples.Length; i++)
            {
                if (samples[i].R >= minR)
                    keep.Add(samples[i]);
            }
            return keep.ToArray();
        }

        public static void SaveCsv(string path, PiSample[] samples)
        {
            if (path == null) throw new ArgumentNullException("path");
            string dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("SNo,r,y");
            for (int i = 0; i < samples.Length; i++)
            {
                sb.AppendFormat(CultureInfo.InvariantCulture,
                    "{0},{1:G15},{2:G15}\r\n", i, samples[i].R, samples[i].Y);
            }
            File.WriteAllText(path, sb.ToString());
        }

        public static void ToArrays(PiSample[] samples, out double[][] xin, out double[][] xout)
        {
            xin = new double[samples.Length][];
            xout = new double[samples.Length][];
            for (int i = 0; i < samples.Length; i++)
            {
                xin[i] = new double[] { samples[i].R };
                xout[i] = new double[] { samples[i].Y };
            }
        }

        /// <summary>
        /// Integer-lattice Gauss counts (x^2+y^2 &lt;= r^2 on Z^2) for self-test.
        /// Independent of the grid used in GenerateAreaData.
        /// </summary>
        public static string VerifyKnownLatticeCounts()
        {
            int[] rs = new int[] { 0, 1, 2, 3, 4, 5 };
            int[] expect = new int[] { 1, 5, 13, 29, 49, 81 };
            StringBuilder sb = new StringBuilder();
            bool all = true;
            for (int i = 0; i < rs.Length; i++)
            {
                int got = CountIntegerDisk(rs[i]);
                bool ok = got == expect[i];
                if (!ok) all = false;
                sb.AppendFormat(CultureInfo.InvariantCulture,
                    "lattice_r={0},{1},{2},{3}\r\n", rs[i], got, expect[i], ok ? "PASS" : "FAIL");
            }
            sb.AppendLine(all ? "pi_geometry_lattice,PASS" : "pi_geometry_lattice,FAIL");
            return sb.ToString();
        }

        private static bool Inside(int x, int y, double c, double r2, int res)
        {
            if (x < 0 || y < 0 || x >= res || y >= res)
                return false;
            double dx = (x + 0.5) - c;
            double dy = (y + 0.5) - c;
            return dx * dx + dy * dy <= r2;
        }

        private static double ClosedPolylineLength(List<double> xs, List<double> ys, double cx, double cy)
        {
            int n = xs.Count;
            if (n == 0) return 0;
            if (n == 1) return 0;
            int[] idx = new int[n];
            for (int i = 0; i < n; i++) idx[i] = i;
            Array.Sort(idx, delegate(int a, int b)
            {
                double aa = Math.Atan2(ys[a] - cy, xs[a] - cx);
                double bb = Math.Atan2(ys[b] - cy, xs[b] - cx);
                return aa.CompareTo(bb);
            });
            double len = 0;
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                int ia = idx[i];
                int ib = idx[j];
                double dx = xs[ib] - xs[ia];
                double dy = ys[ib] - ys[ia];
                len += Math.Sqrt(dx * dx + dy * dy);
            }
            return len;
        }

        private static int CountIntegerDisk(int r)
        {
            int r2 = r * r;
            int n = 0;
            for (int y = -r; y <= r; y++)
            {
                for (int x = -r; x <= r; x++)
                {
                    if (x * x + y * y <= r2)
                        n++;
                }
            }
            return n;
        }
    }
}
