﻿// -----------------------------------------------------------------------
// <copyright file="RoqueSettings.cs" company="">
// TODO: Update copyright text.
// </copyright>
// -----------------------------------------------------------------------

using System.Configuration;

namespace Cinchcast.Roque.Core.Configuration
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;

    /// <summary>
    /// Roque config section
    /// </summary>
    public class Roque : ConfigurationSection
    {
        private static Roque _Settings = ConfigurationManager.GetSection("roque") as Roque;

        public static Roque Settings
        {
            get
            {
                return _Settings;
            }
        }

        [ConfigurationProperty("defaultQueueType")]
        public string DefaultQueueType
        {
            get { return this["defaultQueueType"] as string; }
            set { this["defaultQueueType"] = value; }
        }

        [ConfigurationProperty("restartIfMemorySizeIsMoreThan")]
        public long RestartIfMemorySizeIsMoreThan
        {
            get { return (long)this["restartIfMemorySizeIsMoreThan"]; }
            set { this["restartIfMemorySizeIsMoreThan"] = value; }
        }

        [ConfigurationProperty("restartOnFileChanges", DefaultValue = true)]
        public bool RestartOnFileChanges
        {
            get { return (bool)this["restartOnFileChanges"]; }
            set { this["restartOnFileChanges"] = value; }
        }

        [ConfigurationProperty("signDynamicProxyModule", DefaultValue = false)]
        public bool SignDynamicProxyModule
        {
            get { return (bool)this["signDynamicProxyModule"]; }
            set { this["signDynamicProxyModule"] = value; }
        }

        [ConfigurationProperty("disableEventBroadcasting", DefaultValue = false)]
        public bool DisableEventBroadcasting
        {
            get { return (bool)this["disableEventBroadcasting"]; }
            set { this["disableEventBroadcasting"] = value; }
        }

        [ConfigurationProperty("queues")]
        public QueueCollection Queues
        {
            get
            {
                return this["queues"] as QueueCollection;
            }
        }

        [ConfigurationProperty("workers")]
        public WorkerCollection Workers
        {
            get
            {
                return this["workers"] as WorkerCollection;
            }
        }

        [ConfigurationProperty("subscribers")]
        public SubscriberCollection Subscribers
        {
            get
            {
                return this["subscribers"] as SubscriberCollection;
            }
        }
        
        [ConfigurationProperty("triggers")]
        public TriggerCollection Triggers
        {
            get
            {
                return this["triggers"] as TriggerCollection;
            }
        }
    }
}
