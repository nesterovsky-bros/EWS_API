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
    /// The application identifier.
    /// </summary>
    public static readonly string ApplicationId = typeof(RulesService).FullName;

    #region IRulesService Members
    /// <summary>
    /// Retrieves a collection of rules for the specified mailbox. 
    /// </summary>
    /// <param name="systemName">a system name (group name) to check.</param>
    /// <param name="mailbox">a mailbox address.</param>
    /// <returns>a collection of Rule instances or null.</returns>
    public IEnumerable<Rule> GetRules(string systemName, string mailbox)
    {
      using (var model = new EWSQueueEntities())
      {
        return model.Rules.Where(
          rule => (rule.GroupName == systemName) && (rule.Email == mailbox)).
            ToList();
      }
    }

    /// <summary>
    /// Retrieve date and time when there was last change state check.
    /// </summary>
    /// <param name="systemName">a name of system to check.</param>
    /// <returns>
    /// A date and time of the latest change state check or null.
    /// </returns>
    public DateTime? GetLastCheck(string systemName)
    {
      using (var model = new EWSQueueEntities())
      {
        return model.ChangeStateRequests.
          Where(
            request =>
              (request.ApplicationId == ApplicationId) &&
              (request.GroupName == systemName)).
          Select(request => request.LastCheck).
          FirstOrDefault();
      }
    }

    /// <summary>
    /// Update date and time of the latest change state check.
    /// </summary>
    /// <param name="systemName">a name of system to update.</param>
    /// <param name="timestamp">
    /// A new date and time of the latest change state check.
    /// </param>
    public void UpdateLastCheck(string systemName, DateTime timestamp)
    {
      using (var model = new EWSQueueEntities())
      {
        var changeStateRequest = model.ChangeStateRequests.
          Where(
            request =>
              (request.ApplicationId == ApplicationId) &&
              (request.GroupName == systemName)).
          FirstOrDefault();

        if (changeStateRequest == null)
        {
          changeStateRequest = new ChangeStateRequest
          {
            ApplicationId = ApplicationId,
            GroupName = systemName,
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
    #endregion
  }
}
