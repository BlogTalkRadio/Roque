// -----------------------------------------------------------------------
// <copyright file="RoqueCommander.cs" company="">
// TODO: Update copyright text.
// </copyright>
// -----------------------------------------------------------------------

using Cinchcast.Roque.Core;

namespace Cinchcast.Roque.Common
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;

    /// <summary>
    /// Work service implementation example
    /// </summary>
    public class RoqueCommander : IRoqueCommander
    {
        public void StopWorker(string name)
        {
            Worker.Get(name).Stop().Wait();
        }

        public void StartWorker(string name)
        {
            var worker = Worker.Get(name);
            if (worker.State == Worker.WorkerState.Created || worker.State == Worker.WorkerState.Stopped)
            {
                Worker.Get(name).Start();
            }
        }
    }
}
