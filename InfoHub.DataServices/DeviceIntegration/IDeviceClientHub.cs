using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using System.Threading;
using System.Threading.Tasks;

//Note: Just 1 more layer of protection the plugin assembly has to be signed!

namespace InContex.DataServices.DeviceIntegration
{
    /// <summary>
    /// A device client hub server as the communication interface for real-time devices. A device hub can 
    /// be any server or application that can communicate to one or more devices, such as an OPC Server.
    /// </summary>
    public interface IDeviceClientHub
    {

        /// <summary>
        /// The minimum interval the monitor can wait before responding. If monitor 
        /// has not responded within the specified period then the service will assume communication failure. If no new data 
        /// is available to report then an empty response should be returned.
        /// </summary>
        double MinimumReportingInterval { get; set; }

        /// <summary>
        /// Asynchronously open a connection to device.
        /// </summary>
        /// <param name="connectionString">Connection string containing information necessary to establish communications 
        /// with underlying device.
        /// </param>
        /// <param name="minimumReportingInterval">The minimum interval the monitor can wait before responding. If monitor 
        /// has not responded within the specified period then the service will assume communication failure. If no new data 
        /// is available to report then an empty response should be returned.
        /// </param>
        Task OpenAsync(string connectionString, double minimumReportingInterval);
        
        /// <summary>
        /// Asynchronously close a connection to device.
        /// </summary>
        Task CloseAsync();
        void MonitorVariable(VariableDescriptor variableDescriptor, int samplingInterval);

        Task ReceiveAsync();
    }
}