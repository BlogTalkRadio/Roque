// -----------------------------------------------------------------------
// <copyright file="RoqueProxies.cs" company="">
// TODO: Update copyright text.
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using Castle.DynamicProxy;
using Castle.DynamicProxy.Generators;

namespace Cinchcast.Roque.Core
{
    /// <summary>
    /// Generates dynamic proxies that intercept any method call and enqueues a job on a <see cref="Roque.Core.Queue"/>
    /// </summary>
    public static class RoqueProxyGenerator
    {
        private class Interceptor<T> : IInterceptor
        {
            public Queue Queue { get; private set; }

            private string _TypeName;

            private bool _EnqueueAsync;

            public Interceptor(Queue queue, bool enqueueAsync = false)
            {
                Queue = queue;
                _TypeName = typeof(T).AssemblyQualifiedName;
                _EnqueueAsync = enqueueAsync;
            }

            public void Intercept(IInvocation invocation)
            {
                var job = Job.Create(_TypeName, invocation.Method.Name, invocation.Arguments);
                if (_EnqueueAsync)
                {
                    Queue.EnqueueAsync(job);
                }
                else
                {
                    Queue.Enqueue(job);
                }
            }
        }

        private static ProxyGenerator _Generator;

        /// <summary>
        /// Base class for dynamic work proxies. When a method on an instance is invoked a new job is sent to the <see cref="Queue"/>
        /// </summary>
        public class Proxy
        {
            private Queue _Queue;
            public Queue Queue
            {
                get
                {
                    return _Queue;
                }
                internal set
                {
                    if (_Queue != null)
                    {
                        throw new InvalidOperationException("Proxy Queue cannot be modified");
                    }
                    _Queue = value;
                }
            }
        }

        /// <summary>
        /// Creates a new proxy for <typeparamref name="T"/> class, all methods will be intercepted and enqueued for async execution 
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="queueName">the queue to send all invocations</param>
        /// <param name="enqueueAsync">if true enqueueing will be done async</param>
        /// <returns>the transparent proxy which enqueues all invocations for async execution</returns>
        public static T Create<T>(string queueName, bool enqueueAsync = false)
            where T : class
        {
            return Create<T>(Queue.Get(queueName), enqueueAsync);
        }

        /// <summary>
        /// Creates a new proxy for <typeparamref name="T"/> class, all methods will be intercepted and enqueued for async execution 
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="queue">the queue to send all invocations</param>
        /// <param name="enqueueAsync">if true enqueueing will be done async</param>
        /// <returns>the transparent proxy which enqueues all invocations for async execution</returns>
        public static T Create<T>(Queue queue, bool enqueueAsync = false)
            where T : class
        {
            if (_Generator == null)
            {
                _Generator = new ProxyGenerator(disableSignedModule: !Configuration.Roque.Settings.SignDynamicProxyModule);
            }
            var options = new ProxyGenerationOptions();
            options.BaseTypeForInterfaceProxy = typeof(Proxy);
            var interceptor = new Interceptor<T>(queue);
            var proxy = _Generator.CreateInterfaceProxyWithoutTarget<T>(options, interceptor);
            (proxy as Proxy).Queue = interceptor.Queue;
            return proxy;
        }

    }
}
