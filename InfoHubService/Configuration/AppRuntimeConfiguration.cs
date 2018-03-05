using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InContex.Service.Configuration
{
    public enum AppRuntimeType
    {
        Service,
        Console
    }

    /// <summary>
    /// Class stores application specific configuration setting that can affect the runtime behavior of the application. 
    /// </summary>
    public class AppRuntimeConfiguration
    {
        public static AppRuntimeType RuntimeMode { get; set; }

        static AppRuntimeConfiguration()
        {
            RuntimeMode = AppRuntimeType.Service;
        }

    }
}
