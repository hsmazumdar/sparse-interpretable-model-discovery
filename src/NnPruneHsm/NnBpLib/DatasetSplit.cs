using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace VnnBp.VnnBpLib
{
    /// <summary>
    /// Deterministic stratified train / validation / test split (default 70/15/15).
    /// Pruning and early-stopping must use validation only; never the test set.
    /// </summary>
    public sealed class DatasetSplit
    {
        public double[][] TrainIn;
        public double[][] TrainOut;
        public double[][] ValIn;
        public double[][] ValOut;
        public double[][] TestIn;
        public double[][] TestOut;
        public int RandomSeed;
        public double TrainFraction;
        public double ValFraction;
        public double TestFraction;

        public static DatasetSplit Create(
            double[][] inData,
            double[][] outData,
            int randomSeed,
            double trainFraction,
            double valFraction,
            double testFraction)
        {
            if (inData == null) throw new ArgumentNullException("inData");
            if (outData == null) throw new ArgumentNullException("outData");
            if (inData.Length != outData.Length)
                throw new ArgumentException("inData and outData length mismatch.");
            if (inData.Length == 0)
                throw new ArgumentException("Dataset is empty.");

            double sum = trainFraction + valFraction + testFraction;
            if (Math.Abs(sum - 1.0) > 1e-9)
                throw new ArgumentException("Split fractions must sum to 1.0.");

            DatasetSplit split = new DatasetSplit();
            split.RandomSeed = randomSeed;
            split.TrainFraction = trainFraction;
            split.ValFraction = valFraction;
            split.TestFraction = testFraction;

            // Stratify by first output threshold 0.5 when possible
            List<int> pos = new List<int>();
            List<int> neg = new List<int>();
            for (int i = 0; i < inData.Length; i++)
            {
                double t = (outData[i] != null && outData[i].Length > 0) ? outData[i][0] : 0.0;
                if (t >= 0.5) pos.Add(i);
                else neg.Add(i);
            }

            Random rnd = new Random(randomSeed);
            Shuffle(pos, rnd);
            Shuffle(neg, rnd);

            List<int> trainIdx = new List<int>();
            List<int> valIdx = new List<int>();
            List<int> testIdx = new List<int>();
            AssignStratified(pos, trainFraction, valFraction, trainIdx, valIdx, testIdx);
            AssignStratified(neg, trainFraction, valFraction, trainIdx, valIdx, testIdx);

            // Tiny datasets: ensure non-empty train; allow empty val/test by borrowing
            if (trainIdx.Count == 0 && inData.Length > 0)
                trainIdx.Add(0);
            if (valIdx.Count == 0 && trainIdx.Count > 1)
            {
                valIdx.Add(trainIdx[trainIdx.Count - 1]);
                trainIdx.RemoveAt(trainIdx.Count - 1);
            }
            if (testIdx.Count == 0 && trainIdx.Count > 1)
            {
                testIdx.Add(trainIdx[trainIdx.Count - 1]);
                trainIdx.RemoveAt(trainIdx.Count - 1);
            }
            // If still empty val, reuse train for pruning eval (documented fallback)
            if (valIdx.Count == 0)
                valIdx.AddRange(trainIdx);
            if (testIdx.Count == 0)
                testIdx.AddRange(valIdx);

            split.TrainIn = Slice(inData, trainIdx);
            split.TrainOut = Slice(outData, trainIdx);
            split.ValIn = Slice(inData, valIdx);
            split.ValOut = Slice(outData, valIdx);
            split.TestIn = Slice(inData, testIdx);
            split.TestOut = Slice(outData, testIdx);
            return split;
        }

        public static DatasetSplit CreateDefault(double[][] inData, double[][] outData, int randomSeed)
        {
            return Create(inData, outData, randomSeed, 0.70, 0.15, 0.15);
        }

        private static void AssignStratified(
            List<int> group,
            double trainFraction,
            double valFraction,
            List<int> trainIdx,
            List<int> valIdx,
            List<int> testIdx)
        {
            int n = group.Count;
            int nTrain = (int)Math.Floor(n * trainFraction);
            int nVal = (int)Math.Floor(n * valFraction);
            int nTest = n - nTrain - nVal;
            if (nTrain + nVal + nTest != n)
                nTest = n - nTrain - nVal;
            int i = 0;
            for (int k = 0; k < nTrain && i < n; k++, i++)
                trainIdx.Add(group[i]);
            for (int k = 0; k < nVal && i < n; k++, i++)
                valIdx.Add(group[i]);
            for (; i < n; i++)
                testIdx.Add(group[i]);
        }

        private static void Shuffle(List<int> list, Random rnd)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = rnd.Next(i + 1);
                int tmp = list[i];
                list[i] = list[j];
                list[j] = tmp;
            }
        }

        private static double[][] Slice(double[][] src, List<int> idx)
        {
            double[][] dst = new double[idx.Count][];
            for (int i = 0; i < idx.Count; i++)
                dst[i] = src[idx[i]];
            return dst;
        }
    }

    /// <summary>
    /// Research reproducibility CSV logging (invariant culture).
    /// </summary>
    public sealed class ResearchLogger
    {
        private readonly string _folder;
        private readonly string _trainingPath;
        private readonly string _pruningPath;
        private bool _trainingHeaderWritten;
        private bool _pruningHeaderWritten;

        public ResearchLogger(string folder)
        {
            if (string.IsNullOrEmpty(folder))
                folder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
            _folder = folder;
            if (!Directory.Exists(_folder))
                Directory.CreateDirectory(_folder);
            _trainingPath = Path.Combine(_folder, "training_history.csv");
            _pruningPath = Path.Combine(_folder, "pruning_history.csv");
        }

        public string Folder { get { return _folder; } }

        public void LogTrainingEpoch(
            int epoch,
            double trainingLoss,
            double validationLoss,
            double trainingAccuracy,
            double validationAccuracy,
            int activeNeurons,
            int activeConnections,
            double learningRate,
            int randomSeed)
        {
            if (!_trainingHeaderWritten)
            {
                WriteLine(_trainingPath,
                    "epoch,training_loss,validation_loss,training_accuracy,validation_accuracy,active_neurons,active_connections,learning_rate,random_seed");
                _trainingHeaderWritten = true;
            }
            WriteLine(_trainingPath, string.Format(CultureInfo.InvariantCulture,
                "{0},{1:G15},{2:G15},{3:G15},{4:G15},{5},{6},{7:G15},{8}",
                epoch, trainingLoss, validationLoss, trainingAccuracy, validationAccuracy,
                activeNeurons, activeConnections, learningRate, randomSeed));
        }

        public void LogPruningAttempt(
            int attempt,
            int batchSize,
            int connectionsBefore,
            int connectionsAfter,
            double validationLossBefore,
            double validationLossAfter,
            bool accepted,
            int fineTuneEpochs)
        {
            if (!_pruningHeaderWritten)
            {
                WriteLine(_pruningPath,
                    "attempt,batch_size,active_connections_before,active_connections_after,validation_loss_before,validation_loss_after,relative_loss_change,accepted,fine_tuning_epochs");
                _pruningHeaderWritten = true;
            }
            double rel = 0;
            if (Math.Abs(validationLossBefore) > 1e-15)
                rel = (validationLossAfter - validationLossBefore) / Math.Abs(validationLossBefore);
            WriteLine(_pruningPath, string.Format(CultureInfo.InvariantCulture,
                "{0},{1},{2},{3},{4:G15},{5:G15},{6:G15},{7},{8}",
                attempt, batchSize, connectionsBefore, connectionsAfter,
                validationLossBefore, validationLossAfter, rel,
                accepted ? 1 : 0, fineTuneEpochs));
        }

        public void ExportFinalNetwork(VnnBpNet net)
        {
            if (net == null) return;
            string connPath = Path.Combine(_folder, "final_network_connections.csv");
            string nuPath = Path.Combine(_folder, "final_network_neurons.csv");
            string eqPath = Path.Combine(_folder, "final_network_equation.txt");

            StringBuilder cb = new StringBuilder();
            cb.AppendLine("index,src,dst,weight");
            for (int i = 0; i < net.maxConnections; i++)
            {
                cb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                    "{0},{1},{2},{3:G15}",
                    i, net.conn[i].srcNuNo, net.conn[i].dstNuNo, net.conn[i].wgt));
            }

            StringBuilder nb = new StringBuilder();
            nb.AppendLine("index,funno,ihono,bias");
            for (int i = 0; i < net.maxNeurons; i++)
            {
                nb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                    "{0},{1},{2},{3:G15}",
                    i, net.neuron[i].funno, (int)net.neuron[i].ihono, net.neuron[i].bias));
            }
            WriteAllTextSafe(connPath, cb.ToString());
            WriteAllTextSafe(nuPath, nb.ToString());
            WriteAllTextSafe(eqPath, net.ExportEquation());
        }

        private static void WriteLine(string path, string line)
        {
            for (int attempt = 0; attempt < 8; attempt++)
            {
                try
                {
                    using (StreamWriter sw = new StreamWriter(path, true, Encoding.UTF8))
                    {
                        sw.WriteLine(line);
                    }
                    return;
                }
                catch (IOException)
                {
                    System.Threading.Thread.Sleep(50 * (attempt + 1));
                }
                catch (UnauthorizedAccessException)
                {
                    System.Threading.Thread.Sleep(50 * (attempt + 1));
                }
            }
            // Last resort: do not crash the experiment over a log line.
            try
            {
                string alt = path + ".retry.txt";
                using (StreamWriter sw = new StreamWriter(alt, true, Encoding.UTF8))
                    sw.WriteLine(line);
            }
            catch
            {
            }
        }

        private static void WriteAllTextSafe(string path, string contents)
        {
            for (int attempt = 0; attempt < 8; attempt++)
            {
                try
                {
                    File.WriteAllText(path, contents);
                    return;
                }
                catch (IOException)
                {
                    System.Threading.Thread.Sleep(50 * (attempt + 1));
                }
                catch (UnauthorizedAccessException)
                {
                    System.Threading.Thread.Sleep(50 * (attempt + 1));
                }
            }
        }
    }

    /// <summary>
    /// Optional legacy-vs-corrected comparison configuration.
    /// </summary>
    public sealed class ComparisonOptions
    {
        public bool UseLegacyMath;
        public int RandomSeed = 42;
        public int Epochs = 10;
    }
}
