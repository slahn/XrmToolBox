﻿// PROJECT : MsCrmTools.WebResourcesManager
// This project was developed by Tanguy Touzard
// CODEPLEX: http://xrmtoolbox.codeplex.com
// BLOG: http://mscrmtools.blogspot.com

using Microsoft.Xrm.Sdk;
using System;
using System.Windows.Forms;

namespace MsCrmTools.WebResourcesManager.Forms
{
    internal partial class UpdateForm : Form
    {
        #region Variables

        /// <summary>
        /// Current script
        /// </summary>
        private readonly WebResource currentWebResource;

        /// <summary>
        /// Current Prefix for names depending on selected solution
        /// </summary>
        private string currentPrefix = string.Empty;

        /// <summary>
        /// Xrm Organization Service
        /// </summary>
        private IOrganizationService innerService;

        #endregion Variables

        #region Constructor

        /// <summary>
        /// Initializes a new instance of class UpdateForm
        /// </summary>
        /// <param name="script">Script to display or to create</param>
        /// <param name="service">Xrm Organization Service</param>
        public UpdateForm(WebResource script, IOrganizationService service)
        {
            InitializeComponent();

            innerService = service;
            currentWebResource = script;

            FillControls();
        }

        #endregion Constructor

        #region Properties

        /// <summary>
        /// Script to update
        /// </summary>
        internal WebResource WebRessource
        {
            get
            {
                return currentWebResource;
            }
        }

        #endregion Properties

        #region Methods

        private void FillControls()
        {
            if (!string.IsNullOrEmpty(currentWebResource.FilePath))
                txtPath.Text = currentWebResource.FilePath;

            if (currentWebResource.WebResourceEntity.Contains("name"))
                txtName.Text = currentWebResource.WebResourceEntity["name"].ToString();

            if (currentWebResource.WebResourceEntity.Contains("displayname"))
                txtDisplayName.Text = currentWebResource.WebResourceEntity["displayname"].ToString();

            if (currentWebResource.WebResourceEntity.Contains("description"))
                txtDescription.Text = currentWebResource.WebResourceEntity["description"].ToString();
        }

        #endregion Methods

        private void btnCancel_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
        }

        private void btnOK_Click(object sender, EventArgs e)
        {
            currentWebResource.WebResourceEntity["displayname"] = txtDisplayName.Text;
            currentWebResource.WebResourceEntity["description"] = txtDescription.Text;

            DialogResult = DialogResult.OK;
        }
    }
}