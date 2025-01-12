﻿using Microsoft.Crm.Sdk.Messages;
using Microsoft.Xrm.Client.Services;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Client;
using Microsoft.Xrm.Sdk.Discovery;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;

namespace McTools.Xrm.Connection
{
    #region Event Args Class Definition

    public class ConnectionFailedEventArgs : EventArgs
    {
        public string FailureReason { get; set; }
        public object Parameter { get; set; }
    }

    public class ConnectionSucceedEventArgs : EventArgs
    {
        public ConnectionDetail ConnectionDetail { get; set; }
        public IOrganizationService OrganizationService { get; set; }
        public object Parameter { get; set; }
    }

    public class DeleteConnectionEventArgs : EventArgs
    {
    }

    public class EditConnectEventArgs : EventArgs
    {
    }

    public class RequestPasswordEventArgs : EventArgs
    {
        public RequestPasswordEventArgs(ConnectionDetail connectionDetail)
        {
            ConnectionDetail = connectionDetail;
        }

        public ConnectionDetail ConnectionDetail { get; set; }
    }

    public class StepChangedEventArgs : EventArgs
    {
        public string CurrentStep { get; set; }
    }

    public class UseProxyEventArgs : EventArgs
    {
        public IWebProxy Proxy { get; set; }
    }

    #endregion Event Args Class Definition

    /// <summary>
    /// Manager that handles all connection operations
    /// </summary>
    public class ConnectionManager
    {
        #region Delegates

        public delegate void ConnectionFailedEventHandler(object sender, ConnectionFailedEventArgs e);

        public delegate void ConnectionListUpdatedEventHandler(object sender, EventArgs e);

        public delegate void ConnectionSucceedEventHandler(object sender, ConnectionSucceedEventArgs e);

        public delegate bool RequestPasswordEventHandler(object sender, RequestPasswordEventArgs e);

        public delegate void StepChangedEventHandler(object sender, StepChangedEventArgs e);

        public delegate void UseProxyEventHandler(object sender, UseProxyEventArgs e);

        #endregion Delegates

        #region Event Handlers

        public event ConnectionFailedEventHandler ConnectionFailed;

        public event ConnectionListUpdatedEventHandler ConnectionListUpdated;

        public event ConnectionSucceedEventHandler ConnectionSucceed;

        public event RequestPasswordEventHandler RequestPassword;

        public event StepChangedEventHandler StepChanged;

        public event UseProxyEventHandler UseProxy;

        #endregion Event Handlers

        #region Constants

        internal const string CryptoHashAlgorythm = "SHA1";
        internal const string CryptoInitVector = "ahC3@bCa2Didfc3d";
        internal const int CryptoKeySize = 256;
        internal const string CryptoPassPhrase = "MsCrmTools";
        internal const int CryptoPasswordIterations = 2;
        internal const string CryptoSaltValue = "Tanguy 92*";
        private const string ConfigFileName = "mscrmtools2011.config";

        #endregion Constants

        private static ConnectionManager instance;

        #region Constructor

        /// <summary>
        /// Initializes a new instance of class ConnectionManager
        /// </summary>
        private ConnectionManager()
        {
            ConnectionsList = LoadConnectionsList();

            var fsw = new FileSystemWatcher(new FileInfo(ConfigFileName).Directory.FullName, ConfigFileName);
            fsw.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size;
            fsw.EnableRaisingEvents = true;
            fsw.Changed += fsw_Changed;

            ServicePointManager.ServerCertificateValidationCallback += ValidateRemoteCertificate;
        }

        // callback used to validate the certificate in an SSL conversation
        private static bool ValidateRemoteCertificate(
        object sender,
            X509Certificate certificate,
            X509Chain chain,
            SslPolicyErrors policyErrors
        )
        {
            return true;
        }

        private void fsw_Changed(object sender, FileSystemEventArgs e)
        {
            if (e.ChangeType == WatcherChangeTypes.Changed)
            {
                ConnectionsList = LoadConnectionsList();

                ConnectionListUpdated(null, new EventArgs());
            }
        }

        #endregion Constructor

        #region Properties

        public static ConnectionManager Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = new ConnectionManager();
                }

                return instance;
            }
        }

        /// <summary>
        /// List of Crm connections
        /// </summary>
        public CrmConnections ConnectionsList { get; set; }

        #endregion Properties

        #region Methods

        /// <summary>
        /// Launch the Crm connection process
        /// </summary>
        /// <param name="detail">Details of the Crm connection</param>
        /// <param name="connectionParameter">A parameter to retrieve after connection</param>
        public void ConnectToServer(ConnectionDetail detail, object connectionParameter)
        {
            var parameters = new List<object> { detail, connectionParameter };

            // Runs the connection asynchronously
            var worker = new BackgroundWorker();
            worker.DoWork += WorkerDoWork;
            worker.RunWorkerCompleted += WorkerRunWorkerCompleted;
            worker.RunWorkerAsync(parameters);
        }

        /// <summary>
        /// Launch the Crm connection process
        /// </summary>
        /// <param name="detail">Details of the Crm connection</param>
        public void ConnectToServer(ConnectionDetail detail)
        {
            ConnectToServer(detail, null);
        }

        /// <summary>
        /// Restore Crm connections list from the file
        /// </summary>
        /// <returns>List of Crm connections</returns>
        public CrmConnections LoadConnectionsList()
        {
            try
            {
                CrmConnections crmConnections;
                if (File.Exists(ConfigFileName))
                {
                    crmConnections = CrmConnections.LoadFromFile(ConfigFileName);

                    if (!string.IsNullOrEmpty(crmConnections.Password))
                    {
                        crmConnections.Password = CryptoManager.Decrypt(crmConnections.Password,
                        CryptoPassPhrase,
                        CryptoSaltValue,
                        CryptoHashAlgorythm,
                        CryptoPasswordIterations,
                        CryptoInitVector,
                        CryptoKeySize);
                    }

                    foreach (var detail in crmConnections.Connections)
                    {
                        // Fix for new connection code
                        if (string.IsNullOrEmpty(detail.OrganizationUrlName))
                        {
                            if (detail.UseIfd || detail.UseOnline || detail.UseOsdp)
                            {
                                var uri = new Uri(detail.OrganizationServiceUrl);
                                detail.OrganizationUrlName = uri.Host.Split('.')[0];
                            }
                            else
                            {
                                detail.OrganizationUrlName = detail.Organization;
                            }
                        }

                        // Fix old connection for TimeOut
                        if (detail.Timeout == TimeSpan.Zero)
                        {
                            detail.Timeout = new TimeSpan(1200000000);
                        }
                    }
                }
                else
                {
                    crmConnections = new CrmConnections
                    {
                        Connections = new List<ConnectionDetail>()
                    };
                }

                return crmConnections;
            }
            catch (Exception error)
            {
                throw new Exception("Error while deserializing configuration file. Details: " + error.Message);
            }
        }

        /// <summary>
        /// Saves Crm connections list to file
        /// </summary>
        public void SaveConnectionsFile()
        {
            if (!string.IsNullOrEmpty(ConnectionsList.Password))
            {
                ConnectionsList.Password = CryptoManager.Encrypt(ConnectionsList.Password,
                    CryptoPassPhrase,
                    CryptoSaltValue,
                    CryptoHashAlgorythm,
                    CryptoPasswordIterations,
                    CryptoInitVector,
                    CryptoKeySize);
            }

            ConnectionsList.SerializeToFile(ConfigFileName);
        }

        /// <summary>
        /// Tests the specified connection
        /// </summary>
        /// <param name="service">Organization service</param>
        public void TestConnection(IOrganizationService service)
        {
            try
            {
                SendStepChange("Testing connection...");

                var request = new WhoAmIRequest();
                service.Execute(request);
            }
            catch (Exception error)
            {
                throw new Exception("Test connection failed: " + CrmExceptionHelper.GetErrorMessage(error, false));
            }
        }

        /// <summary>
        /// Connects to a Crm server
        /// </summary>
        /// <param name="parameters">List of parameters</param>
        /// <returns>An exception or an IOrganizationService</returns>
        private object Connect(List<object> parameters)
        {
            WebRequest.DefaultWebProxy = WebRequest.GetSystemWebProxy();

            var detail = (ConnectionDetail)parameters[0];
            SendStepChange("Creating Organization service proxy...");

            // Connecting to Crm server
            try
            {
                var service = (OrganizationService)detail.GetOrganizationService();

                ((OrganizationServiceProxy)service.InnerService).SdkClientVersion = detail.OrganizationVersion;

                TestConnection(service);

                // If the current connection detail does not contain the web
                // application url, we search for it
                if (string.IsNullOrEmpty(detail.WebApplicationUrl))
                {
                    var discoService = (DiscoveryService)detail.GetDiscoveryService();
                    var result = (RetrieveOrganizationResponse)discoService.Execute(new RetrieveOrganizationRequest { UniqueName = detail.Organization });
                    detail.WebApplicationUrl = result.Detail.Endpoints[EndpointType.WebApplication];
                }

                // We search for organization version
                var vRequest = new RetrieveVersionRequest();
                var vResponse = (RetrieveVersionResponse)service.Execute(vRequest);

                detail.OrganizationVersion = vResponse.Version;

                var currentConnection = ConnectionsList.Connections.FirstOrDefault(x => x.ConnectionId == detail.ConnectionId);
                if (currentConnection != null)
                {
                    currentConnection.WebApplicationUrl = detail.WebApplicationUrl;
                    currentConnection.OrganizationVersion = vResponse.Version;
                    currentConnection.SavePassword = detail.SavePassword;
                    detail.CopyPasswordTo(currentConnection);
                }

                detail.LastUsedOn = DateTime.Now;

                SaveConnectionsFile();

                return service;
            }
            catch (Exception error)
            {
                return error;
            }
        }

        /// <summary>
        /// Working process
        /// </summary>
        /// <param name="sender">BackgroundWorker object</param>
        /// <param name="e">BackgroundWorker object parameters</param>
        private void WorkerDoWork(object sender, DoWorkEventArgs e)
        {
            object result = Connect((List<object>)e.Argument);
            e.Result = e.Argument;
            ((List<object>)e.Result).Add(result);
        }

        private void WorkerRunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            var parameters = (List<object>)e.Result;

            if (parameters.Count == 3)
            {
                var error = parameters[2] as Exception;
                if (error != null)
                {
                    SendFailureMessage(CrmExceptionHelper.GetErrorMessage(error, false), parameters[1]);
                }
                else
                {
                    var service = parameters[2] as IOrganizationService;
                    if (service != null)
                    {
                        SendSuccessMessage(service, parameters);
                    }
                }
            }
        }

        #endregion Methods

        #region Send Events

        /// <summary>
        /// Sends a connection failure message
        /// </summary>
        /// <param name="failureReason">Reason of the failure</param>
        private void SendFailureMessage(string failureReason, object parameter)
        {
            if (ConnectionFailed != null)
            {
                var args = new ConnectionFailedEventArgs
                {
                    FailureReason = failureReason,
                    Parameter = parameter
                };

                ConnectionFailed(this, args);
            }
        }

        /// <summary>
        /// Sends a step change message
        /// </summary>
        /// <param name="step">New step</param>
        private void SendStepChange(string step)
        {
            var args = new StepChangedEventArgs
            {
                CurrentStep = step
            };

            if (StepChanged != null)
            {
                StepChanged(this, args);
            }
        }

        /// <summary>
        /// Sends a connection success message
        /// </summary>
        /// <param name="service">IOrganizationService generated</param>
        /// <param name="parameters">List of parameters</param>
        private void SendSuccessMessage(IOrganizationService service, List<object> parameters)
        {
            if (ConnectionSucceed != null)
            {
                var args = new ConnectionSucceedEventArgs
                {
                    OrganizationService = service,
                    ConnectionDetail = (ConnectionDetail)parameters[0],
                    Parameter = parameters[1]
                };

                ConnectionSucceed(this, args);
            }
        }

        #endregion Send Events
    }
}