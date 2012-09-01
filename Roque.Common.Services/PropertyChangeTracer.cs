// -----------------------------------------------------------------------
// <copyright file="MyEventLogger.cs" company="">
// TODO: Update copyright text.
// </copyright>
// -----------------------------------------------------------------------

using System.ComponentModel;

namespace Cinchcast.Roque.Common
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;

    /// <summary>
    /// Subscriber class example. All methods starting with "Subscribe" will be called on initialization.
    /// All events you subscribe to in Subscribe* methods will be routed to the queue you attach this class to. 
    /// </summary>
    public class PropertyChangeTracer
    {
        public void SubscribeToPropertyChanges(INotifyPropertyChanged source)
        {
            source.PropertyChanged += new PropertyChangedEventHandler(source_PropertyChanged);
        }

        void source_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            System.Diagnostics.Trace.TraceInformation(string.Format("Property '{0}' changed", e.PropertyName));
        }
    }
}
