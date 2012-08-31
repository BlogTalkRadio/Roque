// -----------------------------------------------------------------------
// <copyright file="Queue.cs" company="">
// TODO: Update copyright text.
// </copyright>
// -----------------------------------------------------------------------

using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Cinchcast.Roque.Core
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using Newtonsoft.Json;

    /// <summary>
    /// Represents a queue of pending jobs
    /// </summary>
    public abstract class Queue
    {
        public const string DefaultEventQueueName = "_events";

        private static IDictionary<string, Queue> _Instances = new Dictionary<string, Queue>();

        /// <summary>
        /// Get a queue by name
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public static Queue Get(string name)
        {
            Queue queue;
            if (string.IsNullOrWhiteSpace(name))
            {
                name = string.Empty;
            }
            if (!_Instances.TryGetValue(name, out queue))
            {
                try
                {
                    if (Configuration.Roque.Settings == null)
                    {
                        throw new Exception("Roque configuration section not found");
                    }
                    var queuesConfig = Configuration.Roque.Settings.Queues;
                    if (queuesConfig == null)
                    {
                        throw new Exception("No queues found in configuration");
                    }
                    var queueConfig = queuesConfig[name];
                    if (queueConfig == null)
                    {
                        throw new Exception("Queue not found: " + name);
                    }
                    var queueType = Type.GetType(queueConfig.QueueType);
                    if (queueType == null)
                    {
                        throw new Exception("Queue type not found: " + queueConfig.QueueType);
                    }

                    queue = (Queue)Activator.CreateInstance(queueType, queueConfig.Name, queueConfig.Settings.ToDictionary());
                    _Instances[queue.Name] = queue;
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
            return queue;
        }

        private static QueueArray _All;

        /// <summary>
        /// Returns all queues declared in configuration
        /// </summary>
        public static QueueArray All
        {
            get
            {
                if (_All == null)
                {
                    _All = new QueueArray(Configuration.Roque.Settings.Queues.OfType<Configuration.QueueElement>()
                        .Select(queueConfig => Queue.Get(queueConfig.Name)).ToArray());
                }
                return _All;
            }
        }

        public string Name { get; private set; }

        protected IDictionary<string, string> Settings { get; private set; }

        public Queue(string name, IDictionary<string, string> settings = null)
        {
            Name = name;
            Settings = settings ?? new Dictionary<string, string>();
        }

        /// <summary>
        /// Adds a new job to the queue asynchronously.
        /// </summary>
        /// <param name="job"></param>
        /// <returns></returns>
        public Task EnqueueAsync(Job job)
        {
            return Task.Factory.StartNew(() =>
            {
                Enqueue(job);
            });
        }

        /// <summary>
        /// Adds a new job to the queue.
        /// </summary>
        /// <param name="job"></param>
        /// <returns>true if the job was enqueued successfully</returns>
        public bool Enqueue(Job job)
        {
            // serialize async and enqueue
            string data;
            try
            {
                if (RoqueTrace.Switch.TraceVerbose)
                {
                    Trace.TraceInformation(string.Format("Adding job to {0}", Name));
                }
                data = JsonConvert.SerializeObject(job);
            }
            catch (Exception ex)
            {
                if (RoqueTrace.Switch.TraceError)
                {
                    Trace.TraceError("Error serializing job: " + ex.Message, ex);
                }
                return false;
            }
            try
            {
                if (job.IsEvent)
                {
                    EnqueueJsonEvent(data, job.Target, job.Method);
                }
                else
                {
                    EnqueueJson(data);
                }
                if (RoqueTrace.Switch.TraceVerbose)
                {
                    Trace.TraceInformation(string.Format("Added job to {0}", Name));
                }
            }
            catch (Exception ex)
            {
                if (RoqueTrace.Switch.TraceError)
                {
                    Trace.TraceError("Error adding job: " + ex.Message, ex);
                }
                return false;
            }
            return true;
        }

        /// <summary>
        /// Picks a job from this queue. If there queue is empty blocks until one is obtained.
        /// </summary>
        /// <param name="worker">the worker that requests the job, the job will me marked as in progress for this worker</param>
        /// <param name="timeoutSeconds">if blocked waiting for a job after this time null will be return</param>
        /// <returns>a job or null if none could be obtained before timeout</returns>
        public Job Dequeue(Worker worker, int timeoutSeconds = 10)
        {
            try
            {
                if (timeoutSeconds < 1)
                {
                    timeoutSeconds = 1;
                }
                if (RoqueTrace.Switch.TraceVerbose)
                {
                    Trace.TraceInformation(string.Format("Worker {0} waiting job on {0}", worker.Name, Name));
                }
                string data = DequeueJson(worker, timeoutSeconds);
                if (string.IsNullOrEmpty(data))
                {
                    return null;
                }
                Job job = JsonConvert.DeserializeObject<Job>(data);
                if (RoqueTrace.Switch.TraceVerbose)
                {
                    Trace.TraceInformation(string.Format("Worker {0} received job from {1}", worker.Name, Name));
                }
                return job;
            }
            catch (Exception ex)
            {
                if (RoqueTrace.Switch.TraceError)
                {
                    Trace.TraceError("Error dequeuing job: " + ex.Message, ex);
                }
                throw;
            }
        }

        /// <summary>
        /// Picks a job from this queue. If there queue is empty blocks until one is obtained.
        /// </summary>
        /// <param name="worker">the worker that requests the job, the job will me marked as in progress for this worker</param>
        /// <param name="timeoutSeconds">if blocked waiting for a job after this time null will be return</param>
        /// <returns>a job or null if none could be obtained before timeout</returns>
        public Job Peek(out long length)
        {
            try
            {
                string data = PeekJson(out length);
                if (string.IsNullOrEmpty(data))
                {
                    return null;
                }
                Job job = JsonConvert.DeserializeObject<Job>(data);
                return job;
            }
            catch (Exception ex)
            {
                if (RoqueTrace.Switch.TraceError)
                {
                    Trace.TraceError("Error peeking job: " + ex.Message, ex);
                }
                throw;
            }
        }

        public DateTime? GetTimeOfLastJobCompleted()
        {
            return DoGetTimeOfLastJobCompleted();
        }

        protected abstract DateTime? DoGetTimeOfLastJobCompleted();

        /// <summary>
        /// Get the job that is currently marked as in progress for a worker.
        /// </summary>
        /// <param name="worker"></param>
        /// <returns>an in progress job, or null if none was found</returns>
        public Job GetInProgressJob(Worker worker)
        {
            try
            {
                if (RoqueTrace.Switch.TraceVerbose)
                {
                    Trace.TraceInformation(string.Format("Looking for pending jobs on {0}", Name));
                }

                IQueueWithInProgressData queueWithInProgress = this as IQueueWithInProgressData;

                if (queueWithInProgress == null)
                {
                    // this queue doesn't support resuming work in progress
                    return null;
                }
                string data = queueWithInProgress.GetInProgressJson(worker);
                if (string.IsNullOrEmpty(data))
                {
                    return null;
                }
                Job job = JsonConvert.DeserializeObject<Job>(data);
                job.MarkAsResuming();
                if (RoqueTrace.Switch.TraceVerbose)
                {
                    Trace.TraceInformation(string.Format("Resuming pending job on {0}", Name));
                }
                return job;
            }
            catch (Exception ex)
            {
                if (RoqueTrace.Switch.TraceError)
                {
                    Trace.TraceError("Error dequeuing in progress job: " + ex.Message, ex);
                }
                throw;
            }
        }

        /// <summary>
        /// Mark a job as completed by a worker, removing it from in progress.
        /// </summary>
        /// <param name="worker"></param>
        /// <param name="job"></param>
        public void Completed(Worker worker, Job job, bool failed = false)
        {
            IQueueWithInProgressData queueWithInProgress = this as IQueueWithInProgressData;
            if (queueWithInProgress != null)
            {
                queueWithInProgress.JobCompleted(worker, job, failed);
            }
        }

        protected abstract void EnqueueJson(string data);

        protected abstract void EnqueueJsonEvent(string data, string target, string eventName);

        protected abstract string DequeueJson(Worker worker, int timeoutSeconds);

        protected abstract string PeekJson(out long length);

        public void ReportEventSubscription(string sourceQueue, string target, string eventName)
        {
            try
            {
                DoReportEventSubscription(sourceQueue, target, eventName);
            }
            catch (Exception ex)
            {
                if (RoqueTrace.Switch.TraceError)
                {
                    Trace.TraceError("Error reporting event subscription: " + ex.Message, ex);
                }
                throw;
            }
        }

        protected abstract void DoReportEventSubscription(string sourceQueue, string target, string eventName);

        public abstract IDictionary<string, string[]> GetSubscribers();

    }
}
