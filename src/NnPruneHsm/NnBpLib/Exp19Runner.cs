using System;
using System.Diagnostics;
using System.IO;

namespace VnnBp.VnnBpLib
{
    /// <summary>Exp19: pi-never-objective policy audit via exp19_pi_policy.py.</summary>
    public static class Exp19Runner
    {
        public static int Run(string[] args)
        {
            return RunPython("exp19_pi_policy.py");
        }

        internal static int RunPython(string scriptName)
        {
            string root = FindProjectRoot();
            string script = Path.Combine(root, scriptName);
            if (!File.Exists(script))
            {
                Console.WriteLine("ERROR: missing " + script);
                return 2;
            }
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName = "python";
                psi.Arguments = "\"" + script + "\" --root \"" + root + "\"";
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
