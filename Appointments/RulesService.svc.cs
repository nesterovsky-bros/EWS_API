using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.ServiceModel;
using System.Text;
using Microsoft.Practices.Unity;

namespace Bnhp.Office365
{
  public class RulesService : IRulesService
  {
    /// <summary>
    /// A settings instance.
    /// </summary>
    [Dependency]
    public Settings settings { get; set; }

    #region IRulesService Members
    /// <summary>
    /// Retrieves a collection of rules for the specified mailbox. 
    /// </summary>
    /// <param name="mailbox">a mailbox address.</param>
    /// <returns>a collection of Rule instances or null.</returns>
    public IEnumerable<Rule> GetRules(string mailbox)
    {
      using (var model = new EWSQueueEntities())
      {
        return model.Rules.Where(rule => rule.Email == mailbox);
      }
    }

    /// <summary>
    /// Retrieve date and time when there was last change state check.
    /// </summary>
    /// <returns>
    /// A date and time of the latest change state check or null.
    /// </returns>
    public DateTime? GetLastCheck()
    {
      using (var model = new EWSQueueEntities())
      {
        return model.ChangeStateRequests.
          Where(
            request =>
              (request.ApplicationId == settings.RulesEngineApplicationId) &&
              (request.GroupName == settings.RulesEngineGroupName)).
          Select(request => request.LastCheck).
          FirstOrDefault();
      }
    }

    /// <summary>
    /// Update date and time of the latest change state check.
    /// </summary>
    /// <param name="timestamp">
    /// A new date and time of the latest change state check.
    /// </param>
    public void UpdateLastCheck(DateTime timestamp)
    {
      using (var model = new EWSQueueEntities())
      {
        var changeStateRequest = model.ChangeStateRequests.
          Where(
            request =>
              (request.ApplicationId == settings.RulesEngineApplicationId) &&
              (request.GroupName == settings.RulesEngineGroupName)).
          FirstOrDefault();

        if (changeStateRequest == null)
        {
          changeStateRequest = new ChangeStateRequest
          {
            ApplicationId = settings.RulesEngineApplicationId,
            GroupName = settings.RulesEngineGroupName,
            LastCheck = timestamp
          };

          model.ChangeStateRequests.Add(changeStateRequest);
        }
        else
        {
          changeStateRequest.LastCheck = timestamp;
        }

        model.SaveChanges();
      }
    }

    /// <summary>
    /// Retrieves the system name.
    /// </summary>
    /// <returns>a system name.</returns>
    public string GetSystemName()
    {
      return settings.RulesEngineGroupName;
    }
    #endregion
  }
}
