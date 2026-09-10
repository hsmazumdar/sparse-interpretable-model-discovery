using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace VnnBp.VnnBpLib
{
    /// <summary>
    /// 2-D circle / disk classification labels. Target from geometry only:
    /// t = 1[(x-cx)^2 + (y-cy)^2 &lt;= R^2]. No Math.PI.
    /// </summary>
    public static class CircleDataGenerator
    {
        public const double DefaultCenter = 0.5;
        public const double DefaultRadius = 0.25;

        public static void Generate(
            int sampleCount,
            int randomSeed,
            double centerX,
            double centerY,
            double radius,
            out double[][] xin,
            out double[][] xout)
        {
            if (sampleCount < 10) sampleCount = 10;
            xin = new double[sampleCount][];
            xout = new double[sampleCount][];
            double r2 = radius * radius;
            Random rnd = new Random(randomSeed);
            for (int i = 0; i < sampleCount; i++)
            {
                double x = rnd.NextDouble();
                double y = rnd.NextDouble();
                double d = (x - centerX) * (x - centerX) + (y - centerY) * (y - centerY);
                xin[i] = new double[] { x, y };
                xout[i] = new double[] { d <= r2 ? 1.0 : 0.0 };
            }
        }

        public static void SaveCsv(string path, double[][] xin, double[][] xout)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("SNo,x,y,t");
            for (int i = 0; i < xin.Length; i++)
            {
                sb.AppendFormat(CultureInfo.InvariantCulture,
                    "{0},{1:G15},{2:G15},{3:G15}\r\n",
                    i, xin[i][0], xin[i][1], xout[i][0]);
            }
            string dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            File.WriteAllText(path, sb.ToString());
        }
    }
}
