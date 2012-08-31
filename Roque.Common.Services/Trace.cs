// -----------------------------------------------------------------------
// <copyright file="Trace.cs" company="">
// TODO: Update copyright text.
// </copyright>
// -----------------------------------------------------------------------

using Cinchcast.Roque.Core;

namespace Cinchcast.Roque.Common
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;

    /// <summary>
    /// TODO: Update summary.
    /// </summary>
    public class Trace : ITrace
    {

        public void TraceVerbose(string format, params object[] arguments)
        {
            if (Roque.Core.RoqueTrace.Switch.TraceVerbose)
            {
                System.Diagnostics.Trace.TraceInformation(format);
            }
        }

        public void TraceInformation(string format, params object[] arguments)
        {
            if (Roque.Core.RoqueTrace.Switch.TraceInfo)
            {
                System.Diagnostics.Trace.TraceInformation(format);
            }
        }

        public void TraceError(string format, params object[] arguments)
        {
            if (Roque.Core.RoqueTrace.Switch.TraceError)
            {
                System.Diagnostics.Trace.TraceInformation(format, arguments);
            }
        }

        public void TraceWarning(string format, params object[] arguments)
        {
            if (Roque.Core.RoqueTrace.Switch.TraceWarning)
            {
                System.Diagnostics.Trace.TraceInformation(format, arguments);
            }
        }
    }
}
