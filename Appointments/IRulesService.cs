using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.ServiceModel;
using System.Text;

namespace Bnhp.Office365
{
  /// <summary>
  /// An interface for a change state listener for mailboxes.
  /// </summary>
  [ServiceContract]
  public interface IRulesService
  {
    /// <summary>
    /// Retrieves a collection of rules for the specified mailbox. 
    /// </summary>
    /// <param name="systemName">a name of system to get rules.</param>
    /// <param name="mailbox">a mailbox address.</param>
    /// <returns>a collection of Rule instances or null.</returns>
    [OperationContract]
    IEnumerable<Rule> GetRules(string systemName, string mailbox);

    /// <summary>
    /// Retrieve date and time when there was last change state check.
    /// </summary>
    /// <param name="systemName">a name of system to check.</param>
    /// <returns>
    /// A date and time of the latest change state check or null.
    /// </returns>
    [OperationContract]
    DateTime? GetLastCheck(string systemName);

    /// <summary>
    /// Update date and time of the latest change state check.
    /// </summary>
    /// <param name="systemName">a name of system to update.</param>
    /// <param name="timestamp">
    /// A new date and time of the latest change state check.
    /// </param>
    [OperationContract]
    void UpdateLastCheck(string systemName, DateTime timestamp);
  }
}
