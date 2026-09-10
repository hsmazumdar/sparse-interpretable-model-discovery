using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using VnnBp.VnnBpLib;

namespace VnnBp
{
    static class Program
    {
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool AttachConsole(int dwProcessId);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool AllocConsole();

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool FreeConsole();

        private const int AttachParentProcess = -1;

        /// <summary>
        /// The main entry point for the application.
        /// --selftest     activation / gradient / lattice checks
        /// --pi-discover  pixel-count / boundary-trace Pi rediscovery experiment
        /// --exp02        canonical A=b+kr^2 recovery
        /// --exp03        canonical C=b+kr circumference recovery
        /// --exp04        stochastic k_N → π convergence
        /// --exp01        circle boundary (x-0.5)^2+(y-0.5)^2 <= R^2
        /// </summary>
        [STAThread]
        static void Main(string[] args)
        {
            if (args != null && args.Length > 0)
            {
                EnsureConsole();
                string cmd = args[0];
                if (cmd == "--selftest" || cmd == "/selftest")
                {
                    string report = GradientCheck.RunBasicChecks();
                    string outPath = Path.Combine(
                        AppDomain.CurrentDomain.BaseDirectory,
                        "gradient_check_report.txt");
                    File.WriteAllText(outPath, report);
                    Console.WriteLine(report);
                    Console.WriteLine("Wrote " + outPath);
                    return;
                }
                if (cmd == "--pi-discover" || cmd == "/pi-discover")
                {
                    int code = PiExperimentRunner.Run(args);
                    Environment.ExitCode = code;
                    return;
                }
                if (cmd == "--exp02" || cmd == "/exp02" || cmd == "--exp-02")
                {
                    int code = Exp02Runner.Run(args);
                    Environment.ExitCode = code;
                    return;
                }
                if (cmd == "--exp03" || cmd == "/exp03" || cmd == "--exp-03")
                {
                    int code = Exp03Runner.Run(args);
                    Environment.ExitCode = code;
                    return;
                }
                if (cmd == "--exp04" || cmd == "/exp04" || cmd == "--exp-04")
                {
                    int code = Exp04Runner.Run(args);
                    Environment.ExitCode = code;
                    return;
                }
                if (cmd == "--exp01" || cmd == "/exp01" || cmd == "--exp-01")
                {
                    int code = Exp01Runner.Run(args);
                    Environment.ExitCode = code;
                    return;
                }
                if (cmd == "--exp05" || cmd == "/exp05" || cmd == "--exp-05")
                {
                    int code = Exp05Runner.Run(args);
                    Environment.ExitCode = code;
                    return;
                }
                if (cmd == "--exp06" || cmd == "/exp06" || cmd == "--exp-06")
                {
                    int code = Exp05Runner.RunExp06(args);
                    Environment.ExitCode = code;
                    return;
                }
                if (cmd == "--exp07" || cmd == "/exp07" || cmd == "--exp-07")
                {
                    int code = Exp07Runner.Run(args);
                    Environment.ExitCode = code;
                    return;
                }
                if (cmd == "--exp08" || cmd == "/exp08" || cmd == "--exp-08")
                {
                    int code = Exp08Runner.Run(args);
                    Environment.ExitCode = code;
                    return;
                }
                if (cmd == "--exp09" || cmd == "/exp09" || cmd == "--exp-09")
                {
                    int code = Exp09Runner.Run(args);
                    Environment.ExitCode = code;
                    return;
                }
                if (cmd == "--exp10" || cmd == "/exp10" || cmd == "--exp-10")
                {
                    int code = Exp10Runner.Run(args);
                    Environment.ExitCode = code;
                    return;
                }
                if (cmd == "--exp11" || cmd == "/exp11" || cmd == "--exp-11")
                {
                    int code = Exp11Runner.Run(args);
                    Environment.ExitCode = code;
                    return;
                }
                if (cmd == "--exp12" || cmd == "/exp12" || cmd == "--exp-12")
                {
                    int code = Exp12Runner.Run(args);
                    Environment.ExitCode = code;
                    return;
                }
                if (cmd == "--exp13" || cmd == "/exp13" || cmd == "--exp-13")
                {
                    int code = Exp13Runner.Run(args);
                    Environment.ExitCode = code;
                    return;
                }
                if (cmd == "--exp14" || cmd == "/exp14" || cmd == "--exp-14"
                    || cmd == "--exp15" || cmd == "/exp15" || cmd == "--exp-15"
                    || cmd == "--exp14-15" || cmd == "--exp-14-15")
                {
                    int code = Exp14Runner.Run(args);
                    Environment.ExitCode = code;
                    return;
                }
                if (cmd == "--exp16" || cmd == "/exp16" || cmd == "--exp-16")
                {
                    int code = Exp16Runner.Run(args);
                    Environment.ExitCode = code;
                    return;
                }
                if (cmd == "--exp17" || cmd == "/exp17" || cmd == "--exp-17")
                {
                    int code = Exp17Runner.Run(args);
                    Environment.ExitCode = code;
                    return;
                }
                if (cmd == "--exp18" || cmd == "/exp18" || cmd == "--exp-18")
                {
                    int code = Exp18Runner.Run(args);
                    Environment.ExitCode = code;
                    return;
                }
                if (cmd == "--exp19" || cmd == "/exp19" || cmd == "--exp-19")
                {
                    int code = Exp19Runner.Run(args);
                    Environment.ExitCode = code;
                    return;
                }
                if (cmd == "--exp20" || cmd == "/exp20" || cmd == "--exp-20")
                {
                    int code = Exp20Runner.Run(args);
                    Environment.ExitCode = code;
                    return;
                }
                if (cmd == "--exp19-20" || cmd == "--exp-19-20")
                {
                    int c1 = Exp19Runner.Run(args);
                    if (c1 != 0) { Environment.ExitCode = c1; return; }
                    int c2 = Exp20Runner.Run(args);
                    Environment.ExitCode = c2;
                    return;
                }
                if (cmd == "--help" || cmd == "-h" || cmd == "/?")
                {
                    Console.WriteLine("NnPruneHsm");
                    Console.WriteLine("  --exp07..--exp13  prior experiments");
                    Console.WriteLine("  --exp14 / --exp15 / --exp14-15");
                    Console.WriteLine("      reproducibility audit + manuscript Tables A-D");
                    Console.WriteLine("  --exp16           manuscript figures from Results CSVs");
                    Console.WriteLine("  --exp17           statistical reporting (CIs, paired, structure rates)");
                    Console.WriteLine("  --exp18           freeze/audit pre-registered success criteria");
                    Console.WriteLine("  --exp19           pi never used as train/prune objective");
                    Console.WriteLine("  --exp20           manuscript rewrite minimum gates");
                    Console.WriteLine("  --exp19-20        run Exp19 then Exp20");
                    Console.WriteLine("  Data are generated WITHOUT Math.PI.");
                    return;
                }
            }
            try { FreeConsole(); } catch { }
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new VnnBpDemo());
        }

        private static void EnsureConsole()
        {
            if (!AttachConsole(AttachParentProcess))
                AllocConsole();
        }
    }
}
