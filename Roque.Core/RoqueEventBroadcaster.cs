﻿// -----------------------------------------------------------------------
// <copyright file="RoqueEventBroadcaster.cs" company="">
// TODO: Update copyright text.
// </copyright>
// -----------------------------------------------------------------------

using System.Diagnostics;
using System.Reflection;
using Newtonsoft.Json;

namespace Cinchcast.Roque.Core
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;

    /// <summary>
    /// Subscribes to event and broadcasts them into multiple <see cref="Queue"/>
    /// </summary>
    public class RoqueEventBroadcaster
    {
        private class Handler
        {
            public object Source { get; private set; }

            public Type SourceType { get; private set; }

            public EventInfo EventInfo { get; private set; }

            public RoqueEventBroadcaster Broadcaster { get; private set; }

            private static MethodInfo _OnEventType = typeof(Handler).GetMethod("OnEvent");

            public Handler(RoqueEventBroadcaster broadcaster, Type sourceType, object source, string eventName)
            {
                Broadcaster = broadcaster;
                Source = source;
                SourceType = sourceType;
                EventInfo = SourceType.GetEvent(eventName);
                if (EventInfo == null)
                {
                    if (sourceType.IsInterface)
                    {
                        // search event in parent interfaces
                        foreach (var parentInterface in sourceType.GetInterfaces())
                        {
                            EventInfo = parentInterface.GetEvent(eventName);
                            if (EventInfo != null)
                            {
                                break;
                            }
                        }
                    }
                    if (EventInfo == null)
                    {
                        throw new Exception(string.Format("Event not found. Type: {0}, EventName: {1}", sourceType.FullName, eventName));
                    }
                }
                var handlerDelegate = Delegate.CreateDelegate(EventInfo.EventHandlerType, this, _OnEventType, true);

                EventInfo.AddEventHandler(source, handlerDelegate);
            }

            public void OnEvent(object sender, EventArgs args)
            {
                bool hasSubscribers;
                try
                {
                    hasSubscribers = Broadcaster.Queue.HasSubscribersForEvent(SourceType.FullName, EventInfo.Name);
                }
                catch (Exception ex)
                {
                    // error getting list of subscribers, we don't know but there might be subscribers
                    hasSubscribers = true;
                    RoqueTrace.Source.Trace(TraceEventType.Error, "Error looking for subscribers to this event: {0}. Event: {1}:{2}, Queue:{3}",
                        ex.Message, SourceType.FullName, EventInfo.Name, Broadcaster.Queue.Name);
                }

                if (hasSubscribers)
                {
                    var job = Job.Create(SourceType.FullName, EventInfo.Name, args);
                    job.IsEvent = true;
                    if (Broadcaster.EnqueueAsync)
                    {
                        Broadcaster.Queue.EnqueueAsync(job);
                    }
                    else
                    {
                        Broadcaster.Queue.Enqueue(job);
                    }
                }
                else
                {
                    RoqueTrace.Source.Trace(TraceEventType.Verbose, "No subscriber for this event, omitting. Event: {0}:{1}, Queue:{2}",
                        SourceType.FullName, EventInfo.Name, Broadcaster.Queue.Name);
                }
            }
        }

        private class Handler<T> : Handler
        {
            public Handler(RoqueEventBroadcaster broadcaster, T source, string eventName)
                : base(broadcaster, typeof(T), source, eventName)
            {
            }
        }

        /// <summary>
        /// The target queue for this broadcaster. By default is the reserved name queue "_events"
        /// </summary>
        public Queue Queue { get; private set; }

        /// <summary>
        /// If true messages are enqueued asynchronously (using <see cref="Queue.EnqueueAsync"/>)
        /// </summary>
        public bool EnqueueAsync { get; set; }

        private IDictionary<Tuple<Type, string>, Handler> Handlers = new Dictionary<Tuple<Type, string>, Handler>();

        /// <summary>
        /// Create a new event broadcaster
        /// </summary>
        /// <param name="queueName">target queue for this broadcaster, by default "_events"</param>
        public RoqueEventBroadcaster(string queueName = Queue.DefaultEventQueueName)
            : this(Queue.Get(queueName))
        {
        }

        /// <summary>
        /// Create a new event broadcaster
        /// </summary>
        /// <param name="queue">target queue for this broadcaster, by default "_events"</param>
        public RoqueEventBroadcaster(Queue queue)
        {
            Queue = queue;
        }

        /// <summary>
        /// Subscribes to a specific event making it available for remote subscribers
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source"></param>
        /// <param name="eventName"></param>
        public void Subscribe<T>(T source, string eventName)
        {
            if (Configuration.Roque.Settings.DisableEventBroadcasting)
            {
                RoqueTrace.Source.Trace(TraceEventType.Warning, "Roque event broadcasting is disabled, omitting subscription");
                return;
            }
            var key = Tuple.Create<Type, string>(typeof(T), eventName);
            if (!Handlers.ContainsKey(key))
            {
                var handler = new Handler<T>(this, source, eventName);
                Handlers[key] = handler;
            }
        }

        /// <summary>
        /// Subscribes to all events in <typeparamref name="T"/> making them available for remote subscribers
        /// </summary>
        /// <typeparam name="T">the type or interface to look for events</typeparam>
        /// <param name="source">the instance to attach to</param>
        /// <param name="subscribeToInheritedInterfaces">if true and <typeparamref name="T"/> is an interface, all events in parent interfaces are used too</param>
        public void SubscribeToAll<T>(T source, bool subscribeToInheritedInterfaces = true)
        {
            if (Configuration.Roque.Settings.DisableEventBroadcasting)
            {
                RoqueTrace.Source.Trace(TraceEventType.Warning, "Roque event broadcasting is disabled, omitting subscription");
                return;
            }

            List<Type> types = new List<Type>();
            types.Add(typeof(T));

            if (typeof(T).IsInterface && subscribeToInheritedInterfaces)
            {
                foreach (var parentInterface in typeof(T).GetInterfaces())
                {
                    types.Add(parentInterface);
                }
            }

            foreach (var type in types)
            {
                var eventInfos = type.GetEvents();
                foreach (var eventInfo in eventInfos)
                {
                    Subscribe<T>(source, eventInfo.Name);
                }
            }
        }
    }
}
