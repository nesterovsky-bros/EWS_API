using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.ServiceModel;
using System.ServiceProcess;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Bnhp.Office365
{
  /// <summary>
  /// Windows service that executes rules engine 
  /// that handles mailboxes changes.
  /// </summary>
  partial class RulesEngineService : ServiceBase
  {
    /// <summary>
    /// The main entry point for the process.
    /// </summary>
    /// <param name="args">command line arguments, if any.</param>
    /// <remarks>
    /// Start the application with "-cmd" command line argument in order 
    /// to run it in console mode.
    /// </remarks>
    public static void Main(string[] args)
    {
      try
      {
        var IsConsoleApp = false;

        if (args != null)
        {
          for (int i = 0; i < args.Length; i++)
          {
            if ("-cmd" == args[i].ToLower())
            {
              var found = false;

              foreach (TraceListener listener in Trace.Listeners)
              {
                if (listener is ConsoleTraceListener)
                {
                  found = true;

                  break;
                }
              }

              if (!found)
              {
                Trace.Listeners.Add(
                  new TextWriterTraceListener(Console.Out, "Console"));
              }

              IsConsoleApp = true;

              break;
            }
            else if ("-svc" == args[i].ToLower())
            {
              break;
            }
            // continue otherwise
          }
        }

        var service = new RulesEngineService();

        if (IsConsoleApp)
        {
          // start as console application
          service.Mode = "console";

          service.OnStart(null);

          Console.WriteLine("Press enter to stop the server.");
          Console.ReadLine();

          service.OnStop();
        }
        else
        {
          // starts as Windows service
          ServiceBase.Run(service);
        }
      }
      catch (Exception e)
      {
        Trace.TraceError(e.ToString());

        throw e;
      }
    }

    #region Windows service artifacts
    /// <summary>
    /// Creates an instance of this Windows service.
    /// </summary>
    public RulesEngineService()
    {
      InitializeComponent();

      cancellationTokenSource = new CancellationTokenSource();

      WaitPeriod = int.TryParse(
        ConfigurationManager.AppSettings["WaitPeriod"], 
        out WaitPeriod) ?
          WaitPeriod * 1000 : 30000;
      
      SystemNames =
        (ConfigurationManager.AppSettings["SystemNames"] ?? "").Split(' ');

      Mode = (ConfigurationManager.AppSettings["Mode"] ?? "service").ToLower();
    }

    /// <summary>
    /// Starts this Windows service instance.
    /// </summary>
    /// <param name="args">command line arguments.</param>
    protected override async void OnStart(string[] args)
    {
      if (Mode == "wcf")
      {
        if (serviceHost != null)
        {
          serviceHost.Close();
        }

        // Create a ServiceHost for the WcfRulesEngine type and 
        // provide the base address.
        serviceHost = new ServiceHost(typeof(WcfRulesEngine));

        // Open the ServiceHostBase to create listeners and start 
        // listening for messages.
        serviceHost.Open();
      }
      else // run in "service" mode
      {
        var cancelationToken = cancellationTokenSource.Token;

        try
        {
          while (true)
          {
            foreach (var system in SystemNames)
            {
              await RulesEngine.ExecuteAsync(system, cancelationToken);
            }

            await Task.Delay(WaitPeriod, cancelationToken);
          }
        }
        catch (OperationCanceledException)
        {
          // exit gracefully from the Windows service
        }
        catch (Exception e)
        {
          Trace.TraceError(e.ToString());

          throw;
        }
      }
    }

    /// <summary>
    /// Stops this Windows service instance.
    /// </summary>
    protected override void OnStop()
    {
      if (cancellationTokenSource != null)
      {
        cancellationTokenSource.Cancel();
      }
      
      if (serviceHost != null)
      {
        serviceHost.Close();

        serviceHost = null;
      }
    }
    #endregion

    #region Private fields
    // a WCF service host
    private ServiceHost serviceHost;

    // A wait period in milliseconds between next loops of change requests.
    private int WaitPeriod;

    // An array of system names to process.
    private string[] SystemNames;

    // The service mode: "wcf", "service" or "console".
    private string Mode;

    // A cancelation token source for canceling of rules engines' tasks.
    private CancellationTokenSource cancellationTokenSource;
    #endregion
  }
}
