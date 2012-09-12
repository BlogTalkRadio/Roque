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
    public class WorkerHost : IDisposable
    {
        class Process : MarshalByRefObject
        {
            private WorkerArray _WorkerArray;

            public void Start(string worker = null)
            {
                _WorkerArray = string.IsNullOrEmpty(worker) ? Worker.All : new WorkerArray(Worker.Get(worker));
                _WorkerArray.Start(onlyAutoStart: string.IsNullOrEmpty(worker));
                AppDomain.CurrentDomain.DomainUnload += CurrentDomain_DomainUnload;
            }

            void CurrentDomain_DomainUnload(object sender, EventArgs e)
            {
                AppDomain.CurrentDomain.DomainUnload -= CurrentDomain_DomainUnload;
                _WorkerArray.StopAndWait();
            }

            public void Stop()
            {
                _WorkerArray.StopAndWait();
            }

            public void GCCollect()
            {
                GC.Collect();
            }
        }

        private Process _Process;

        private string _ProcessWorker;

        private bool _Stopping;

        /// <summary>
        /// If true when any *.config or *.dll file changes the Host will be restarted.
        /// </summary>
        public bool RestartOnFileChanges { get; set; }

        /// <summary>
        /// If more than zero, AppDomain allocated memory is monitored. If it exceeds this value the Host will be restarted.
        /// </summary>
        public long RestartIfMemorySizeIsMoreThan { get; set; }

        /// <summary>
        /// AppDomain where workers are running
        /// </summary>
        public AppDomain AppDomain { get; private set; }

        private Timer _Timer;

        /// <summary>
        /// Creates a new Host for workers
        /// </summary>
        public WorkerHost()
        {
            var settings = Roque.Core.Configuration.Roque.Settings;
            RestartOnFileChanges = settings.RestartOnFileChanges;
            RestartIfMemorySizeIsMoreThan = settings.RestartIfMemorySizeIsMoreThan;
        }

        /// <summary>
        /// Starts the worker(s) of this host.
        /// </summary>
        /// <param name="worker">a name of a worker in config. or null to start all workers with autoStart=true</param>
        public void Start(string worker = null)
        {
            if (AppDomain == null)
            {
                var appDomainSetup = new AppDomainSetup()
                    {
                        ShadowCopyFiles = true.ToString()
                    };
                AppDomain = AppDomain.CreateDomain("RoqueWorkers" + Guid.NewGuid(), null, appDomainSetup);
            }
            if (_Process != null || _Stopping)
            {
                throw new Exception("A process in this host is already started");
            }
            _Process = (Process)AppDomain.CreateInstanceAndUnwrap(
                typeof(Process).Assembly.FullName,
                typeof(Process).FullName);
            _ProcessWorker = worker;
            Trace.TraceInformation("Starting...");
            _Process.Start(worker);
            if (RestartOnFileChanges)
            {
                new FileWatcher().OnConfigOrDllChanges(Restart, true);
            }
            if (_Timer == null)
            {
                _Timer = new Timer(TimerTick, null, TimeSpan.FromSeconds(10), TimeSpan.FromMinutes(1));
            }
        }

        private void TimerTick(object state)
        {
            if (this.AppDomain != null && _Process != null && !_Stopping)
            {
                _Process.GCCollect();
                if (RestartIfMemorySizeIsMoreThan > 0)
                {
                    AppDomain.MonitoringIsEnabled = true;
                    long bytes = this.AppDomain.MonitoringSurvivedMemorySize;
                    if (bytes > RestartIfMemorySizeIsMoreThan)
                    {
                        Trace.TraceWarning("[MaxMemorySizeCheck] Restarting!. Memory Size exceeded maximum limit ({0}MB)", Math.Round(bytes / 1024.0 / 1024.0, 1));
                        Restart();
                    }
                    else
                    {
                        Trace.TraceInformation("[MaxMemorySizeCheck] Memory Size is normal ({0}MB)", Math.Round(bytes / 1024.0 / 1024.0, 1));
                    }
                }
            }
        }

        /// <summary>
        /// Stops the worker(s) unloading AppDomain
        /// </summary>
        public void Stop()
        {
            if (_Process == null || _Stopping)
            {
                return;
            }
            _Stopping = true;
            var appDomain = AppDomain;
            AppDomain = null;
            _Process = null;
            _Process = null;
            Trace.TraceInformation("Stopping...");
            AppDomain.Unload(appDomain);
            Trace.TraceInformation("Stopped");
            _Stopping = false;
        }

        /// <summary>
        /// Restarts the worker(s) on a new AppDomain (unloading the current one)
        /// </summary>
        public void Restart()
        {
            if (_Stopping)
            {
                return;
            }
            Stop();
            Start(_ProcessWorker);
        }

        public void Dispose()
        {
            Stop();
        }
    }
}
