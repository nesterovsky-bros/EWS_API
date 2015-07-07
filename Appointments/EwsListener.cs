namespace Bnhp.Office365
{
  using System;
  using System.Linq;
  using System.Collections.Generic;
  using System.Data.Entity;
  using System.Runtime.Serialization;
  using System.Threading.Tasks;
  using Microsoft.Practices.Unity;
  using Microsoft.Exchange.WebServices.Autodiscover;
  using System.Net;
  using System.Threading;

  /// <summary>
  /// A EWS listener.
  /// </summary>
  public class EwsListener: IDisposable
  {
    /// <summary>
    /// A settings instance.
    /// </summary>
    [Dependency]
    public Settings Settings { get; set; }

    /// <summary>
    /// A service instance.
    /// </summary>
    [Dependency]
    public IAppointments Service { get; set; }

    /// <summary>
    /// Starts the listener.
    /// </summary>
    public async Task Start()
    {
      var cancellationSource = new CancellationTokenSource(
        TimeSpan.FromMinutes(Settings.ExchangeListenerRecyclePeriod));

      cancellationToken = cancellationSource.Token;
    
      //using(var model = new EWSQueueEntities())
      //{
      //  await model.BankMailboxes.
      //    Where(mailbox => !mailbox.Invalid).
      //    ToDictionaryAsync(mailbox => mailbox.mailAddress, cancellationToken);
      //}

      //await Task.WhenAll(
      //  mailboxes.Values.Select(mailbox => SetupMailbox(mailbox)));
    }

    /// <summary>
    /// Stops the listener.
    /// </summary>
    public void Stop()
    {
      // TODO: implement this.
    }

    /// <summary>
    /// Disposes the listener.
    /// </summary>
    public void Dispose()
    {
      Stop();
    }

    /// <summary>
    /// Setups a BankMailbox instance.
    /// </summary>
    /// <param name="mailbox">A mailbox instance.</param>
    /// <returns>A BankMailbox instance.</returns>
    public async Task<BankMailbox> SetupMailbox(BankMailbox mailbox)
    {
      if ((mailbox.ewsUrl == null) || (mailbox.groupingInformation == null))
      {
        GetUserSettingsResponse userInfo = null;


        try
        {
          userInfo = await AutoDiscovery.GetUserSettings(
            Settings.AutoDiscoveryUrl,
            Settings.ExchangeUserName,
            Settings.ExchangePassword,
            Settings.AttemptsToDiscoverUrl,
            mailbox.mailAddress,
            cancellationToken);
        }
        catch
        { 
          // Consider the user invalid.
          mailbox.Invalid = true;
        }

        using(var model = new EWSQueueEntities())
        {
          model.BankMailboxes.Attach(mailbox);

          if (userInfo != null)
          {
            mailbox.ewsUrl =
              userInfo.Settings[UserSettingName.ExternalEwsUrl] as string;
            mailbox.groupingInformation =
              userInfo.Settings[UserSettingName.GroupingInformation] as string;
          }

          await model.SaveChangesAsync(cancellationToken);
        }
      }

      return mailbox;
    }
 
    /// <summary>
    /// Cancellation source.
    /// </summary>
    private CancellationToken cancellationToken;
  }
}