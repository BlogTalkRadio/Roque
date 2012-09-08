// -----------------------------------------------------------------------
// <copyright file="RoqueApp.cs" company="">
// TODO: Update copyright text.
// </copyright>
// -----------------------------------------------------------------------

using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using CLAP;
using CLAP.Validation;
using Cinchcast.Roque.Common;
using Cinchcast.Roque.Core;
using Cinchcast.Roque.Core.Configuration;

namespace Cinchcast.Roque.Service
{
    using System;
    using System.Linq;

    /// <summary>
    /// Roque console app
    /// </summary>
    public class RoqueApp
    {
        public class MyClass : INotifyPropertyChanged
        {

            public event PropertyChangedEventHandler PropertyChanged;
            private string _Name;

            public string Name
            {
                get { return _Name; }
                set
                {
                    _Name = value;
                    var handler = PropertyChanged;
                    if (handler != null)
                    {
                        handler(this, new PropertyChangedEventArgs("Name"));
                    }
                }
            }
        }

        [Verb(Description = "Run all workers in config")]
        private static void Work([CLAP.Description("worker to start, or none to start all")]string worker)
        {
            var host = new WorkerHost();
            host.Start(worker);
            Console.WriteLine();
            Console.WriteLine("PRESS ANY KEY TO STOP");
            Console.WriteLine();
            Console.ReadKey(true);
            host.Stop();
            Console.WriteLine("Goodbye!");
        }

        [Verb(Description = "enqueue test jobs on a queue")]
        private static void TestEnqueue([Required]string queue, [MoreThan(0)][LessThan(100001)] uint count = 10000, bool events = false)
        {
            Stopwatch stopwatch = new Stopwatch();
            if (events)
            {
                MyClass myObject = new MyClass();
                RoqueEventBroadcaster broadcaster = new RoqueEventBroadcaster(queue);
                broadcaster.SubscribeToAll<INotifyPropertyChanged>(myObject);
                stopwatch.Start();
                for (int i = 1; i <= count; i++)
                {
                    myObject.Name = "New value #" + i;
                }
            }
            else
            {
                var traceProxy = RoqueProxyGenerator.Create<ITrace>(Queue.Get(queue));
                stopwatch.Start();
                for (int i = 1; i <= count; i++)
                {
                    traceProxy.TraceVerbose("TEST MESSAGE #" + i);
                }
            }
            stopwatch.Stop();
            Console.WriteLine("{0} jobs enqueued", count);
            Console.WriteLine("Time elapsed: {0}", stopwatch.Elapsed);
        }

        [Verb(Description = "Take a look at queues current status")]
        private static void Status(
            [CLAP.Description("if provided and a longer queues is found exit with error code 1")]int maxLength = 0,
            [CLAP.Description("(seconds) if provided and an the next job in a queue is older exit with error code 1")] int maxAge = 0,
            [CLAP.Description("queues to check, or none to check all")]params string[] queues)
        {
            Queue[] queuesToCheck;
            if (queues == null || queues.Length == 0)
            {
                queuesToCheck = Queue.All.ToArray();
            }
            else
            {
                queuesToCheck = queues.Select(name => Queue.Get(name)).ToArray();
            }

            int tooOldError = 0;
            int tooLongError = 0;
            foreach (var queue in queuesToCheck)
            {
                long length;
                Job job = queue.Peek(out length);
                if (job == null)
                {
                    DateTime? lastComplete = queue.GetTimeOfLastJobCompleted();
                    string lastCompleteString = "";
                    if (lastComplete != null)
                    {
                        lastCompleteString = ". Last job completed " + Job.AgeToString(DateTime.UtcNow.Subtract(lastComplete.Value)) + "ago";
                    }
                    Console.WriteLine(string.Format("Queue {0} is empty{1}", queue.Name, lastCompleteString));
                }
                else
                {
                    TimeSpan jobAge = DateTime.UtcNow.Subtract(job.CreationUtc);
                    bool tooOld = false;
                    bool tooLong = false;
                    if (maxAge > 0)
                    {
                        tooOld = jobAge.TotalSeconds > maxAge;
                        if (tooOld)
                        {
                            tooOldError++;
                        }
                    }
                    if (maxLength > 0)
                    {
                        tooLong = length > maxLength;
                        if (tooLong)
                        {
                            tooLongError++;
                        }
                    }
                    Console.WriteLine(string.Format("Queue {0} has {1} pending jobs. Next job was created {2}ago.{3}{4}",
                                                    queue.Name, length, job.GetAgeString(), tooOld ? " [TOO OLD]" : "",
                                                    tooLong ? " [TOO LONG]" : ""));
                }
            }

            if (tooOldError > 0)
            {
                Console.Error.WriteLine(string.Format("ERROR: {0} queue{1} have too old pending jobs", tooOldError,
                                                      tooOldError == 1 ? "" : "s"));
            }
            if (tooLongError > 0)
            {
                Console.Error.WriteLine(string.Format("ERROR: {0} queue{1} have too many pending jobs", tooLongError,
                                                      tooLongError == 1 ? "" : "s"));
            }
            if (tooOldError > 0 || tooLongError > 0)
            {
                Environment.Exit(1);
            }
        }

        [Verb(Description = "Take a look at event subscriptions")]
        private static void Events(
            [CLAP.Description("queues to check, or none to check all")]params string[] queues)
        {
            Queue[] queuesToCheck;
            if (queues == null || queues.Length == 0)
            {
                queuesToCheck = Queue.All.ToArray();
            }
            else
            {
                queuesToCheck = queues.Select(name => Queue.Get(name)).ToArray();
            }

            foreach (var queue in queuesToCheck)
            {
                IDictionary<string, string[]> subscribers = queue.GetSubscribers();

                Console.WriteLine(string.Format("Queue {0} has {1} event{2} with subscribers", queue.Name, subscribers.Count, subscribers.Count == 1 ? "" : "s"));
                foreach (var subscriber in subscribers)
                {
                    Console.WriteLine(string.Format("   {0} is observed by {1}", subscriber.Key, string.Join(", ", subscriber.Value)));
                }
            }
        }

        [Empty, Help]
        public static void Help(string help)
        {
            // this is an empty handler that prints
            // the automatic help string to the console.
            Console.WriteLine(help);
        }
    }
}
