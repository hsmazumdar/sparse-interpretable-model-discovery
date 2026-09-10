using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.Collections;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace VnnBp.VnnBpLib
{
    public class VnnBpNeuron
    {
        //**********************************************************
        public double nuIn;
        public double nuOut;
        public double errin;
        public double errout;
        public double bias;
        public double biasTmp;
        public double biasPP;
        public short sno;  //serial number from input to output starting at 1
        public NuType ihono;//None=0, In=1, Out=2, Hdn=3
        public short funno; //Neuron activation function
        public byte state; //temp verible for update state
        public NETPOS netpos;
        public GRIDPOS gridPos;  //visual position on the grid
       // public short lyr; //in:0,lyr1:1,lyr2:2,.....,out:N
        //**********************************************************
        public VnnBpNeuron()
        {
        }
        //**********************************************************
        public void SetPosition(int posno, int xg, int yg)
        {
         sno=(short)posno;
         gridPos.X = (short)xg;
         gridPos.Y = (short)yg;
        }
        //**********************************************************
        public void SetType(int typ)
        {
            funno = (byte)typ;
        }
        //**********************************************************
        public void SetHiddenLayerNo(int hdnlyr)
        {
            netpos.lyrno = (byte)hdnlyr;
        }
        //**********************************************************
    }
}
