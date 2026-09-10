using System;
using System.Collections.Generic;
using System.Text;
using System.Xml;
using System.IO;
using System.Collections;
using System.Drawing;

namespace VnnBp.VnnBpLib
{
    class VnnBpLodSav
    {
        //**********************************************************
        public VnnBpProj proj;
        string[] cfgHdr = new string[] { "Type", "Num" };
        string[] nuHdr = new string[] { "SNo", "NuType", "NuFun", "ColNo", "RowNo", "Bias" };
        string[] connHdr = new string[] { "SNo", "Src", "Dst", "Weight" };
        string[] connPHdr = new string[] { "SNo", "SNet", "SNu", "Det", "Dnu", "Weight" };
        //string projName = "";
        //string[] cfgNam = new string[] { "typ", "no" };
        //string[] nuNam = new string[] { "sno", "nutype", "nufun", "colno", "rowno", "bias" };
        //string[] connNam = new string[] { "sno", "src", "dst", "wgt" };
        //**********************************************************
        public VnnBpLodSav()
        {
        }
        //**********************************************************
        public VnnBpLodSav(VnnBpProj vnnPrj)
        {
            this.proj = vnnPrj;
        }
        //**********************************************************
        public void SaveProjFile(string flnm)
        {
            string data = SaveProjData();
            File.WriteAllText(flnm, data);
        }
        //**********************************************************
        public string SaveProjData()
        {
            string prjStr = null;
            Encoding utf8noBOM = new UTF8Encoding(false);
            XmlWriterSettings settings = new XmlWriterSettings();
            settings.Indent = true;
            settings.Encoding = utf8noBOM;
            using (MemoryStream output = new MemoryStream())
            {
                using (XmlWriter writer = XmlWriter.Create(output, settings))
                {
                    writer.WriteStartDocument();
                    //VnnBpProj.................................
                    writer.WriteStartElement("VnnBpProj"); //<VnnBpProj>
                    //Proj Name.................................
                    //writer.WriteStartElement("Name");// <Name>Name</Name>
                    //writer.WriteString(proj.projName);
                    //writer.WriteEndElement();
                    //Proj Detail.................................
                    //writer.WriteStartElement("Detail");// <Detail>detail</Detail>
                    //writer.WriteString(proj.projDetail);
                    //writer.WriteEndElement();
                    //Net Limit Numbers.................................
                    //writer.WriteStartElement("NetLmt");// <Nets>Nets-Count</Nets>
                    //writer.WriteString(proj.maxNetLimit.ToString());
                    //writer.WriteEndElement();
                    //Nets .................................
                    if (proj.vnnnet != null)
                    {
                        //Net Numbers.................................
                        writer.WriteStartElement("Nets");// <Nets>Nets-Count</Nets>
             int nn = proj.vnnnet.Length + 1;//?????
                        writer.WriteString(proj.vnnnet.Length.ToString());
                        writer.WriteEndElement();
                        for (int i = 0; i < proj.vnnnet.Length; i++)
                        {
                            SaveNetData(writer, i);
                          //  SaveDataCsvFiles(i);
                        }
                       // SaveInterConns(writer);
                    }
                    else
                    {
                        //Net Numbers 0.................................
                        writer.WriteStartElement("Nets");// <Nets>Nets-Count</Nets>
                        writer.WriteString("0");
                        writer.WriteEndElement();
                    }
                    writer.WriteEndElement();
                    writer.WriteEndDocument();
                }
                prjStr = Encoding.Default.GetString(output.ToArray());
            }
            return prjStr;
        }
        //**********************************************************
        private void SaveDataCsvFiles(int netNo)
        {
            if (proj.projFileName == null)
                return;
            string fol1 = Directory.GetParent(proj.projFileName).Parent.FullName + @"\Data";
            string tngFlNm = fol1 + @"\TngDt" + netNo.ToString().PadLeft(3, '0') + ".csv";
            string tstFlNm = fol1 + @"\TstDt" + netNo.ToString().PadLeft(3, '0') + ".csv";

            bool flg1 = File.Exists(tngFlNm);
            bool flg2 = File.Exists(tstFlNm);
            int nn = 0;
            int din = 0;
            int dout = 0;
            if (!flg1)
            {
                StreamWriter sw1 = new StreamWriter(tngFlNm);
                string str1 = "SNo,";
                for (int i = 0; i < proj.vnnnet[netNo].neuron.Length; i++)
                {
                    if ((proj.vnnnet[netNo].neuron[i].ihono == VnnBpLib.NuType.In) | (proj.vnnnet[netNo].neuron[i].ihono == VnnBpLib.NuType.Out))
                    {
                        if (proj.vnnnet[netNo].neuron[i].ihono == VnnBpLib.NuType.In)
                        {
                            string s1 = "DI-" + netNo + "-" + din;
                            str1 += s1 + ",";
                            din++;
                        }
                        nn++;
                    }
                }
                str1 = str1.TrimEnd(',');
                sw1.WriteLine(str1);
                sw1.Close();
            }
            if (!flg2)
            {
                nn = 0;
                din = 0;
                dout = 0;
                StreamWriter sw2 = new StreamWriter(tstFlNm);
                string str1 = "SNo,";
                for (int i = 0; i < proj.vnnnet[netNo].neuron.Length; i++)
                {
                    if ((proj.vnnnet[netNo].neuron[i].ihono == VnnBpLib.NuType.In) | (proj.vnnnet[netNo].neuron[i].ihono == VnnBpLib.NuType.Out))
                    {
                        if (proj.vnnnet[netNo].neuron[i].ihono == VnnBpLib.NuType.Out)
                        {
                            string s2 = "DO-" + netNo + "-" + dout;
                            str1 += s2 + ",";
                            dout++;
                        }
                        nn++;
                    }
                }
                str1 = str1.TrimEnd(',');
                str1 = str1.TrimEnd(',');
                sw2.WriteLine(str1);
                sw2.Close();
            }
        }
        //**********************************************************
        private void SaveInterConns(XmlWriter writer)
        {
            //Connections............................
            writer.WriteStartElement("ConnP");
            writer.WriteStartElement("Dt");
            string buf = "Size,";
            if (proj.connP != null)
            {
                buf += proj.connP.Length.ToString();
            }
            else
            {
                buf += "0";
            }
            writer.WriteString(buf);
            writer.WriteEndElement();//"Dt"
            writer.WriteStartElement("Hd");
            buf = "";
            for (int i = 0; i < connPHdr.Length; i++)
            {
                buf += connPHdr[i] + ",";
            }
            buf = buf.TrimEnd(',');
            writer.WriteString(buf);
            writer.WriteEndElement();//"Hd"
            if (proj.connP != null)
            {
                for (int i = 0; i < proj.connP.Length; i++)
                {
                    writer.WriteStartElement("Dt");
                    buf = i.ToString() + ",";
                    buf += proj.connP[i].srcNetNo.ToString() + ",";
                    buf += proj.connP[i].srcNuNo.ToString() + ",";
                    buf += proj.connP[i].dstNetNo.ToString() + ",";
                    buf += proj.connP[i].dstNuNo.ToString() + ",";
                    buf += proj.connP[i].wgt.ToString();
                    writer.WriteString(buf);
                    writer.WriteEndElement();//"Dt"
                }
            }
            writer.WriteEndElement();//"Conn"
        }
        //**********************************************************
        public void SaveNetData(XmlWriter writer, int netNo)
        {
            VnnBpNet vnnBpNet = proj.vnnnet[netNo];
            //VnnBpNet.................................
            writer.WriteStartElement("VnnBpNet"); //<VnnBpNet>
            //Net Name.................................
            writer.WriteStartElement("Name");// <Name>
            writer.WriteString(vnnBpNet.netName);
            writer.WriteEndElement();// </Name>
            //Net Detail.................................
            writer.WriteStartElement("Detail");// <Detail>
            writer.WriteString(proj.vnnnet[netNo].netDetail);
            writer.WriteEndElement();// </Detail>
            //Net Data.................................
            writer.WriteStartElement("Data");// <Data>
            writer.WriteStartElement("TngData");// <TngData>
            writer.WriteString("Training FileName: TngDt" + netNo.ToString().PadLeft(3, '0') + ".csv");
            writer.WriteEndElement();// </TngData>
            writer.WriteStartElement("TestData");// <TestData>
            writer.WriteString("Test FileName: TstDt" + netNo.ToString().PadLeft(3, '0') + ".csv");
            writer.WriteEndElement();// </TestData>
            writer.WriteEndElement();// </Data>
            //Config.................................
            writer.WriteStartElement("Cfg");// <Cfg>value</Cfg>
            writer.WriteStartElement("Hd");
            string buf = "";
            for (int i = 0; i < cfgHdr.Length; i++)
            {
                buf += cfgHdr[i] + ",";
            }
            buf = buf.TrimEnd(',');
            writer.WriteString(buf);
            writer.WriteEndElement();//"Hd"
            writer.WriteStartElement("Dt");
            buf = "in," + vnnBpNet.maxInputs.ToString();
            writer.WriteString(buf);
            writer.WriteEndElement();//"Dt"

            writer.WriteStartElement("Dt");
            buf = "out," + vnnBpNet.maxOutputs.ToString();
            writer.WriteString(buf);
            writer.WriteEndElement();//"Dt"

            writer.WriteStartElement("Dt");
            buf = "hdn,";
            for (int i = 0; i < vnnBpNet.nuLayers.Length - 2; i++)//-2 to exclude in & out
            {
                buf += vnnBpNet.nuLayers[i + 1].ToString()+",";//+1 to exclude in
            }
            buf = buf.TrimEnd(',');
            writer.WriteString(buf);
            writer.WriteEndElement();//"Dt"

            writer.WriteStartElement("Dt");
            buf = "typ" + "," + vnnBpNet.connTypeNo;
            writer.WriteString(buf);
            writer.WriteEndElement();//"Dt"

            writer.WriteStartElement("Dt");
            buf = "con" + "," + vnnBpNet.maxConnections;
            writer.WriteString(buf);
            writer.WriteEndElement();//"Dt"
            //
            writer.WriteStartElement("Dt");
            buf = "pos" + "," + vnnBpNet.maxNeurons;
            writer.WriteString(buf);
            writer.WriteEndElement();//"Dt"
            //
            writer.WriteStartElement("Dt");
            Point pt = new Point(0, 0);
            if (proj.vnnnet[netNo].trainthisnet != null)
            {
                pt = proj.vnnnet[netNo].trainthisnet.Location;
            }
            buf = "tng" + "," + pt.X.ToString() + "," + pt.Y.ToString();
            writer.WriteString(buf);
            writer.WriteEndElement();//"Dt"
            //
            writer.WriteEndElement();//"Cfg"
            //End Config.................................
            //Neurons................................
            //"SNo", "NuType", "NuFun", "ColNo", "RowNo", "Bias"
            writer.WriteStartElement("Nu");
            writer.WriteStartElement("Hd");
            buf = "";
            for (int i = 0; i < nuHdr.Length; i++)
            {
                buf += nuHdr[i] + ",";
            }
            buf = buf.TrimEnd(',');
            writer.WriteString(buf);
            writer.WriteEndElement();//"Hd"

            for (int i = 0; i < vnnBpNet.neuron.Length; i++)
            {
                writer.WriteStartElement("Dt");
                buf = i.ToString() + ",";
                //buf += vnnBpNet.ihoName[(byte)(vnnBpNet.neuron[i].ihono2)] + ",";
                buf += vnnBpNet.neuron[i].ihono + ",";
                buf += VnnBpNet.funName[vnnBpNet.neuron[i].funno] + ",";
                buf += (vnnBpNet.neuron[i].netpos.lyrno).ToString() + ",";
                buf += (vnnBpNet.neuron[i].netpos.rowno).ToString() + ",";
                buf += vnnBpNet.neuron[i].bias;
                writer.WriteString(buf);
                writer.WriteEndElement();//"Dt"
            }
            writer.WriteEndElement();//"Nu"

            //Connections............................
            writer.WriteStartElement("Conn");
            writer.WriteStartElement("Hd");
            buf = "";
            for (int i = 0; i < connHdr.Length; i++)
            {
                buf += connHdr[i] + ",";
            }
            buf = buf.TrimEnd(',');
            writer.WriteString(buf);
            writer.WriteEndElement();//"Hd"

            for (int i = 0; i < vnnBpNet.conn.Length; i++)
            {
                writer.WriteStartElement("Dt");
                buf = i.ToString() + ",";
                buf += vnnBpNet.conn[i].srcNuNo.ToString() + ",";
                buf += vnnBpNet.conn[i].dstNuNo.ToString() + ",";
                buf += vnnBpNet.conn[i].wgt.ToString();
                writer.WriteString(buf);
                writer.WriteEndElement();//"Dt"
            }
            writer.WriteEndElement();//"Conn"
            //GridPos................................
            //"SNo", "PosX", "PosY"
            writer.WriteStartElement("Pos");
            writer.WriteStartElement("Hd");
            buf = "SNo, PosX, PosY";
            writer.WriteString(buf);
            writer.WriteEndElement();//"Hd"

            for (int i = 0; i < vnnBpNet.neuron.Length; i++)
            {
                writer.WriteStartElement("Dt");
                buf = i.ToString() + ",";
                //buf += vnnBpNet.neuron[i].netpos.rowno.ToString() + ",";
                //buf += vnnBpNet.neuron[i].netpos.lyrno.ToString() + ",";
                buf += vnnBpNet.neuron[i].gridPos.X.ToString() + ",";
                buf += vnnBpNet.neuron[i].gridPos.Y.ToString();
                writer.WriteString(buf);
                writer.WriteEndElement();//"Dt"
            }
            writer.WriteEndElement();//"Nu"
            //.......................................
            writer.WriteEndElement();//"VnnBp"
        }
        //**********************************************************
        //**********************************************************
        public string LoadProjData(string flnm)
        {
            XmlTextReader reader = new XmlTextReader(flnm);
            ArrayList al = new ArrayList();
            while (reader.Read())
            {
                reader.MoveToContent();
                al.Add(reader.NodeType);
                al.Add(reader.Name);
                al.Add(reader.Value);
            }
            reader.Close();
            //StreamWriter sw = new StreamWriter("test.txt");
            //for (int i = 0; i < al.Count; i++)
            //{
            //    sw.WriteLine(i.ToString() + "  " + al[i].ToString());
            //}
            //sw.Close();
            if (al[1].ToString() != "VnnBpProj")
            {
                return "Error";
            }
            proj = new VnnBpProj();
            //proj.projName = al[8].ToString();
            //proj.maxNetLimit = int.Parse(al[26].ToString());
            int nets = int.Parse(al[35-27].ToString());
            proj.vnnnet = new VnnBpNet[nets];
            int ndx = 35-27;
            int ptr = 0;
            for (int n = 0; n < nets; n++)
            {//configStr contains=>"maxIn, maxOut, connType, layer1, layer2, ..."
                ptr = GetElement(al, ndx, "VnnBpNet");
                string netName = al[ptr + 8].ToString();
                ptr = GetElement(al, ptr, "Cfg");

                ndx = GetElement(al, ptr + 1, "VnnBpNet");
                int typ = int.Parse(al[ptr + 44].ToString().Split(',')[1]);
                int ins = int.Parse(al[ptr + 17].ToString().Split(',')[1]);
                int outs = int.Parse(al[ptr + 26].ToString().Split(',')[1]);
                int nh = al[ptr + 35].ToString().Split(',').Length;
                int con = int.Parse(al[ptr + 53].ToString().Split(',')[1]);
                int ptr2 = ptr;
                //if (al[ptr + 71].ToString() != "")//to make project compatible with old version
                //{
                //    int tng1 = int.Parse(al[ptr + 71].ToString().Split(',')[1]);
                //    int tng2 = int.Parse(al[ptr + 71].ToString().Split(',')[2]);
                //}
                int hdns = 0;

               // string configStr = netName + "," + ins.ToString() + "," + outs.ToString() + "," + VnnBpNet.connTypeName[typ] + ",";

                string configStr = netName + "," + ins.ToString() + "," + outs.ToString() + "," + con.ToString() + ",";
                if (nh > 1)
                {
                    string[] hddt = al[ptr + 35].ToString().Split(',');
                    for (int j = 1; j < hddt.Length; j++)
                    {
                        configStr += hddt[j] + ",";
                        hdns += int.Parse(hddt[j]);
                    }
                    configStr = configStr.TrimEnd(',');
                }
                proj.vnnnet[n] = new VnnBpNet(configStr);//***Create Net*******
                proj.vnnnet[n].netName = netName;

                ptr = GetElement(al, ptr, "Nu");
                int nuno = 0;
                for (int i = 0; i < ins; i++)
                {
                    ptr = GetElement(al, ptr + 1, "Dt");
                    string[] inn = al[ptr + 5].ToString().Split(',');
                    proj.vnnnet[n].neuron[nuno].sno = short.Parse(inn[0]);
                    string nutyp = inn[1];
                    proj.vnnnet[n].neuron[nuno].ihono = GetNuType(inn[1]);
                    proj.vnnnet[n].neuron[nuno].netpos.lyrno = short.Parse(inn[3]);
                    proj.vnnnet[n].neuron[nuno].netpos.rowno = short.Parse(inn[4]);
                    proj.vnnnet[n].neuron[nuno].bias = double.Parse(inn[5]);
                    proj.vnnnet[n].neuron[nuno].funno = GetFunNo(inn[2]);

                    if (proj.vnnnet[n].neuron[nuno].ihono == NuType.In)// Important change for inter object connection/training
                    {//temp now; must be conditional
                        proj.vnnnet[n].neuron[nuno].bias = 0;
                    }
                    nuno++;
                }
                for (int i = 0; i < hdns; i++)
                {
                    ptr = GetElement(al, ptr + 1, "Dt");
                    string[] hdnn = al[ptr + 5].ToString().Split(',');
                    string nutyp = hdnn[1];
                    proj.vnnnet[n].neuron[nuno].ihono = GetNuType(hdnn[1]);
                    proj.vnnnet[n].neuron[nuno].netpos.lyrno = short.Parse(hdnn[3]);
                    proj.vnnnet[n].neuron[nuno].netpos.rowno = short.Parse(hdnn[4]);
                    proj.vnnnet[n].neuron[nuno].bias = double.Parse(hdnn[5]);
                    proj.vnnnet[n].neuron[nuno].funno = GetFunNo(hdnn[2]);
                    nuno++;
                }
                for (int i = 0; i < outs; i++)
                {
                    ptr = GetElement(al, ptr + 1, "Dt");
                    string[] outn = al[ptr + 5].ToString().Split(',');
                    proj.vnnnet[n].neuron[nuno].ihono = GetNuType(outn[1]);
                    proj.vnnnet[n].neuron[nuno].funno = GetFunNo(outn[2]);
                    proj.vnnnet[n].neuron[nuno].netpos.lyrno = short.Parse(outn[3]);
                    proj.vnnnet[n].neuron[nuno].netpos.rowno = short.Parse(outn[4]);
                    proj.vnnnet[n].neuron[nuno].bias = double.Parse(outn[5]);
                    proj.vnnnet[n].neuron[nuno].funno = GetFunNo(outn[2]);
                    nuno++;
                }
                ptr = GetElement(al, ptr, "Conn");
                for (int i = 0; i < proj.vnnnet[n].conn.Length; i++)
                {
                    ptr = GetElement(al, ptr + 1, "Dt");
                    string[] outn = al[ptr + 5].ToString().Split(',');
                    if (proj.vnnnet[n].conn[i] == null)
                    {
                        proj.vnnnet[n].conn[i] = new VnnBpConnection();
                    }
                    proj.vnnnet[n].conn[i].srcNuNo = short.Parse(outn[1]);
                    proj.vnnnet[n].conn[i].dstNuNo = short.Parse(outn[2]);
                    proj.vnnnet[n].conn[i].wgt = double.Parse(outn[3]);
                }
                ptr = GetElement(al, ptr, "Pos");
                for (int i = 0; i < proj.vnnnet[n].neuron.Length; i++)
                {
                    ptr = GetElement(al, ptr + 1, "Dt");
                    string[] outn = al[ptr + 5].ToString().Split(',');
                    proj.vnnnet[n].neuron[i].sno = short.Parse(outn[0]);
                    proj.vnnnet[n].neuron[i].gridPos.X = short.Parse(outn[1]);
                    proj.vnnnet[n].neuron[i].gridPos.Y = short.Parse(outn[2]);
                }
                if (al[ptr2 + 71].ToString() != "")//to make project compatible with old version
                {
                    int tng1 = int.Parse(al[ptr2 + 71].ToString().Split(',')[1]);
                    int tng2 = int.Parse(al[ptr2 + 71].ToString().Split(',')[2]);
                    proj.vnnnet[n].pos = new Point(tng1, tng2);
                }
            }
            if (nets > 0)
            {
                ptr = GetElement(al, ptr, "ConnP");
                if (ptr >= al.Count)
                    return "";
                ptr = ptr + 8;
                string[] outn2 = al[ptr].ToString().Split(',');
                proj.connP = new VnnBpInterConn[int.Parse(outn2[1])];
                for (int n = 0; n < proj.connP.Length; n++)
                {
                    ptr = GetElement(al, ptr, "Dt");
                    string[] outn = al[ptr + 5].ToString().Split(',');

                    proj.connP[n] = new VnnBpInterConn();
                    proj.connP[n].srcNetNo = short.Parse(outn[1]);
                    proj.connP[n].srcNuNo = short.Parse(outn[2]);
                    proj.connP[n].dstNetNo = short.Parse(outn[3]);
                    proj.connP[n].dstNuNo = short.Parse(outn[4]);
                    proj.connP[n].wgt = double.Parse(outn[5]);
                    ptr += 5;
                }
            }
            return "";
        }
        //**********************************************************
        private short GetFunNo(string p)
        {//"nil", "y=x", "exp+", "exp+-", "sqrt", "dly=1", "dly=d" 
            for (short i = 0; i < VnnBpNet.funName.Length; i++)
            {
                if (VnnBpNet.funName[i] == p)
                    return i;
            }
            return 0;
        }
        //**********************************************************
        private NuType GetNuType(string p)
        {//public enum NuType { None, In, Out, Hdn };
            switch (p)
            {
                case "None":
                    return NuType.None;
                    break;
                case "In":
                    return NuType.In;
                    break;
                case "Out":
                    return NuType.Out;
                    break;
                case "Hdn":
                    return NuType.Hdn;
                    break;
            }
            return NuType.None;
        }
        //**********************************************************
        private int GetElement(ArrayList al, int p, string tag)
        {
            int no = p;
            for (no = p; no < al.Count; no++)
            {
                if (al[no].ToString() == "Element")
                {
                    if (al[no + 1].ToString() == tag)
                        break;
                }
            }
            return no;
        }
        //**********************************************************
        public void SaveNetFile(string flnm)
        {
    //<Name>VNetName</Name>
    //<Detail>No Details</Detail>
    //<Data>
    //  <TngData>Training FileName: TngDt000.csv</TngData>
    //  <TestData>Test FileName: TstDt000.csv</TestData>
    //</Data>
        }
        //**********************************************************
        public void LoadNetFile(string flnm)
        {
        }
        //**********************************************************
    }
}
