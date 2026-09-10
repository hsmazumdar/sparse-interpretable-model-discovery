using System;
using System.Collections.Generic;

namespace VnnBp.VnnBpLib
{
    /// <summary>
    /// Loss used for training / gradient calculation.
    /// </summary>
    public enum LossType
    {
        MeanSquaredError = 0,
        BinaryCrossEntropy = 1
    }

    /// <summary>
    /// Connection importance score for pruning.
    /// </summary>
    public enum PruningScoreType
    {
        AbsoluteWeight = 0,
        WeightTimesActivation = 1,
        FirstOrderTaylor = 2
    }

    /// <summary>
    /// Neuron activation type codes (preserve numeric file representation).
    /// funno 4 is historically named "sqrt" but implements quadratic / squared-input sigmoid.
    /// </summary>
    public enum NeuronFunNo : short
    {
        Nil = 0,
        Linear = 1,
        Sigmoid = 2,
        CentredSigmoid = 3,
        QuadraticSigmoid = 4, // legacy UI name: "sqrt"; z uses (y_i-0.5)^2, y=sigmoid(z)
        Delay1 = 5,
        DelayD = 6,
        QuadraticLinear = 7   // z = b + sum w y_i^2 (uncentered), y = z
    }

    public sealed class TrainingOptions
    {
        public double LearningRate = 0.01;
        public int Epochs = 1;
        public int RandomSeed = 42;
        public LossType LossType = LossType.MeanSquaredError;
        public bool ShuffleEachEpoch = true;
    }

    public sealed class PruningOptions
    {
        public double RelativeLossTolerance = 0.02;
        public double AbsoluteLossTolerance = 1e-8;
        public PruningScoreType ScoreType = PruningScoreType.AbsoluteWeight;
        public bool FineTuneAfterAcceptedPruning = true;
        /// <summary>
        /// Sample-update cycles after topology change (original algorithm: 10000 ReinforceConnections samples).
        /// Not full-dataset epochs.
        /// </summary>
        public int FineTuneEpochs = 10000;
        public bool FineTuneAfterPruning
        {
            get { return FineTuneAfterAcceptedPruning; }
            set { FineTuneAfterAcceptedPruning = value; }
        }
    }

    public sealed class EvaluationResult
    {
        public double Loss;
        public double MeanAbsoluteError;
        public double MeanSquaredError;
        public double Accuracy;
        public double Precision;
        public double Recall;
        public double F1Score;
        public int TruePositive;
        public int TrueNegative;
        public int FalsePositive;
        public int FalseNegative;
        public int SampleCount;
    }

    public sealed class NetworkValidationResult
    {
        public bool IsValid;
        public List<string> Messages = new List<string>();
    }

    public sealed class TrainingResult
    {
        public double TrainingLoss;
        public int EpochsCompleted;
        public bool ChangedParameters;
    }

    /// <summary>
    /// Numerically stable activations and shared math helpers.
    /// </summary>
    public static class NnMath
    {
        public const double EpsilonLoss = 1e-12;
        public const double Centre = 0.5;

        /// <summary>
        /// Numerically stable logistic sigmoid: y = 1 / (1 + exp(-x)).
        /// Does not overflow for large |x|.
        /// </summary>
        public static double Sigmoid(double x)
        {
            if (x >= 0.0)
            {
                double expNeg = Math.Exp(-x);
                return 1.0 / (1.0 + expNeg);
            }
            double expPos = Math.Exp(x);
            return expPos / (1.0 + expPos);
        }

        /// <summary>
        /// dy/dz for standard sigmoid when y = sigmoid(z).
        /// </summary>
        public static double SigmoidDerivativeFromOutput(double y)
        {
            return y * (1.0 - y);
        }

        /// <summary>
        /// Centred sigmoid: y = sigmoid(z) - 0.5.
        /// Correct dy/dz = (y + 0.5) * (0.5 - y) = sigmoid(z) * (1 - sigmoid(z)).
        /// Do not use y*(1-y) for centred sigmoid.
        /// </summary>
        public static double CentredSigmoidDerivativeFromOutput(double y)
        {
            return (y + Centre) * (Centre - y);
        }

        public static double BinaryCrossEntropy(double target, double prediction)
        {
            if (prediction < EpsilonLoss) prediction = EpsilonLoss;
            if (prediction > 1.0 - EpsilonLoss) prediction = 1.0 - EpsilonLoss;
            return -(target * Math.Log(prediction)
                + (1.0 - target) * Math.Log(1.0 - prediction));
        }

        public static bool IsFinite(double v)
        {
            return !double.IsNaN(v) && !double.IsInfinity(v);
        }

        /// <summary>
        /// Activation output from net input z, by funno.
        /// </summary>
        public static double Activate(short funno, double z)
        {
            switch (funno)
            {
                case 0:
                    return 0.0;
                case 1:
                case 5:
                case 6:
                case 7: // QuadraticLinear: identity on z
                    return z;
                case 2:
                case 4: // QuadraticSigmoid: y = sigmoid(z)
                    return Sigmoid(z);
                case 3:
                    return Sigmoid(z) - Centre;
                default:
                    return 0.0;
            }
        }

        /// <summary>
        /// dy/dz from stored output y, by funno.
        /// </summary>
        public static double DyDzFromOutput(short funno, double y)
        {
            switch (funno)
            {
                case 1:
                case 5:
                case 6:
                case 7:
                    return 1.0;
                case 2:
                case 4:
                    return SigmoidDerivativeFromOutput(y);
                case 3:
                    return CentredSigmoidDerivativeFromOutput(y);
                default:
                    return 0.0;
            }
        }

        public static double Clamp01(double v)
        {
            if (v < 0.0) return 0.0;
            if (v > 1.0) return 1.0;
            return v;
        }

        public static bool UsesCenteredSquareInputs(short funno)
        {
            return funno == 4;
        }

        public static bool UsesUncenteredSquareInputs(short funno)
        {
            return funno == 7;
        }

        public static bool IsSigmoidActivation(short funno)
        {
            return funno == 2 || funno == 3 || funno == 4;
        }
    }
}
