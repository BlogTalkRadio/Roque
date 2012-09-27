// -----------------------------------------------------------------------
// <copyright file="DummyProxyGenerator.cs" company="">
// TODO: Update copyright text.
// </copyright>
// -----------------------------------------------------------------------

using System.ComponentModel;
using Castle.DynamicProxy;

namespace Cinchcast.Roque.Core
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;

    /// <summary>
    /// Creates dynamic proxies that handle events
    /// </summary>
    public static class EventProxyGenerator
    {
        /// <summary>
        /// An interceptor that performs no action (doesn't call Proceed)
        /// </summary>
        public class NoActionInterceptor : IInterceptor
        {
            private static NoActionInterceptor _Default;
            public static NoActionInterceptor Default
            {

                get
                {
                    if (_Default == null)
                    {
                        _Default = new NoActionInterceptor();
                    }
                    return _Default;
                }
            }

            private NoActionInterceptor()
            {
            }

            public void Intercept(IInvocation invocation)
            {
                // do nothing
            }
        }

        public interface IEventProxy
        {
            Delegate[] GetHandlersForEvent(string eventName);
            void BeginTrackingSubscriptions();
            string[] GetEventsWithNewSubscriptions();
        }

        /// <summary>
        /// An interceptor that supports events
        /// </summary>
        public class EventInterceptor : IInterceptor
        {
            public IDictionary<string, IList<Delegate>> EventDelegates = new Dictionary<string, IList<Delegate>>();
            public IDictionary<string, int> EventDelegatesCounts = new Dictionary<string, int>();

            public void Intercept(IInvocation invocation)
            {
                if (invocation.Method.Name == "GetHandlersForEvent")
                {
                    IList<Delegate> delegates;
                    if (EventDelegates.TryGetValue(invocation.Arguments[0] as string, out delegates))
                    {
                        invocation.ReturnValue = delegates.ToArray();
                    }
                    else
                    {
                        invocation.ReturnValue = new Delegate[0];
                    }
                    return;
                }
                if (invocation.Method.Name == "BeginTrackingSubscriptions")
                {
                    EventDelegatesCounts = EventDelegates.ToDictionary(kv => kv.Key, kv => kv.Value.Count);
                    return;
                }
                if (invocation.Method.Name == "GetEventsWithNewSubscriptions")
                {
                    invocation.ReturnValue = EventDelegates.Where(kv => !EventDelegatesCounts.ContainsKey(kv.Key) || kv.Value.Count > EventDelegatesCounts[kv.Key])
                        .Select(kv => kv.Key).ToArray();
                    return;
                }

                if (invocation.Method.Name.StartsWith("add_"))
                {
                    string eventName = invocation.Method.Name.Substring(4);
                    var evenInfo = invocation.Proxy.GetType().GetEvent(eventName);
                    if (evenInfo != null)
                    {
                        IList<Delegate> delegates;
                        if (!EventDelegates.TryGetValue(eventName, out delegates))
                        {
                            delegates = new List<Delegate>();
                            EventDelegates[eventName] = delegates;
                        }
                        delegates.Add((Delegate)invocation.Arguments[0]);
                        return;
                    }
                }
                if (invocation.Method.Name.StartsWith("remove_"))
                {
                    string eventName = invocation.Method.Name.Substring(7);
                    var evenInfo = invocation.Proxy.GetType().GetEvent(eventName);
                    if (evenInfo != null)
                    {
                        IList<Delegate> delegates;
                        if (!EventDelegates.TryGetValue(eventName, out delegates))
                        {
                            delegates = new List<Delegate>();
                            EventDelegates[eventName] = delegates;
                        }
                        delegates.Remove((Delegate)invocation.Arguments[0]);
                        return;
                    }
                }
            }
        }

        private static ProxyGenerator ProxyGenerator;

        public static object CreateEventInterfaceProxy(Type interfaceType)
        {
            if (ProxyGenerator == null)
            {
                ProxyGenerator = new ProxyGenerator(disableSignedModule: !Configuration.Roque.Settings.SignDynamicProxyModule);
            }
            return ProxyGenerator.CreateInterfaceProxyWithoutTarget(interfaceType, new[] { typeof(IEventProxy) }, new EventInterceptor());
        }
    }
}
