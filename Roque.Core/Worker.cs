// -----------------------------------------------------------------------
// <copyright file="Worker.cs" company="">
// TODO: Update copyright text.
// </copyright>
// -----------------------------------------------------------------------

using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Cinchcast.Roque.Core.Configuration;

namespace Cinchcast.Roque.Core
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;

    /// <summary>
    /// a Worker sequentially runs jobs from a <see cref="Queue"/>
    /// </summary>
    public class Worker
    {
        public event EventHandler Stopped;

        public enum WorkerState
        {
            Created,
            Waiting,
            Working,
            Stopped
        }

        public WorkerState State { get; private set; }

        public bool IsStopRequested { get; private set; }

        public Queue Queue { get; protected set; }

        public string Name { get; set; }

        public int TooManyErrors { get; set; }

        public int TooManyErrorsRetrySeconds { get; set; }

        public bool AutoStart { get; private set; }

        private static IDictionary<string, Worker> _Instances = new Dictionary<string, Worker>();

        private static WorkerArray _All;

        /// <summary>
        /// Returns all workers declared in configuration
        /// </summary>
        public static WorkerArray All
        {
            get
            {
                if (_All == null)
                {
                    foreach (var workerConfig in Configuration.Roque.Settings.Workers.OfType<Configuration.WorkerElement>())
                    {
                        Worker.Get(workerConfig.Name);
                    }
                    _All = new WorkerArray(_Instances.Values.ToArray());
                }
                return _All;
            }
        }

        private static Worker _Single;

        /// <summary>
        /// Returns the worker declared in configuration (throws an exception if none of more than one is found)
        /// </summary>
        public static Worker Single
        {
            get
            {
                if (_Single == null)
                {
                    _Single = All.Single();
                }
                return _Single;
            }
        }

        public bool IsResuming { get; private set; }

        /// <summary>
        /// Get a worker by name
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public static Worker Get(string name)
        {
            Worker worker;
            if (string.IsNullOrWhiteSpace(name))
            {
                name = string.Empty;
            }
            if (!_Instances.TryGetValue(name, out worker))
            {
                try
                {
                    var workersConfig = Configuration.Roque.Settings.Workers;
                    if (workersConfig == null)
                    {
                        throw new Exception("No workers found in configuration");
                    }
                    var workerConfig = workersConfig[name];
                    if (workerConfig == null)
                    {
                        throw new Exception("Worker not found: " + name);
                    }
                    worker = new Worker(workerConfig.Name, Queue.Get(workerConfig.Queue), workerConfig.TooManyErrors, workerConfig.TooManyErrorsRetrySeconds);
                    worker.AutoStart = workerConfig.AutoStart;
                }
                catch (Exception ex)
                {
                    if (RoqueTrace.Switch.TraceError)
                    {
                        Trace.TraceError(ex.Message, ex);
                    }
                    throw;
                }
            }
            return worker;
        }

        private Guid _ID = Guid.Empty;

        /// <summary>
        /// Uniquely identifies a worker on it's queues. It's persisted in a file .Guid.txt to enable job resuming. 
        /// </summary>
        public Guid ID
        {
            get
            {
                if (_ID == Guid.Empty)
                {
                    string guidFilePath = Path.Combine(Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location), "Roque.Worker." + Name.ToLowerInvariant() + ".Guid.txt");
                    if (!File.Exists(guidFilePath))
                    {
                        _ID = Guid.NewGuid();
                        File.WriteAllText(guidFilePath, ID.ToString());
                    }
                    else
                    {
                        _ID = new Guid(File.ReadAllText(guidFilePath));
                    }
                }
                return _ID;
            }
        }

        private Task _CurrentWork;

        private Worker(string name, Queue queue, int tooManyErrors = 10, int tooManyErrorsRetrySeconds = 30)
        {
            if (_Instances.ContainsKey(name))
            {
                throw new Exception("Worker name already exists: " + name);
            }
            this.Name = name;
            this.Queue = queue;
            this.TooManyErrors = tooManyErrors;
            this.TooManyErrorsRetrySeconds = tooManyErrorsRetrySeconds;
            _Instances[Name] = this;
        }

        private bool _SubscribersRegistered;

        /// <summary>
        /// Sequentially pick and run jobs from the <see cref="Queue"/> until stop is requested.
        /// </summary>
        public void Work()
        {
            try
            {
                IsStopRequested = false;

                if (State == WorkerState.Waiting)
                {
                    throw new Exception("This worker is already waiting for work");
                }
                if (State == WorkerState.Working)
                {
                    throw new Exception("This worker is already working");
                }
                State = WorkerState.Waiting;
                if (RoqueTrace.Switch.TraceInfo)
                {
                    var assemblyName = Assembly.GetAssembly(typeof(Worker)).GetName();
                    Trace.TraceInformation("Worker {0} started. Roque v{1}. AppDomain: {2}", Name, assemblyName.Version, AppDomain.CurrentDomain.FriendlyName);
                }

                while (!_SubscribersRegistered && !IsStopRequested)
                {
                    try
                    {
                        Executor.Default.RegisterSubscribersForWorker(this);
                        _SubscribersRegistered = true;
                    }
                    catch
                    {
                        // error registering subscriber, log is already done
                        if (RoqueTrace.Switch.TraceInfo)
                        {
                            Trace.TraceInformation("Error registering subscribers, retrying in 10 seconds...");
                        }
                        Thread.Sleep(10000);
                    }
                }

                bool attemptWipResume = true;

                int consecutiveErrors = 0;
                int retries = 0;
                bool firstTime = true;
                bool shouldRetry = false;

                Stopwatch stopwatchBatchWork = null;
                Stopwatch stopwatchLastDequeue = null;

                while (!IsStopRequested)
                {
                    Job job = null;
                    try
                    {
                        State = WorkerState.Waiting;
                        stopwatchLastDequeue = new Stopwatch();
                        stopwatchLastDequeue.Start();
                        if (attemptWipResume)
                        {
                            job = Queue.GetInProgressJob(this);
                            attemptWipResume = false;
                            if (job == null)
                            {
                                if (RoqueTrace.Switch.TraceInfo)
                                {
                                    Trace.TraceInformation("No pending jobs to resume for worker: " + Name);
                                }
                            }
                            else
                            {
                                retries = 1;
                                if (RoqueTrace.Switch.TraceInfo)
                                {
                                    Trace.TraceInformation("Resuming pending job for worker: " + Name);
                                }
                            }
                        }

                        if (job == null)
                        {
                            if (shouldRetry)
                            {
                                job = Queue.GetInProgressJob(this);
                                shouldRetry = false;
                                if (RoqueTrace.Switch.TraceInfo)
                                {
                                    Trace.TraceInformation(string.Format("Retry #'{0}'. Worker {1}, Method: {2}", retries, Name, job.Method));
                                }
                            }
                            else
                            {
                                job = Queue.Dequeue(this, firstTime ? 1 : 10);
                                retries = 0;
                            }
                        }

                        if (job != null)
                        {
                            if (stopwatchBatchWork == null)
                            {
                                stopwatchBatchWork = new Stopwatch();
                                stopwatchBatchWork.Start();
                            }
                            State = WorkerState.Working;
                            job.Execute();
                            consecutiveErrors = 0;
                            try
                            {
                                Queue.Completed(this, job);
                            }
                            catch (Exception ex)
                            {
                                if (RoqueTrace.Switch.TraceError)
                                {
                                    Trace.TraceError(
                                        "Error marking job as completed for worker " + Name + ": " + ex.Message, ex);
                                }
                            }
                        }
                        else
                        {
                            if (stopwatchBatchWork != null)
                            {
                                if (RoqueTrace.Switch.TraceInfo)
                                {
                                    Trace.TraceInformation("Queue is empty. Worker {0} worked for: {1}", Name,
                                                           stopwatchBatchWork.Elapsed.Subtract(stopwatchLastDequeue.Elapsed));
                                }
                                stopwatchBatchWork = null;
                            }
                        }
                    }
                    catch (Exception jobEx)
                    {
                        State = WorkerState.Waiting;
                        consecutiveErrors++;

                        if (job != null)
                        {
                            ShouldRetryException retryEx = jobEx as ShouldRetryException;
                            if (retryEx != null && (retryEx.MaxTimes > 0 && retries >= retryEx.MaxTimes))
                            {
                                retryEx = null;
                            }

                            if (retryEx == null)
                            {
                                // job completed with errors, but we can move on
                                try
                                {
                                    Queue.Completed(this, job, true);
                                }
                                catch (Exception ex)
                                {
                                    if (RoqueTrace.Switch.TraceError)
                                    {
                                        Trace.TraceError(
                                            "Error marking job as completed for worker " + Name + ": " + ex.Message, ex);
                                    }
                                }
                                if (consecutiveErrors >= TooManyErrors)
                                {
                                    if (RoqueTrace.Switch.TraceInfo)
                                    {
                                        Trace.TraceInformation(string.Format("Too many errors on worker '{0}', picking next job in {1} seconds", Name, TooManyErrorsRetrySeconds));
                                    }
                                    // too many errors, wait some time before picking next job
                                    Thread.Sleep(TimeSpan.FromSeconds(TooManyErrorsRetrySeconds));
                                }
                            }
                            else
                            {
                                retries++;
                                shouldRetry = true;
                                if (RoqueTrace.Switch.TraceInfo)
                                {
                                    Trace.TraceInformation(string.Format("Retrying failed job on worker '{0}' in {1}", Name, retryEx.Delay));
                                }
                                // wait some time before retrying the failed job
                                if (retryEx.Delay.Ticks > 0)
                                {
                                    Thread.Sleep(retryEx.Delay);
                                }
                            }
                        }
                    }
                    firstTime = false;
                }
                IsStopRequested = false;
            }
            catch (Exception ex)
            {
                if (RoqueTrace.Switch.TraceError)
                {
                    Trace.TraceError("Error running worker " + Name + ": " + ex.Message, ex);
                }
            }
            finally
            {
                State = WorkerState.Stopped;
                if (RoqueTrace.Switch.TraceInfo)
                {
                    Trace.TraceInformation("Worker stopped: " + Name);
                }
                var handler = Stopped;
                if (handler != null)
                {
                    Stopped(this, EventArgs.Empty);
                }
            }
        }

        /// <summary>
        /// Starts working async
        /// </summary>
        /// <returns></returns>
        public Task Start()
        {
            return _CurrentWork = Task.Factory.StartNew(Work);
        }

        /// <summary>
        /// Request stop of this worker
        /// </summary>
        /// <returns></returns>
        public Task Stop()
        {
            if (_CurrentWork == null || _CurrentWork.IsCompleted)
            {
                return Task.Factory.StartNew(() => { });
            }
            if (!IsStopRequested)
            {
                IsStopRequested = true;
            }
            return _CurrentWork;
        }
    }
}
