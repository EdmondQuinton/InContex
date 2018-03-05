using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;
using InContex.Service.Configuration;
using NLog;

namespace InContex.Service
{
    static class Program
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        static void Main(string[] args)
        {
            if(args != null)
            {
                foreach(string arg in args)
                {
                    string argument = arg.ToUpperInvariant().Trim();

                    switch(argument)
                    {
                        case "-C":
                            AppRuntimeConfiguration.RuntimeMode = AppRuntimeType.Console;
                            break;
                    }
                }
            }


            ServiceBase[] ServicesToRun;
            ServicesToRun = new ServiceBase[]
            {
                new InfoHubService()
            };

            logger.Info("Application runtime mode set \"{0}\"", AppRuntimeConfiguration.RuntimeMode.ToString());

            if (AppRuntimeConfiguration.RuntimeMode != AppRuntimeType.Console)
            {
                logger.Info("Starting service...");
                ServiceBase.Run(ServicesToRun);
            }
            else
            {
                logger.Info("Starting console...");
                InfoHubService consoleService = ServicesToRun[0] as InfoHubService;
                consoleService.StartConsole(args);

                Console.WriteLine("Press 'Q' to quit ...");
                char key = Console.ReadKey(true).KeyChar;

                while(key != 'Q' && key != 'q')
                {
                    key = Console.ReadKey(true).KeyChar;
                }

                logger.Info("Stopping Console...");
                consoleService.StopConsole();
            }
        }
    }
}
