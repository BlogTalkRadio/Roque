// -----------------------------------------------------------------------
// <copyright file="Trace.cs" company="">
// TODO: Update copyright text.
// </copyright>
// -----------------------------------------------------------------------

namespace Cinchcast.Roque.Common
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;

    /// <summary>
    /// TODO: Update summary.
    /// </summary>
    public interface ITrace
    {
        void TraceVerbose(string format, params object[] arguments);
        void TraceInformation(string format, params object[] arguments);
        void TraceError(string format, params object[] arguments);
        void TraceWarning(string format, params object[] arguments);
    }
}
