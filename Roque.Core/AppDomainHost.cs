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
    /// Activate objects on a separate AppDomain.
    /// </summary>
    public abstract class AppDomainHost : IDisposable
    {
        public abstract class Process : MarshalByRefObject
        {
            public virtual void Start(dynamic parameters = null)
            {
                AppDomain.CurrentDomain.DomainUnload += CurrentDomain_DomainUnload;
                OnStart(parameters);
            }

            void CurrentDomain_DomainUnload(object sender, EventArgs e)
            {
                AppDomain.CurrentDomain.DomainUnload -= CurrentDomain_DomainUnload;
                OnStop();
            }

            public void Stop()
            {
                OnStop();
            }

            public void GCCollect()
            {
                GC.Collect();
            }

            public abstract void OnStart(dynamic parameters);

            public abstract void OnStop();
        }

        private Process _Process;

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
        public AppDomainHost()
        {
            try
            {
                var settings = Roque.Core.Configuration.Roque.Settings;
                RestartOnFileChanges = settings.RestartOnFileChanges;
                RestartIfMemorySizeIsMoreThan = settings.RestartIfMemorySizeIsMoreThan;
            }
            catch (Exception ex)
            {
                Trace.TraceError("Error parsing roque configuration block. {0}", ex.Message, ex);
            }
        }

        protected abstract Type GetProcessType();

        /// <summary>
        /// Starts the process of this host.
        /// </summary>
        public void Start(dynamic parameters = null)
        {
            if (AppDomain == null)
            {
                var appDomainSetup = new AppDomainSetup()
                    {
                        ShadowCopyFiles = true.ToString()
                    };
                AppDomain = AppDomain.CreateDomain("AppDomainHost_" + Guid.NewGuid(), null, appDomainSetup);
            }
            if (_Process != null || _Stopping)
            {
                throw new Exception("A process in this host is already started");
            }

            Type processType = GetProcessType();

            _Process = (Process)AppDomain.CreateInstanceAndUnwrap(
                processType.Assembly.FullName,
                processType.FullName);
            Trace.TraceInformation("Starting...");
            _Process.Start(parameters);
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
        /// Restarts the process on a new AppDomain (unloading the current one)
        /// </summary>
        public void Restart()
        {
            if (_Stopping)
            {
                return;
            }
            Stop();
            Start();
        }

        public void Dispose()
        {
            Stop();
        }
    }

    /// <summary>
    /// Activate objects on a separate AppDomain.
    /// </summary>
    /// <typeparam name="TProcess"></typeparam>
    public class AppDomainHost<TProcess> : AppDomainHost
        where TProcess : AppDomainHost.Process
    {
        protected override Type GetProcessType()
        {
            return typeof(TProcess);
        }
    }
}
