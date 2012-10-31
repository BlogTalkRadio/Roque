// -----------------------------------------------------------------------
// <copyright file="RoqueApp.cs" company="">
// TODO: Update copyright text.
// </copyright>
// -----------------------------------------------------------------------

using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Management;
using System.Reflection;
using System.Runtime.InteropServices;
using System.ServiceProcess;
using CLAP;
using CLAP.Validation;
using Cinchcast.Roque.Common;
using Cinchcast.Roque.Core;
using Cinchcast.Roque.Core.Configuration;
using Cinchcast.Roque.Triggers;

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
            var triggerHost = new TriggerHost();
            triggerHost.Start();
            Console.WriteLine();
            Console.WriteLine("PRESS ANY KEY TO STOP");
            Console.WriteLine();
            Console.ReadKey(true);
            host.Stop();
            triggerHost.Stop();
            Console.WriteLine("Goodbye!");
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

        [Verb(Description = "Take a look at triggers")]
        private static void Triggers(
            [CLAP.Description("triggers to check, or none to check all")]params string[] triggers)
        {
            Trigger[] triggersToCheck;
            if (triggers == null || triggers.Length == 0)
            {
                triggersToCheck = Trigger.All.Triggers;
            }
            else
            {
                triggersToCheck = Trigger.All.Triggers.Where(t => triggers.Contains(t.Name)).ToArray();
            }

            foreach (var trigger in triggersToCheck)
            {
                Console.WriteLine(string.Format("Trigger {0}", trigger.Name));
                Console.WriteLine(string.Format("    Type: {0}", trigger.GetType().Name));
                Console.WriteLine(string.Format("    Queue: {0}", trigger.Queue.Name));

                Job job = trigger.JobCreator();

                Console.WriteLine(string.Format("    Target: {0}", job.Target));
                Console.WriteLine(string.Format("    Method: {0}", job.Method));
                foreach (var argument in job.Arguments)
                {
                    Console.WriteLine(string.Format("    Argument: {0}", argument));
                }

                DateTime? lastExecution = trigger.GetLastExecution();
                DateTime? nextExecution = trigger.GetNextExecution();

                Console.WriteLine(string.Format("    Last Execution: {0}", lastExecution == null ? "unknown" : lastExecution.ToString() + " GMT"));
                if (nextExecution != null)
                {
                    Console.WriteLine(string.Format("      ({0}ago)", Job.AgeToString(DateTime.UtcNow.Subtract(lastExecution.Value))));
                }
                Console.WriteLine(string.Format("    Next Execution: {0}", nextExecution == null ? "unknown" : nextExecution.ToString() + " GMT"));
                if (nextExecution != null)
                {
                    if (nextExecution <= DateTime.UtcNow)
                    {
                        Console.WriteLine(string.Format("      Should run soon. ({0}overdue)", Job.AgeToString(DateTime.UtcNow.Subtract(nextExecution.Value))));
                    }
                    else
                    {
                        Console.WriteLine(string.Format("      (in {0})", Job.AgeToString(nextExecution.Value.Subtract(DateTime.UtcNow))));
                    }
                }
            }
        }

        [Verb(Description = "Copy roque binaries to another folder")]
        private static void CopyBinaries([CLAP.Description("target folder, by default is current folder")]string path,
            [CLAP.Description("overwrite files, even if target is up to date")] bool force = false,
            [CLAP.Description("in silent mode only changes are printed")] bool silent = false)
        {
            var sourceDir = new DirectoryInfo(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location));

            string targetPath = path;
            if (string.IsNullOrWhiteSpace(targetPath))
            {
                targetPath = ".";
            }
            targetPath = Path.GetFullPath(targetPath);
            if (!silent)
            {
                Console.WriteLine("Copying binaries to: " + targetPath);
            }
            var targetDir = new DirectoryInfo(targetPath);

            try
            {
                CopyBinaryFiles(sourceDir, targetDir, force, silent);
            }
            catch (IOException ex)
            {
                if (IsSharingViolation(ex))
                {
                    Console.WriteLine("[ERROR] " + ex.Message);
                    string targetExe = Path.Combine(targetPath, "roque.exe").ToLowerInvariant();
                    var service = ServiceController.GetServices().FirstOrDefault(svc => svc.Status != ServiceControllerStatus.Stopped && GetPathOfService(svc.ServiceName).ToLowerInvariant() == targetExe);
                    if (service != null)
                    {
                        Console.WriteLine("Roque binaries on this folder are running as service " + service.ServiceName);
                        Console.WriteLine("Stopping to update...");
                        service.Stop();
                        Console.WriteLine("Stopped");
                        CopyBinaryFiles(sourceDir, targetDir, force, silent);
                        Console.WriteLine("Binaries updated, Starting...");
                        service.Start();
                        Console.WriteLine("Started");
                    }
                    else
                    {
                        Console.WriteLine("Check that roque is stopped and retry");
                    }
                }
                else
                {
                    throw;
                }
            }
        }

        [Verb(Description = "enqueue test jobs on a queue")]
        private static void DoTestEnqueue([Required]string queue, [MoreThan(0)][LessThan(100001)] uint count = 10000, bool events = false)
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

        private static void CopyBinaryFiles(DirectoryInfo sourceDir, DirectoryInfo targetDir, bool force = false, bool silent = false)
        {
            foreach (var file in sourceDir.GetFiles())
            {
                string extension = file.Extension.ToLowerInvariant();
                if (!extension.EndsWith(".config") && !extension.EndsWith("log") && !extension.EndsWith(".transform"))
                {
                    string targetFile = Path.Combine(targetDir.FullName, file.Name);
                    if (!File.Exists(targetFile))
                    {
                        // new file, just copy
                        file.CopyTo(targetFile, true);
                        Console.WriteLine("  [CREATED] " + file.Name);
                    }
                    else
                    {
                        if (force)
                        {
                            file.CopyTo(targetFile, true);
                            Console.WriteLine("  [COPIED] " + file.Name);
                        }
                        else
                        {

                            FileVersionInfo sourceVersion = FileVersionInfo.GetVersionInfo(file.FullName);
                            FileVersionInfo targetVersion = FileVersionInfo.GetVersionInfo(targetFile);
                            if (string.IsNullOrEmpty(sourceVersion.ProductVersion) || string.IsNullOrEmpty(targetVersion.ProductVersion))
                            {
                                // non-versioned file, check for modification date
                                if (file.LastWriteTimeUtc != File.GetLastWriteTimeUtc(targetFile))
                                {
                                    file.CopyTo(targetFile, true);
                                    Console.WriteLine("  [UPDATED] " + file.Name);
                                }
                                else
                                {
                                    if (!silent)
                                    {
                                        Console.WriteLine("  [UP TO DATE] " + file.Name);
                                    }
                                }
                            }
                            else
                            {
                                if (sourceVersion.ProductVersion != targetVersion.ProductVersion)
                                {
                                    // different versions, overwrite
                                    file.CopyTo(targetFile, true);
                                    Console.WriteLine("  [UPDATED] " + file.Name + " " + targetVersion.ProductVersion + " => " + sourceVersion.ProductVersion);
                                }
                                else
                                {
                                    // updated, ignore
                                    if (!silent)
                                    {
                                        Console.WriteLine("  [UP TO DATE] " + file.Name + " " + targetVersion.ProductVersion);
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        private static bool IsSharingViolation(IOException ex)
        {
            return -2147024864 == Marshal.GetHRForException(ex);
        }

        public static string GetPathOfService(string serviceName)
        {
            WqlObjectQuery wqlObjectQuery = new WqlObjectQuery(string.Format("SELECT * FROM Win32_Service WHERE Name = '{0}'", serviceName));
            ManagementObjectSearcher managementObjectSearcher = new ManagementObjectSearcher(wqlObjectQuery);
            ManagementObjectCollection managementObjectCollection = managementObjectSearcher.Get();

            foreach (ManagementObject managementObject in managementObjectCollection)
            {
                return managementObject.GetPropertyValue("PathName").ToString();
            }

            return null;
        }

        [Empty, Help]
        public static void Help(string help)
        {
            // this is an empty handler that prints
            // the automatic help string to the console.
            var name = Assembly.GetAssembly(typeof(RoqueApp)).GetName();
            Console.WriteLine(string.Format("Roque v{0}", name.Version));
            Console.WriteLine(help);
        }
    }
}
