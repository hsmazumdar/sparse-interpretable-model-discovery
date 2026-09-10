using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text;

namespace VnnBp.VnnBpLib
{
    //*********************************************************
    public enum ConnType { NoConn, InLayerConn, In2LayerConn, AllLayerConn, FullyConn, PartlyConn };
    public struct INOUT
    {
        public short sno;
        public double value;
    };

    public enum NuType { None, In, Out, Hdn };

    public struct NETPOS
    {
        public short lyrno;//feed-forward layer no
        public short rowno;//position number in a layer
    };
    public struct GRIDPOS
    {
        public short X;
        public short Y;
    };
    //*********************************************************
    public class VnnBpProj : IDisposable
    {
        //*****************************************************
        public string projFileName = "";
        public string projName = "";
        public string projDetail = "No Details";
        public VnnBpNet[] vnnnet;
        public VnnBpInterConn[] connP;
        public int maxNetLimit = 3;
        public short selNet = 0;
        public string errMsg = "";
        Random rnd = new Random(DateTime.Now.Millisecond);
        public string netDetails = "";
        //*****************************************************
        public VnnBpProj()
        {
        }
        //*****************************************************
        ~VnnBpProj()
        {
        }
        //*****************************************************
        #region IDisposable Members
        public void Dispose()
        {
            //throw new Exception("The method or operation is not implemented.");
        }
        #endregion
        //*****************************************************
        public bool AddNet(string configStr, string netDetails)
        {//configStr contains=>"maxIn, maxOut, connType, layer1, layer2, ..."
            this.netDetails = netDetails;
            errMsg = "";
            if (vnnnet == null)
            {
                vnnnet = new VnnBpNet[1];
                vnnnet[0] = new VnnBpNet(configStr);
                vnnnet[0].netDetail = this.netDetails;
                return true;
            }
            if (vnnnet.Length >= maxNetLimit)
            {
                errMsg = "Max Net Limit (" + maxNetLimit.ToString() + ") Exceeded";
                return false;
            }
            ArrayList al = new ArrayList();
            for (int i = 0; i < vnnnet.Length; i++)
            {
                al.Add(vnnnet[i]);
            }
            vnnnet = new VnnBpNet[vnnnet.Length + 1];
            for (int i = 0; i < al.Count; i++)
            {
                vnnnet[i] = (VnnBpNet)al[i];
            }
            vnnnet[vnnnet.Length-1] = new VnnBpNet(configStr);
            selNet = (short)(vnnnet.Length - 1);
            vnnnet[0].netDetail = this.netDetails;
            return true;
        }
        //*****************************************************
        public bool AddNetCopyPast(int p)
        {//configStr contains=>"maxIn, maxOut, connType, layer1, layer2, ..."
            errMsg = "";
            if (vnnnet.Length >= maxNetLimit)
            {
                maxNetLimit++;
                errMsg = "Max Net Limit (" + maxNetLimit.ToString() + ") Exceeded";
                //return false;
            }
            ArrayList al = new ArrayList();
            for (int i = 0; i < vnnnet.Length; i++)
            {
                al.Add(vnnnet[i]);
            }
            VnnBpNet vnnbpnet = DuplicateNet(vnnnet[p]);
            al.Add(vnnbpnet);
            string fol1 = Directory.GetParent(projFileName).Parent.FullName + @"\Data";
            string tngFlNmS = fol1 + @"\TngDt" + p.ToString().PadLeft(3, '0') + ".csv";
            string tngFlNmD = fol1 + @"\TngDt" + vnnnet.Length.ToString().PadLeft(3, '0') + ".csv";
            File.Copy(tngFlNmS, tngFlNmD, true);
            string tstFlNmS = fol1 + @"\TstDt" + p.ToString().PadLeft(3, '0') + ".csv";
            string tstFlNmD = fol1 + @"\TstDt" + vnnnet.Length.ToString().PadLeft(3, '0') + ".csv";
            File.Copy(tstFlNmS, tstFlNmD, true);
            string oiFunS = fol1 + @"\oiFun" + p.ToString().PadLeft(3, '0') + ".txt";
            if (File.Exists(oiFunS))
            {
                string oiFunD = fol1 + @"\oiFun" + vnnnet.Length.ToString().PadLeft(3, '0') + ".txt";
                File.Copy(oiFunS, oiFunD, true);
            }
            vnnnet = new VnnBpNet[vnnnet.Length + 1];
            for (int i = 0; i < al.Count; i++)
            {
                vnnnet[i] = (VnnBpNet)al[i];
            }
            for (int i = 0; i < vnnnet[vnnnet.Length - 1].neuron.Length; i++)
			{
                vnnnet[vnnnet.Length - 1].neuron[i].gridPos.X += 10;
			}
            for (int i = 0; i < vnnnet[selNet].neuron.Length; i++)
            {
                vnnnet[vnnnet.Length - 1].neuron[i].gridPos.X = vnnnet[selNet].neuron[i].gridPos.X;
                vnnnet[vnnnet.Length - 1].neuron[i].gridPos.Y = vnnnet[selNet].neuron[i].gridPos.Y;
                vnnnet[vnnnet.Length - 1].neuron[i].bias = vnnnet[selNet].neuron[i].bias;
                vnnnet[vnnnet.Length - 1].neuron[i].biasPP = vnnnet[selNet].neuron[i].biasPP;
                // InitializeNeuronsPosition2();
            }
            
            selNet = (short)(vnnnet.Length - 1);
            return true;
        }
        //**********************************************************
        private VnnBpNet DuplicateNet(VnnBpNet vnnBpNet)
        { //configStr contains=>"maxIn, maxOut, connType, layer1, layer2, ..."
            string configStr = vnnBpNet.netName + "Copy" + ",";
            configStr += vnnBpNet.nuLayers[0].ToString() + ",";
            configStr += vnnBpNet.nuLayers[vnnBpNet.nuLayers.Length - 1].ToString() + ",";
            configStr += vnnBpNet.maxConnections.ToString() + ",";
            for (int i = 1; i < vnnBpNet.nuLayers.Length - 1; i++)
            {
                configStr += vnnBpNet.nuLayers[i].ToString() + ",";
            }
            configStr = configStr.TrimEnd(',');
            VnnBpNet vnnBpNetNew = new VnnBpNet(configStr);
            for (int i = 0; i < vnnBpNet.conn.Length; i++)
            {
                vnnBpNetNew.conn[i] = new VnnBpConnection();
                vnnBpNetNew.conn[i].wgt = vnnBpNet.conn[i].wgt;
                vnnBpNetNew.conn[i].srcNuNo = vnnBpNet.conn[i].srcNuNo;
                vnnBpNetNew.conn[i].dstNuNo = vnnBpNet.conn[i].dstNuNo;
                vnnBpNetNew.conn[i].wgtPP = vnnBpNet.conn[i].wgtPP;
                vnnBpNetNew.conn[i].wgtTmp = vnnBpNet.conn[i].wgtTmp;
            }
            for (int i = 0; i < vnnBpNet.neuron.Length; i++)
            {
                vnnBpNetNew.neuron[i].netpos.lyrno = vnnBpNet.neuron[i].netpos.lyrno;
                vnnBpNetNew.neuron[i].netpos.rowno = vnnBpNet.neuron[i].netpos.rowno;
            }
            return vnnBpNetNew;
        }
        //**********************************************************
        public bool DeleateNet(int p)
        {
            errMsg = "";
            if (p > vnnnet.Length - 1)
            {
                errMsg = "Index Outside Limit";
                return false;
            }
            ArrayList al = new ArrayList();
            for (int i = 0; i < vnnnet.Length; i++)
            {
                al.Add(vnnnet[i]);
            }
            al.RemoveAt(p);
            vnnnet = new VnnBp.VnnBpLib.VnnBpNet[al.Count];
            for (int i = 0; i < vnnnet.Length; i++)
            {
                vnnnet[i] = (VnnBp.VnnBpLib.VnnBpNet)al[i];
            }
            selNet = (short)(p - 1);
            if (selNet < 0)
                selNet = 0;
            for (int i = 0; i < connP.Length; i++)
            {
                if (connP[i].dstNetNo > p)
                    connP[i].dstNetNo--;
                if (connP[i].srcNetNo > p)
                    connP[i].srcNetNo--;
            }
            return true;
        }
        //*****************************************************
        //public bool DeleteNet(int no)
        //{
        //    errMsg = "";
        //    if (no > vnnnet.Length - 1)
        //    {
        //        errMsg = "Index Outside Limit";
        //        return false;
        //    }
        //    ArrayList al = new ArrayList();
        //    for (int i = 0; i < vnnnet.Length; i++)
        //    {
        //        if (i != no)
        //        {
        //            al.Add(vnnnet[i]);
        //        }
        //    }
        //    vnnnet = new VnnBpNet[al.Count];
        //    for (int i = 0; i < al.Count; i++)
        //    {
        //        vnnnet[i] = (VnnBpNet)al[i];
        //    }
        //    selNet = (short)(no - 1);
        //    if (selNet < 0)
        //        selNet = 0;
        //    return true;
        //}
        //**********************************************************
        public void AddConnectionP(short netSrc, short nuSrc, short netDst, short nuDst)
        {
            if (connP == null)
            {
                connP = new VnnBp.VnnBpLib.VnnBpInterConn[1];
                connP[0] = new VnnBp.VnnBpLib.VnnBpInterConn();
                connP[0].srcNuNo = nuSrc;
                connP[0].srcNetNo = netSrc;
                connP[0].dstNuNo = nuDst;
                connP[0].dstNetNo = netDst;
                connP[0].wgt = 0.5 - (double)rnd.NextDouble();
            }
            else
            {
                ArrayList al = new ArrayList();
                for (int i = 0; i < connP.Length; i++)
                {
                    al.Add(connP[i]);
                }
                connP = new VnnBp.VnnBpLib.VnnBpInterConn[al.Count + 1];
                for (int i = 0; i < al.Count; i++)
                {
                    connP[i] = (VnnBp.VnnBpLib.VnnBpInterConn)al[i];
                }
                int n = al.Count;
                connP[n] = new VnnBp.VnnBpLib.VnnBpInterConn();
                connP[n].srcNuNo = nuSrc;
                connP[n].srcNetNo = netSrc;
                connP[n].dstNuNo = nuDst;
                connP[n].dstNetNo = netDst;
                connP[n].wgt = 0.5 - (double)rnd.NextDouble();
            }
        }
        //*****************************************************
        internal void UpdateConnObj(short netNo, int nuNo)
        {
            ArrayList al = new ArrayList();
            if (connP == null)
                return;
            for (int i = 0; i < connP.Length; i++)
            {
                bool flg = true;
                if ((connP[i].srcNetNo == netNo) & (connP[i].srcNuNo == nuNo))
                {
                    flg = false;
                }
                if ((connP[i].dstNetNo == netNo) & (connP[i].dstNuNo == nuNo))
                {
                    flg = false;
                }
                if (flg)
                {
                    al.Add(connP[i]);
                }
            }
            connP = new VnnBpInterConn[al.Count];
            for (int i = 0; i < connP.Length; i++)
            {
                connP[i] = (VnnBpInterConn)al[i];
                if (connP[i].srcNetNo == netNo)
                {
                    if (connP[i].srcNuNo >= nuNo)
                    {
                        connP[i].srcNuNo--;
                    }
                }
                if (connP[i].dstNetNo == netNo)
                {
                    if (connP[i].dstNuNo >= nuNo)
                    {
                        connP[i].dstNuNo--;
                    }
                }
            }
        }
        //*****************************************************
        internal void DeleateConnP(short selNet)
        {
            if(connP==null)
                return;
            if (connP.Length <= 0)
                return;
            ArrayList al = new ArrayList();
            for (int i = 0; i < connP.Length; i++)
            {
                if ((connP[i].srcNetNo != selNet) & (connP[i].dstNetNo != selNet))
                {
                    al.Add(connP[i]);
                }
            }
            connP = new VnnBpInterConn[al.Count];
            for (int i = 0; i < al.Count; i++)
            {
                connP[i] = (VnnBpInterConn)al[i];
            }
        }
        //*****************************************************
        internal void AddConnection(short selNet, short srcNu, short dstNu)
        {
            ArrayList al = new ArrayList();
            for (int i = 0; i < vnnnet[selNet].conn.Length; i++)
            {
                al.Add(vnnnet[selNet].conn[i]);
            }
            vnnnet[selNet].conn = new VnnBpConnection[al.Count + 1];
            vnnnet[selNet].maxConnections++;
            for (int i = 0; i < al.Count; i++)
            {
                vnnnet[selNet].conn[i] = (VnnBp.VnnBpLib.VnnBpConnection)al[i];
            }
            int n = al.Count;
            vnnnet[selNet].conn[n] = new VnnBp.VnnBpLib.VnnBpConnection();
            vnnnet[selNet].conn[n].srcNuNo = srcNu;
            vnnnet[selNet].conn[n].dstNuNo = dstNu;
            vnnnet[selNet].conn[n].wgt = 0.5 - (double)rnd.NextDouble();
        }
        //*****************************************************
        internal void DeleteConnection(short selNet, short srcNu, short dstNu)
        {
            ArrayList al = new ArrayList();
            for (int i = 0; i < vnnnet[selNet].conn.Length; i++)
            {
                if ((vnnnet[selNet].conn[i].srcNuNo == srcNu) & (vnnnet[selNet].conn[i].dstNuNo == dstNu))
                {

                }
                else
                {
                    al.Add(vnnnet[selNet].conn[i]);
                }
            }
            vnnnet[selNet].conn = new VnnBpConnection[al.Count];
            vnnnet[selNet].maxConnections--;
            for (int i = 0; i < al.Count; i++)
            {
                vnnnet[selNet].conn[i] = (VnnBp.VnnBpLib.VnnBpConnection)al[i];
            }
        }
        //*****************************************************
    }
}
