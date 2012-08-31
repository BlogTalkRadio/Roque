using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.ServiceProcess;
using System.Text;
using System.Threading;
using CLAP;
using Cinchcast.Roque.Common;
using Cinchcast.Roque.Core;
using Cinchcast.Roque.Core.Configuration;

namespace Cinchcast.Roque.Service
{
    static class Program
    {

        private static string[] args = Environment.GetCommandLineArgs().Skip(1).ToArray();

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        static void Main()
        {
            if (Environment.UserInteractive)
            {
                Parser.RunConsole<RoqueApp>(args);
            }
            else
            {
                ServiceBase[] ServicesToRun;
                ServicesToRun = new ServiceBase[] { new RoqueService() };
                ServiceBase.Run(ServicesToRun);
            }
        }

    }
}
