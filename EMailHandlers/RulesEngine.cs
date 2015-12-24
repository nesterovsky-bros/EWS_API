using System;
using System.Net;
using System.Security;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Configuration;
using System.Diagnostics;
using System.Text.RegularExpressions;

using Bnhp.Office365.EwsServiceReference;
using Bnhp.Office365.RulesServiceReference;
using Bnhp.Office365.Configuration;
using System.Threading;

namespace Bnhp.Office365
{
  /// <summary>
  /// A EWS API client that handles mailboxes changes.
  /// </summary>
  public class RulesEngine
  {
    /// <summary>
    /// Executes asynchronously a process of handling Inbox changes 
    /// for the specified system. 
    /// </summary>
    /// <param name="systemName">a system name, which changes to process.</param>
    /// <param name="token">
    /// a CancellationToken instance in case when one need to abort 
    /// this asynchronous method call.
    /// </param>
    public static async Task ExecuteAsync(
      string systemName, 
      CancellationToken token = default(CancellationToken))
    {
      var mailbox = "";
      var action = "";

      try
      {
        // retrieves the latest executed request for the specified system
        var rulesService = new RulesServiceClient();
        var timestamp = await rulesService.GetLastCheckAsync(systemName);

        token.ThrowIfCancellationRequested();

        // retrieves latest changes for this system
        var ewsService = new EwsServiceClient();
        var changes = await ewsService.GetChangesAsync(
          systemName,
          null,
          "Inbox",
          timestamp,
          null,
          null,
          null);

        token.ThrowIfCancellationRequested();

        // next timestamp
        timestamp = DateTime.Now;

        foreach (var change in changes)
        {
          if (change.ChangeType != ChangeType.Created)
          {
            continue;
          }

          mailbox = change.Email;
          action = "";
          
          // gets rules for this system and mailbox where were changes
          var rules = await rulesService.GetRulesAsync(systemName, mailbox);

          if (rules == null)
          {
            Trace.TraceWarning(
              "There is no rules for the mailbox: \"" + mailbox + 
              "\", system: \"" + systemName + "\".");

            // skip it
            continue;
          }

          // executes action handlers for the current mailbox
          foreach (var rule in rules)
          {
            token.ThrowIfCancellationRequested();

            action = rule.Action;
            
            var handler = GetHandler(action);

            if (handler == null)
            {
              Trace.TraceWarning(
                "There is no handler for action \"" + action + 
                "\", mailbox: \"" + mailbox + 
                "\", system: \"" + systemName + "\".");

              // skip it
              continue;
            }

            var message = await ewsService.GetMessageAsync(mailbox, change.ItemID);

            token.ThrowIfCancellationRequested();

            if (!await handler.Handle(ewsService, message, mailbox, rule.Params))
            {
              Trace.TraceError(
                "Handler for action \"" + action + "\" fails.\n" +
                "Mailbox: \"" + mailbox + "\", system: \"" + 
                "\", params: " + rule.Params);
            }
          }
        }

        if (changes.Length > 0)
        {
          // sets a new value of last request time
          await rulesService.UpdateLastCheckAsync(systemName, timestamp.Value);
        }
      }
      catch (Exception e)
      {
        Trace.TraceError(
          "E-mail handling fails" + 
          (string.IsNullOrEmpty(action) ? "" : (" for action \"" + action + "\"")) +
          (string.IsNullOrEmpty(mailbox) ? "" : (" for mailbox \"" + mailbox + "\"")) +
          " for system: \"" + systemName + "\". Stack trace:\n" + e.ToString());
      }
    }

    /// <summary>
    /// Retrieves an e-mail handler by action name.
    /// </summary>
    /// <param name="actionName">an action name to handle.</param>
    /// <returns>an instance of a corresponding IEMailHandler or null.</returns>
    /// <remarks>
    /// this method reads once a list of supported actions from configuration file 
    /// and creates a static dictionary for the next requests.
    /// </remarks>
    public static IEMailHandler GetHandler(string actionName)
    {
      var handler = null as IEMailHandler;

      if (handlers == null)
      {
        var tmpHandlers = new Dictionary<string, IEMailHandler>();
        var config =
          ConfigurationManager.GetSection("emailHandlers") as
          HandlersConfigurationSection;

        if ((config != null) && (config.Handlers != null))
        {
          for (int i = 0, c = config.Handlers.Count; i < c; i++)
          {
            var item = config.Handlers[i];

            handler =
              Activator.CreateInstance(item.Handler) as IEMailHandler;

            if (!string.IsNullOrEmpty(item.Action) && (handler != null))
            {
              tmpHandlers.Add(item.Action, handler);
            }
          }
        }

        handlers = tmpHandlers;
      }

      return (handlers != null) && 
        handlers.TryGetValue(actionName, out handler) ? handler : null;
    }

    /// <summary>
    /// Pairs action name, an e-mail handler for all supported actions.
    /// </summary>
    private static Dictionary<string, IEMailHandler> handlers;
  }
}
