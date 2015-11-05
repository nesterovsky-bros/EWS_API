using System;
using System.Collections.Generic;
using System.Threading;
using Microsoft.Exchange.WebServices.Data;

namespace Bnhp.Office365
{
  public class Settings
  {
    public ApplicationUser[] ApplicationUsers { get; set; }
    public int HangingConnectionLimit { get; set; }
    public int EWSMaxConcurrency { get; set; }
    public double RequestTimeout { get; set; }
    public string AutoDiscoveryUrl { get; set; }
    public int UsersPerUsersSettins { get; set; }
    public int ExchangeListenerRecyclePeriod { get; set; }
    public int RetryCount { get; set; }
    public bool EWSTrace { get; set; }
    public Dictionary<string, ExtendedPropertyDefinition> ExtendedPropertyDefinitions
    {
      get; set;
    }

    /// <summary>
    /// Unique extended properties set ID.
    /// </summary>
    public static Guid ExtendedPropertySetId =
      new Guid("{DB04D3EE-8160-45EE-8A77-145D8A042231}");
  }

}