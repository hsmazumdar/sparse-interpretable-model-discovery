using System;

namespace VnnBp.VnnBpLib
{
    /// <summary>Exp20: manuscript rewrite gates via exp20_manuscript_gates.py.</summary>
    public static class Exp20Runner
    {
        public static int Run(string[] args)
        {
            return Exp19Runner.RunPython("exp20_manuscript_gates.py");
        }
    }
}
