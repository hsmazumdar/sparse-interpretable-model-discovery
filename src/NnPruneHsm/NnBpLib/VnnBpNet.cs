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
    public class VnnBpNet
    {
        //**********************************************************
        Random rnd = new Random(DateTime.Now.Millisecond);
        public TrainThisNet trainthisnet = null;
        public ImageIO imageio = null;
        public string netName = "";
        public string netDetail = "No Details";
        public VnnBpNeuron[] neuron;
        public int maxNeurons;
        public int maxNeuronsTmp;
        public int maxNeuronsPP;
        public int maxPos;
        public VnnBpConnection[] conn;
        public int maxConnections;
        public int maxConnectionsTmp;
        public int maxConnectionsPP;
        public INOUT[] inputs; //Input to input of Input Neurons
        public int maxInputs;
        public INOUT[] outputs;//Desired output
        public int maxOutputs;
        public int[] nuLayers;//0=inMax, 1=outMax, 2=lyr1, 3=lyr2, ..n=lyrN
        public string connType = "";
        public int connTypeNo = 0;
        public double eta = 0.01;
        public double etab = 0.01;
        /// <summary>Loss for gradient updates. Default MSE preserves legacy training behaviour.</summary>
        public LossType lossType = LossType.MeanSquaredError;
        public static string[] ihoName = new string[] { "in", "out", "hdn" };//ihono=> in=0, out=1, hidden=2
        public static string[] funName = new string[] { "nil", "y=x", "exp+", "exp+-", "sqrt", "dly=1", "dly=d", "sq" }; // "sqrt"=QuadraticSigmoid (4); "sq"=QuadraticLinear (7)
        public static string[] connTypeName = new string[] { "No Conn", "In Layer Conn", "All Layer Conn", "Fully Conn", "Partly Conn" };
        public bool plotUpdate = false;
        public bool newUpdate = false;
        public int intErr = 0;
        public double lastErr = 0;
        public Bitmap bm1 = null;
        public int tp, tn, fp, fn;
        public Point pos;
        //**********************************************************
        public VnnBpNet()
        {   //Creating a net with this constructor will
            // create a net with a input neuron, a output neuron and a connection
            string configStr = "1,1,1";
            SetConfigData2(configStr);
            pos = new Point(0, 0);
        }
        //**********************************************************
        public VnnBpNet(string configStr)
        {  //configStr contains=>"maxIn, maxOut, connType, layer1, layer2, ..."
            if (configStr.Contains(" Conn"))
            {
                SetConfigData2(configStr);
            }
            else
            {
                SetConfigData(configStr);
            }
        }
        //**********************************************************
        public VnnBpNet(string configStr, ArrayList dt)
        {  //configStr contains=>"maxIn, maxOut, connType, layer1, layer2, ..."
            //dt contains=> neuron.bias and connecioon values

            LoadNet(dt);
        }
        //**********************************************************
        public VnnBpNeuron[] GetNrurons()
        {  
            return neuron;
            //DeleteAllConnections();
        }
        //**********************************************************
        public bool SetConfigData2(string configStr)
        {  //configStr contains=>"maxIn, maxOut, connType, layer1, layer2, ..."

            if (configStr == "")
                return false;
            string[] wrds = configStr.Split(',');
            if (wrds.Length < 2)
                return false;
            netName = wrds[0];
            maxInputs = System.Convert.ToInt32(wrds[1]);
            maxOutputs = System.Convert.ToInt32(wrds[2]);
            inputs = new INOUT[maxInputs];
            outputs = new INOUT[maxOutputs];
            connType = wrds[3];
            int p = 2;
            if (wrds[4] == "0")
                p++;
            if (wrds[4] == "")
                p++;
            nuLayers = new int[wrds.Length - p];
            maxNeurons = 0;
            nuLayers[0] = System.Convert.ToInt32(wrds[1]);//input
            maxNeurons += nuLayers[0];
            nuLayers[nuLayers.Length - 1] = System.Convert.ToInt32(wrds[2]);//output
            maxNeurons += nuLayers[nuLayers.Length - 1];
            int j = 1;
            if (wrds[4] == "")
                wrds[4] = "0";
            int hl = System.Convert.ToInt32(wrds[4]);
            if (hl > 0)
            {
                for (int i = 4; i < wrds.Length; i++)
                {
                    nuLayers[j] = System.Convert.ToInt32(wrds[i]);
                    maxNeurons += nuLayers[j];
                    j++;
                }
            }
            neuron = new VnnBpNeuron[maxNeurons];

            //byte ihono;  //in=0, out=1, hidden=2
            short sNo = 1;
            int s = 0;
            int inNo = maxInputs;
            int outNo = maxOutputs;
            short rowNo = 0;
            short colNo = 0;
            for (int i = 0; i < inNo; i++)
            {
                neuron[s] = new VnnBpNeuron();
                neuron[s].sno = sNo;
                neuron[s].ihono = VnnBp.VnnBpLib.NuType.In;//in=0
                neuron[s].funno = 1;//non=> output=input 
                neuron[s].netpos.rowno = rowNo;
                neuron[s].netpos.lyrno = colNo;
                neuron[s].bias = 0; //Hsm 25072023
                //neuron[s].bias = (rnd.NextDouble() - 0.5) * 2.0;
                rowNo++;
                sNo++;
                s++;
            }
            colNo++;
            if (nuLayers[1] > 0)
            {
                for (int i = 1; i < nuLayers.Length - 1; i++)
                {
                    rowNo = 0;
                    for (int k = 0; k < nuLayers[i]; k++)
                    {
                        neuron[s] = new VnnBpNeuron();
                        neuron[s].sno = sNo;
                        neuron[s].ihono = VnnBp.VnnBpLib.NuType.Hdn;//hidden=2
                        neuron[s].funno = 2;//exp
                        neuron[s].netpos.rowno = rowNo;
                        neuron[s].netpos.lyrno = colNo;
                        neuron[s].bias = (rnd.NextDouble() - 0.5) * 2.0;
                        rowNo++;
                        sNo++;
                        s++;
                    }
                    colNo++;
                }
            }
            rowNo = 0;
            for (int i = 0; i < outNo; i++)
            {
                neuron[s] = new VnnBpNeuron();
                neuron[s].sno = sNo;
                neuron[s].ihono = VnnBp.VnnBpLib.NuType.Out;//out=1
                neuron[s].funno = 2;//exp
                neuron[s].netpos.rowno = rowNo;
                neuron[s].netpos.lyrno = colNo;
                neuron[s].bias = (rnd.NextDouble() - 0.5) * 2.0;
                sNo++;
                rowNo++;
                s++;
            }
            switch (connType)
            {
                case "No Conn"://No Conn
                     connTypeNo = 0;
                    break;
                case "In Layer Conn"://In Layer Conn
                    InLayerConnected(nuLayers);
                    connTypeNo = 1;
                    break;
                case "In2 Layer Conn"://In2 Layer Conn
                    In2LayerConnected(nuLayers);
                    connTypeNo = 2;
                    break;
                case "All Layer Conn"://All Layer Conn
                    AllLayerConnected(nuLayers);
                    connTypeNo = 3;
                    break;
                case "Fully Conn"://Fully Conn
                    FullyConnected(nuLayers);
                    connTypeNo = 4;
                    break;
                case "Partly Conn"://Partly Conn
                    PartiallyConnected(nuLayers);
                    connTypeNo = 5;
                    break;
                default:
                    connTypeNo = System.Convert.ToInt32(connType);
                    switch (connTypeNo)
                    {
                        case 0://No Conn
                            break;
                        case 1://In Layer Conn
                            InLayerConnected(nuLayers);
                            break;
                        case 2://In Layer Conn
                            In2LayerConnected(nuLayers);
                            break;
                        case 3://All Layer Conn
                            AllLayerConnected(nuLayers);
                            break;
                        case 4://Fully Conn
                            FullyConnected(nuLayers);
                            break;
                        case 5://Partly Conn
                            PartiallyConnected(nuLayers);
                            break;
                    }
                    break;
            }
            MapInputOutputLookupTables();
            return true;
        }
        //**********************************************************
        public bool SetConfigData(string configStr)
        {  //configStr contains=>"maxIn, maxOut, maxConnections, layer1, layer2, ..."

            if (configStr == "")
                return false;
            string[] wrds = configStr.Split(',');
            if (wrds.Length < 2)
                return false;
            netName = wrds[0];
            maxInputs = System.Convert.ToInt32(wrds[1]);
            maxOutputs = System.Convert.ToInt32(wrds[2]);
            inputs = new INOUT[maxInputs];
            outputs = new INOUT[maxOutputs];
           // connType = wrds[3];
            connType = "Partly Conn";//Partially Connected*******************************************************************??????
            connTypeNo = 4;
            maxConnections = int.Parse(wrds[3]);
            int p = 2;
            if (wrds[4] == "0")
                p++;
            if (wrds[4] == "")
                p++;
            nuLayers = new int[wrds.Length - p];
            maxNeurons = 0;
            nuLayers[0] = System.Convert.ToInt32(wrds[1]);//input
            maxNeurons += nuLayers[0];
            nuLayers[nuLayers.Length - 1] = System.Convert.ToInt32(wrds[2]);//output
            maxNeurons += nuLayers[nuLayers.Length - 1];
            int j = 1;
            if (wrds[4] == "")
                wrds[4] = "0";
            int hl = System.Convert.ToInt32(wrds[4]);
            if (hl > 0)
            {
                for (int i = 4; i < wrds.Length; i++)
                {
                    nuLayers[j] = System.Convert.ToInt32(wrds[i]);
                    maxNeurons += nuLayers[j];
                    j++;
                }
            }
            neuron = new VnnBpNeuron[maxNeurons];

            //byte ihono;  //in=0, out=1, hidden=2
            short sNo = 1;
            int s = 0;
            int inNo = maxInputs;
            int outNo = maxOutputs;
            short rowNo = 0;
            short colNo = 0;
            for (int i = 0; i < inNo; i++)
            {
                neuron[s] = new VnnBpNeuron();
                neuron[s].sno = sNo;
                neuron[s].ihono = VnnBp.VnnBpLib.NuType.In;//in=0
                neuron[s].funno = 1;//non=> output=input 
                neuron[s].netpos.rowno = rowNo;
                neuron[s].netpos.lyrno = colNo;
                neuron[s].bias = 0;
                //neuron[s].bias = (rnd.NextDouble() - 0.5) * 2.0;//Hsm 25072023
                rowNo++;
                sNo++;
                s++;
            }
            colNo++;
            if (nuLayers[1] > 0)
            {
                for (int i = 1; i < nuLayers.Length - 1; i++)
                {
                    rowNo = 0;
                    for (int k = 0; k < nuLayers[i]; k++)
                    {
                        neuron[s] = new VnnBpNeuron();
                        neuron[s].sno = sNo;
                        neuron[s].ihono = VnnBp.VnnBpLib.NuType.Hdn;//hidden=2
                        neuron[s].funno = 2;//exp
                        neuron[s].netpos.rowno = rowNo;
                        neuron[s].netpos.lyrno = colNo;
                        neuron[s].bias = (rnd.NextDouble() - 0.5) * 2.0;
                        rowNo++;
                        sNo++;
                        s++;
                    }
                    colNo++;
                }
            }
            rowNo = 0;
            for (int i = 0; i < outNo; i++)
            {
                neuron[s] = new VnnBpNeuron();
                neuron[s].sno = sNo;
                neuron[s].ihono = VnnBp.VnnBpLib.NuType.Out;//out=1
                neuron[s].funno = 2;//exp
                neuron[s].netpos.rowno = rowNo;
                neuron[s].netpos.lyrno = colNo;
                neuron[s].bias = (rnd.NextDouble() - 0.5) * 2.0;
                sNo++;
                rowNo++;
                s++;
            }
            switch (connType)
            {
                case "No Conn"://No Conn
                    connTypeNo = 0;
                    break;
                case "In Layer Conn"://In Layer Conn
                    InLayerConnected(nuLayers);
                    connTypeNo = 1;
                    break;
                case "In2 Layer Conn"://In Layer Conn
                    In2LayerConnected(nuLayers);
                    connTypeNo = 2;
                    break;
                case "All Layer Conn"://All Layer Conn
                    AllLayerConnected(nuLayers);
                    connTypeNo = 3;
                    break;
                case "Fully Conn"://Fully Conn
                    FullyConnected(nuLayers);
                    connTypeNo = 4;
                    break;
                case "Partly Conn"://Pa
                    PartiallyConnected(nuLayers);
                    connTypeNo = 5;
                    break;
                default:
                    connTypeNo = System.Convert.ToInt32(connType);
                    switch (connTypeNo)
                    {
                        case 0://No Conn
                            break;
                        case 1://In Layer Conn
                            InLayerConnected(nuLayers);
                            break;
                        case 2://In Layer Conn
                            In2LayerConnected(nuLayers);
                            break;
                        case 3://All Layer Conn
                            AllLayerConnected(nuLayers);
                            break;
                        case 4://Fully Conn
                            FullyConnected(nuLayers);
                            break;
                        case 5://Partly Conn
                            PartiallyConnected(nuLayers);
                            break;
                    }
                    break;
            }
            MapInputOutputLookupTables();
            return true;
        }
        //**********************************************************
        private void PartiallyConnected(int[] lyr)
        {
            conn = new VnnBpConnection[maxConnections];
            return;

            int sNo = 0;
            short lyrsum = 0;
            short lyrsumOld = 0;
            ArrayList al = new ArrayList();
            for (short i = 1; i < lyr.Length; i++)
            {
                lyrsum += (short)nuLayers[i - 1];
                for (short k = 0; k < (short)lyr[i]; k++)
                {
                    for (short j = 0; j < (short)lyr[i - 1]; j++)
                    {
                        VnnBpConnection cn = new VnnBpConnection();
                        cn.srcNuNo = (short)(j + lyrsumOld);
                        cn.dstNuNo = (short)(k + lyrsum);
                        cn.wgt = (rnd.NextDouble() - 0.5) * 2.0;
                        sNo++;
                        al.Add(cn);
                    }
                }
                lyrsumOld = lyrsum;
            }

            //TBD:
          //  maxConnections = sNo;
            conn = new VnnBpConnection[maxConnections];
            return;

            for (int i = 0; i < maxConnections; i++)
            {
                conn[i] = (VnnBpConnection)al[i];
            }
        }
        //**********************************************************
        public bool LoadNet(ArrayList dt)
        {  //
            if (dt == null)
                return false;
            if (dt.Count == 0)
                return false;
            int n1=0;
            ArrayList hdnLst = new ArrayList();
            string[] wrds;
            for (; ; )
            {
                wrds = dt[n1].ToString().Split(',');
                bool exitFlg = false;
                if (wrds[0].Length > 3)
                {
                    if (wrds[0].Substring(0, 3).Contains("hdn"))
                        wrds[0] = wrds[0].Substring(0, 3);
                }
                switch (wrds[0])
                {
                    case "in":
                    case "In":
                        maxInputs = System.Convert.ToInt32(wrds[1]);
                        break;
                    case "out":
                    case "Out":
                        maxOutputs = System.Convert.ToInt32(wrds[1]);
                        break;
                    case "hdn":
                    case "Hdn":
                        hdnLst.Add(System.Convert.ToInt32(wrds[1]));
                        break;
                    case "typ":
                        connTypeNo = System.Convert.ToInt32(wrds[1]);
                        connType = connTypeName[connTypeNo];
                        break;
                    case "con":
                        maxConnections = System.Convert.ToInt32(wrds[1]);
                        break;
                    case "pos":
                        maxPos = System.Convert.ToInt32(wrds[1]);
                        break;
                    default:
                        exitFlg = true;   
                        break;
                }
                if (exitFlg)
                    break;
                n1++;
            }
            inputs = new INOUT[maxInputs];
            outputs = new INOUT[maxOutputs];

            nuLayers = new int[2+hdnLst.Count];
            maxNeurons = 0;

            nuLayers[0] = maxInputs;//input
            maxNeurons += nuLayers[0];
            nuLayers[nuLayers.Length - 1] = maxOutputs;//output
            maxNeurons += nuLayers[nuLayers.Length - 1];
            int j = 1;

            for (int i = 0; i < hdnLst.Count; i++)
            {
                nuLayers[j] = System.Convert.ToInt32(hdnLst[i].ToString());
                 maxNeurons += nuLayers[j];
                j++;
            }
            neuron = new VnnBpNeuron[maxNeurons];

            for (int i = 0; i < maxNeurons; i++)
            {
                string[] w2 = dt[n1].ToString().Split(',');
                neuron[i] = new VnnBpNeuron();
                neuron[i].sno = System.Convert.ToInt16(w2[0].ToString());//sno
                switch (w2[1].ToString())
                {
                    case "in":
                    case "In":
                        neuron[i].ihono = NuType.In;
                        break;
                    case "out":
                    case "Out":
                        neuron[i].ihono = NuType.Out;
                        break;
                    case "hdn":
                    case "Hdn":
                        neuron[i].ihono = NuType.Hdn;
                        break;
                }
                switch (w2[2].ToString())
                {
                    case "nil":
                        neuron[i].funno = 0;
                        break;
                    case "y=x":
                        neuron[i].funno = 1;
                        break;
                    case "exp+":
                        neuron[i].funno = 2;
                        break;
                    case "exp+-":
                        neuron[i].funno = 3;
                        break;
                    case "sqrt":
                        neuron[i].funno = 4;
                        break;
                    case "dly=1":
                        neuron[i].funno = 5;
                        break;
                    case "dly=d":
                        neuron[i].funno = 6;
                        break;
                }
                neuron[i].netpos.rowno = System.Convert.ToInt16(w2[3].ToString());//rowno
                neuron[i].netpos.lyrno = System.Convert.ToInt16(w2[4].ToString());//lyrno
                neuron[i].bias = System.Convert.ToDouble(w2[5].ToString());//bias
                n1++;
            }
            conn = new VnnBpConnection[maxConnections];
            for (int i = 0; i < maxConnections; i++)
            {
                if (n1 >= dt.Count)
                    break;
                conn[i] = new VnnBpConnection();
                string[] w2 = dt[n1].ToString().Split(',');
                int sno = System.Convert.ToInt16(w2[0].ToString());//sno
                conn[i].srcNuNo = System.Convert.ToInt16(w2[1].ToString());//src
                conn[i].dstNuNo = System.Convert.ToInt16(w2[2].ToString());//dst
                conn[i].wgt = System.Convert.ToDouble(w2[3].ToString());//Weight
                n1++;
            }

            for (int i = 0; i < neuron.Length; i++)
            {
                if (n1 >= dt.Count)
                    break;
                string[] w2 = dt[n1].ToString().Split(',');
                int sno = System.Convert.ToInt16(w2[0].ToString());//sno
                neuron[i].gridPos.X = System.Convert.ToInt16(w2[1].ToString());//X
                neuron[i].gridPos.Y = System.Convert.ToInt16(w2[2].ToString());//Y
                n1++;
            }
            return true;
        }
        //******************************************************************
        public void InLayerConnected(int[] lyr)
        {
            int sNo = 0;
            short lyrsum = 0;
            short lyrsumOld = 0;
            ArrayList al = new ArrayList();
            for (short i = 1; i < lyr.Length; i++)
            {
                lyrsum += (short)nuLayers[i - 1];
                for (short k = 0; k < (short)lyr[i]; k++)
                {
                    for (short j = 0; j < (short)lyr[i - 1]; j++)
                    {
                        VnnBpConnection cn = new VnnBpConnection();
                        cn.srcNuNo = (short)(j + lyrsumOld);
                        cn.dstNuNo = (short)(k + lyrsum);
                        cn.wgt = (rnd.NextDouble() - 0.5) * 2.0;
                        sNo++;
                        al.Add(cn);
                    }
                }
                lyrsumOld = lyrsum;
            }
            maxConnections = sNo;
            conn = new VnnBpConnection[maxConnections];
            for (int i = 0; i < maxConnections; i++)
            {
                conn[i] = (VnnBpConnection)al[i];
            }
        }
        //**********************************************************
        public void In2LayerConnected(int[] lyr)
        {//24.01.2018 Alternate layer connected 
            //eg 0-1;0-2:1-2;1-3:2-3:
            int sNo = 0;
            short lyrsum = 0;
            short lyrsumOld = 0;
            ArrayList al = new ArrayList();
            for (short i = 1; i < lyr.Length; i++)
            {
                lyrsum += (short)nuLayers[i - 1];
                for (short k = 0; k < (short)lyr[i]; k++)
                {
                    for (short j = 0; j < (short)lyr[i - 1]; j++)
                    {
                        VnnBpConnection cn = new VnnBpConnection();
                        cn.srcNuNo = (short)(j + lyrsumOld);
                        cn.dstNuNo = (short)(k + lyrsum);
                        cn.wgt = (rnd.NextDouble() - 0.5) * 2.0;
                        sNo++;
                        al.Add(cn);
                    }
                }
                lyrsumOld = lyrsum;
            }



            lyrsum = 0;
            lyrsumOld = 0;
            lyrsum = (short)nuLayers[0];
            for (short i = 2; i < lyr.Length; i++)
            {
                lyrsum += (short)nuLayers[i-1];
                for (short k = 0; k < (short)lyr[i]; k++)
                {
                    for (short j = 0; j < (short)lyr[i - 2]; j++)
                    {
                        VnnBpConnection cn = new VnnBpConnection();
                        cn.srcNuNo = (short)(j + lyrsumOld);
                        cn.dstNuNo = (short)(k + lyrsum);
                        cn.wgt = (rnd.NextDouble() - 0.5) * 2.0;
                        sNo++;
                        al.Add(cn);
                    }
                }
                lyrsumOld += (short)nuLayers[i - 2];
            }

            maxConnections = sNo;
            conn = new VnnBpConnection[maxConnections];
            for (int i = 0; i < maxConnections; i++)
            {
                conn[i] = (VnnBpConnection)al[i];
            }
        }
        //******************************************************************
        public void AllLayerConnected(int[] lyr)
        {
            int nuMax = 0;
            for (int i = 0; i < lyr.Length; i++)
            {
                nuMax += lyr[i];
            }

            int sNo = 0;
            ArrayList al = new ArrayList();
            for (int i = lyr[0]; i < nuMax; i++)
            {
                int lyrsum = 0;
                for (int j = 0; j < lyr.Length - 1; j++)
                {
                    if (lyrsum >= i)
                        break;
                    for (int k = 0; k < lyr[j]; k++)
                    {
                        if (lyrsum + lyr[j] > i)
                        {
                            break;
                        }
                        VnnBpConnection cn = new VnnBpConnection();
                        cn.srcNuNo = (short)(k + lyrsum);
                        cn.dstNuNo = (short)(i);
                        cn.wgt = (rnd.NextDouble() - 0.5) * 2.0;
                        sNo++;
                        al.Add(cn);
                    }
                    lyrsum += lyr[j];
                }
            }
            maxConnections = sNo;
            conn = new VnnBpConnection[maxConnections];
            for (int i = 0; i < maxConnections; i++)
            {
                conn[i] = (VnnBpConnection)al[i];
            }
        }
        //******************************************************************
        public void FullyConnected(int[] lyr)
        {
            int nuMax = 0;
            for (int i = 0; i < lyr.Length; i++)
            {
                nuMax += lyr[i];
            }

            int sNo = 0;
            ArrayList al = new ArrayList();
            for (int i = 0; i < nuMax - 1; i++)
            {
                for (int j = i + 1; j < nuMax; j++)
                {
                    if (j >= lyr[0])
                    {
                        VnnBpConnection cn = new VnnBpConnection();
                        cn.srcNuNo = (short)i;
                        cn.dstNuNo = (short)j;
                        cn.wgt = (rnd.NextDouble() - 0.5) * 2.0;
                        sNo++;
                        al.Add(cn);
                    }
                }
            }
            maxConnections = sNo;
            conn = new VnnBpConnection[maxConnections];
            for (int i = 0; i < maxConnections; i++)
            {
                conn[i] = (VnnBpConnection)al[i];
            }
        }
        //******************************************************************
        public void partlyConnected(int[] lyr)
        {
        }
        //******************************************************************
        //public void SetNeuronParameters(int sno, int posno, int xpos, int ypos, byte type, byte hlyr)
        //{
        //    neuron[sno].SetPosition(posno, xpos, ypos);
        //    neuron[sno].SetType(type);
        //    neuron[sno].SetHiddenLayerNo(hlyr);
        //}
        //**********************************************************
        public void MapInputOutputLookupTables()
        {
            maxInputs = 0;
            maxOutputs = 0;
            for (int n = 0; n < maxNeurons; n++)
            {
                if (neuron[n].ihono == VnnBp.VnnBpLib.NuType.In) maxInputs++;
                if (neuron[n].ihono == VnnBp.VnnBpLib.NuType.Out) maxOutputs++;
            }
            inputs = new INOUT[maxInputs];
            outputs = new INOUT[maxOutputs];
            int[] npl = new int[neuron[neuron.Length - 1].netpos.lyrno + 1];
            int i = 0;
            int j = 0;
            for (int n = 0; n < maxNeurons; n++)
            {
                if (neuron[n].ihono == VnnBp.VnnBpLib.NuType.In)
                {//Input Neuron
                    inputs[i].sno = (short)n;
                    i++;
                }
                if (neuron[n].ihono == VnnBp.VnnBpLib.NuType.Out)
                {//Output Neuron
                    outputs[j].sno = (short)n;
                    j++;
                }
                npl[neuron[n].netpos.lyrno]++;
            }

        }
        //**********************************************************
        public void SetRandomWeights()
        {
            int n;
            for (n = 0; n < maxNeurons; n++)
            {
                if (neuron[n].ihono == VnnBp.VnnBpLib.NuType.In)
                {
                    neuron[n].bias = 0.0;
                    //neuron[n].bias = (0.5 - (double)rnd.NextDouble()) / 1.0;//Hsm 25072023
                }
                else
                {
                    neuron[n].bias = (0.5 - (double)rnd.NextDouble())/1.0;
                }
            }
            for (n = 0; n < maxConnections; n++)
            {
                conn[n].wgt = (0.5 - (double)rnd.NextDouble())/1.0;
            }
        }
        //**********************************************************
        public void SetRandomWeights(double fraction)
        {
            int n;
            for (n = 0; n < maxNeurons; n++)
            {
                    if (neuron[n].ihono == VnnBp.VnnBpLib.NuType.In)
                    {
                        //neuron[n].bias = 0.0;
                        neuron[n].bias = (0.5 - (double)rnd.NextDouble()) / 1.0;
                    }
                    else
                    {
                        if (rnd.NextDouble() < fraction)
                        {
                            neuron[n].bias = (0.5 - (double)rnd.NextDouble()) / 1.0;
                        }
                    }
            }
            for (n = 0; n < maxConnections; n++)
            {
                if (rnd.NextDouble() < fraction)
                {
                    conn[n].wgt = (0.5 - (double)rnd.NextDouble()) / 1.0;
                }
            }
        }

        //**********************************************************
        /// <summary>
        /// Set neuron output from net input without mutating nuIn for overflow avoidance.
        /// funno 4 (legacy "sqrt") is QuadraticSigmoid: y = sigmoid(z).
        /// </summary>
        private void SetNeuronOutputFromNetInput(int m)
        {
            neuron[m].nuOut = NnMath.Activate(neuron[m].funno, neuron[m].nuIn);
        }

        /// <summary>
        /// Input contribution into destination net input.
        /// Ordinary: w * y_i ; Quadratic destination (funno 4): w * (y_i - 0.5)^2
        /// </summary>
        private double WeightedSourceContribution(int connIndex, int srcIndex, int dstIndex)
        {
            double y = neuron[srcIndex].nuOut;
            short dFun = neuron[dstIndex].funno;
            if (NnMath.UsesCenteredSquareInputs(dFun))
            {
                double c = y - NnMath.Centre;
                return conn[connIndex].wgt * c * c;
            }
            if (NnMath.UsesUncenteredSquareInputs(dFun))
                return conn[connIndex].wgt * y * y;
            return conn[connIndex].wgt * y;
        }

        /// <summary>dy/dz from stored output for neuron m.</summary>
        private double NeuronDyDz(int m)
        {
            return NnMath.DyDzFromOutput(neuron[m].funno, neuron[m].nuOut);
        }

        //**********************************************************
        public void UpdateNet()
        {
            int n;
            for (n = 0; n < maxNeurons; n++)//Neuron all in = Bias
            {
                neuron[n].nuIn = neuron[n].bias;
                neuron[n].state = 0;
            }
            for (n = 0; n < maxInputs; n++)
            {//Assigne Inputs
                int m = inputs[n].sno;
                neuron[m].state = 1;
                neuron[m].nuIn += (inputs[n].value - 0.0);//HSM15032017 Input neuron=Input value
                SetNeuronOutputFromNetInput(m);
            }//Input Neurons are Updated

            int i, j;
            j = -1;
            for (n = 0; n < maxConnections; n++)
            {
                i = conn[n].srcNuNo;
                if (j != i)
                {
                    SetNeuronOutputFromNetInput(i);
                    j = i;
                }
                neuron[conn[n].dstNuNo].nuIn +=
                    WeightedSourceContribution(n, i, conn[n].dstNuNo);
            }

            for (int m = 0; m < maxNeurons; m++)
            {
                if (neuron[m].state == 0)
                    SetNeuronOutputFromNetInput(m);
            }
        }
        //**********************************************************
        public void UpdateNet(double noise)
        {
            int n;
            for (n = 0; n < maxNeurons; n++)//Neuron all in = Bias
            {
                neuron[n].nuIn = neuron[n].bias;
                neuron[n].state = 0;
            }
            double ns = 0;
            for (n = 0; n < maxInputs; n++)
            {//Assigne Inputs
                int m = inputs[n].sno;
                neuron[m].state = 1;
                neuron[m].nuIn += (inputs[n].value - 0.0);//HSM15032017 Input neuron=Input value
                if (neuron[m].funno == 2 || neuron[m].funno == 3 || neuron[m].funno == 4)
                {
                    ns = (rnd.Next(21) - 10) * noise;
                    neuron[m].nuIn += ns;
                }
                SetNeuronOutputFromNetInput(m);
            }//Input Neurons are Updated

            int i, j;
            j = -1;
            for (n = 0; n < maxConnections; n++)
            {
                i = conn[n].srcNuNo;
                if (j != i)
                {
                    if (neuron[i].funno == 2 || neuron[i].funno == 3 || neuron[i].funno == 4)
                    {
                        ns = (rnd.Next(21) - 10) * noise;
                        neuron[i].nuIn += ns;
                    }
                    SetNeuronOutputFromNetInput(i);
                    j = i;
                }
                // Keep quadratic destination transform consistent with silent UpdateNet
                neuron[conn[n].dstNuNo].nuIn +=
                    WeightedSourceContribution(n, i, conn[n].dstNuNo);
            }

            for (int m = 0; m < maxNeurons; m++)
            {
                if (neuron[m].state == 0)
                {
                    if (neuron[m].funno == 2 || neuron[m].funno == 3 || neuron[m].funno == 4)
                    {
                        ns = (rnd.Next(21) - 10) * noise;
                        neuron[m].nuIn += ns;
                    }
                    SetNeuronOutputFromNetInput(m);
                }
            }
        }
        //**********************************************************
        /// <summary>
        /// Legacy backprop name retained for comparison mode; see Git baseline for original body.
        /// </summary>
        public double ReinforceConnections_Legacy()
        {
            return ReinforceConnections();
        }

        /// <summary>
        /// Backpropagation with corrected centred-sigmoid and quadratic gradients.
        /// errout accumulates dLoss/dy; errin is delta = dLoss/dz = errout * dy/dz.
        /// Quadratic dest: dLoss/dy_i += 2*w*(y_i-0.5)*delta_j ;
        /// dLoss/dw = delta_j * (y_i-0.5)^2.
        /// </summary>
        public double ReinforceConnections()
        {
            int n, dn, sn;
            double er = 0;
            for (n = 0; n < maxNeurons; n++)
            {
                neuron[n].errout = 0;
                neuron[n].errin = 0;
            }
            intErr = 0;
            for (n = 0; n < maxOutputs; n++)
            {
                int oi = outputs[n].sno;
                double y = neuron[oi].nuOut;
                double t = outputs[n].value;
                if (lossType == LossType.BinaryCrossEntropy)
                {
                    // Sigmoid + BCE: dLoss/dz = y - t (do not multiply sigmoid' again).
                    neuron[oi].errout = y - t;
                    neuron[oi].errin = y - t;
                    er += NnMath.BinaryCrossEntropy(t, NnMath.Clamp01(y));
                }
                else
                {
                    // MSE reporting uses |y-t|; gradient uses dL/dy = (y-t).
                    neuron[oi].errout = y - t;
                    er += Math.Abs(y - t);
                }
                if (y >= 0.5)
                    intErr += (int)Math.Abs(1 - t);
                else
                    intErr += (int)Math.Abs(t);
            }

            // Backward: accumulate dLoss/dy at sources.
            // dest contrib uses dLoss/dz_dest = errout_dest * dy/dz (or errin if BCE output).
            for (n = maxConnections - 1; n >= 0; n--)
            {
                dn = conn[n].dstNuNo;
                sn = conn[n].srcNuNo;
                short dFun = neuron[dn].funno;
                if (dFun == 0 || dFun == 5 || dFun == 6)
                    continue;

                double destDelta;
                if (lossType == LossType.BinaryCrossEntropy && neuron[dn].ihono == NuType.Out)
                    destDelta = neuron[dn].errin;
                else
                    destDelta = neuron[dn].errout * NeuronDyDz(dn);

                if (NnMath.UsesCenteredSquareInputs(dFun))
                {
                    double centeredSource = neuron[sn].nuOut - NnMath.Centre;
                    neuron[sn].errout += 2.0 * conn[n].wgt * centeredSource * destDelta;
                }
                else if (NnMath.UsesUncenteredSquareInputs(dFun))
                {
                    neuron[sn].errout += 2.0 * conn[n].wgt * neuron[sn].nuOut * destDelta;
                }
                else
                {
                    neuron[sn].errout += conn[n].wgt * destDelta;
                }
            }

            for (n = 0; n < maxNeurons; n++)
            {
                if (lossType == LossType.BinaryCrossEntropy && neuron[n].ihono == NuType.Out)
                    continue; // errin already set
                switch (neuron[n].funno)
                {
                    case 0:
                        neuron[n].errin = 0;
                        break;
                    case 5:
                    case 6:
                        break;
                    default:
                        neuron[n].errin = neuron[n].errout * NeuronDyDz(n);
                        break;
                }
            }

            for (n = 0; n < maxConnections; n++)
            {
                dn = conn[n].dstNuNo;
                sn = conn[n].srcNuNo;
                short dFun = neuron[dn].funno;
                if (dFun == 0 || dFun == 5 || dFun == 6)
                    continue;
                if (NnMath.UsesCenteredSquareInputs(dFun))
                {
                    double centeredSource = neuron[sn].nuOut - NnMath.Centre;
                    conn[n].wgt -= eta * neuron[dn].errin * centeredSource * centeredSource;
                }
                else if (NnMath.UsesUncenteredSquareInputs(dFun))
                {
                    double ySrc = neuron[sn].nuOut;
                    conn[n].wgt -= eta * neuron[dn].errin * ySrc * ySrc;
                }
                else
                {
                    // Ordinary connection: dL/dw = delta_j * y_i (also for centred-sigmoid destinations)
                    conn[n].wgt -= eta * neuron[dn].errin * neuron[sn].nuOut;
                }
            }
            for (n = 0; n < maxNeurons; n++)
            {
                if (neuron[n].ihono != NuType.In)
                    neuron[n].bias -= etab * neuron[n].errin;
            }
            return er;
        }
        //**********************************************************
        /// <summary>
        /// Forward-only error and confusion counts. Does not update weights or biases.
        /// </summary>
        public double GetAllErrors()
        {
            tp = 0;
            tn = 0;
            fp = 0;
            fn = 0;
            double er = 0;
            for (int n = 0; n < maxOutputs; n++)
            {
                double prediction = neuron[outputs[n].sno].nuOut;
                double target = outputs[n].value;
                neuron[outputs[n].sno].errout = prediction - target;
                bool predictedPositive = prediction >= 0.5;
                bool actualPositive = target >= 0.5;
                if (actualPositive && predictedPositive)
                    tp++;
                else if (!actualPositive && !predictedPositive)
                    tn++;
                else if (!actualPositive && predictedPositive)
                    fp++;
                else
                    fn++;
                er += Math.Abs(prediction - target);
            }
            return er;
        }
        //**********************************************************
        public void DeleteNeurons(MouseEventArgs e, short nuNo)
        {
            ArrayList al = new ArrayList();
            for (int i = 0; i < neuron.Length; i++)
			{
                if (i != nuNo)
                {
                    al.Add(neuron[i]);
                }
                else
                {
                    
                }
			}
            neuron =new VnnBpNeuron[al.Count];
            for (short i = 0; i < neuron.Length; i++)
            {
                neuron[i] = (VnnBpNeuron)al[i];
                neuron[i].sno = (short)(i + 1);
            }
            al = new ArrayList();
            for (int i = 0; i < conn.Length; i++)
            {

                if ((conn[i].srcNuNo != nuNo)&(conn[i].dstNuNo != nuNo))
                {
                    al.Add(conn[i]);
                }
            }
            conn=new VnnBpConnection[al.Count];
            for (int i = 0; i < conn.Length; i++)
            {
                conn[i] = (VnnBpConnection)al[i];
                if (conn[i].srcNuNo >= nuNo)
                    conn[i].srcNuNo--;
                if (conn[i].dstNuNo >= nuNo)
                    conn[i].dstNuNo--;
            }
            al.Clear();
            maxNeurons = neuron.Length;
            maxConnections = conn.Length;
            sort_connections();
        }
        //**********************************************************
        public void InsertNeurons(short nuNo, VnnBp.VnnBpLib.NuType nutyp, GRIDPOS gridPos, NETPOS netPos)//short lyrNo, short rowNo
        {
            int ct = connTypeNo;
            VnnBpNeuron nu1 = new VnnBpNeuron();
            if (nutyp == VnnBp.VnnBpLib.NuType.In)
            {//Input neuron
                //nu1.bias = (rnd.NextDouble() - 0.5) * 2.0;//Hsm 25072023
                nu1.bias = 0.0;
                nu1.funno = 1;//y=x
            }
            else
            {//Hidden or output neuron
                nu1.bias = (rnd.NextDouble() - 0.5) * 2.0;
                nu1.funno = 2;//exp+
            }
            nu1.ihono = nutyp;
            nu1.netpos.lyrno = netPos.lyrno;
            nu1.gridPos = gridPos;
            nu1.netpos.rowno = netPos.rowno;
            nu1.sno = 0;
            ArrayList al = new ArrayList();
            for (int i = 0; i < neuron.Length; i++)
            {
                if (i == nuNo)
                {
                    al.Add(nu1);//insert neuron
                }
                if (i >= nuNo)
                {
                    if (neuron[i].netpos.lyrno == nu1.netpos.lyrno)
                    {
                        neuron[i].netpos.rowno++;
                    }
                }
                al.Add(neuron[i]);
            }
            if (nuNo == neuron.Length)
            {
                nu1.gridPos = gridPos;
                al.Add(nu1);//insert neuron
            }

            neuron = new VnnBpNeuron[al.Count];
            for (short i = 0; i < neuron.Length; i++)
            {
                neuron[i] = (VnnBpNeuron)al[i];
                neuron[i].sno = (short)(i + 1);
            }
            al = new ArrayList();

            for (int i = 0; i < conn.Length; i++)
            {
                if (conn[i].srcNuNo >= nuNo)
                {
                    conn[i].srcNuNo++;
                }
                if (conn[i].dstNuNo >= nuNo)
                {
                    conn[i].dstNuNo++;
                }
                al.Add(conn[i]);
            }
            switch (connTypeNo)
            {
                case 0://No Conn
                    break;
                case 1://In Layer Conn
                    InsertNuInLayerConn(nuNo, al);
                    break;
                case 2://In Layer Conn
                    InsertNuInLayerConn(nuNo, al);
                    break;
                case 3://All Layer Conn
                    InsertNuAllLayerConn(nuNo, al);
                    break;
                case 4://Fully Conn
                    InsertNuFullyConn(nuNo, al);
                    break;
                case 5://Partly Conn
                    break;
            }
            conn = new VnnBpConnection[al.Count];
            for (int i = 0; i < conn.Length; i++)
            {
                conn[i] = (VnnBpConnection)al[i];
            }
            al.Clear();
            maxNeurons = neuron.Length;
            sort_connections();
        }
        //**********************************************************
        private void InsertNuInLayerConn(short nuNo, ArrayList al)
        {
            for (short i = 0; i < nuNo; i++)
            {
                if (neuron[nuNo].netpos.lyrno-1 == neuron[i].netpos.lyrno)
                {
                    VnnBpConnection con = new VnnBpConnection();
                    con.dstNuNo = nuNo;
                    con.srcNuNo = i;
                    con.wgt = (rnd.NextDouble() - 0.5) * 2.0;
                    al.Add(con);
                }
            }
            for (short i = (short)(nuNo + 1); i < neuron.Length; i++)
            {
                if (neuron[nuNo].netpos.lyrno+1 == neuron[i].netpos.lyrno)
                {
                    VnnBpConnection con = new VnnBpConnection();
                    con.dstNuNo = i;
                    con.srcNuNo = nuNo;
                    con.wgt = (rnd.NextDouble() - 0.5) * 2.0;
                    al.Add(con);
                }
            }
        }
        //**********************************************************
        private void InsertNuIn2LayerConn(short nuNo, ArrayList al)
        {
            //for (short i = 0; i < nuNo; i++)
            //{
            //    if (neuron[nuNo].netpos.lyrno - 1 == neuron[i].netpos.lyrno)
            //    {
            //        VnnBpConnection con = new VnnBpConnection();
            //        con.dstNuNo = nuNo;
            //        con.srcNuNo = i;
            //        con.wgt = (rnd.NextDouble() - 0.5) * 2.0;
            //        al.Add(con);
            //    }
            //}
            //for (short i = (short)(nuNo + 1); i < neuron.Length; i++)
            //{
            //    if (neuron[nuNo].netpos.lyrno + 1 == neuron[i].netpos.lyrno)
            //    {
            //        VnnBpConnection con = new VnnBpConnection();
            //        con.dstNuNo = i;
            //        con.srcNuNo = nuNo;
            //        con.wgt = (rnd.NextDouble() - 0.5) * 2.0;
            //        al.Add(con);
            //    }
            //}
        }
        //**********************************************************
        private void InsertNuAllLayerConn(short nuNo, ArrayList al)
        {
            for (short i = 0; i < nuNo; i++)
            {
                if (neuron[nuNo].netpos.lyrno != neuron[i].netpos.lyrno)
                {
                    VnnBpConnection con = new VnnBpConnection();
                    con.dstNuNo = nuNo;
                    con.srcNuNo = i;
                    con.wgt = (rnd.NextDouble() - 0.5) * 2.0;
                    al.Add(con);
                }
            }
            for (short i = (short)(nuNo + 1); i < neuron.Length; i++)
            {
                if (neuron[nuNo].netpos.lyrno != neuron[i].netpos.lyrno)
                {
                    VnnBpConnection con = new VnnBpConnection();
                    con.dstNuNo = i;
                    con.srcNuNo = nuNo;
                    con.wgt = (rnd.NextDouble() - 0.5) * 2.0;
                    al.Add(con);
                }
            }
        }
        //**********************************************************
        private void InsertNuFullyConn(short nuNo, ArrayList al)
        {
            for (short i = 0; i < nuNo; i++)
            {
                    VnnBpConnection con = new VnnBpConnection();
                    con.dstNuNo = nuNo;
                    con.srcNuNo = i;
                    con.wgt = (rnd.NextDouble() - 0.5) * 2.0;
                    al.Add(con);
            }
            for (short i = (short)(nuNo + 1); i < neuron.Length; i++)
            {
                    VnnBpConnection con = new VnnBpConnection();
                    con.dstNuNo = i;
                    con.srcNuNo = nuNo;
                    con.wgt = (rnd.NextDouble() - 0.5) * 2.0;
                    al.Add(con);
            }
        }
        //**********************************************************
        public void UpdateNetConfig()
        {
            short maxLyrs = 0;
            maxInputs = 0;
            maxOutputs = 0;
            for (int i = 0; i < neuron.Length; i++)
            {
                if (maxLyrs < neuron[i].gridPos.X)
                    maxLyrs = (short)neuron[i].gridPos.X;
                if (neuron[i].ihono == VnnBp.VnnBpLib.NuType.In) //in=0, out=1, hidden=2
                {
                    maxInputs++;
                }
                if (neuron[i].ihono == VnnBp.VnnBpLib.NuType.Out) //in=0, out=1, hidden=2
                {
                    maxOutputs++;
                }
            }
            short[] lyrs = new short[maxLyrs+1];
            for (int i = 0; i < neuron.Length; i++)
            {
                lyrs[neuron[i].gridPos.X]++;
            }
            short ln = 0;
            for (int i = 0; i < lyrs.Length; i++)
            {
                if (lyrs[i] > 0)
                    ln++;
            }
            nuLayers = new int[ln];
            int k = 0;
            for (int i = 0; i < lyrs.Length; i++)
            {
                if (lyrs[i] > 0)
                {
                    nuLayers[k] = lyrs[i];
                    k++;
                }
            }

        }
        //**********************************************************
        public double get_error()
        {
            double err = 0.0; int n;
            for (n = 0; n < maxOutputs; n++)
                err += Math.Abs(neuron[outputs[n].sno].nuOut - outputs[n].value);
            return (err);
        }
         //**********************************************************
        public void DeleteAllConnections()
        {
            conn = new VnnBpConnection[0];
            maxConnections = conn.Length;
        }
        //**********************************************************
        internal void DeleteAllConnections(short nuNo)
        {
            ArrayList al = new ArrayList();
            for (int i = 0; i < conn.Length; i++)
            {
                if ((conn[i].srcNuNo != nuNo)&(conn[i].dstNuNo != nuNo))
                {
                    al.Add(conn[i]);
                }
                else
                {

                }
            }
            conn = new VnnBpConnection[al.Count];
            for (short i = 0; i < conn.Length; i++)
            {
                conn[i] = (VnnBpConnection)al[i];
            }
            al.Clear();
            maxConnections = conn.Length;
            sort_connections();
        }
        //**********************************************************
        internal void EnlargeConnectionsWgt(double gain)
        {
            for (int i = 0; i < conn.Length; i++)
            {
                conn[i].wgt *= gain;
            }
        }
        //**********************************************************
        public void sort_connections()
        {
           ArrayList badCon = TestConnections();
            int i, j;
            int p, q;
            double w;
            for (i = 0; i < maxConnections; i++)
            {
                if (conn[i].srcNuNo == conn[i].dstNuNo)
                {
                    conn[i].srcNuNo = 9999;
                    conn[i].dstNuNo = 9999;
                }/* ?????? */
            }
            for (j = 0; j < maxConnections - 1; j++)
            {
                for (i = j + 1; i < maxConnections; i++)
                {
                    Int32 s = conn[i].srcNuNo;
                    p = (s << 16) + conn[i].dstNuNo;
                    s = conn[j].srcNuNo;
                    q = (s << 16) + conn[j].dstNuNo;
                    if (p < q)
                    {
                        short k = conn[i].srcNuNo;
                        conn[i].srcNuNo = conn[j].srcNuNo;
                        conn[j].srcNuNo = k;
                        k = conn[i].dstNuNo;
                        conn[i].dstNuNo = conn[j].dstNuNo;
                        conn[j].dstNuNo = k;
                        w = conn[i].wgt;
                        conn[i].wgt = conn[j].wgt;
                        conn[j].wgt = w;
                        w = conn[i].wgtTmp;
                        conn[i].wgtTmp = conn[j].wgtTmp;
                        conn[j].wgtTmp = w;
                    }
                    else if (p == q)
                    {
                        conn[i].srcNuNo = 9999;
                        conn[i].dstNuNo = 9999;
                    }
                }
            }
            for (int k = 0; k < maxConnections; k++)
            {
                if ((conn[k].srcNuNo == 9999) | (conn[k].dstNuNo == 9999))
                {
                    maxConnections = k;
                    return;
                }
            }
            i = maxConnections;
            if (i > 0)
            {
                do
                {
                    i--;
                } while ((conn[i].srcNuNo == 9999 & (conn[i].dstNuNo == 9999)));
                maxConnections = i + 1;
            }
            ///////maxConnections = i + 1;
        }
        //**********************************************************
        private ArrayList TestConnections()
        {
            int nn = -1;
            ArrayList al = new ArrayList();
            for (int i = 0; i < maxConnections; i++)
            {
                if (conn[i].srcNuNo == conn[i].dstNuNo)
                {
                    al.Add(i);
                }
                if (conn[i].srcNuNo == 9999)
                {
                    nn = i;
                }
                if (conn[i].dstNuNo == 9999)
                {
                    nn = i;
                }
            }
            return al;
        }

        /// <summary>
        /// Pure evaluation over provided samples: forward only, no training.
        /// Each sample is double[] {inputs..., targets...} with lengths maxInputs+maxOutputs,
        /// or pass separate inRows/outRows via Evaluate(inData, outData).
        /// </summary>
        public EvaluationResult Evaluate(double[][] inRows, double[][] outRows)
        {
            if (inRows == null) throw new ArgumentNullException("inRows");
            if (outRows == null) throw new ArgumentNullException("outRows");
            EvaluationResult r = new EvaluationResult();
            double sumAbs = 0;
            double sumSq = 0;
            double sumLoss = 0;
            int total = inRows.Length;
            for (int s = 0; s < total; s++)
            {
                for (int i = 0; i < maxInputs; i++)
                    inputs[i].value = inRows[s][i];
                for (int i = 0; i < maxOutputs; i++)
                    outputs[i].value = outRows[s][i];
                UpdateNet();
                for (int i = 0; i < maxOutputs; i++)
                {
                    double prediction = neuron[outputs[i].sno].nuOut;
                    double target = outRows[s][i];
                    double diff = prediction - target;
                    sumAbs += Math.Abs(diff);
                    sumSq += diff * diff;
                    if (lossType == LossType.BinaryCrossEntropy)
                        sumLoss += NnMath.BinaryCrossEntropy(target, NnMath.Clamp01(prediction));
                    else
                        sumLoss += 0.5 * diff * diff;

                    bool predictedPositive = prediction >= 0.5;
                    bool actualPositive = target >= 0.5;
                    if (actualPositive && predictedPositive) r.TruePositive++;
                    else if (!actualPositive && !predictedPositive) r.TrueNegative++;
                    else if (!actualPositive && predictedPositive) r.FalsePositive++;
                    else r.FalseNegative++;
                }
            }
            int decisions = r.TruePositive + r.TrueNegative + r.FalsePositive + r.FalseNegative;
            r.SampleCount = total;
            r.MeanAbsoluteError = decisions > 0 ? sumAbs / decisions : 0;
            r.MeanSquaredError = decisions > 0 ? sumSq / decisions : 0;
            r.Loss = decisions > 0 ? sumLoss / Math.Max(1, total) : 0;
            r.Accuracy = decisions > 0 ? (double)(r.TruePositive + r.TrueNegative) / decisions : 0;
            int denP = r.TruePositive + r.FalsePositive;
            int denR = r.TruePositive + r.FalseNegative;
            r.Precision = denP > 0 ? (double)r.TruePositive / denP : 0;
            r.Recall = denR > 0 ? (double)r.TruePositive / denR : 0;
            double pr = r.Precision + r.Recall;
            r.F1Score = pr > 0 ? 2.0 * r.Precision * r.Recall / pr : 0;
            // sync confusion fields used by UI
            tp = r.TruePositive; tn = r.TrueNegative; fp = r.FalsePositive; fn = r.FalseNegative;
            return r;
        }

        /// <summary>
        /// One training epoch: optional shuffle, forward+backward, parameter updates.
        /// </summary>
        public TrainingResult TrainEpoch(double[][] inRows, double[][] outRows, TrainingOptions options)
        {
            if (inRows == null) throw new ArgumentNullException("inRows");
            if (outRows == null) throw new ArgumentNullException("outRows");
            if (options == null) options = new TrainingOptions();

            LossType previousLoss = lossType;
            lossType = options.LossType;
            double prevEta = eta;
            double prevEtab = etab;
            eta = options.LearningRate;
            etab = options.LearningRate;

            int n = inRows.Length;
            int[] order = new int[n];
            for (int i = 0; i < n; i++) order[i] = i;
            if (options.ShuffleEachEpoch)
            {
                Random rndLocal = new Random(options.RandomSeed);
                for (int i = n - 1; i > 0; i--)
                {
                    int j = rndLocal.Next(i + 1);
                    int tmp = order[i];
                    order[i] = order[j];
                    order[j] = tmp;
                }
            }

            // Snapshot to detect parameter change
            double[] wBefore = new double[maxConnections];
            double[] bBefore = new double[maxNeurons];
            for (int i = 0; i < maxConnections; i++) wBefore[i] = conn[i].wgt;
            for (int i = 0; i < maxNeurons; i++) bBefore[i] = neuron[i].bias;

            double totalLoss = 0;
            for (int s = 0; s < n; s++)
            {
                int idx = order[s];
                for (int i = 0; i < maxInputs; i++)
                    inputs[i].value = inRows[idx][i];
                for (int i = 0; i < maxOutputs; i++)
                    outputs[i].value = outRows[idx][i];
                UpdateNet();
                totalLoss += ReinforceConnections();
            }

            bool changed = false;
            for (int i = 0; i < maxConnections && !changed; i++)
                if (conn[i].wgt != wBefore[i]) changed = true;
            for (int i = 0; i < maxNeurons && !changed; i++)
                if (neuron[i].bias != bBefore[i]) changed = true;

            TrainingResult result = new TrainingResult();
            result.TrainingLoss = n > 0 ? totalLoss / n : 0;
            result.EpochsCompleted = 1;
            result.ChangedParameters = changed;

            eta = prevEta;
            etab = prevEtab;
            lossType = previousLoss;
            return result;
        }

        /// <summary>
        /// Fine-tune for a fixed number of full-dataset epochs.
        /// Prefer FineTuneSampleCycles for post-pruning (matches original 10k sample updates).
        /// </summary>
        public TrainingResult FineTune(double[][] inRows, double[][] outRows, int epochs, TrainingOptions options)
        {
            if (options == null) options = new TrainingOptions();
            TrainingResult last = new TrainingResult();
            for (int e = 0; e < epochs; e++)
            {
                TrainingOptions opt = new TrainingOptions();
                opt.LearningRate = options.LearningRate;
                opt.LossType = options.LossType;
                opt.ShuffleEachEpoch = options.ShuffleEachEpoch;
                opt.RandomSeed = options.RandomSeed + e;
                last = TrainEpoch(inRows, outRows, opt);
                last.EpochsCompleted = e + 1;
            }
            return last;
        }

        /// <summary>
        /// Post-pruning retraining: N stochastic sample updates (UpdateNet + ReinforceConnections).
        /// This matches the original get_total_error(10000) training behaviour — not N full epochs.
        /// </summary>
        public TrainingResult FineTuneSampleCycles(
            double[][] inRows,
            double[][] outRows,
            int sampleCycles,
            TrainingOptions options)
        {
            if (inRows == null) throw new ArgumentNullException("inRows");
            if (outRows == null) throw new ArgumentNullException("outRows");
            if (options == null) options = new TrainingOptions();
            if (sampleCycles < 0) sampleCycles = 0;

            LossType previousLoss = lossType;
            lossType = options.LossType;
            double prevEta = eta;
            double prevEtab = etab;
            eta = options.LearningRate;
            etab = options.LearningRate;

            Random rndLocal = new Random(options.RandomSeed);
            double totalLoss = 0;
            bool changed = false;
            double probeW = (maxConnections > 0) ? conn[0].wgt : 0;

            for (int s = 0; s < sampleCycles; s++)
            {
                int rn = rndLocal.Next(inRows.Length);
                for (int i = 0; i < maxInputs; i++)
                    inputs[i].value = inRows[rn][i];
                for (int i = 0; i < maxOutputs; i++)
                    outputs[i].value = outRows[rn][i];
                UpdateNet();
                totalLoss += ReinforceConnections();
            }

            if (maxConnections > 0 && conn[0].wgt != probeW)
                changed = true;
            else
            {
                // any weight change
                // (probe may coincidentally match; treat cycles>0 + nonzero eta as likely changed)
                changed = sampleCycles > 0 && options.LearningRate != 0;
            }

            TrainingResult result = new TrainingResult();
            result.TrainingLoss = sampleCycles > 0 ? totalLoss / sampleCycles : 0;
            result.EpochsCompleted = sampleCycles; // report cycles completed
            result.ChangedParameters = changed;

            eta = prevEta;
            etab = prevEtab;
            lossType = previousLoss;
            return result;
        }

        // Last cleanup diagnostics (for pruning logs / UI)
        public int LastRemovedZeroInput;
        public int LastRemovedZeroOutput;
        public int LastLinearContractions;
        public int LastNonlinearContractions;

        public NetworkValidationResult ValidateTopology()
        {
            NetworkValidationResult result = new NetworkValidationResult();
            result.IsValid = true;
            for (int i = 0; i < maxConnections; i++)
            {
                int s = conn[i].srcNuNo;
                int d = conn[i].dstNuNo;
                if (s < 0 || s >= maxNeurons || d < 0 || d >= maxNeurons)
                {
                    result.IsValid = false;
                    result.Messages.Add("Connection " + i + " has invalid neuron index.");
                }
                else if (s == d)
                {
                    result.IsValid = false;
                    result.Messages.Add("Connection " + i + " is a self-loop.");
                }
                if (!NnMath.IsFinite(conn[i].wgt))
                {
                    result.IsValid = false;
                    result.Messages.Add("Connection " + i + " has non-finite weight.");
                }
            }
            for (int i = 0; i < maxNeurons; i++)
            {
                // funno 7 = QuadraticLinear (uncentered square inputs); must be accepted
                if (neuron[i].funno < 0 || neuron[i].funno > 7)
                {
                    result.IsValid = false;
                    result.Messages.Add("Neuron " + i + " has unrecognized funno.");
                }
                if (!NnMath.IsFinite(neuron[i].bias))
                {
                    result.IsValid = false;
                    result.Messages.Add("Neuron " + i + " has non-finite bias.");
                }
            }
            // duplicate active connections
            for (int i = 0; i < maxConnections; i++)
            {
                for (int j = i + 1; j < maxConnections; j++)
                {
                    if (conn[i].srcNuNo == conn[j].srcNuNo && conn[i].dstNuNo == conn[j].dstNuNo)
                    {
                        result.IsValid = false;
                        result.Messages.Add("Duplicate connection " + i + " and " + j);
                    }
                }
            }
            return result;
        }

        /// <summary>
        /// Export readable equations for each non-input neuron.
        /// </summary>
        public string ExportEquation()
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            for (int j = 0; j < maxNeurons; j++)
            {
                if (neuron[j].ihono == NuType.In)
                    continue;
                System.Text.StringBuilder terms = new System.Text.StringBuilder();
                terms.AppendFormat(System.Globalization.CultureInfo.InvariantCulture,
                    "{0:G15}", neuron[j].bias);
                for (int c = 0; c < maxConnections; c++)
                {
                    if (conn[c].dstNuNo != j)
                        continue;
                    int i = conn[c].srcNuNo;
                    if (NnMath.UsesCenteredSquareInputs(neuron[j].funno))
                    {
                        terms.AppendFormat(System.Globalization.CultureInfo.InvariantCulture,
                            " + {0:G15}*(n{1} - 0.5)^2", conn[c].wgt, i);
                    }
                    else if (NnMath.UsesUncenteredSquareInputs(neuron[j].funno))
                    {
                        terms.AppendFormat(System.Globalization.CultureInfo.InvariantCulture,
                            " + {0:G15}*(n{1})^2", conn[c].wgt, i);
                    }
                    else
                    {
                        terms.AppendFormat(System.Globalization.CultureInfo.InvariantCulture,
                            " + {0:G15}*n{1}", conn[c].wgt, i);
                    }
                }
                string body = terms.ToString();
                if (neuron[j].funno == 1 || neuron[j].funno == 7)
                    sb.AppendFormat("n{0} = {1}\r\n", j, body);
                else if (neuron[j].funno == 3)
                    sb.AppendFormat("n{0} = sigmoid({1}) - 0.5\r\n", j, body);
                else if (neuron[j].funno == 2 || neuron[j].funno == 4)
                    sb.AppendFormat("n{0} = sigmoid({1})\r\n", j, body);
                else
                    sb.AppendFormat("n{0} = f{1}({2})\r\n", j, neuron[j].funno, body);
            }
            return sb.ToString();
        }

        /// <summary>
        /// Deep-copy snapshot for transactional prune / retrain rollback.
        /// </summary>
        public NetworkSnapshot CreateCompleteSnapshot()
        {
            NetworkSnapshot s = new NetworkSnapshot();
            s.MaxNeurons = maxNeurons;
            s.MaxConnections = maxConnections;
            s.MaxInputs = maxInputs;
            s.MaxOutputs = maxOutputs;
            s.Eta = eta;
            s.Etab = etab;
            s.LossType = lossType;
            if (nuLayers != null)
            {
                s.NuLayers = new int[nuLayers.Length];
                Array.Copy(nuLayers, s.NuLayers, nuLayers.Length);
            }

            int nCap = (neuron != null) ? neuron.Length : maxNeurons;
            int cCap = (conn != null) ? conn.Length : maxConnections;
            s.NeuronFunNo = new short[nCap];
            s.NeuronIho = new NuType[nCap];
            s.NeuronBias = new double[nCap];
            s.NeuronBiasTmp = new double[nCap];
            s.NeuronBiasPP = new double[nCap];
            s.NeuronSno = new short[nCap];
            s.NeuronGridX = new short[nCap];
            s.NeuronGridY = new short[nCap];
            s.NeuronLyrNo = new short[nCap];
            for (int i = 0; i < nCap; i++)
            {
                s.NeuronFunNo[i] = neuron[i].funno;
                s.NeuronIho[i] = neuron[i].ihono;
                s.NeuronBias[i] = neuron[i].bias;
                s.NeuronBiasTmp[i] = neuron[i].biasTmp;
                s.NeuronBiasPP[i] = neuron[i].biasPP;
                s.NeuronSno[i] = neuron[i].sno;
                s.NeuronGridX[i] = neuron[i].gridPos.X;
                s.NeuronGridY[i] = neuron[i].gridPos.Y;
                s.NeuronLyrNo[i] = neuron[i].netpos.lyrno;
            }

            s.ConnSrc = new short[cCap];
            s.ConnDst = new short[cCap];
            s.ConnWgt = new double[cCap];
            s.ConnWgtTmp = new double[cCap];
            s.ConnWgtPP = new double[cCap];
            s.ConnSrcTmp = new short[cCap];
            s.ConnDstTmp = new short[cCap];
            s.ConnSrcPP = new short[cCap];
            s.ConnDstPP = new short[cCap];
            for (int i = 0; i < cCap; i++)
            {
                s.ConnSrc[i] = conn[i].srcNuNo;
                s.ConnDst[i] = conn[i].dstNuNo;
                s.ConnWgt[i] = conn[i].wgt;
                s.ConnWgtTmp[i] = conn[i].wgtTmp;
                s.ConnWgtPP[i] = conn[i].wgtPP;
                s.ConnSrcTmp[i] = conn[i].srcTmp;
                s.ConnDstTmp[i] = conn[i].dstTmp;
                s.ConnSrcPP[i] = conn[i].srcPP;
                s.ConnDstPP[i] = conn[i].dstPP;
            }

            s.InputSno = new short[maxInputs];
            s.OutputSno = new short[maxOutputs];
            for (int i = 0; i < maxInputs; i++) s.InputSno[i] = inputs[i].sno;
            for (int i = 0; i < maxOutputs; i++) s.OutputSno[i] = outputs[i].sno;
            return s;
        }

        public void RestoreCompleteSnapshot(NetworkSnapshot s)
        {
            if (s == null) throw new ArgumentNullException("s");
            maxNeurons = s.MaxNeurons;
            maxConnections = s.MaxConnections;
            maxInputs = s.MaxInputs;
            maxOutputs = s.MaxOutputs;
            eta = s.Eta;
            etab = s.Etab;
            lossType = s.LossType;
            if (s.NuLayers != null)
            {
                nuLayers = new int[s.NuLayers.Length];
                Array.Copy(s.NuLayers, nuLayers, s.NuLayers.Length);
            }

            int nCap = s.NeuronFunNo.Length;
            int cCap = s.ConnSrc.Length;
            if (neuron == null || neuron.Length < nCap)
                neuron = new VnnBpNeuron[nCap];
            for (int i = 0; i < nCap; i++)
            {
                if (neuron[i] == null) neuron[i] = new VnnBpNeuron();
                neuron[i].funno = s.NeuronFunNo[i];
                neuron[i].ihono = s.NeuronIho[i];
                neuron[i].bias = s.NeuronBias[i];
                neuron[i].biasTmp = s.NeuronBiasTmp[i];
                neuron[i].biasPP = s.NeuronBiasPP[i];
                neuron[i].sno = s.NeuronSno[i];
                neuron[i].gridPos.X = s.NeuronGridX[i];
                neuron[i].gridPos.Y = s.NeuronGridY[i];
                neuron[i].netpos.lyrno = s.NeuronLyrNo[i];
            }

            if (conn == null || conn.Length < cCap)
                conn = new VnnBpConnection[cCap];
            for (int i = 0; i < cCap; i++)
            {
                if (conn[i] == null) conn[i] = new VnnBpConnection();
                conn[i].srcNuNo = s.ConnSrc[i];
                conn[i].dstNuNo = s.ConnDst[i];
                conn[i].wgt = s.ConnWgt[i];
                conn[i].wgtTmp = s.ConnWgtTmp[i];
                conn[i].wgtPP = s.ConnWgtPP[i];
                conn[i].srcTmp = s.ConnSrcTmp[i];
                conn[i].dstTmp = s.ConnDstTmp[i];
                conn[i].srcPP = s.ConnSrcPP[i];
                conn[i].dstPP = s.ConnDstPP[i];
            }

            if (inputs == null || inputs.Length < maxInputs)
                inputs = new INOUT[maxInputs];
            if (outputs == null || outputs.Length < maxOutputs)
                outputs = new INOUT[maxOutputs];
            for (int i = 0; i < maxInputs; i++)
                inputs[i].sno = s.InputSno[i];
            for (int i = 0; i < maxOutputs; i++)
                outputs[i].sno = s.OutputSno[i];
        }

        private bool IsProtectedNeuron(int i)
        {
            if (neuron[i].funno == 5 || neuron[i].funno == 6)
                return true;
            for (int k = 0; k < maxInputs; k++)
                if (inputs[k].sno == i) return true;
            for (int k = 0; k < maxOutputs; k++)
                if (outputs[k].sno == i) return true;
            return false;
        }

        /// <summary>
        /// Delete ordinary hidden neurons with no active incoming OR no active outgoing path side.
        /// </summary>
        public bool RemoveNoInputOrNoOutputHiddenNeuronsOnce()
        {
            for (int i = 0; i < maxNeurons; i++)
            {
                if (IsProtectedNeuron(i))
                    continue;
                int inCount = 0, outCount = 0;
                for (int c = 0; c < maxConnections; c++)
                {
                    if (conn[c].dstNuNo == i) inCount++;
                    if (conn[c].srcNuNo == i) outCount++;
                }
                if (inCount == 0 || outCount == 0)
                {
                    if (inCount == 0) LastRemovedZeroInput++;
                    if (outCount == 0) LastRemovedZeroOutput++;
                    // Drop incident connections first
                    for (int c = 0; c < maxConnections; c++)
                    {
                        if (conn[c].srcNuNo == i || conn[c].dstNuNo == i)
                        {
                            conn[c].srcNuNo = 9999;
                            conn[c].dstNuNo = 9999;
                        }
                    }
                    sort_connections();
                    DeleteNeuronAt(i);
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Legacy name: only fully isolated neurons (kept for compatibility).
        /// </summary>
        public bool RemoveDisconnectedNeuronsOnce()
        {
            return RemoveNoInputOrNoOutputHiddenNeuronsOnce();
        }

        private void DeleteNeuronAt(int i)
        {
            for (int j = 0; j < maxConnections; j++)
            {
                if (conn[j].srcNuNo > i) conn[j].srcNuNo--;
                if (conn[j].dstNuNo > i) conn[j].dstNuNo--;
            }
            for (int j = i + 1; j < maxNeurons; j++)
                neuron[j - 1] = neuron[j];
            for (int j = 0; j < maxInputs; j++)
                if (inputs[j].sno > i) inputs[j].sno--;
            for (int j = 0; j < maxOutputs; j++)
                if (outputs[j].sno > i) outputs[j].sno--;
            maxNeurons--;
        }

        /// <summary>
        /// Merge one-input/one-output hidden neuron j: i→j→k into i→k.
        /// Linear: algebraically exact before retrain.
        /// Nonlinear: approximate structural candidate; accept only after post-prune retrain + validation.
        /// </summary>
        public bool ContractSingleInputSingleOutputNeuronOnce()
        {
            for (int i = 0; i < maxNeurons; i++)
            {
                if (IsProtectedNeuron(i))
                    continue;
                // Supported ordinary types: linear, sigmoid, centred, quadratic-sigmoid.
                // funno 7 (square-linear) is not contracted into a linear destination:
                // that would replace y_i^2 with y_i.
                short f = neuron[i].funno;
                if (f < 1 || f > 4)
                    continue;

                int inEdge = -1, outEdge = -1, inCount = 0, outCount = 0;
                for (int c = 0; c < maxConnections; c++)
                {
                    if (conn[c].dstNuNo == i) { inEdge = c; inCount++; }
                    if (conn[c].srcNuNo == i) { outEdge = c; outCount++; }
                }
                if (inCount != 1 || outCount != 1)
                    continue;

                int src = conn[inEdge].srcNuNo;
                int dst = conn[outEdge].dstNuNo;
                if (src == dst)
                    continue;

                double wij = conn[inEdge].wgt;
                double wjk = conn[outEdge].wgt;
                double bj = neuron[i].bias;
                double mergedWeight = wij * wjk;

                int existing = -1;
                for (int c = 0; c < maxConnections; c++)
                {
                    if (c == inEdge || c == outEdge) continue;
                    if (conn[c].srcNuNo == src && conn[c].dstNuNo == dst)
                    { existing = c; break; }
                }
                if (existing >= 0)
                {
                    conn[existing].wgt += mergedWeight;
                    conn[outEdge].srcNuNo = 9999;
                    conn[outEdge].dstNuNo = 9999;
                }
                else
                {
                    conn[outEdge].srcNuNo = (short)src;
                    conn[outEdge].dstNuNo = (short)dst;
                    conn[outEdge].wgt = mergedWeight;
                }
                // Bias absorb is exact for linear; approximate seed for nonlinear (retrain recovers).
                neuron[dst].bias += wjk * bj;

                conn[inEdge].srcNuNo = 9999;
                conn[inEdge].dstNuNo = 9999;
                sort_connections();
                if (f == 1) LastLinearContractions++;
                else LastNonlinearContractions++;
                DeleteNeuronAt(i);
                return true;
            }
            return false;
        }

        /// <summary>Compatibility alias.</summary>
        public bool ContractLinearNeuronsOnce()
        {
            return ContractSingleInputSingleOutputNeuronOnce();
        }

        public void CompactConnections()
        {
            sort_connections();
            if (conn != null && maxConnections < conn.Length)
            {
                VnnBpConnection[] trimmed = new VnnBpConnection[Math.Max(maxConnections, 1)];
                for (int i = 0; i < maxConnections; i++)
                    trimmed[i] = conn[i];
                if (maxConnections == 0)
                    trimmed = new VnnBpConnection[0];
                conn = trimmed;
            }
        }

        public void CompactNeurons()
        {
            if (neuron != null && maxNeurons < neuron.Length)
            {
                VnnBpNeuron[] trimmed = new VnnBpNeuron[Math.Max(maxNeurons, 1)];
                for (int i = 0; i < maxNeurons; i++)
                    trimmed[i] = neuron[i];
                neuron = trimmed;
            }
        }

        public void CleanupTopologyAfterPruning()
        {
            LastRemovedZeroInput = 0;
            LastRemovedZeroOutput = 0;
            LastLinearContractions = 0;
            LastNonlinearContractions = 0;
            bool changed;
            do
            {
                changed = false;
                if (RemoveNoInputOrNoOutputHiddenNeuronsOnce())
                    changed = true;
                if (ContractSingleInputSingleOutputNeuronOnce())
                    changed = true;
            }
            while (changed);
            CompactConnections();
            CompactNeurons();
            UpdateNuLayers();
        }

        //**********************************************************
        //**********************************************************
        /// <summary>
        /// Legacy entry: topology cleanup after pruning.
        /// </summary>
        public void remove_neurons()
        {
            CleanupTopologyAfterPruning();
        }

        /// <summary>Original remove_neurons kept for reference / comparison.</summary>
        public void remove_neurons_Legacy()
        {
            CleanupTopologyAfterPruning();
        }
        //////////////////////////////////////////////////////////////////////
        public void UpdateNuLayers()
        {
            if (neuron == null || maxNeurons <= 0)
                return;
            System.Collections.Generic.List<short> xs = new System.Collections.Generic.List<short>(maxNeurons);
            for (int i = 0; i < maxNeurons; i++)
                xs.Add(neuron[i].gridPos.X);
            xs.Sort();
            System.Collections.Generic.List<int> layers = new System.Collections.Generic.List<int>();
            short xOld = xs[0];
            int cn = 0;
            for (int i = 0; i < xs.Count; i++)
            {
                if (xOld == xs[i])
                    cn++;
                else
                {
                    layers.Add(cn);
                    cn = 1;
                    xOld = xs[i];
                }
            }
            layers.Add(cn);
            nuLayers = layers.ToArray();
        }
        //////////////////////////////////////////////////////////////////////
        /// <summary>
        /// Remove n smallest-|weight| connections (magnitude pruning).
        /// Sorts in place (ascending |wgt|) then truncates maxConnections so
        /// RestoreOriginalWeights can revive the tail by restoring maxConnections.
        /// </summary>
        public void remove_connections(int n)
        {
            if (n < 0) n = 0;
            int i, j;
            for (i = 0; i < maxNeurons; i++)
            {
                if ((neuron[i].funno == 5) | (neuron[i].funno == 6))
                {
                    for (j = 0; j < maxConnections; j++)
                    {
                        if (conn[j].dstNuNo == i)
                        {
                            conn[j].wgt = 0;
                            n++;
                        }
                    }
                }
            }

            // Array.Sort indices by ascending |weight|, then permute conn[0..max)
            int m = maxConnections;
            if (m <= 0)
                return;
            int[] order = new int[m];
            for (i = 0; i < m; i++) order[i] = i;
            Array.Sort(order, delegate(int a, int b)
            {
                double pa = Math.Abs(conn[a].wgt);
                double pb = Math.Abs(conn[b].wgt);
                if (pa < pb) return -1;
                if (pa > pb) return 1;
                return a.CompareTo(b);
            });

            VnnBpConnection[] sorted = new VnnBpConnection[m];
            for (i = 0; i < m; i++)
                sorted[i] = conn[order[i]];
            for (i = 0; i < m; i++)
                conn[i] = sorted[i];

            // Drop the first n (smallest); keep them in the array beyond maxConnections for rollback
            if (n > m) n = m;
            // Move smallest n to the end so truncation removes them
            VnnBpConnection[] rotated = new VnnBpConnection[m];
            int keep = m - n;
            for (i = 0; i < keep; i++)
                rotated[i] = conn[i + n]; // larger weights first among survivors? keep original relative: survivors are conn[n..m)
            for (i = 0; i < n; i++)
                rotated[keep + i] = conn[i]; // pruned tail
            for (i = 0; i < m; i++)
                conn[i] = rotated[i];

            maxConnections = keep;
            sort_connections();
        }
        //**********************************************************
        internal void SaveThisWeights()
        {
            for (int i = 0; i < conn.Length; i++)
            {
                conn[i].wgtTmp = conn[i].wgt;
                conn[i].srcTmp = conn[i].srcNuNo;
                conn[i].dstTmp = conn[i].dstNuNo;
            }
            for (int i = 0; i < neuron.Length; i++)
            {
                neuron[i].biasTmp = neuron[i].bias;
            }
            maxConnectionsTmp = maxConnections;
            maxNeuronsTmp = maxNeurons;
        }
        //**********************************************************
        internal void RestoreOriginalWeights()
        {
            for (int i = 0; i < conn.Length; i++)
            {
                conn[i].wgt = conn[i].wgtTmp;
                conn[i].srcNuNo = conn[i].srcTmp;
                conn[i].dstNuNo = conn[i].dstTmp;
            }
            for (int i = 0; i < neuron.Length; i++)
            {
                neuron[i].bias = neuron[i].biasTmp;
            }
            maxConnections = maxConnectionsTmp;
            maxNeurons = maxNeuronsTmp;
        }
        //**********************************************************
        internal void SavePrePrineWeights()
        {
            for (int i = 0; i < conn.Length; i++)
            {
                conn[i].wgtPP = conn[i].wgt;
                conn[i].srcPP = conn[i].srcNuNo;
                conn[i].dstPP = conn[i].dstNuNo;
            }
            for (int i = 0; i < neuron.Length; i++)
            {
                neuron[i].biasPP = neuron[i].bias;
            }
            maxConnectionsPP = maxConnections;
            maxNeuronsPP = maxNeurons;
        }
        //**********************************************************
        internal void RestorePrePrineWeights()
        {
            for (int i = 0; i < conn.Length; i++)
            {
                conn[i].wgt = conn[i].wgtPP;
                conn[i].srcNuNo = conn[i].srcPP;
                conn[i].dstNuNo = conn[i].dstPP;
            }
            for (int i = 0; i < neuron.Length; i++)
            {
                neuron[i].bias = neuron[i].biasPP;
            }
            maxConnections = maxConnectionsPP;
            maxNeurons = maxNeuronsPP;
        }
        //**********************************************************
    }
}
