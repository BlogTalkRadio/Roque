// -----------------------------------------------------------------------
// <copyright file="Trace.cs" company="">
// TODO: Update copyright text.
// </copyright>
// -----------------------------------------------------------------------

using System.Diagnostics;
using Cinchcast.Roque.Core;

namespace Cinchcast.Roque.Common
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;

    /// <summary>
    /// Work service implementation example
    /// </summary>
    public class Trace : ITrace
    {

        public void TraceVerbose(string format, params object[] arguments)
        {
            RoqueTrace.Source.Trace(TraceEventType.Verbose, format, arguments);
        }

        public void TraceInformation(string format, params object[] arguments)
        {
            RoqueTrace.Source.Trace(TraceEventType.Information, format, arguments);
        }

        public void TraceError(string format, params object[] arguments)
        {
            RoqueTrace.Source.Trace(TraceEventType.Error, format, arguments);
        }

        public void TraceWarning(string format, params object[] arguments)
        {
            RoqueTrace.Source.Trace(TraceEventType.Warning, format, arguments);
        }

        public void TracePing()
        {
            RoqueTrace.Source.Trace(TraceEventType.Information, "ping");
        }

        public void TraceInformationString(string message)
        {
            RoqueTrace.Source.Trace(TraceEventType.Information, message);
        }
    }
}
