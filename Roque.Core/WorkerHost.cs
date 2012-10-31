using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Cinchcast.Roque.Core;

namespace Cinchcast.Roque.Service
{
    /// <summary>
    /// Runs workers on a separate AppDomain.
    /// </summary>
    public class WorkerHost : AppDomainHost<WorkerHost.WorkerProcess>
    {
        public class WorkerProcess : AppDomainHost.Process
        {
            private WorkerArray _WorkerArray;

            public override void OnStart(dynamic parameters)
            {
                string worker = parameters as string;
                _WorkerArray = string.IsNullOrEmpty(worker) ? Worker.All : new WorkerArray(Worker.Get(worker));
                _WorkerArray.Start(onlyAutoStart: string.IsNullOrEmpty(worker));
            }

            public override void OnStop()
            {
                _WorkerArray.StopAndWait();
            }
        }
    }
}
