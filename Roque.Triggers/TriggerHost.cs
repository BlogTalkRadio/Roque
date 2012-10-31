using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Cinchcast.Roque.Core;
using Cinchcast.Roque.Service;

namespace Cinchcast.Roque.Triggers
{
    /// <summary>
    /// Hosts a TriggerWatcher into a separate AppDomain
    /// </summary>
    public class TriggerHost : AppDomainHost<TriggerHost.TriggerProcess>
    {
        public class TriggerProcess : AppDomainHost.Process
        {
            public override void OnStart(dynamic parameters)
            {
                Trigger.All.Start();
            }

            public override void OnStop()
            {
                Trigger.All.Stop().Wait();
            }
        }
    }
}
