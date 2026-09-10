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
    public class VnnBpConnection
    {
        //**********************************************************
        public short srcNuNo;
        public short dstNuNo;
        public double wgt;
        public double wgtTmp;
        public double wgtPP;
        /// <summary>Topology snapshot for transactional prune rollback.</summary>
        public short srcTmp;
        public short dstTmp;
        public short srcPP;
        public short dstPP;
        //**********************************************************
        public VnnBpConnection()
        {
        }
        //**********************************************************
    }
    public class VnnBpInterConn:VnnBpConnection
    {
        //**********************************************************
        public short srcNetNo;
        public short dstNetNo;
        //**********************************************************
        public VnnBpInterConn()
        {
        }
        //**********************************************************
    }
}
