namespace Bnhp.Office365
{
  using System;
  using System.Linq;
  using System.Collections.Generic;
  using System.Runtime.Serialization;
  using System.Threading.Tasks;
  using Microsoft.Practices.Unity;
  using Microsoft.Exchange.WebServices.Autodiscover;

  using Office365 = Microsoft.Exchange.WebServices.Data;
  using System.Threading;
  using System.Diagnostics;
  
  /// <summary>
  /// Auto discovery API
  /// </summary>
  public class AutoDiscovery
  {
    /// <summary>
    /// Gets user settings using AutoDiscovery service.
    /// </summary>
    /// <param name="autoDiscoveryUrl">
    /// Address of the AutoDiscovery service.
    /// </param>
    /// <param name="serviceUserName">
    /// User name to connect AutoDiscovery service.
    /// </param>
    /// <param name="servicePassword">
    /// User password to connect AutoDiscovery service.
    /// </param>
    /// <param name="maxHops">
    /// Number of attempts to perform auto discovery.
    /// </param>
    /// <param name="email">Email to get user settings for.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>User settings for an email.</returns>
    public static Task<GetUserSettingsResponse> GetUserSettings(
      string autoDiscoveryUrl,
      string serviceUserName,
      string servicePassword,
      int maxHops,
      string email,
      CancellationToken cancellationToken = default(CancellationToken))
    {
      var autodiscoverService = new AutodiscoverService();

      autodiscoverService.Url = new Uri(autoDiscoveryUrl);
      autodiscoverService.Credentials =
        new Office365.WebCredentials(serviceUserName, servicePassword);

      return GetUserSettings(
        autodiscoverService,
        email,
        maxHops,
        new []
        {
          UserSettingName.GroupingInformation,
          UserSettingName.ExternalEwsUrl
        },
        cancellationToken);
    }

    /// <summary>
    /// Gets user settings using AutoDiscovery service.
    /// </summary>
    /// <param name="service">An instance of the AutoDiscovery service.</param>
    /// <param name="email">Email to get user settings for.</param>
    /// <param name="maxHops">
    /// Number of attempts to perform auto discovery.
    /// </param>
    /// <param name="settings">Settings to request.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>User settings for an email.</returns>
    public static async Task<GetUserSettingsResponse> GetUserSettings(
      AutodiscoverService service,
      string email,
      int maxHops,
      UserSettingName[] settings,
      CancellationToken cancellationToken = default(CancellationToken))
    {
      if (maxHops <= 1)
      {
        maxHops = 1;
      }

      Uri url = null;
      GetUserSettingsResponse response = null;
      Exception error = null;
      var wait = false;

      await Task.Yield();

      for(int attempt = 0; attempt < maxHops; attempt++)
      {
        cancellationToken.ThrowIfCancellationRequested();

        service.Url = url;
        service.EnableScpLookup = (attempt < 2);

        if (wait)
        {
          wait = false;

          await Task.Delay(30000, cancellationToken);
        }

        try
        {
          response = service.GetUserSettings(email, settings);
        }
        catch(AutodiscoverResponseException e)
        {
          error = e;

          if (e.ErrorCode == AutodiscoverErrorCode.ServerBusy)
          {
            Trace.TraceWarning(
              "Server is busy to autodiscover: {0}. {1}", 
              email, 
              e);

            // The server is too busy to process the request waiting 30sec.
            wait = true;

            //try again until we get an answer!!!
            continue;
          }

          throw;
        }
          
        if (response.ErrorCode == AutodiscoverErrorCode.RedirectAddress)
        {
          url = new Uri(response.RedirectTarget);
        }
        else if (response.ErrorCode == AutodiscoverErrorCode.RedirectUrl)
        {
          url = new Uri(response.RedirectTarget);
        }
        else
        {
          if (response.ErrorCode == AutodiscoverErrorCode.InvalidUser)
          {
            throw new ApplicationException(
              "Mailbox " + email + "was not found.");
          }

          return response;
        }
      }

      throw error ?? 
        new ApplicationException(
          "No suitable Autodiscover endpoint was found.");
    }
  }
}