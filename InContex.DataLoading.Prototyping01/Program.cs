using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using NLog;

namespace InContex.DataLoading.Prototyping01
{
    class Program
    {
        public static DataLoader loader = new DataLoader();
        private static Logger __logger = LogManager.GetCurrentClassLogger();

        static void Main(string[] args)
        {

            Task dataLoaderTask = null;
            var tokenSource = new CancellationTokenSource();
            var token = tokenSource.Token;

            __logger.Info("Creating task to upload data streams to SQL Server...");
            try
            {
                dataLoaderTask = Task.Run(() => loader.ProcessStreams(token));
            }
            catch (Exception ex)
            {
                __logger.Error(ex, "Error occurred while importing data streams.");
            }

            __logger.Info("Data loader is running. Press enter to quit.");
            Console.ReadLine();

            try
            {
                tokenSource.Cancel();
                dataLoaderTask.Wait();
            }
            catch (Exception ex)
            {
                __logger.Error("Exception: " + ex.ToString());
            }
        }
    }
}
