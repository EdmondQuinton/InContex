using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;

namespace InContex.Service
{
    public partial class InfoHubService : ServiceBase
    {
        public InfoHubService()
        {
            InitializeComponent();
        }

        protected override void OnStart(string[] args)
        {
        }

        protected override void OnStop()
        {
        }

        public void StartConsole(string[] args)
        {
            OnStart(args);
        }

        public void StopConsole()
        {
            OnStop();
        }
    }
}
