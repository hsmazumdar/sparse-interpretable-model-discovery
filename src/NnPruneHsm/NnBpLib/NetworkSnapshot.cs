using System;

namespace VnnBp.VnnBpLib
{
    /// <summary>
    /// Deep-copy snapshot of network topology and parameters for transactional pruning rollback.
    /// Does not store references to live neuron/connection objects.
    /// </summary>
    public sealed class NetworkSnapshot
    {
        public int MaxNeurons;
        public int MaxConnections;
        public int MaxInputs;
        public int MaxOutputs;
        public double Eta;
        public double Etab;
        public LossType LossType;
        public int[] NuLayers;

        public short[] NeuronFunNo;
        public NuType[] NeuronIho;
        public double[] NeuronBias;
        public double[] NeuronBiasTmp;
        public double[] NeuronBiasPP;
        public short[] NeuronSno;
        public short[] NeuronGridX;
        public short[] NeuronGridY;
        public short[] NeuronLyrNo;

        public short[] ConnSrc;
        public short[] ConnDst;
        public double[] ConnWgt;
        public double[] ConnWgtTmp;
        public double[] ConnWgtPP;
        public short[] ConnSrcTmp;
        public short[] ConnDstTmp;
        public short[] ConnSrcPP;
        public short[] ConnDstPP;

        public short[] InputSno;
        public short[] OutputSno;
    }
}
