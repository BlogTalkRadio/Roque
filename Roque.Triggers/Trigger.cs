using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using BookSleeve;
using Cinchcast.Roque.Core;
using Cinchcast.Roque.Core.Configuration;
using Cinchcast.Roque.Redis;

namespace Cinchcast.Roque.Triggers
{
    public class Trigger
    {
        private static TriggerWatcher _All;

        public static TriggerWatcher All
        {
            get
            {
                if (_All == null)
                {
                    var triggerConfigs = Cinchcast.Roque.Core.Configuration.Roque.Settings.Triggers.OfType<TriggerElement>();
                    var triggers = new List<Trigger>();
                    foreach (var triggerConfig in triggerConfigs)
                    {
                        Trigger trigger = (Trigger)Activator.CreateInstance(Type.GetType(triggerConfig.TriggerType));
                        trigger.Name = triggerConfig.Name;
                        trigger.Configure(
                            triggerConfig.Queue,
                            triggerConfig.TargetTypeFullName,
                            triggerConfig.TargetMethodName,
                            triggerConfig.TargetArgument,
                            triggerConfig.Settings.ToDictionary());
                        triggers.Add(trigger);
                    }
                    _All = new TriggerWatcher(triggers.ToArray());
                }
                return _All;
            }
        }

        public string Name { get; set; }

        public bool Active { get; private set; }

        public IDictionary<string, string> Settings { get; private set; }

        public RedisQueue Queue { get; private set; }

        public Func<Job> JobCreator { get; private set; }

        private RedisLiveConnection _Connection;

        public RedisLiveConnection Connection
        {
            get
            {
                if (_Connection == null)
                {
                    _Connection = Queue.Connection;
                }
                return _Connection;
            }
        }

        public Trigger()
        {
            Settings = new Dictionary<string, string>();
        }

        public void Configure(string queue, string targetTypeFullName, string targetMethodName, string targetArgument, IDictionary<string, string> settings)
        {
            Settings = settings;
            Queue = (RedisQueue)Roque.Core.Queue.Get(queue);
            _Connection = Queue.Connection;
            JobCreator = () =>
                {
                    Job job = Job.Create(targetTypeFullName, targetMethodName);
                    if (!string.IsNullOrEmpty(targetArgument))
                    {
                        job.Arguments = new[] { targetArgument };
                    }
                    return job;
                };
        }

        public DateTime? GetLastExecution()
        {
            try
            {
                string lastExecutionString = Connection.GetOpen().Hashes.GetString(0, GetRedisKey(), "lastexecution").Result;
                if (string.IsNullOrEmpty(lastExecutionString))
                {
                    return null;
                }
                return DateTime.Parse(lastExecutionString, CultureInfo.InvariantCulture);
            }
            catch (Exception ex)
            {
                RoqueTrace.Source.Trace(TraceEventType.Error, "Error while obtaining trigger {1} last execution time: {0}", ex.Message, Name, ex);
                throw;
            }
        }

        public void Activate()
        {
            if (Active)
            {
                return;
            }

            OnActivate();

            RoqueTrace.Source.TraceEvent(TraceEventType.Information, -1, "Trigger activated. Type: {0}, Name: {1}", GetType().Name, Name);
        }

        public void Deactivate()
        {
            if (!Active)
            {
                return;
            }
            OnDeactivate();
            Active = false;
        }

        protected virtual string GetRedisKey(string suffixFormat = null, params object[] parameters)
        {
            return GetRedisKeyForTrigger(Name, suffixFormat, parameters);
        }

        protected virtual string GetRedisKeyForTrigger(string triggerName, string suffixFormat = null, params object[] parameters)
        {
            var key = new StringBuilder(RedisQueue.QueuePrefix);
            key.Append(triggerName);
            if (!string.IsNullOrEmpty(suffixFormat))
            {
                key.Append(":");
                key.Append(string.Format(suffixFormat, parameters));
            }
            return key.ToString();
        }

        protected virtual void OnActivate()
        {
        }

        protected virtual void OnDeactivate()
        {
        }

        public virtual DateTime? GetNextExecution()
        {
            return GetNextExecution(GetLastExecution());
        }

        protected virtual DateTime? GetNextExecution(DateTime? lastExecution)
        {
            // by default next execution is unknown
            return null;
        }

        public void Execute(bool force = false)
        {
            DateTime? lastExecution = null;
            DateTime? nextExecution = null;
            try
            {
                lastExecution = GetLastExecution();
                nextExecution = GetNextExecution(lastExecution);
                Connection.GetOpen().Hashes.Set(0, GetRedisKey(), "nextexecution", nextExecution == null ? null :
                    nextExecution.Value.ToString("s", CultureInfo.InvariantCulture)).Wait();
            }
            catch (Exception ex)
            {
                RoqueTrace.Source.Trace(TraceEventType.Error, "Error while getting next execution for trigger {1}: {0}", ex.Message, Name, ex);
            }

            if (force || nextExecution != null)
            {
                if (force || nextExecution <= DateTime.UtcNow)
                {
                    ExecuteNow(lastExecution, force);
                }
                else
                {
                    RoqueTrace.Source.TraceEvent(TraceEventType.Information, -1, "Trigger will run at {2} GMT. Type: {0}, Name: {1}", GetType().Name, Name, nextExecution.Value);
                    Thread.Sleep((int)Math.Min(
                        TimeSpan.FromMinutes(1).TotalMilliseconds,
                        Math.Max(nextExecution.Value.Subtract(DateTime.UtcNow).TotalMilliseconds, 1000)));
                }
            }
        }

        protected void ExecuteNow(DateTime? lastExecution, bool force = false)
        {
            bool lockObtained = false;
            RedisConnection connection = null;
            try
            {
                connection = Connection.GetOpen();
                // semaphore, prevent multiple executions
                lockObtained = connection.Strings.TakeLock(0, GetRedisKey("executing"), "1", 5).Result;
                if (!lockObtained)
                {
                    // trigger already executing, abort this execution
                    return;
                }
                if (lastExecution != GetLastExecution())
                {
                    // the trigger got executed right now, abort this execution
                    return;
                }

                if (EnqueueJob())
                {
                    RoqueTrace.Source.TraceEvent(TraceEventType.Information, -1, "Trigger executed. Type: {0}, Name: {1}", GetType().Name, Name);
                    var recentExecution = DateTime.UtcNow;
                    var nextExecution = GetNextExecution(recentExecution);
                    connection.Hashes.Set(0, GetRedisKey(), "lastexecution", recentExecution.ToString("s", CultureInfo.InvariantCulture)).Wait();
                    connection.Hashes.Set(0, GetRedisKey(), "nextexecution", nextExecution == null ? null :
                        nextExecution.Value.ToString("s", CultureInfo.InvariantCulture)).Wait();
                    RoqueTrace.Source.TraceEvent(TraceEventType.Information, -1, "Trigger next execution: {2} GMT. Type: {0}, Name: {1}", GetType().Name, Name, nextExecution);
                }
            }
            catch (Exception ex)
            {
                RoqueTrace.Source.Trace(TraceEventType.Error, "Error while executing trigger {1}: {0}", ex.Message, Name, ex);
            }
            finally
            {
                if (lockObtained)
                {
                    try
                    {
                        connection.Strings.ReleaseLock(0, GetRedisKey("executing")).Wait();
                    }
                    catch (Exception ex)
                    {
                        RoqueTrace.Source.Trace(TraceEventType.Error, "Error while releasing trigger {1} lock: {0}", ex.Message, Name, ex);
                    }
                }
            }
        }

        protected virtual bool EnqueueJob()
        {
            try
            {
                Job job = JobCreator();
                bool enqueued = Queue.Enqueue(job);
                RoqueTrace.Source.TraceEvent(TraceEventType.Information, -1, "Trigger enqueued job: {0}.{1}. Type: {2}, Name: {3}", job.Target, job.Method, GetType().Name, Name);
                return enqueued;
            }
            catch (Exception ex)
            {
                RoqueTrace.Source.Trace(TraceEventType.Error, "Error while enqueing trigger {1} job: {0}", ex.Message, Name, ex);
                return false;
            }
        }

    }
}
