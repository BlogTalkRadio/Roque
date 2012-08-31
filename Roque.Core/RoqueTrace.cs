// -----------------------------------------------------------------------
// <copyright file="Trace.cs" company="">
// TODO: Update copyright text.
// </copyright>
// -----------------------------------------------------------------------

using System.Diagnostics;

namespace Cinchcast.Roque.Core
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;

    /// <summary>
    /// Roque Trace Switch, configure using:
    /// 
    /// 
    ///   <system.diagnostics>
    ///       <switches>
    ///         <add name="Roque" value="Off|Verbose|Info|Warning|Error" />
    ///       </switches>
    ///   </system.diagnostics>
    /// 
    /// </summary>
    public static class RoqueTrace
    {
        public static TraceSwitch Switch = new TraceSwitch("roque", "All Roque events");
    }
}
