﻿using System;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

namespace XrmToolBox.Forms
{
    public partial class WelcomeDialog : Form
    {
        public WelcomeDialog(string version)
        {
            InitializeComponent();

            lblVersion.Text = string.Format("version: {0}", version);

            ManageLicense();

            var timer = new Timer();
            timer.Tick += TimerTick;
            timer.Interval = 3000;
            timer.Start();
        }

        private void ManageLicense()
        {
            try
            {
                var location = Assembly.GetExecutingAssembly().Location;
                var fiLocation = new FileInfo(location);
                var assembly = Assembly.LoadFile(fiLocation.Directory + "\\McTools.StopAdvertisement.dll");

                if (assembly != null)
                {
                    Type type = assembly.GetType("McTools.StopAdvertisement.LicenseManager");
                    if (type == null) { return; }

                    MethodInfo methodInfo = type.GetMethod("IsValid");
                    if (methodInfo == null) { return; }

                    object classInstance = Activator.CreateInstance(type, null);

                    if ((bool)methodInfo.Invoke(classInstance, null))
                    {
                        PropertyInfo userNameInfo = type.GetProperty("UserName");
                        PropertyInfo orgNameInfo = type.GetProperty("OrganizationName");

                        var userName = userNameInfo.GetValue(classInstance, null).ToString();
                        var orgName = orgNameInfo.GetValue(classInstance, null).ToString();

                        lblSupport.Text = string.Format(lblSupport.Text,
                            userName,
                            orgName.Length > 0 && orgName != userName ? string.Format(" ({0})", orgName) : "");

                        pnlSupport.Visible = true;
                    }
                }
            }
            catch (FileNotFoundException)
            {
                // Nothing to do as if the file is missing, it's not big deal
            }
            catch (NotSupportedException)
            {
                MessageBox.Show(this,
                    "It seems you maybe forgot to unblock XrmToolBox.zip before extracting it. XrmToolBox can't work as expected until you unblocked all files. To do so, display XrmToolBox.zip properties and unblock the file before extracting it", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                //pnlSupport.Visible = false;
                //panel2.Visible = true;
            }
        }

        private void TimerTick(object sender, EventArgs e)
        {
            ((Timer)sender).Stop();
            Close();
        }
    }
}