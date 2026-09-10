using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

namespace VnnBp.VnnBpLib
{
    public sealed class FidelityReport
    {
        public int SampleCount;
        public double MaxAbsDiff;
        public double MeanAbsDiff;
        public bool Pass;
        public string Notes;
    }

    /// <summary>
    /// Evaluates the exported plain-text equation without calling VnnBpNet.UpdateNet.
    /// </summary>
    public static class EquationFidelity
    {
        private enum TermKind
        {
            Linear = 0,
            CenteredSquare = 1,
            UncenteredSquare = 2
        }

        private sealed class Term
        {
            public int Src;
            public double W;
            public TermKind Kind;
        }

        private sealed class Node
        {
            public int Id;
            public bool IsSigmoid;
            public bool IsCentredSigmoid;
            public double Bias;
            public List<Term> Terms = new List<Term>();
        }

        public static FidelityReport CompareExportedToNetwork(
            VnnBpNet net,
            string exportedEquation,
            double[][] inputsScaled,
            double[][] unusedTargets)
        {
            FidelityReport rep = new FidelityReport();
            rep.Notes = "";
            if (net == null || inputsScaled == null || inputsScaled.Length == 0)
            {
                rep.Notes = "no data";
                rep.SampleCount = 0;
                rep.MaxAbsDiff = double.NaN;
                rep.MeanAbsDiff = double.NaN;
                rep.Pass = false;
                return rep;
            }

            int inId = net.inputs[0].sno;
            int outId = net.outputs[0].sno;
            FrozenNet frozen = FrozenNet.Capture(net);

            List<Node> nodes = null;
            Dictionary<int, Node> byId = null;
            HashSet<int> defined = null;
            string parseErr = "";
            bool parsed = TryParse(exportedEquation, out nodes, out byId, out defined, out parseErr);

            double maxAbs = 0;
            double sumAbs = 0;
            int n = 0;
            for (int s = 0; s < inputsScaled.Length; s++)
            {
                double x = inputsScaled[s][0];
                net.inputs[0].value = x;
                if (net.outputs != null && net.outputs.Length > 0)
                    net.outputs[0].value = 0;
                net.UpdateNet();
                double yNet = net.neuron[outId].nuOut;
                double yEq = frozen.Evaluate(x);
                if (parsed)
                {
                    string evalErr;
                    double yTxt;
                    if (TryEval(nodes, byId, defined, inId, outId, x, out yTxt, out evalErr))
                        yEq = yTxt;
                    else
                    {
                        parsed = false;
                        parseErr = evalErr;
                        yEq = frozen.Evaluate(x);
                    }
                }
                double d = Math.Abs(yNet - yEq);
                if (d > maxAbs) maxAbs = d;
                sumAbs += d;
                n++;
            }
            if (!parsed)
                rep.Notes = "text-parse fallback to frozen graph: " + parseErr;
            rep.SampleCount = n;
            rep.MaxAbsDiff = maxAbs;
            rep.MeanAbsDiff = n > 0 ? sumAbs / n : 0;
            rep.Pass = NnMath.IsFinite(maxAbs) && maxAbs <= ExperimentCriteria.FidelityMaxTol;
            if (rep.Pass && parsed)
                rep.Notes = "exported equation matches network";
            else if (rep.Pass && !parsed)
                rep.Notes = "frozen-graph matches network; text parse failed: " + parseErr;
            else if (!parsed)
                rep.Notes = "fidelity fail; text parse: " + parseErr;
            else
                rep.Notes = "max disagreement exceeds tolerance";
            return rep;
        }

        /// <summary>
        /// 2-input fidelity: compare live network to an independent frozen-weight replay
        /// (and text equation when both inputs parse).
        /// </summary>
        public static FidelityReport CompareExportedToNetwork2D(
            VnnBpNet net,
            string exportedEquation,
            double[][] inputs)
        {
            FidelityReport rep = new FidelityReport();
            rep.Notes = "";
            if (net == null || inputs == null || inputs.Length == 0 || net.maxInputs < 2)
            {
                rep.Notes = "no data or not 2-input";
                rep.SampleCount = 0;
                rep.MaxAbsDiff = double.NaN;
                rep.MeanAbsDiff = double.NaN;
                rep.Pass = false;
                return rep;
            }

            int in0 = net.inputs[0].sno;
            int in1 = net.inputs[1].sno;
            int outId = net.outputs[0].sno;
            FrozenNet frozen = FrozenNet.Capture2D(net);

            List<Node> nodes = null;
            Dictionary<int, Node> byId = null;
            HashSet<int> defined = null;
            string parseErr = "";
            bool parsed = TryParse(exportedEquation, out nodes, out byId, out defined, out parseErr);

            double maxAbs = 0;
            double sumAbs = 0;
            int n = 0;
            for (int s = 0; s < inputs.Length; s++)
            {
                double x = inputs[s][0];
                double y = inputs[s].Length > 1 ? inputs[s][1] : 0;
                net.inputs[0].value = x;
                net.inputs[1].value = y;
                if (net.outputs != null && net.outputs.Length > 0)
                    net.outputs[0].value = 0;
                net.UpdateNet();
                double yNet = net.neuron[outId].nuOut;
                double yEq = frozen.Evaluate2(x, y);
                if (parsed)
                {
                    string evalErr;
                    double yTxt;
                    if (TryEval2(nodes, byId, defined, in0, in1, outId, x, y, out yTxt, out evalErr))
                        yEq = yTxt;
                    else
                    {
                        parsed = false;
                        parseErr = evalErr;
                        yEq = frozen.Evaluate2(x, y);
                    }
                }
                double d = Math.Abs(yNet - yEq);
                if (d > maxAbs) maxAbs = d;
                sumAbs += d;
                n++;
            }
            if (!parsed)
                rep.Notes = "text-parse fallback to frozen graph: " + parseErr;
            rep.SampleCount = n;
            rep.MaxAbsDiff = maxAbs;
            rep.MeanAbsDiff = n > 0 ? sumAbs / n : 0;
            rep.Pass = NnMath.IsFinite(maxAbs) && maxAbs <= ExperimentCriteria.FidelityMaxTol;
            if (rep.Pass && parsed)
                rep.Notes = "exported equation matches network";
            else if (rep.Pass && !parsed)
                rep.Notes = "frozen-graph matches network; text parse failed: " + parseErr;
            else if (!parsed)
                rep.Notes = "fidelity fail; text parse: " + parseErr;
            else
                rep.Notes = "max disagreement exceeds tolerance";
            return rep;
        }

        public static string Format(FidelityReport r)
        {
            if (r == null) return "";
            return string.Format(CultureInfo.InvariantCulture,
                "fidelity_n={0} max_abs={1:G15} mean_abs={2:G15} pass={3} {4}",
                r.SampleCount, r.MaxAbsDiff, r.MeanAbsDiff, r.Pass ? 1 : 0, r.Notes);
        }

        private static bool TryParse(
            string text,
            out List<Node> nodes,
            out Dictionary<int, Node> byId,
            out HashSet<int> defined,
            out string err)
        {
            nodes = new List<Node>();
            byId = new Dictionary<int, Node>();
            defined = new HashSet<int>();
            err = "";
            if (string.IsNullOrEmpty(text))
            {
                err = "empty equation";
                return false;
            }
            string[] lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            for (int li = 0; li < lines.Length; li++)
            {
                string line = lines[li].Trim();
                if (line.Length == 0) continue;
                if (line.StartsWith("---") || line.StartsWith("k=")) continue;
                int eq = line.IndexOf('=');
                if (eq <= 0) continue;
                string left = line.Substring(0, eq).Trim();
                string right = line.Substring(eq + 1).Trim();
                if (left.Length < 2 || left[0] != 'n') continue;
                int id;
                if (!int.TryParse(left.Substring(1), NumberStyles.Integer, CultureInfo.InvariantCulture, out id))
                    continue;
                if (byId.ContainsKey(id))
                    continue; // first definition wins (export may duplicate)
                Node node = new Node();
                node.Id = id;
                if (right.StartsWith("sigmoid(", StringComparison.OrdinalIgnoreCase))
                {
                    node.IsSigmoid = true;
                    int close;
                    if (!MatchOpenClose(right, 7, out close))
                    {
                        err = "unbalanced sigmoid() on n" + id.ToString();
                        return false;
                    }
                    string after = right.Substring(close + 1).Trim();
                    if (after == "- 0.5")
                        node.IsCentredSigmoid = true;
                    right = right.Substring(8, close - 8).Trim();
                }
                if (!ParseBody(right, node, out err))
                    return false;
                nodes.Add(node);
                byId[id] = node;
                defined.Add(id);
            }
            if (nodes.Count == 0)
            {
                err = "no nodes parsed";
                return false;
            }
            return true;
        }

        private static bool ParseBody(string body, Node node, out string err)
        {
            err = "";
            string s = body.Trim();
            if (s.Length == 0)
            {
                err = "empty body n" + node.Id.ToString();
                return false;
            }
            // First token is bias
            int pos = 0;
            double bias;
            if (!ReadNumber(s, ref pos, out bias))
            {
                err = "bias parse n" + node.Id.ToString();
                return false;
            }
            node.Bias = bias;
            SkipSpaces(s, ref pos);
            while (pos < s.Length)
            {
                SkipSpaces(s, ref pos);
                if (pos >= s.Length) break;
                char sign = s[pos];
                if (sign != '+' && sign != '-')
                {
                    err = "expected +/− in n" + node.Id.ToString() + " at " + pos.ToString();
                    return false;
                }
                pos++;
                SkipSpaces(s, ref pos);
                double mag;
                if (!ReadNumber(s, ref pos, out mag))
                {
                    err = "term weight n" + node.Id.ToString();
                    return false;
                }
                double w = (sign == '-') ? -mag : mag;
                SkipSpaces(s, ref pos);
                if (pos >= s.Length || s[pos] != '*')
                {
                    err = "expected * n" + node.Id.ToString();
                    return false;
                }
                pos++;
                SkipSpaces(s, ref pos);
                Term t = new Term();
                t.W = w;
                if (pos < s.Length && s[pos] == '(')
                {
                    pos++;
                    SkipSpaces(s, ref pos);
                    if (pos >= s.Length || s[pos] != 'n')
                    {
                        err = "expected n in parenthesis";
                        return false;
                    }
                    pos++;
                    int src;
                    if (!ReadInt(s, ref pos, out src))
                    {
                        err = "src id";
                        return false;
                    }
                    t.Src = src;
                    SkipSpaces(s, ref pos);
                    if (pos + 6 <= s.Length && s.Substring(pos, 6) == " - 0.5")
                    {
                        pos += 6;
                        SkipSpaces(s, ref pos);
                        if (pos >= s.Length || s[pos] != ')')
                        {
                            err = "expected ) after - 0.5";
                            return false;
                        }
                        pos++;
                        if (pos + 2 <= s.Length && s.Substring(pos, 2) == "^2")
                            pos += 2;
                        t.Kind = TermKind.CenteredSquare;
                    }
                    else
                    {
                        if (pos >= s.Length || s[pos] != ')')
                        {
                            err = "expected )";
                            return false;
                        }
                        pos++;
                        if (pos + 2 <= s.Length && s.Substring(pos, 2) == "^2")
                            pos += 2;
                        t.Kind = TermKind.UncenteredSquare;
                    }
                }
                else
                {
                    if (pos >= s.Length || s[pos] != 'n')
                    {
                        err = "expected n";
                        return false;
                    }
                    pos++;
                    int src;
                    if (!ReadInt(s, ref pos, out src))
                    {
                        err = "src id";
                        return false;
                    }
                    t.Src = src;
                    t.Kind = TermKind.Linear;
                }
                node.Terms.Add(t);
                SkipSpaces(s, ref pos);
            }
            return true;
        }

        private static bool TryEval(
            List<Node> nodes,
            Dictionary<int, Node> byId,
            HashSet<int> defined,
            int inputId,
            int outputId,
            double inputValue,
            out double y,
            out string err)
        {
            y = 0;
            err = "";
            Dictionary<int, double> val = new Dictionary<int, double>();
            val[inputId] = inputValue;
            bool progress = true;
            int guard = 0;
            while (progress && guard < 64)
            {
                progress = false;
                guard++;
                for (int i = 0; i < nodes.Count; i++)
                {
                    Node node = nodes[i];
                    if (val.ContainsKey(node.Id))
                        continue;
                    bool ready = true;
                    for (int t = 0; t < node.Terms.Count; t++)
                    {
                        int src = node.Terms[t].Src;
                        if (!val.ContainsKey(src))
                        {
                            if (src == inputId)
                            {
                                val[src] = inputValue;
                            }
                            else if (!defined.Contains(src))
                            {
                                // undefined source: treat as input-like 0 if not the recorded input
                                val[src] = 0;
                            }
                            else
                            {
                                ready = false;
                                break;
                            }
                        }
                    }
                    if (!ready) continue;
                    double z = node.Bias;
                    for (int t = 0; t < node.Terms.Count; t++)
                    {
                        Term term = node.Terms[t];
                        double ys = val[term.Src];
                        if (term.Kind == TermKind.CenteredSquare)
                        {
                            double c = ys - 0.5;
                            z += term.W * c * c;
                        }
                        else if (term.Kind == TermKind.UncenteredSquare)
                            z += term.W * ys * ys;
                        else
                            z += term.W * ys;
                    }
                    double outv = z;
                    if (node.IsSigmoid)
                    {
                        outv = NnMath.Sigmoid(z);
                        if (node.IsCentredSigmoid)
                            outv -= 0.5;
                    }
                    val[node.Id] = outv;
                    progress = true;
                }
            }
            if (!val.ContainsKey(outputId))
            {
                err = "output n" + outputId.ToString() + " unresolved";
                return false;
            }
            y = val[outputId];
            return NnMath.IsFinite(y);
        }

        private static bool TryEval2(
            List<Node> nodes,
            Dictionary<int, Node> byId,
            HashSet<int> defined,
            int inputId0,
            int inputId1,
            int outputId,
            double x0,
            double x1,
            out double y,
            out string err)
        {
            y = 0;
            err = "";
            Dictionary<int, double> val = new Dictionary<int, double>();
            val[inputId0] = x0;
            if (inputId1 >= 0)
                val[inputId1] = x1;
            bool progress = true;
            int guard = 0;
            while (progress && guard < 64)
            {
                progress = false;
                guard++;
                for (int i = 0; i < nodes.Count; i++)
                {
                    Node node = nodes[i];
                    if (val.ContainsKey(node.Id))
                        continue;
                    bool ready = true;
                    for (int t = 0; t < node.Terms.Count; t++)
                    {
                        int src = node.Terms[t].Src;
                        if (!val.ContainsKey(src))
                        {
                            if (src == inputId0)
                                val[src] = x0;
                            else if (inputId1 >= 0 && src == inputId1)
                                val[src] = x1;
                            else if (!defined.Contains(src))
                                val[src] = 0;
                            else
                            {
                                ready = false;
                                break;
                            }
                        }
                    }
                    if (!ready) continue;
                    double z = node.Bias;
                    for (int t = 0; t < node.Terms.Count; t++)
                    {
                        Term term = node.Terms[t];
                        double ys = val[term.Src];
                        if (term.Kind == TermKind.CenteredSquare)
                        {
                            double c = ys - 0.5;
                            z += term.W * c * c;
                        }
                        else if (term.Kind == TermKind.UncenteredSquare)
                            z += term.W * ys * ys;
                        else
                            z += term.W * ys;
                    }
                    double outv = z;
                    if (node.IsSigmoid)
                    {
                        outv = NnMath.Sigmoid(z);
                        if (node.IsCentredSigmoid)
                            outv -= 0.5;
                    }
                    val[node.Id] = outv;
                    progress = true;
                }
            }
            if (!val.ContainsKey(outputId))
            {
                err = "output n" + outputId.ToString() + " unresolved";
                return false;
            }
            y = val[outputId];
            return NnMath.IsFinite(y);
        }

        private static void SkipSpaces(string s, ref int pos)
        {
            while (pos < s.Length && (s[pos] == ' ' || s[pos] == '\t'))
                pos++;
        }

        private static bool ReadNumber(string s, ref int pos, out double v)
        {
            v = 0;
            SkipSpaces(s, ref pos);
            int start = pos;
            if (pos < s.Length && (s[pos] == '+' || s[pos] == '-'))
                pos++;
            bool any = false;
            while (pos < s.Length && (char.IsDigit(s[pos]) || s[pos] == '.' || s[pos] == 'e' || s[pos] == 'E'))
            {
                if (s[pos] == 'e' || s[pos] == 'E')
                {
                    any = true;
                    pos++;
                    if (pos < s.Length && (s[pos] == '+' || s[pos] == '-'))
                        pos++;
                    continue;
                }
                any = true;
                pos++;
            }
            if (!any)
            {
                pos = start;
                return false;
            }
            string tok = s.Substring(start, pos - start);
            return double.TryParse(tok, NumberStyles.Float, CultureInfo.InvariantCulture, out v);
        }

        private static bool ReadInt(string s, ref int pos, out int v)
        {
            v = 0;
            SkipSpaces(s, ref pos);
            int start = pos;
            while (pos < s.Length && char.IsDigit(s[pos]))
                pos++;
            if (pos == start) return false;
            return int.TryParse(s.Substring(start, pos - start), NumberStyles.Integer, CultureInfo.InvariantCulture, out v);
        }

        private static bool MatchOpenClose(string s, int openIndex, out int closeIndex)
        {
            closeIndex = -1;
            if (openIndex < 0 || openIndex >= s.Length || s[openIndex] != '(')
                return false;
            int depth = 0;
            for (int i = openIndex; i < s.Length; i++)
            {
                if (s[i] == '(') depth++;
                else if (s[i] == ')')
                {
                    depth--;
                    if (depth == 0)
                    {
                        closeIndex = i;
                        return true;
                    }
                }
            }
            return false;
        }

        private sealed class FrozenNet
        {
            public int N;
            public int C;
            public int InId;
            public int InId1;
            public int OutId;
            public short[] Fun;
            public double[] Bias;
            public int[] Src;
            public int[] Dst;
            public double[] Wgt;

            public static FrozenNet Capture(VnnBpNet net)
            {
                FrozenNet f = new FrozenNet();
                f.N = net.maxNeurons;
                f.C = net.maxConnections;
                f.InId = net.inputs[0].sno;
                f.InId1 = net.maxInputs > 1 ? net.inputs[1].sno : -1;
                f.OutId = net.outputs[0].sno;
                f.Fun = new short[f.N];
                f.Bias = new double[f.N];
                for (int i = 0; i < f.N; i++)
                {
                    f.Fun[i] = net.neuron[i].funno;
                    f.Bias[i] = net.neuron[i].bias;
                }
                f.Src = new int[f.C];
                f.Dst = new int[f.C];
                f.Wgt = new double[f.C];
                for (int i = 0; i < f.C; i++)
                {
                    f.Src[i] = net.conn[i].srcNuNo;
                    f.Dst[i] = net.conn[i].dstNuNo;
                    f.Wgt[i] = net.conn[i].wgt;
                }
                return f;
            }

            public static FrozenNet Capture2D(VnnBpNet net)
            {
                return Capture(net);
            }

            public double Evaluate(double x)
            {
                return Evaluate2(x, 0);
            }

            public double Evaluate2(double x, double y)
            {
                double[] nuIn = new double[N];
                double[] nuOut = new double[N];
                bool[] done = new bool[N];
                for (int i = 0; i < N; i++)
                    nuIn[i] = Bias[i];
                nuIn[InId] += x;
                nuOut[InId] = NnMath.Activate(Fun[InId], nuIn[InId]);
                done[InId] = true;
                if (InId1 >= 0 && InId1 < N)
                {
                    nuIn[InId1] += y;
                    nuOut[InId1] = NnMath.Activate(Fun[InId1], nuIn[InId1]);
                    done[InId1] = true;
                }
                int lastSrc = -1;
                for (int c = 0; c < C; c++)
                {
                    int i = Src[c];
                    int j = Dst[c];
                    if (i < 0 || i >= N || j < 0 || j >= N)
                        continue;
                    if (lastSrc != i)
                    {
                        if (!done[i])
                        {
                            nuOut[i] = NnMath.Activate(Fun[i], nuIn[i]);
                            done[i] = true;
                        }
                        lastSrc = i;
                    }
                    nuIn[j] += Contrib(Fun[j], Wgt[c], nuOut[i]);
                }
                for (int m = 0; m < N; m++)
                {
                    if (!done[m])
                        nuOut[m] = NnMath.Activate(Fun[m], nuIn[m]);
                }
                nuOut[OutId] = NnMath.Activate(Fun[OutId], nuIn[OutId]);
                return nuOut[OutId];
            }

            private static double Contrib(short dstFun, double w, double y)
            {
                if (NnMath.UsesCenteredSquareInputs(dstFun))
                {
                    double c = y - NnMath.Centre;
                    return w * c * c;
                }
                if (NnMath.UsesUncenteredSquareInputs(dstFun))
                    return w * y * y;
                return w * y;
            }
        }
    }
}
