using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using Cinchcast.Roque.Core;
using Cinchcast.Roque.Triggers;

namespace Cinchcast.Roque.Service
{
    public partial class RoqueService : ServiceBase
    {
        private WorkerHost _Host;
        private TriggerHost _TriggerHost;

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
            if (_TriggerHost == null)
            {
                _TriggerHost = new TriggerHost();
            }
            _TriggerHost.Start();
        }

        protected override void OnStop()
        {
            _Host.Stop();
            _TriggerHost.Stop();
        }
    }
}
