// -----------------------------------------------------------------------
// <copyright file="RoqueEventBroadcaster.cs" company="">
// TODO: Update copyright text.
// </copyright>
// -----------------------------------------------------------------------

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

                var handlerDelegate = Delegate.CreateDelegate(EventInfo.EventHandlerType, this, _OnEventType, true);

                EventInfo.AddEventHandler(source, handlerDelegate);
            }

            public void OnEvent(object sender, EventArgs args)
            {
                var job = Job.Create(SourceType.FullName, EventInfo.Name, args);
                job.IsEvent = true;
                Broadcaster.Queue.Enqueue(job);
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
        public void SubscribeToAll<T>(T source)
        {
            var eventInfos = typeof(T).GetEvents();
            foreach (var eventInfo in eventInfos)
            {
                Subscribe<T>(source, eventInfo.Name);
            }
        }
    }
}
