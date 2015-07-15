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

      using(var model = CreateModel())
      {
        await model.BankMailboxes.
          Where(
            mailbox =>
              !mailbox.Invalid &&
              ((mailbox.ewsUrl == null) || (mailbox.groupingInformation == null))).
          AsNoTracking().
          ForEachAsync(
            async mailbox =>
            {
              tasks.Add(DiscoverMailbox(mailbox));

              if (tasks.Count >= flushSize)
              {
                await UpdateMailboxes(await Task.WhenAll(tasks));
                tasks.Clear();
              }
            },
            cancellationToken);

        if (tasks.Count > 0)
        {
          await UpdateMailboxes(await Task.WhenAll(tasks));
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

        mailbox.ewsUrl = 
          userInfo.Settings[UserSettingName.ExternalEwsUrl] as string;
        mailbox.groupingInformation = 
          userInfo.Settings[UserSettingName.GroupingInformation] as string;
        mailbox.inboxSyncStatus = null;
        mailbox.calendarSyncStatus = null;
      }
      catch
      { 
        // Consider the user invalid.
        mailbox.Invalid = invalid;
      }

      return mailbox;
    }

    /// <summary>
    /// Updates information in the mailboxes.
    /// </summary>
    /// <param name="mailboxes">A list of mailboxes to update.</param>
    private async Task UpdateMailboxes(
      IEnumerable<BankMailbox> mailboxes)
    {
      using(var model = CreateModel())
      {
        foreach(var mailbox in mailboxes)
        {
          if (mailbox != null)
          {
            model.Entry(mailbox).State = EntityState.Modified;
          }
        }

        await model.SaveChangesAsync(cancellationToken);
      }
    }

    /// <summary>
    /// Syncs a mail box.
    /// </summary>
    /// <param name="service">A Exchange service instance.</param>
    /// <param name="mailbox">A mailbox to synchronize.</param>
    /// <returns>Synced mail box, or null if mail box is up to date.</returns>
    private async Task<BankMailbox> SyncMailbox(
      Office365.ExchangeService service, 
      BankMailbox mailbox)
    {
      if ((mailbox.ewsUrl == null) || (mailbox.groupingInformation == null))
      {
        return null;
      }

      service.ImpersonatedUserId = new Office365.ImpersonatedUserId(
        Office365.ConnectingIdType.SmtpAddress,
        mailbox.mailAddress);

      var changed = false;

      try
      {
        if (mailbox.notifyOnNewMails)
        {
          var syncState = await SyncMailbox(
            mailbox,
            service,
            Office365.WellKnownFolderName.Inbox,
            mailbox.inboxSyncStatus);

          if (mailbox.inboxSyncStatus != syncState)
          {
            mailbox.inboxSyncStatus = syncState;
            changed = true;
          }
        }

        if (mailbox.notifyOnNewAppointments)
        {
          var syncState = await SyncMailbox(
            mailbox,
            service,
            Office365.WellKnownFolderName.Calendar,
            mailbox.calendarSyncStatus);

          if (mailbox.calendarSyncStatus != syncState)
          {
            mailbox.calendarSyncStatus = syncState;
            changed = true;
          }
        }
      }
      catch
      {
        changed = true;
        mailbox.calendarSyncStatus = null;
        mailbox.inboxSyncStatus = null;
      }

      return changed ? mailbox : null;
    }

    /// <summary>
    /// Syncs and updates a mail box.
    /// </summary>
    /// <param name="emailAddress">An email address to sync.</param>
    /// <returns>Task instance.</returns>
    private async Task SyncAndUpdateMailbox(string emailAddress)
    {
      var mailbox = null as BankMailbox;

      using(var model = CreateModel())
      {
        mailbox = await model.BankMailboxes.
          Where(
            item =>
              (item.mailAddress == emailAddress) &&
              (item.ewsUrl != null) &&
              !item.Invalid).
          AsNoTracking().
          FirstOrDefaultAsync();
      }

      if (mailbox == null)
      {
        return;
      }

      var service = GetService(mailbox);

      mailbox = await SyncMailbox(service, mailbox);

      if (mailbox != null)
      {
        await UpdateMailboxes(new[] { mailbox });
      }
    }

    /// <summary>
    /// Syncs a mail box.
    /// </summary>
    /// <param name="mailbox">A mailbox to synchronize.</param>
    /// <param name="service">An Exchange service.</param>
    /// <param name="folderId">A folder id.</param>
    /// <param name="syncState">A folder SyncState.</param>
    /// <returns>A new syncState value.</returns>
    private async Task<string> SyncMailbox(
      BankMailbox mailbox,
      Office365.ExchangeService service, 
      Office365.FolderId folderId,
      string syncState)
    {
      var state = syncState;
      var hasMore = false;

      do
      {
        cancellationToken.ThrowIfCancellationRequested();

        var now = DateTime.Now;
        var taskSource = new TaskCompletionSource<
          Office365.ChangeCollection<Office365.ItemChange>>();

        service.BeginSyncFolderItems(
          asyncResult =>
          {
            try
            {
              cancellationToken.ThrowIfCancellationRequested();
              taskSource.SetResult(service.EndSyncFolderItems(asyncResult));
            }
            catch(Exception e)
            {
              taskSource.SetException(e);
            }
          },
          null,
          folderId,
          Office365.PropertySet.IdOnly,
          null,
          512,
          Office365.SyncFolderItemsScope.NormalItems,
          state);

        try
        {
          var changes = await taskSource.Task;

          if (changes.Count > 0)
          {
            using (var model = CreateModel())
            {
              model.BankNotifications.AddRange(
                changes.Select(
                  change => new BankNotification
                  {
                    UpdatedAt = now,
                    itemId = change.ItemId.UniqueId,
                    mailAddress = mailbox.mailAddress,
                    ChangeType = (int)change.ChangeType
                  }));

              await model.SaveChangesAsync(cancellationToken);
            }
          }

          state = changes.SyncState;
          hasMore = changes.MoreChangesAvailable;
        }
        catch
        { 
          // TODO: log error
          return null;
        }
      }
      while(hasMore);

      return state;
    }

    /// <summary>
    /// Listens for mailboxes.
    /// </summary>
    /// <returns></returns>
    private async Task ListenMailboxes()
    {
      var prev = null as BankMailbox;
      var group = new List<BankMailbox>();

      using(var model = CreateModel())
      {
        await model.BankMailboxes.
          Where(mailbox => 
            !mailbox.Invalid && 
            (mailbox.ewsUrl != null) && 
            (mailbox.groupingInformation != null) &&
            (mailbox.notifyOnNewMails || mailbox.notifyOnNewAppointments)).
          OrderBy(mailbox => mailbox.ewsUrl).
          ThenBy(mailbox => mailbox.groupingInformation).
          ThenBy(mailbox => mailbox.mailAddress).
          AsNoTracking().
          ForEachAsync(
            async mailbox =>
            {
              if ((prev != null) &&
                ((prev.ewsUrl != mailbox.ewsUrl) ||
                  (prev.groupingInformation != mailbox.groupingInformation) ||
                  (group.Count >= 200)))
              {
                await ListenMailboxes(group);
                group.Clear();
              }

              group.Add(mailbox);
              prev = mailbox;
            },
            cancellationToken);

        if (group.Count > 0)
        {
          await ListenMailboxes(group);
          group.Clear();
        }
      }
    }

    /// <summary>
    /// Syncs and subscribes a group of mail boxes.
    /// </summary>
    /// <param name="mailboxes">A group of mail boxes.</param>
    /// <returns>A task instance.</returns>
    private async Task ListenMailboxes(IEnumerable<BankMailbox> mailboxes)
    {
      var primaryMailbox = mailboxes.FirstOrDefault();

      if (primaryMailbox == null)
      {
        return;
      }

      var primaryService = GetService(primaryMailbox);

      Func<
        Office365.ExchangeService, 
        BankMailbox, 
        Task<Office365.StreamingSubscription>> subscribe =
        (service, mailbox) =>
        {
          service.HttpHeaders.Add("X-AnchorMailbox", primaryMailbox.mailAddress);
          service.HttpHeaders.Add("X-PreferServerAffinity", "true");

          var folderIds = new List<Office365.FolderId>();

          if (mailbox.notifyOnNewAppointments)
          {
            folderIds.Add(Office365.WellKnownFolderName.Calendar);
          }

          if (mailbox.notifyOnNewMails)
          {
            folderIds.Add(Office365.WellKnownFolderName.Inbox);
          }

          var taskSource =
            new TaskCompletionSource<Office365.StreamingSubscription>();

          service.BeginSubscribeToStreamingNotifications(
            asyncResult =>
            {
              try
              {
                cancellationToken.ThrowIfCancellationRequested();
                taskSource.SetResult(
                  service.EndSubscribeToStreamingNotifications(asyncResult));
              }
              catch(OperationCanceledException e)
              {
                taskSource.SetException(e);

                return;
              }
              catch(Office365.ServiceResponseException)
              {
                mailbox.ewsUrl = null;
                mailbox.groupingInformation = null;
                mailbox.calendarSyncStatus = null;
                mailbox.inboxSyncStatus = null;
                taskSource.SetResult(null);
              }
              catch
              {
                // TODO: log error.
                taskSource.SetResult(null);
              }
            },
            null,
            folderIds,
            Office365.EventType.NewMail,
            Office365.EventType.Created,
            Office365.EventType.Deleted);

          return taskSource.Task;
        };

      var primarySubscription = 
        await subscribe(primaryService, primaryMailbox);

      var subscriptions = 
        new[] { primarySubscription }.
        Concat(
          await Task.WhenAll(
            mailboxes.
              Skip(1).
              Select(
                mailbox =>
                {
                  var service = GetService(mailbox);

                  service.CookieContainer.Add(
                    service.Url,
                    primaryService.CookieContainer.
                      GetCookies(primaryService.Url));

                  // set X-BackEndOverrideCookie

                  return subscribe(service, mailbox);
                }))).
        ToArray();

      var subscriptionMap = subscriptions.
        Zip(
          mailboxes,
          (subscription, mailbox) => new { subscription, mailbox }).
        Where(item => item.subscription != null).
        ToDictionary(
          item => item.subscription.Id, 
          item => item.mailbox.mailAddress);

      await UpdateMailboxes(mailboxes.Where(mailbox => mailbox.ewsUrl == null));

      if (subscriptionMap.Count == 0)
      {
        return;
      }

      var connection = new Office365.StreamingSubscriptionConnection(
        primaryService,
        subscriptions.Where(subscription => subscription != null),
        Settings.ExchangeListenerRecyclePeriod);

      connection.OnNotificationEvent += (sender, args) =>
      {
        var emailAddress = subscriptionMap.Get(args.Subscription.Id);

        if (emailAddress != null)
        {
          // Note: fire and forget task.
          var syncTask = SyncAndUpdateMailbox(emailAddress);
        }
      };

      connection.OnSubscriptionError += (sender, args) =>
      {
        var emailAddress = subscriptionMap.Get(args.Subscription.Id);

        if (emailAddress != null)
        {
          // TODO: handle subscription error.
        }
      };

      connection.OnDisconnect += (sender, args) =>
      {
        var emailAddress = subscriptionMap.Get(args.Subscription.Id);

        if (emailAddress != null)
        {
          // TODO: handle disconnect.
        }
      };

      cancellationToken.ThrowIfCancellationRequested();

      connection.Open();

      await UpdateMailboxes(
        await Task.WhenAll(
          mailboxes.Select(mailbox => SyncMailbox(GetService(mailbox), mailbox))));

      subscriptions = null;
      mailboxes = null;
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
    /// Creates a model instance.
    /// </summary>
    /// <returns></returns>
    private EWSQueueEntities CreateModel()
    {
      var model = new EWSQueueEntities();

      model.Configuration.ProxyCreationEnabled = false;

      return model;
    }

    /// <summary>
    /// Cancellation source.
    /// </summary>
    private CancellationToken cancellationToken;
  }
}