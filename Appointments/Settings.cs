using System.Threading;

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
  }
}