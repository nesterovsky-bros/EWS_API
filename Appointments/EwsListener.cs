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
    /// Disposes the listener.
    /// </summary>
    public void Dispose()
    {
      // TODO: dispose the listener.
    }

    /// <summary>
    /// Gets BankMailbox for the email adress.
    /// </summary>
    /// <param name="emailAddress">An email address.</param>
    /// <returns>A BankMailbox instance.</returns>
    public async Task<BankMailbox> GetBankMailbox(string emailAddress)
    {
      var mailbox = null as BankMailbox;

      using(var model = new EWSQueueEntities())
      {
        mailbox = await model.BankMailboxes.
          Where(item => item.mailAddress == emailAddress).
          FirstOrDefaultAsync();
      }

      if ((mailbox == null) || 
        (mailbox.ewsUrl == null) || 
        (mailbox.groupingInformation == null))
      {
        var userInfo = await AutoDiscovery.GetUserSettings(
          Settings.AutoDiscoveryUrl,
          Settings.ExchangeUserName,
          Settings.ExchangePassword,
          Settings.AttemptsToDiscoverUrl,
          emailAddress);

        using(var model = new EWSQueueEntities())
        {
          if (mailbox == null)
          {
            mailbox = model.BankMailboxes.Create();
            mailbox.mailAddress = emailAddress;
            mailbox.notifyOnNewAppointments = true;
            mailbox.notifyOnNewMails = true;

            model.BankMailboxes.Add(mailbox);
          }
          else
          {
            model.BankMailboxes.Attach(mailbox);
          }

          mailbox.ewsUrl =
            userInfo.Settings[UserSettingName.ExternalEwsUrl] as string;
          mailbox.groupingInformation =
            userInfo.Settings[UserSettingName.GroupingInformation] as string;

          await model.SaveChangesAsync();
        }
      }

      return mailbox;
    }
  }
}