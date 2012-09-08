// -----------------------------------------------------------------------
// <copyright file="Worker.cs" company="">
// TODO: Update copyright text.
// </copyright>
// -----------------------------------------------------------------------

using System.Collections;
using System.Configuration;
using System.Diagnostics;
using System.Reflection;
using System.Threading.Tasks;
using Castle.Core.Resource;
using Castle.DynamicProxy;
using Castle.Windsor;
using Castle.Windsor.Configuration.Interpreters;
using Cinchcast.Roque.Core.Configuration;
using Newtonsoft.Json;

namespace Cinchcast.Roque.Core
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;

    /// <summary>
    /// Executes a <see cref="Job"/> by invoking a method in a service class.
    /// <remarks>
    ///   - If job target type is an interface castle windsor container is used to obtain a service.
    ///   - Job retries are supported by using the <see cref="RetryOnAttribute"/> or throwing a <see cref="ShouldRetryException"/>
    /// </remarks>
    /// </summary>
    public class Executor
    {

        private static WindsorContainer ServiceContainer;

        private class Target
        {
            public object Instance { get; private set; }

            public Type InstanceType { get; private set; }

            public IDictionary<string, MethodInfo> Methods { get; private set; }

            public IDictionary<string, EventInfo> Events { get; private set; }

            public Target(Type type, bool ifNotFoundUseEventProxy = false)
            {
                InstanceType = type;
                if (type.IsInterface || (type.IsAbstract && !type.IsSealed))
                {
                    if (ServiceContainer == null)
                    {
                        ServiceContainer = new WindsorContainer(new XmlInterpreter(new ConfigResource("castle")));
                    }
                    // get an implementation for this interface or abstract class
                    try
                    {
                        Instance = ServiceContainer.Resolve(type);
                    }
                    catch
                    {
                        if (ifNotFoundUseEventProxy && type.IsInterface)
                        {
                            Instance = EventProxyGenerator.CreateEventInterfaceProxy(type);
                        }
                        else
                        {
                            throw;
                        }
                    }
                }
                else
                {
                    if (type.IsAbstract)
                    {
                        // static class
                        Instance = null;
                    }
                    else
                    {
                        Instance = Activator.CreateInstance(type);
                    }
                }
                Methods = new Dictionary<string, MethodInfo>();
                Events = new Dictionary<string, EventInfo>();
            }

            public void Invoke(string methodName, params string[] parameters)
            {
                MethodInfo method;
                if (!Methods.TryGetValue(methodName, out method))
                {
                    try
                    {
                        method = InstanceType.GetMethod(methodName);
                    }
                    catch (AmbiguousMatchException ex)
                    {
                        throw new Exception("Invoked methods can't have multiple overloads: " + methodName, ex);
                    }
                    Methods[methodName] = method;
                }
                var methodParametersInfo = method.GetParameters();
                if (methodParametersInfo.Length != parameters.Length)
                {
                    throw new Exception(string.Format("Wrong number of parameters. Method: {0}, Expected: {1}, Received: {2}", methodName, methodParametersInfo.Length, parameters.Length));
                }
                var parameterValues = new List<object>();
                for (int index = 0; index < parameters.Length; index++)
                {
                    try
                    {
                        parameterValues.Add(JsonConvert.DeserializeObject(parameters[index], methodParametersInfo[index].ParameterType));
                    }
                    catch (Exception ex)
                    {
                        if (RoqueTrace.Switch.TraceError)
                        {
                            Trace.TraceError(string.Format("Error deserializing parameter: {0}. Method: {1}, Parameter: {2}, Expected Type: {3}", ex.Message, method.Name, methodParametersInfo[index].Name, methodParametersInfo[index].ParameterType.FullName), ex);
                        }
                        throw;
                    }
                }
                try
                {
                    method.Invoke(Instance, parameterValues.ToArray());
                }
                catch (TargetInvocationException ex)
                {
                    var jobException = ex.InnerException;
                    if (RoqueTrace.Switch.TraceError)
                    {
                        Trace.TraceError(string.Format("Error invoking job target: {0}\n\n{1}", jobException.Message, jobException));
                    }
                    var jobExceptionType = jobException.GetType();
                    if (jobException is ShouldRetryException)
                    {
                        throw jobException;
                    }
                    var invokedMethod = (Instance == null ? method : Instance.GetType().GetMethod(methodName));
                    var retryOn = invokedMethod.GetCustomAttributes(typeof(RetryOnAttribute), true)
                        .OfType<RetryOnAttribute>()
                        .FirstOrDefault(attr => attr.ExceptionType.IsAssignableFrom(jobExceptionType));
                    if (retryOn == null)
                    {
                        retryOn = invokedMethod.DeclaringType.GetCustomAttributes(typeof(RetryOnAttribute), true)
                            .OfType<RetryOnAttribute>()
                            .FirstOrDefault(attr => attr.ExceptionType.IsAssignableFrom(jobExceptionType));
                    }
                    if (retryOn != null && !(retryOn is DontRetryOnAttribute))
                    {
                        throw retryOn.CreateException(jobException);
                    }
                    throw;
                }
            }

            public void Raise(string eventName, params string[] parameters)
            {
                EventInfo eventInfo;
                if (!Events.TryGetValue(eventName, out eventInfo))
                {
                    eventInfo = InstanceType.GetEvent(eventName);
                    if (eventInfo == null && InstanceType.IsInterface)
                    {
                        // search event in parent interfaces
                        foreach (var parentInterface in InstanceType.GetInterfaces())
                        {
                            eventInfo = parentInterface.GetEvent(eventName);
                            if (eventInfo != null)
                            {
                                break;
                            }
                        }
                    }
                    if (eventInfo == null)
                    {
                        throw new Exception(string.Format("Event not found. Type: {0}, EventName: {1}", InstanceType.FullName, eventName));
                    }
                    Events[eventName] = eventInfo;
                }
                Type eventArgsType = GetEventArgsType(eventInfo);
                EventArgs eventArgsValue;
                try
                {
                    if (parameters.Length > 0)
                    {
                        eventArgsValue = JsonConvert.DeserializeObject(parameters[0], eventArgsType) as EventArgs;
                    }
                    else
                    {
                        eventArgsValue = EventArgs.Empty;
                    }
                }
                catch (Exception ex)
                {
                    if (RoqueTrace.Switch.TraceError)
                    {
                        Trace.TraceError(string.Format("Error deserializing event args: {0}. Event: {1}, Expected Type: {2}", ex.Message, eventInfo.Name, eventArgsType.FullName), ex);
                    }
                    throw;
                }

                MethodInfo handlerMethod = null;
                try
                {
                    if (Instance is EventProxyGenerator.IEventProxy)
                    {
                        var handlers = ((EventProxyGenerator.IEventProxy)Instance).GetHandlersForEvent(eventInfo.Name);
                        if (handlers.Length > 0)
                        {
                            foreach (var handler in handlers)
                            {
                                handlerMethod = handler.Method;
                                handlerMethod.Invoke(handler.Target, new object[] { Instance, eventArgsValue });
                            }
                        }
                        else
                        {
                            if (RoqueTrace.Switch.TraceInfo)
                            {
                                Trace.TraceInformation(string.Format("No suscribers found for event: {0}", eventInfo.Name));
                            }
                        }
                    }
                    else
                    {
                        var privateDelegatesField = Instance.GetType().GetField(eventInfo.Name, BindingFlags.Instance | BindingFlags.NonPublic);
                        var eventDelegate = (MulticastDelegate)privateDelegatesField.GetValue(Instance);
                        if (eventDelegate != null)
                        {
                            foreach (var handler in eventDelegate.GetInvocationList())
                            {
                                handler.Method.Invoke(handler.Target, new object[] { Instance, eventArgsValue });
                            }
                        }
                    }
                }
                catch (TargetInvocationException ex)
                {
                    var jobException = ex.InnerException;
                    if (RoqueTrace.Switch.TraceError)
                    {
                        Trace.TraceError(string.Format("Error invoking event subscriber: {0}\n\n{1}", jobException.Message, jobException));
                    }
                    var jobExceptionType = jobException.GetType();
                    if (jobException is ShouldRetryException)
                    {
                        throw jobException;
                    }
                    var retryOn = handlerMethod.GetCustomAttributes(typeof(RetryOnAttribute), true)
                        .OfType<RetryOnAttribute>()
                        .FirstOrDefault(attr => attr.ExceptionType.IsAssignableFrom(jobExceptionType));
                    if (retryOn == null)
                    {
                        retryOn = handlerMethod.DeclaringType.GetCustomAttributes(typeof(RetryOnAttribute), true)
                            .OfType<RetryOnAttribute>()
                            .FirstOrDefault(attr => attr.ExceptionType.IsAssignableFrom(jobExceptionType));
                    }
                    if (retryOn != null && !(retryOn is DontRetryOnAttribute))
                    {
                        throw retryOn.CreateException(jobException);
                    }
                    throw;
                }
            }
        }

        private IDictionary<string, Target> _Targets = new Dictionary<string, Target>();

        private static Executor _Instance;

        public static Executor Default
        {
            get
            {
                if (_Instance == null)
                {
                    _Instance = new Executor();
                }
                return _Instance;
            }
            set
            {
                _Instance = value;
            }
        }

        public void Execute(Job job)
        {
            try
            {
                if (RoqueTrace.Switch.TraceInfo)
                {
                    Trace.TraceInformation("Job Starting: " + job.Method, job);
                }
                DoExecute(job);
                if (RoqueTrace.Switch.TraceInfo)
                {
                    Trace.TraceInformation("Job Completed: " + job.Method, job);
                }
            }
            catch (Exception ex)
            {
                if (RoqueTrace.Switch.TraceError)
                {
                    Trace.TraceError("Error executing job: " + ex.Message, ex);
                }
                throw;
            }
        }

        protected virtual void DoExecute(Job job)
        {
            InvokeTarget(job);
        }

        private void InvokeTarget(Job job)
        {
            Target target = GetTarget(job.Target);
            if (job.IsEvent)
            {
                target.Raise(job.Method, job.Arguments);
            }
            else
            {
                target.Invoke(job.Method, job.Arguments);
            }
        }

        private Target GetTarget(string targetTypeName, bool ifNotFoundUseEventProxy = false)
        {
            Target target;
            string fullName = targetTypeName.Split(new[] { ',', ' ' }).First();
            if (!_Targets.TryGetValue(fullName, out target))
            {
                Type type = null;
                type = Type.GetType(targetTypeName);
                if (type == null)
                {
                    throw new ShouldRetryException(TimeSpan.FromSeconds(10), 0, new Exception("Type not found: " + targetTypeName));
                }
                target = new Target(type, ifNotFoundUseEventProxy);
                _Targets[fullName] = target;
            }
            return target;
        }

        public void RegisterSubscriber(object subscriber, string sourceQueue = null, string queue = null)
        {
            var suscribeMethods = subscriber.GetType().GetMethods().Where(m => m.Name.StartsWith("Subscribe")).ToArray();
            foreach (var suscribeMethod in suscribeMethods)
            {
                List<object> parameters = new List<object>();

                foreach (var paramInfo in suscribeMethod.GetParameters())
                {
                    try
                    {
                        var instance = GetTarget(paramInfo.ParameterType.AssemblyQualifiedName, true).Instance;
                        parameters.Add(instance);
                        if (instance is EventProxyGenerator.IEventProxy)
                        {
                            ((EventProxyGenerator.IEventProxy)instance).BeginTrackingSubscriptions();
                        }
                    }
                    catch (Exception ex)
                    {
                        if (RoqueTrace.Switch.TraceError)
                        {
                            Trace.TraceError(string.Format("Error injecting subscriber parameter: {0}. Method: {1}, Parameter: {2}, Expected Type: {3}", ex.Message, suscribeMethod.Name, paramInfo.Name, paramInfo.ParameterType.FullName), ex);
                        }
                        throw;
                    }
                }
                suscribeMethod.Invoke(subscriber, parameters.ToArray());
                if (!string.IsNullOrWhiteSpace(sourceQueue) && !string.IsNullOrWhiteSpace(queue))
                {
                    foreach (var paramInfo in suscribeMethod.GetParameters())
                    {
                        var instance = GetTarget(paramInfo.ParameterType.AssemblyQualifiedName, true).Instance;
                        if (instance is EventProxyGenerator.IEventProxy)
                        {
                            string[] eventNames = ((EventProxyGenerator.IEventProxy)instance).GetEventsWithNewSubscriptions();
                            foreach (string eventName in eventNames)
                            {
                                Queue.Get(queue).ReportEventSubscription(sourceQueue, paramInfo.ParameterType.FullName, eventName);
                            }
                            if (RoqueTrace.Switch.TraceVerbose)
                            {
                                Trace.TraceInformation(string.Format("Reported event subscriptions. Events: {0}:{1}, Source Queue: {2}, Queue: {3}", paramInfo.ParameterType.FullName, string.Join(",", eventNames), sourceQueue, queue));
                            }
                        }
                    }
                }
            }
        }

        public void RegisterSubscribersForWorker(Worker worker)
        {
            if (string.IsNullOrEmpty(worker.Name))
            {
                return;
            }

            var workerConfig = Configuration.Roque.Settings.Workers[worker.Name];
            if (workerConfig == null || workerConfig.Subscribers.Count < 1)
            {
                return;
            }

            foreach (var subscriberConfig in workerConfig.Subscribers.OfType<SubscriberElement>())
            {
                try
                {
                    string sourceQueue = subscriberConfig.SourceQueue;
                    if (string.IsNullOrEmpty(sourceQueue))
                    {
                        sourceQueue = Queue.DefaultEventQueueName;
                    }
                    RegisterSubscriber(Activator.CreateInstance(Type.GetType(subscriberConfig.SubscriberType)), sourceQueue, worker.Queue.Name);
                }
                catch (Exception ex)
                {
                    if (RoqueTrace.Switch.TraceError)
                    {
                        Trace.TraceError(string.Format("Error registering subscriber: {0}. Type: {1}", ex.Message, subscriberConfig.SubscriberType), ex);
                    }
                    throw;
                }
            }
        }

        public static Type GetEventArgsType(EventInfo eventType)
        {
            Type t = eventType.EventHandlerType;
            MethodInfo m = t.GetMethod("Invoke");

            var parameters = m.GetParameters();
            return parameters[1].ParameterType;
        }

    }
}
