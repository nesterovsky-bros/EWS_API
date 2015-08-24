using System;
using System.Net;
using System.Security;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Configuration;

using Bnhp.Office365.EwsServiceReference;
using Bnhp.Office365.RulesServiceReference;
using Bnhp.Office365.Configuration;
using System.Diagnostics;

namespace Bnhp.Office365
{
  public class RulesEngine
  {
    /// <summary>
    /// Retrieves an e-mail handler by action actionName.
    /// </summary>
    /// <param actionName="actionName">the action name to handle.</param>
    /// <returns>an instance of a corresponding IEMailHandler or null.</returns>
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

            if (string.IsNullOrEmpty(item.Action) && (handler != null))
            {
              tmpHandlers.Add(item.Action, handler);
            }
          }
        }

        handlers = tmpHandlers;
      }

      return handlers.TryGetValue(actionName, out handler) ? handler : null;
    }

    /// <summary>
    /// Processes a changed e-mail message. 
    /// </summary>
    /// <param actionName="client">an EWS API client.</param>
    /// <param actionName="mailbox">a mailbox address.</param>
    /// <param actionName="messageID">an e-mail message ID to process.</param>
    public void Execute()
    {
      var rulesService = new RulesServiceClient();
      var timestamp = rulesService.GetLastCheck() ?? DateTime.Now;
      var systemName = rulesService.GetSystemName();

      var ewsService = new EwsServiceClient();
      var changes = ewsService.GetChanges(
        systemName, 
        null, 
        "Inbox", 
        timestamp, 
        null, 
        null, 
        null);

      foreach (var change in changes)
      {
        var mailbox = change.Email;
        var rules = rulesService.GetRules(mailbox);

        if (rules == null)
        {
          Trace.TraceWarning("There is no rulse for the mailbox \"" + mailbox + "\".");

          // skip it
          continue;
        }

        foreach (var rule in rules)
        {
          var handler = GetHandler(rule.RuleName);

          if (handler == null)
          {
            Trace.TraceWarning("There is no handler for action \"" + rule.RuleName + "\".");
            
            // skip it
            continue;
          }

          var message = ewsService.GetMessage(mailbox, change.ItemID);

          if (!handler.Handle(ewsService, message, mailbox, rule.Params.Split(' ')))
          {
            Trace.TraceError(
              "Handler for action \"" + rule.RuleName + "\" fails.\n" +
              "Mailbox: " + mailbox + ", params: " + rule.Params);
          }
        }
      }
    }

    private static Dictionary<string, IEMailHandler> handlers;
  }
}
