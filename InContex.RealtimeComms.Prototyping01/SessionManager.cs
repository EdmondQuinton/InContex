using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using NLog;

using Opc.Ua;
using Opc.Ua.Client;

using InContex.Collections.Persisted;
using InContex.RealtimeComms.Prototyping01.Opc;

namespace InContex.RealtimeComms.Prototyping01
{
    public class SessionManager
    {
        private static Logger __logger = LogManager.GetCurrentClassLogger();
        private static int _handleCount = 0;

        private ApplicationConfiguration _configuration = null;
        private string _applicationName = "InContex.Comms.Prototyping";

        private Session _session;
        private Dictionary<string, Subscription> _subscriptions;
        private bool _disposed = false;
        private SignalStore _signalStore;

        public SessionManager()
            : this(null, null, null) { }


        public SessionManager(Uri endpointUrl, string userName, string password)
        {
            __logger.Trace("SessionManager is starting up...");

            ModuleConfiguration moduleConfiguration = new ModuleConfiguration(_applicationName);
            _configuration = moduleConfiguration.Configuration;
            _configuration.CertificateValidator.CertificateValidation += new CertificateValidationEventHandler(CertificateValidation);
            _subscriptions = new Dictionary<string, Subscription>();
            _signalStore = new SignalStore();

            if (endpointUrl != null)
            {
                EndpointConnect(endpointUrl, userName, password).Wait();
            }

        }

        private void CertificateValidation(CertificateValidator validator, CertificateValidationEventArgs e)
        {
            if (e.Error.StatusCode == StatusCodes.BadCertificateUntrusted)
            {
                __logger.Error("Certificate \""
                    + e.Certificate.Subject
                    + "\" not trusted. If you want to trust this certificate, please copy it from the \""
                    + _configuration.SecurityConfiguration.RejectedCertificateStore.StorePath + "/certs"
                    + "\" to the \""
                    + _configuration.SecurityConfiguration.TrustedPeerCertificates.StorePath + "/certs"
                    + "\" folder.");
            }
        }

        public async Task EndpointConnect(Uri endpointUrl, string userName = null, string password = null)
        {
            EndpointDescription selectedEndpoint = CoreClientUtils.SelectEndpoint(endpointUrl.AbsoluteUri, true);
            ConfiguredEndpoint configuredEndpoint = new ConfiguredEndpoint(selectedEndpoint.Server, EndpointConfiguration.Create(_configuration));
            configuredEndpoint.Update(selectedEndpoint);

            if(_session != null)
            {
                _session.Close();
                _session = null;
            }

            // if no user name or password is spesified then assume Anonymous identity else connect with user name and password.
            if (string.IsNullOrWhiteSpace(userName))
            {

                _session = await Session.Create(
                    _configuration,
                    configuredEndpoint,
                    true,
                    false,
                    _configuration.ApplicationName,
                    60000,
                    new UserIdentity(new AnonymousIdentityToken()),
                    null);
            }
            else
            {
                _session = await Session.Create(
                    _configuration,
                    configuredEndpoint,
                    true,
                    false,
                    _configuration.ApplicationName,
                    60000,
                    new UserIdentity(userName, password),
                    null);
            }


            if (_session != null)
            {
                __logger.Info("Created session with updated endpoint " + selectedEndpoint.EndpointUrl + " from server!");
                _session.KeepAlive += new KeepAliveEventHandler((sender, e) => OnKeepAliveNotification(sender, e, _session));
            }
        }


        public void CreateMonitoredItem(string nodeID, int publishingInterval, int handle)
        {
            CreateMonitoredItem("Default", nodeID, publishingInterval, 1, handle);
        }

        /// <summary>
        /// Creates a subscription to a monitored item on an OPC UA server
        /// </summary>
        public void CreateMonitoredItem(string group, string nodeID, int publishingInterval, uint queueSize, int handle)
        {

            if (_session != null)
            {
                bool subscriptionGroupExists = _subscriptions.ContainsKey(group);

                Subscription subscription = null;

                if (subscriptionGroupExists)
                {
                    subscription = _subscriptions[group];
                }
                else
                {
                    subscription = _session.DefaultSubscription;
                    if (_session.AddSubscription(subscription))
                    {
                        subscription.Create();
                    }

                    _subscriptions.Add(group, subscription);
                }

                NodeId nodeLookup = new NodeId(nodeID);
                // get the DisplayName for the node.
                Node node = _session.ReadNode(nodeLookup);


                string nodeDisplayName = node.DisplayName.Text;
                if (String.IsNullOrEmpty(nodeDisplayName))
                {
                    nodeDisplayName = nodeLookup.Identifier.ToString();
                }

                // add the new monitored item.
                MonitoredItem monitoredItem = new MonitoredItem(subscription.DefaultItem);

                _handleCount++;
                monitoredItem.Handle = handle;
                monitoredItem.StartNodeId = nodeLookup;
                monitoredItem.AttributeId = Attributes.Value;
                monitoredItem.DisplayName = nodeDisplayName;
                monitoredItem.MonitoringMode = MonitoringMode.Reporting;
                monitoredItem.SamplingInterval = publishingInterval;
                monitoredItem.QueueSize = queueSize;
                monitoredItem.DiscardOldest = true;

                monitoredItem.Notification += new MonitoredItemNotificationEventHandler(OnMonitoredItemNotification);
                subscription.AddItem(monitoredItem);
                subscription.ApplyChanges();
            }
            else
            {
                __logger.Error("Failed to publish node '{0}' to active server sessions with following endpoint '{1}'", nodeID, _session.ConfiguredEndpoint.ToString());
            }
        }

        /// <summary>
        /// Handler for the standard "keep alive" event sent by all OPC UA servers
        /// </summary>
        private void OnKeepAliveNotification(Session sender, KeepAliveEventArgs e, Session session)
        {
            if (e != null && session != null)
            {
                if (!ServiceResult.IsGood(e.Status))
                {
                    __logger.Trace(String.Format(
                        "Status: {0}/t/tOutstanding requests: {1}/t/tDefunct requests: {2}",
                        e.Status,
                        session.OutstandingRequestCount,
                        session.DefunctRequestCount));
                }
            }
        }

        public void OnMonitoredItemNotification(MonitoredItem monitoredItem, MonitoredItemNotificationEventArgs e)
        {
            try
            {
                if (e.NotificationValue == null || monitoredItem.Subscription.Session == null)
                {
                    return;
                }

                MonitoredItemNotification notification = e.NotificationValue as MonitoredItemNotification;
                if (notification == null)
                {
                    return;
                }

  
                foreach (var value in monitoredItem.DequeueValues())
                {
                    int h = (int)monitoredItem.Handle;

                    AnalogueSignal signalValue = new AnalogueSignal()
                    {
                        SampleDateTimeUTC = value.SourceTimestamp.Ticks,
                        Value = Convert.ToDouble(value.Value),
                        StatusCode = (int)(uint)value.StatusCode,
                        StatusGood = StatusCode.IsGood(value.StatusCode) == true ? 1 : 0,
                        SignalID = (int)monitoredItem.Handle,
                        PreviousSampleDateTimeUTC = value.SourceTimestamp.Ticks,
                        DeltaValue = 0
                    };

                    _signalStore.WriteAnalogue(signalValue);
                    __logger.Trace("{0} - {4}: {1}, {2}, {3}", monitoredItem.DisplayName, value.Value, value.SourceTimestamp, value.StatusCode, h);
                }

            }
            catch (Exception exception)
            {
                __logger.Error(exception, "Error processing monitored item notification");
            }
        }

        public void Close()
        {
            this.Dispose();
        }

        #region Dispose

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (disposing)
            {
                // free managed resources  
                _session.Close();
                _session = null;
                _disposed = true;
            }
            // free unmanaged resources here
        }

        ~SessionManager()
        {
            Dispose(false);
        }

        #endregion

    }
}
