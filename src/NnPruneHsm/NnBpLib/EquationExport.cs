using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace VnnBp.VnnBpLib
{
    /// <summary>
    /// Machine / human / Word-oriented equation export helpers for Exp13.
    /// </summary>
    public static class EquationExport
    {
        public static string ToHumanReadable(string exportedEquation, string canonicalOptional)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("Human-readable equation export");
            sb.AppendLine("==============================");
            if (!string.IsNullOrEmpty(canonicalOptional))
            {
                sb.AppendLine("Canonical form:");
                sb.AppendLine("  " + canonicalOptional.Trim());
                sb.AppendLine();
            }
            sb.AppendLine("Surviving network (neuron-by-neuron):");
            if (!string.IsNullOrEmpty(exportedEquation))
            {
                string[] lines = exportedEquation.Replace("\r\n", "\n").Split('\n');
                for (int i = 0; i < lines.Length; i++)
                {
                    string line = lines[i].Trim();
                    if (line.Length == 0) continue;
                    sb.AppendLine("  " + line);
                }
            }
            return sb.ToString();
        }

        public static string ToLatex(string canonicalOrPlain)
        {
            if (string.IsNullOrEmpty(canonicalOrPlain))
                return "% empty";
            string s = canonicalOrPlain.Trim();
            // Light cleanup for LaTeX
            s = s.Replace("^2", "^{2}");
            s = s.Replace("*", " \\cdot ");
            return "\\begin{equation}\n" + s + "\n\\end{equation}\n";
        }

        /// <summary>
        /// Minimal OMML (Office Math ML) for Word paste / insert.
        /// Handles simple A = b + k*r^2 style; falls back to plain run otherwise.
        /// </summary>
        public static string ToOmml(string displayText)
        {
            if (string.IsNullOrEmpty(displayText)) displayText = "n/a";
            string esc = EscapeXml(displayText.Trim());
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
            sb.AppendLine("<!-- Word-compatible OMML fragment: insert via Alt+= or Open XML -->");
            sb.AppendLine("<m:oMathPara xmlns:m=\"http://schemas.openxmlformats.org/officeDocument/2006/math\">");
            sb.AppendLine("  <m:oMath>");
            sb.AppendLine("    <m:r>");
            sb.AppendLine("      <m:t xml:space=\"preserve\">" + esc + "</m:t>");
            sb.AppendLine("    </m:r>");
            sb.AppendLine("  </m:oMath>");
            sb.AppendLine("</m:oMathPara>");
            return sb.ToString();
        }

        public static string CoefficientsCsv(VnnBpNet net)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("kind,id,src,dst,funno,bias,weight");
            if (net == null) return sb.ToString();
            for (int i = 0; i < net.maxNeurons; i++)
            {
                sb.AppendFormat(CultureInfo.InvariantCulture,
                    "neuron,{0},,,{1},{2},\r\n",
                    i, net.neuron[i].funno,
                    net.neuron[i].bias.ToString("G15", CultureInfo.InvariantCulture));
            }
            for (int c = 0; c < net.maxConnections; c++)
            {
                if (net.conn[c].srcNuNo > 9000) continue;
                sb.AppendFormat(CultureInfo.InvariantCulture,
                    "edge,,{0},{1},,{2}\r\n",
                    net.conn[c].srcNuNo, net.conn[c].dstNuNo,
                    net.conn[c].wgt.ToString("G15", CultureInfo.InvariantCulture));
            }
            return sb.ToString();
        }

        public static string TopologyText(VnnBpNet net)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("Graph topology");
            sb.AppendLine("--------------");
            if (net == null) return sb.ToString();
            sb.AppendFormat(CultureInfo.InvariantCulture,
                "neurons={0}  connections={1}  inputs={2}  outputs={3}\r\n",
                net.maxNeurons, net.maxConnections, net.maxInputs, net.maxOutputs);
            sb.AppendLine();
            sb.AppendLine("Neurons:");
            for (int i = 0; i < net.maxNeurons; i++)
            {
                string role = "hdn";
                if (net.neuron[i].ihono == NuType.In) role = "in";
                else if (net.neuron[i].ihono == NuType.Out) role = "out";
                sb.AppendFormat(CultureInfo.InvariantCulture,
                    "  n{0}  role={1}  funno={2}  bias={3:G15}\r\n",
                    i, role, net.neuron[i].funno, net.neuron[i].bias);
            }
            sb.AppendLine();
            sb.AppendLine("Edges (src -> dst : weight):");
            for (int c = 0; c < net.maxConnections; c++)
            {
                if (net.conn[c].srcNuNo > 9000) continue;
                sb.AppendFormat(CultureInfo.InvariantCulture,
                    "  n{0} -> n{1} : {2:G15}\r\n",
                    net.conn[c].srcNuNo, net.conn[c].dstNuNo, net.conn[c].wgt);
            }
            return sb.ToString();
        }

        public static string MachineJson(VnnBpNet net, string task, string canonical, FidelityReport fid)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("{");
            sb.AppendFormat(CultureInfo.InvariantCulture, "  \"task\": \"{0}\",\n", EscapeJson(task));
            sb.AppendFormat(CultureInfo.InvariantCulture, "  \"canonical\": \"{0}\",\n", EscapeJson(canonical ?? ""));
            sb.AppendFormat(CultureInfo.InvariantCulture, "  \"fidelity_pass\": {0},\n",
                fid != null && fid.Pass ? "true" : "false");
            sb.AppendFormat(CultureInfo.InvariantCulture, "  \"fidelity_max_abs\": {0},\n",
                fid == null ? "null" : Num(fid.MaxAbsDiff));
            sb.AppendLine("  \"neurons\": [");
            if (net != null)
            {
                for (int i = 0; i < net.maxNeurons; i++)
                {
                    string role = "hdn";
                    if (net.neuron[i].ihono == NuType.In) role = "in";
                    else if (net.neuron[i].ihono == NuType.Out) role = "out";
                    sb.AppendFormat(CultureInfo.InvariantCulture,
                        "    {{\"id\":{0},\"role\":\"{1}\",\"funno\":{2},\"bias\":{3}}}{4}\n",
                        i, role, net.neuron[i].funno, Num(net.neuron[i].bias),
                        i + 1 < net.maxNeurons ? "," : "");
                }
            }
            sb.AppendLine("  ],");
            sb.AppendLine("  \"edges\": [");
            if (net != null)
            {
                List<string> edges = new List<string>();
                for (int c = 0; c < net.maxConnections; c++)
                {
                    if (net.conn[c].srcNuNo > 9000) continue;
                    edges.Add(string.Format(CultureInfo.InvariantCulture,
                        "    {{\"src\":{0},\"dst\":{1},\"w\":{2}}}",
                        net.conn[c].srcNuNo, net.conn[c].dstNuNo, Num(net.conn[c].wgt)));
                }
                for (int i = 0; i < edges.Count; i++)
                    sb.Append(edges[i] + (i + 1 < edges.Count ? ",\n" : "\n"));
            }
            sb.AppendLine("  ]");
            sb.AppendLine("}");
            return sb.ToString();
        }

        private static string Num(double v)
        {
            if (!NnMath.IsFinite(v)) return "null";
            return v.ToString("G15", CultureInfo.InvariantCulture);
        }

        private static string EscapeXml(string s)
        {
            if (s == null) return "";
            return s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");
        }

        private static string EscapeJson(string s)
        {
            if (s == null) return "";
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "\\r").Replace("\n", "\\n");
        }
    }
}
