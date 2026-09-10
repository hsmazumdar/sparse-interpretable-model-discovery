using System;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace VnnBp.VnnBpLib
{
    /// <summary>
    /// Exp14/15: reproducibility audit + manuscript tables via exp14_15_build.py.
    /// </summary>
    public static class Exp14Runner
    {
        public static int Run(string[] args)
        {
            bool skip14 = false;
            bool skip15 = false;
            if (args != null)
            {
                for (int i = 0; i < args.Length; i++)
                {
                    if (args[i] == "--skip14") skip14 = true;
                    if (args[i] == "--skip15") skip15 = true;
                    if (args[i] == "--exp15" || args[i] == "/exp15" || args[i] == "--exp-15")
                        skip14 = true; // tables only when invoked as exp15
                }
            }
            // If first arg is --exp14, run both unless flags say otherwise.
            string cmd0 = (args != null && args.Length > 0) ? args[0] : "";
            if (cmd0 == "--exp14" || cmd0 == "/exp14" || cmd0 == "--exp-14")
            {
                // both by default
            }

            string root = FindProjectRoot();
            string script = Path.Combine(root, "exp14_15_build.py");
            if (!File.Exists(script))
            {
                Console.WriteLine("ERROR: missing " + script);
                return 2;
            }

            StringBuilder argsPy = new StringBuilder();
            argsPy.Append("\"").Append(script).Append("\" --root \"").Append(root).Append("\"");
            if (skip14) argsPy.Append(" --skip14");
            if (skip15) argsPy.Append(" --skip15");

            Console.WriteLine("Invoking: python " + argsPy);
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName = "python";
                psi.Arguments = argsPy.ToString();
                psi.UseShellExecute = false;
                psi.RedirectStandardOutput = true;
                psi.RedirectStandardError = true;
                psi.CreateNoWindow = true;
                psi.WorkingDirectory = root;
                using (Process p = Process.Start(psi))
                {
                    if (p == null) return 3;
                    string stdout = p.StandardOutput.ReadToEnd();
                    string stderr = p.StandardError.ReadToEnd();
                    p.WaitForExit();
                    Console.Write(stdout);
                    if (!string.IsNullOrEmpty(stderr))
                        Console.Error.Write(stderr);
                    return p.ExitCode;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("ERROR: " + ex.Message);
                return 4;
            }
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
