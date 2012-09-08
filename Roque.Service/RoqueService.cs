using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using Cinchcast.Roque.Core;

namespace Cinchcast.Roque.Service
{
    public partial class RoqueService : ServiceBase
    {
        private WorkerHost _Host;

        public RoqueService()
        {
            InitializeComponent();
        }

        protected override void OnStart(string[] args)
        {
            if (_Host == null)
            {
                _Host = new WorkerHost();
            }
            _Host.Start();
        }

        protected override void OnStop()
        {
            _Host.Stop();
        }
    }
}
