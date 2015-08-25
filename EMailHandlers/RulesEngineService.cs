using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.Data;
using System.Diagnostics;
using System.Linq;
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
        bool IsConsoleApp = false;

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
          }
        }

        var service = new RulesEngineService();

        if (IsConsoleApp)
        {
          // start as console application
          service.OnStart(null);

          Console.WriteLine("Press enter to stop the server.");
          Console.ReadLine();

          service.OnStop();
        }
        else
        {
          // start as Windows service
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

      WaitPeriod = int.TryParse(
        ConfigurationManager.AppSettings["WaitPeriod"], 
        out WaitPeriod) ?
          WaitPeriod * 1000 : 30000;
      
      SystemNames =
        (ConfigurationManager.AppSettings["SystemNames"] ?? "").Split(' ');
    }

    /// <summary>
    /// Starts this Windows service instance.
    /// </summary>
    /// <param name="args">command line arguments.</param>
    protected override async void OnStart(string[] args)
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

    /// <summary>
    /// Stops this Windows service instance.
    /// </summary>
    protected override void OnStop()
    {
      cancellationTokenSource.Cancel();
    }
    #endregion

    #region Private fields
    // A wait period in milliseconds between next loops of change requests.
    private int WaitPeriod;

    // An array of system names to process.
    private string[] SystemNames;

    // A cancelation token source for canceling of rules engines' tasks.
    private CancellationTokenSource cancellationTokenSource = 
      new CancellationTokenSource();
    #endregion
  }
}
