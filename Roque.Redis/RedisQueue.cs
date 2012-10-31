// -----------------------------------------------------------------------
// <copyright file="RedisQueue.cs" company="">
// TODO: Update copyright text.
// </copyright>
// -----------------------------------------------------------------------

using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using BookSleeve;
using Cinchcast.Roque.Core;

namespace Cinchcast.Roque.Redis
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;

    /// <summary>
    /// Redis-based implementation of a <see cref="Queue"/>
    /// </summary>
    public class RedisQueue : Queue, IQueueWithInProgressData
    {
        /// <summary>
        /// prefix for queues names in Redis
        /// </summary>
        public static string RedisNamespace = "roque:";

        private RedisLiveConnection _Connection;

        public RedisLiveConnection Connection
        {
            get
            {
                if (_Connection == null)
                {
                    _Connection = new RedisLiveConnection(Settings);
                }
                return _Connection;
            }
        }

        private object syncConnection = new object();

        protected IDictionary<string, string[]> _SubscribersCache = new Dictionary<string, string[]>();

        protected RedisSubscriberConnection _SubscribedToSubscribersChangesChannel;

        protected DateTime _SubscribersCacheLastClear = DateTime.Now;

        public static TimeSpan DefaultSubscribersCacheExpiration = TimeSpan.FromMinutes(60);

        public TimeSpan? SubscribersCacheExpiration { get; set; }

        public RedisQueue(string name, IDictionary<string, string> setings)
            : base(name, setings)
        {
        }

        protected virtual string GetRedisKey(string suffixFormat = null, params object[] parameters)
        {
            return GetRedisKeyForQueue(Name, suffixFormat, parameters);
        }

        protected virtual string GetRedisKeyForQueue(string queueName, string suffixFormat = null, params object[] parameters)
        {
            var key = new StringBuilder(RedisNamespace);
            key.Append("q:");
            key.Append(queueName);
            if (!string.IsNullOrEmpty(suffixFormat))
            {
                key.Append(":");
                key.Append(string.Format(suffixFormat, parameters));
            }
            return key.ToString();
        }

        protected string GetWorkerKey(Worker worker)
        {
            return string.Format("w_{0}_{1}", worker.Name, worker.ID);
        }

        protected override void EnqueueJson(string data)
        {
            Connection.GetOpen().Lists.AddFirst(0, GetRedisKey(), data).Wait();
        }

        protected override string DequeueJson(Worker worker, int timeoutSeconds)
        {
            var connection = Connection.GetOpen();

            // move job from queue to worker in progress
            string data = connection.Lists.BlockingRemoveLastAndAddFirstString(0, GetRedisKey(), GetRedisKey("worker:{0}:inprogress", GetWorkerKey(worker)), timeoutSeconds).Result;

            if (!string.IsNullOrEmpty(data))
            {
                try
                {
                    connection.Hashes.Set(0, GetRedisKey("worker:{0}:state", GetWorkerKey(worker)), "currentstart", DateTime.UtcNow.ToString("s", CultureInfo.InvariantCulture));
                }
                catch (Exception ex)
                {
                    RoqueTrace.Source.Trace(TraceEventType.Error, "[REDIS] error registering job start: {0}", ex.Message, ex);
                }
            }
            return data;
        }

        protected override string PeekJson(out long length)
        {
            var connection = Connection.GetOpen();

            string data = connection.Lists.GetString(0, GetRedisKey(), -1).Result;
            if (data == null)
            {
                length = 0;
            }
            else
            {
                length = connection.Lists.GetLength(0, GetRedisKey()).Result;
            }
            return data;
        }

        protected override DateTime? DoGetTimeOfLastJobCompleted()
        {
            var connection = Connection.GetOpen();

            string data = connection.Hashes.GetString(0, GetRedisKey("state"), "lastcomplete").Result;
            if (string.IsNullOrEmpty(data))
            {
                return null;
            }
            else
            {
                return DateTime.Parse(data, CultureInfo.InvariantCulture);
            }
        }

        public string GetInProgressJson(Worker worker)
        {
            var connection = Connection.GetOpen();
            string data = connection.Lists.GetString(0, GetRedisKey("worker:{0}:inprogress", GetWorkerKey(worker)), 0).Result;
            if (data != null)
            {
                try
                {
                    connection.Hashes.Set(0, GetRedisKey("worker:{0}:state", GetWorkerKey(worker)), "currentstart", DateTime.UtcNow.ToString("s", CultureInfo.InvariantCulture));
                }
                catch (Exception ex)
                {
                    RoqueTrace.Source.Trace(TraceEventType.Error, "[REDIS] error registering in progress job start: {0}", ex.Message, ex);
                }
            }
            return data;
        }

        public void JobCompleted(Worker worker, Job job, bool failed)
        {
            try
            {
                var connection = Connection.GetOpen();
                string json = connection.Lists.RemoveFirstString(0, GetRedisKey("worker:{0}:inprogress", GetWorkerKey(worker))).Result;
                if (failed)
                {
                    connection.Lists.AddFirst(0, GetRedisKey("failed"), json).Wait();
                }
                connection.Hashes.Remove(0, GetRedisKey("worker:{0}:state", GetWorkerKey(worker)), "currentstart").Wait();
                connection.Hashes.Set(0, GetRedisKey("worker:{0}:state", GetWorkerKey(worker)), "lastcomplete", DateTime.UtcNow.ToString("s", CultureInfo.InvariantCulture)).Wait();
                connection.Hashes.Set(0, GetRedisKey("state"), "lastcomplete", DateTime.UtcNow.ToString("s", CultureInfo.InvariantCulture)).Wait();
            }
            catch (Exception ex)
            {
                RoqueTrace.Source.Trace(TraceEventType.Error, "[REDIS] error registering job completion: {0}", ex.Message, ex);
                throw;
            }
        }

        public void ClearSubscribersCache()
        {
            _SubscribersCache.Clear();
        }

        protected override void DoReportEventSubscription(string sourceQueue, string target, string eventName)
        {
            var connection = Connection.GetOpen();
            var added = connection.SortedSets.Add(0, GetRedisKeyForQueue(sourceQueue, "events:{0}:{1}:subscribers", target, eventName), Name, 0).Result;
            if (added)
            {
                connection.Publish(GetRedisKeyForQueue(sourceQueue, "events:subscriberschanges"), "+" + target + ":" + eventName).Wait();
                RoqueTrace.Source.Trace(TraceEventType.Information, "[REDIS] Queue {0} subscribed to events {1}:{2} events on queue {3}", Name, target, eventName, sourceQueue);
            }
        }

        public override string[] GetSubscribersForEvent(string target, string eventName)
        {
            string[] subscribers;
            string eventKey = target + ":" + eventName;
            RedisConnection connection = null;

            if (_SubscribedToSubscribersChangesChannel != null &&
                _SubscribedToSubscribersChangesChannel.State != RedisConnectionBase.ConnectionState.Open &&
                _SubscribedToSubscribersChangesChannel.State != RedisConnectionBase.ConnectionState.Opening)
            {
                // connection dropped, create a new one
                _SubscribedToSubscribersChangesChannel = null;
                ClearSubscribersCache();
            }
            if (_SubscribedToSubscribersChangesChannel == null)
            {
                if (connection == null)
                {
                    connection = Connection.GetOpen();
                }
                _SubscribedToSubscribersChangesChannel = connection.GetOpenSubscriberChannel();
                _SubscribedToSubscribersChangesChannel.Subscribe(GetRedisKey("events:subscriberschanges"), (message, bytes) =>
                {
                    RoqueTrace.Source.Trace(TraceEventType.Information, "[REDIS] Subscribers added to {0}, clearing subscribers cache", Name);
                    ClearSubscribersCache();
                });
                RoqueTrace.Source.Trace(TraceEventType.Verbose, "[REDIS] Listening for subscribers changes on queue {0}", Name);
            }

            if (DateTime.Now.Subtract(_SubscribersCacheLastClear) > (SubscribersCacheExpiration ?? DefaultSubscribersCacheExpiration))
            {
                ClearSubscribersCache();
            }

            if (!_SubscribersCache.TryGetValue(eventKey, out subscribers))
            {
                if (connection == null)
                {
                    connection = Connection.GetOpen();
                }
                subscribers = connection.SortedSets.Range(0, GetRedisKey("events:{0}:subscribers", eventKey), 0, -1).Result
                    .Select(set => Encoding.UTF8.GetString(set.Key)).ToArray();
                _SubscribersCache[eventKey] = subscribers;
            }
            return subscribers;
        }

        protected override void EnqueueJsonEvent(string data, string target, string eventName)
        {
            var connection = Connection.GetOpen();

            var subscribers = GetSubscribersForEvent(target, eventName);

            if (subscribers == null || subscribers.Length == 0)
            {
                RoqueTrace.Source.Trace(TraceEventType.Verbose, "No subscriber for this event, enqueue omitted. Event: {0}:{1}, Queue:{2}", target, eventName, Name);
            }
            else
            {
                foreach (var subscriber in subscribers)
                {
                    connection.Lists.AddFirst(0, GetRedisKeyForQueue(subscriber), data).ContinueWith(task =>
                    {
                        if (task.Exception != null)
                        {
                            RoqueTrace.Source.Trace(TraceEventType.Error, "[REDIS] Error enqueuing event on {0}. Event: {1}:{2}. {3}", subscriber, target, eventName, task.Exception.Message, task.Exception);
                        }
                    },
                    TaskContinuationOptions.OnlyOnFaulted);
                }
                if (RoqueTrace.Source.Switch.ShouldTrace(TraceEventType.Verbose))
                {
                    RoqueTrace.Source.Trace(TraceEventType.Verbose, "Event published to queues: {0}. Event: {1}:{2}", string.Join(", ", subscribers), target, eventName);
                }
            }
        }

        public override IDictionary<string, string[]> GetSubscribers()
        {
            var connection = Connection.GetOpen();
            var keys = connection.Keys.Find(0, GetRedisKey("events:*:subscribers")).Result;
            var keyRegex = new Regex("^" + GetRedisKey("events:(.*):subscribers$"));
            var subscribers = new Dictionary<string, string[]>();
            foreach (var key in keys)
            {
                var eventKey = keyRegex.Match(key).Groups[1].Value;
                var targets = connection.SortedSets.Range(0, key, 0, -1).Result
                    .Select(set => Encoding.UTF8.GetString(set.Key)).ToArray();
                if (targets.Length > 0)
                {
                    subscribers[eventKey] = targets;
                }
            }
            return subscribers;
        }
    }
}
