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
        public RoqueService()
        {
            InitializeComponent();
        }

        protected override void OnStart(string[] args)
        {
            Worker.All.Start(onlyAutoStart: true);
        }

        protected override void OnStop()
        {
            Worker.All.Stop();
        }
    }
}
