using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Linq;
using System.Runtime.Serialization;
using System.ServiceModel;
using System.Text;
using System.Threading.Tasks;

namespace Bnhp.Office365
{
  /// <summary>
  /// A  WCF service that wraps rules engine.
  /// </summary>
  public class WcfRulesEngine : IWcfRulesEngine
  {
    /// <summary>
    /// Executes rules engine WCF service.
    /// </summary>
    public void Execute()
    {
      try
      {
        var size = SystemNames.Length;
        var tasks = new Task[size];

        for (int i = 0; i < size; i++)
        {
          tasks[i] = RulesEngine.ExecuteAsync(SystemNames[i]);
        }

        Task.WaitAll(tasks);
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
    /// Gets an array of system names to process.
    /// </summary>
    protected static string[] SystemNames
    {
      get 
      {
        if (systemNames == null)
        {
          systemNames =
            (ConfigurationManager.AppSettings["SystemNames"] ?? "").Split(' ');
        }

        return systemNames;
      }
    }

    // An array of system names to process.
    private static string[] systemNames;
  }
}
