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

  using Office365 = Microsoft.Exchange.WebServices.Data;
  
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

      await DiscoverMailboxes();
      await SyncMailboxes();
      await ListenMailboxes();
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
    /// Discovers all mailboxes.
    /// </summary>
    /// <returns>A task that completes when all mail boxes are in sync.</returns>
    private async Task DiscoverMailboxes()
    {
      var flushSize = 100;
      var tasks = new List<Task<BankMailbox>>();

      Func<Task> flush = async () =>
      {
        var mailboxes = await Task.WhenAll(tasks);

        tasks.Clear();

        using(var model = new EWSQueueEntities())
        {
          foreach(var mailbox in mailboxes)
          {
            model.Entry(mailbox).State = EntityState.Modified;
          }

          await model.SaveChangesAsync(cancellationToken);
        }
      };

      using(var model = new EWSQueueEntities())
      {
        await model.BankMailboxes.AsNoTracking().
          Where(
            mailbox =>
              !mailbox.Invalid &&
              ((mailbox.ewsUrl == null) || (mailbox.groupingInformation == null))).
          ForEachAsync(
            async mailbox =>
            {
              tasks.Add(DiscoverMailbox(mailbox));

              if (tasks.Count >= flushSize)
              {
                await flush();
              }
            },
            cancellationToken);

        if (tasks.Count > 0)
        {
          await flush();
        }
      }
    }

    /// <summary>
    /// Discovers a mailbox instance.
    /// </summary>
    /// <param name="mailbox">A mailbox instance.</param>
    /// <returns>A BankMailbox instance.</returns>
    private async Task<BankMailbox> DiscoverMailbox(BankMailbox mailbox)
    {
      GetUserSettingsResponse userInfo = null;
      var invalid = false;

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
        invalid = true;
      }

      string url = null;
      string group = null;

      if (userInfo != null)
      {
        url = userInfo.Settings[UserSettingName.ExternalEwsUrl] as string;
        group =
          userInfo.Settings[UserSettingName.GroupingInformation] as string;
      }

      return new BankMailbox
      {
        mailAddress = mailbox.mailAddress,
        Invalid = invalid,
        userName = mailbox.userName,
        ewsUrl = url,
        groupingInformation = group,
        notifyOnNewMails = mailbox.notifyOnNewMails,
        notifyOnNewAppointments = mailbox.notifyOnNewAppointments,
        calendarSyncStatus = mailbox.calendarSyncStatus,
        inboxSyncStatus = mailbox.inboxSyncStatus,
        managingServer = mailbox.managingServer
      };
    }

    /// <summary>
    /// Synchornizes all mailboxes.
    /// </summary>
    /// <returns>A task that completes when all mail boxes are in sync.</returns>
    private async Task SyncMailboxes()
    {
      var flushSize = 100;
      var tasks = new List<Task<BankMailbox>>();

      Func<Task> flush = async () =>
      {
        var mailboxes = (await Task.WhenAll(tasks)).
          Where(mailbox => mailbox != null).
          ToArray();

        tasks.Clear();

        if (mailboxes.Length > 0)
        {
          using(var model = new EWSQueueEntities())
          {
            foreach(var mailbox in mailboxes)
            {
              model.Entry(mailbox).State = EntityState.Modified;
            }

            await model.SaveChangesAsync(cancellationToken);
          }
        }
      };

      using(var model = new EWSQueueEntities())
      {
        await model.BankMailboxes.AsNoTracking().
          Where(mailbox => !mailbox.Invalid && (mailbox.ewsUrl != null)).
          ForEachAsync(
            async mailbox =>
            {
              tasks.Add(SyncMailbox(mailbox));

              if (tasks.Count >= flushSize)
              {
                await flush();
              }
            },
            cancellationToken);

        if (tasks.Count > 0)
        {
          await flush();
        }
      }
    }

    /// <summary>
    /// Syncs a mail box.
    /// </summary>
    /// <param name="mailbox">A mailbox to synchronize.</param>
    /// <returns>Synced mail box, or null if mail box is up to date.</returns>
    private async Task<BankMailbox> SyncMailbox(BankMailbox mailbox)
    {
      var hasChanges = false;
      var service = GetService(mailbox);

      try
      {
        if (mailbox.notifyOnNewMails)
        {
          var changes = await SyncMailbox(
            mailbox,
            service,
            Office365.WellKnownFolderName.Inbox,
            mailbox.inboxSyncStatus);

          if (mailbox.inboxSyncStatus != changes.SyncState)
          {
            mailbox.inboxSyncStatus = changes.SyncState;
            hasChanges = true;
          }
        }

        if (mailbox.notifyOnNewAppointments)
        {
          var changes = await SyncMailbox(
            mailbox,
            service,
            Office365.WellKnownFolderName.Inbox,
            mailbox.calendarSyncStatus);

          if (mailbox.calendarSyncStatus != changes.SyncState)
          {
            mailbox.calendarSyncStatus = changes.SyncState;
            hasChanges = true;
          }
        }
      }
      catch
      {
        hasChanges = true;
        mailbox.ewsUrl = null;
      }

      return hasChanges ? mailbox : null;
    }

    /// <summary>
    /// Syncs a mail box.
    /// </summary>
    /// <param name="mailbox">A mailbox to synchronize.</param>
    /// <param name="service">An Exchange service.</param>
    /// <param name="folderId">A folder id.</param>
    /// <param name="syncState">A folder SyncState.</param>
    /// <returns>Synced mail box, or null if mail box is up to date.</returns>
    private Task<Office365.ChangeCollection<Office365.ItemChange>> SyncMailbox(
      BankMailbox mailbox,
      Office365.ExchangeService service, 
      Office365.FolderId folderId,
      string syncState)
    {
      var taskSource = new TaskCompletionSource<
        Office365.ChangeCollection<Office365.ItemChange>>();

      service.BeginSyncFolderItems(
        asyncResult =>
        {
          cancellationToken.ThrowIfCancellationRequested();

          try
          {
            taskSource.SetResult(service.EndSyncFolderItems(asyncResult));
          }
          catch (Exception e)
          {
            taskSource.TrySetException(e);
          }
        },
        null,
        folderId,
        Office365.PropertySet.IdOnly,
        null,
        1,
        Office365.SyncFolderItemsScope.NormalItems,
        syncState);

      return taskSource.Task;
    }

    //
    private async Task ListenMailboxes()
    {

    }

    /// <summary>
    /// Gets an Exchange service instance.
    /// </summary>
    /// <param name="mailbox">A mailbox instance.</param>
    /// <returns>a ExchangeService instance.</returns>
    private Office365.ExchangeService GetService(BankMailbox mailbox)
    {
      var service = new Office365.ExchangeService(
        Office365.ExchangeVersion.Exchange2013);

      service.Credentials = new Office365.WebCredentials(
        Settings.ExchangeUserName,
        Settings.ExchangePassword);
      service.UseDefaultCredentials = false;
      service.PreAuthenticate = true;

      if (Settings.ExchangeUserName != mailbox.mailAddress)
      {
        service.ImpersonatedUserId = new Office365.ImpersonatedUserId(
          Office365.ConnectingIdType.SmtpAddress,
          mailbox.mailAddress);
      }

      service.Url = new Uri(mailbox.ewsUrl);

      return service;
    }

    /// <summary>
    /// Cancellation source.
    /// </summary>
    private CancellationToken cancellationToken;
  }
}