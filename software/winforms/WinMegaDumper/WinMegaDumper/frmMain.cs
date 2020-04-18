﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO.Ports;
using System.Threading;
using System.IO;
using System.Timers;

namespace WinCartDumper
{

    public partial class frmMain : Form
    {

        /*
         * The winform icon is the work of Hopstarter (Jojo Mendoza)
            URL: http://hopstarter.deviantart.com
            License: CC Attribution-Noncommercial-No Derivate 4.0
        */

        MegaDumper megaDumper;

        private const string LABEL_READY = "Ready.";
        private const string LABEL_DUMP = "Reading cart...";
        private const string LABEL_AUTODETECT = "Attempt to autodetect GENDUMPER...";

        public frmMain()
        {
            InitializeComponent();

            megaDumper = new MegaDumper();
            megaDumper.ProgressChanged += MegaDumper_ProgressChanged;
            megaDumper.DoWork += MegaDumper_DoWork;
            megaDumper.RunWorkerCompleted += MegaDumper_RunWorkerCompleted;

        }


        private void refreshPortList(string[] ports)
        {
            serialPortToolStripMenuItem.DropDownItems.Clear();
            serialPortToolStripMenuItem.DropDownItems.Add(autodetectToolStripMenuItem);
            serialPortToolStripMenuItem.DropDownItems.Add(toolStripSerialSeperator);
            foreach (string port in ports)
            {
                ToolStripMenuItem itm = new ToolStripMenuItem(port, null, portToolStripMenuItem_OnClick);
                serialPortToolStripMenuItem.DropDownItems.Add(itm);
            }
        }
        private void refreshPortList()
        {
            string[] ports = SerialPort.GetPortNames();
            refreshPortList(ports);

        }

        private void portToolStripMenuItem_OnClick(object sender, EventArgs e)
        {
            var selectedItem = (ToolStripMenuItem)sender;

            foreach(ToolStripItem itm in serialPortToolStripMenuItem.DropDownItems)
            {
                if(itm == (ToolStripItem)selectedItem)
                {
                    log("Selected serial port " + itm.Text, LogType.Info);
                    serialPortToolStripMenuItem.Text = "Serial Port: <" + itm.Text + ">";
                    serialPortToolStripMenuItem.Tag = itm.Text;
                    selectedItem.Checked = true;
                    megaDumper.Port = itm.Text;
                }
                else
                {
                    if(itm.GetType() == typeof(ToolStripMenuItem))
                    {
                        ((ToolStripMenuItem)itm).Checked = false;
                    }
                }
            }          
        }

        private void frmMain_Load(object sender, EventArgs e)
        {
            refreshPortList();
            toolStripStatusLabel.Text = LABEL_READY;
            SerialPortService.PortsChanged += (sender1, changedArgs) => SerialPortService_PortsChanged(changedArgs.SerialPorts);
        }

        private void SerialPortService_PortsChanged(string[] serialPorts)
        {
            refreshPortList(serialPorts);
        }

        public enum LogType
        {
            Info,
            Warning,
            Success,
            Error
        }


        private static Color colorError = Color.FromArgb(192, 0, 0);
        private static Color colorDefault = Color.FromArgb(0, 0, 0);
        private static Color colorSuccess = Color.FromArgb(0, 192, 0);
        private static Color colorWarning = Color.FromArgb(192, 192, 0);
        public void log(string message)
        {
            log(message, LogType.Info);
        }
        public void log(string message, LogType logtype)
        {
            Color c;
            switch (logtype)
            {
                case LogType.Warning:
                    c = colorWarning;
                    break;
                case LogType.Error:
                    c = colorError;
                    break;
                case LogType.Success:
                    c = colorSuccess;
                    break;
                default:
                    c = colorDefault;
                    break;
            }

            int currentIndex = rtfLog.TextLength;
            StringBuilder sb = new StringBuilder();
            sb.Append(DateTime.Now.ToString("yyyy-MM-dd hh:mm:ss"));
            sb.Append(": ");
            sb.Append(message);
            sb.AppendLine();

            rtfLog.Select(rtfLog.TextLength, 0);
            rtfLog.AppendText(sb.ToString());
            rtfLog.Select(currentIndex, sb.Length);
            rtfLog.SelectionColor = c;
            rtfLog.Select(rtfLog.TextLength, 0);
            rtfLog.ScrollToCaret();
        }

        private void btnAutodetect_Click(object sender, EventArgs e)
        {
            Thread autoDetect = new Thread(AutoDetect);
            autoDetect.Start();
            autoDetect.Join();

            //cmdPorts.Items.AddRange(ports);
        }

        public static void AutoDetectEnd()
        {

        }




        private void autodetectToolStripMenuItem_Click(object sender, EventArgs e)
        {
            AutoDetect();
        }
        private void AutoDetect()
        {
            megaDumper.Operation = MegaDumperOperation.Autodetect;
            megaDumper.RunWorkerAsync();
        }


        public static void Read()
        {

        }



        private void btnGetInfo_Click(object sender, EventArgs e)
        {
            log("Attempt to extract rom header from cart", LogType.Info);

            return;
            megaDumper.Port = "COM5";

            rtfLog.Text += "INFO: Cart header dump start\r\n";

            RomHeader h = megaDumper.getRomHeader();

            rtfLog.Text += "SUCCESS: Retrieved 256 bytes\r\n";

            txtGameTitle.Text = h.DomesticGameTitle;
            txtCopyright.Text = h.Copyright;
            txtRomSize.Text =   ((h.RomAddressEnd - h.RomAddressStart + 1) / 1024 ).ToString() + " kbytes";
            txtRegion.Text = h.Region;
            txtSerialNumber.Text = h.SerialNumber;
            txtSave.Text = ((h.SaveChip.EndAddress - h.SaveChip.StartAddress + 1) / 1024).ToString() + " kbytes";

            
        }
        


        private void btnDump_Click(object sender, EventArgs e)
        {
            if(megaDumper.Port == string.Empty)
            {
                MessageBox.Show("Please select a serial port or run autodect.", "No serial port selected.", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            else
            {
                log("Rom dump started");
                toolStripStatusLabel.Text = LABEL_DUMP;
                megaDumper.Operation = MegaDumperOperation.Dump;
                megaDumper.RunWorkerAsync();
            }
            
        }

        private void MegaDumper_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            toolStripProgressBar.Value = 100;
            rtfLog.Text += (string)e.Result;
            toolStripStatusLabel.Text = LABEL_READY;
        }

        private void MegaDumper_DoWork(object sender, DoWorkEventArgs e)
        {
            MegaDumperResult mdr = new MegaDumperResult();

            if(megaDumper.Operation == MegaDumperOperation.Dump)
            {
                mdr = megaDumper.GetDump((uint)0, (uint)50000);
                e.Result = mdr;
            }
            else if(megaDumper.Operation == MegaDumperOperation.Version)
            {
                string ss = megaDumper.GetVersion();
            }
            else if(megaDumper.Operation == MegaDumperOperation.Autodetect)
            {
                string ss = megaDumper.AutoDetect();
                e.Result = "Mega Dumper detected on " + ss;
            }
                
            //RomHeader h = megaDumper.getRomHeader();

            
        }

        private void MegaDumper_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            toolStripProgressBar.Value = e.ProgressPercentage;
        }

        private void saveLogToolStripMenuItem_Click(object sender, EventArgs e)
        {

        }

        private void aboutToolStripMenuItem_Click(object sender, EventArgs e)
        {
            frmAbout f = new frmAbout();
            f.ShowDialog();
        }

        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this.Close();
        }


    }


}
