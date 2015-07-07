namespace Bnhp.Office365
{
  public class Settings
  {
    public string ExchangeUserName { get; set; }
    public string ExchangePassword { get; set; }
    public double RequestTimeout { get; set; }
    public string AutoDiscoveryUrl { get; set; }
    public int AttemptsToDiscoverUrl { get; set; }
    public int ExchangeConnectionLimit { get; set; }
    public int ExchangeListenerRecyclePeriod { get; set; }
  }
}