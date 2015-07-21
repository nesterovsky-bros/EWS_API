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
  using System.Diagnostics;
  
  /// <summary>
  /// A EWS listener.
  /// </summary>
  public class EwsListener
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
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>A task associated with listener task.</returns>
    public async Task Start(
      CancellationToken cancellationToken = default(CancellationToken))
    {
      this.cancellationToken = cancellationToken;
      //if (running)
      //{
      //  throw new InvalidOperationException("Listener is already running.");
      //}

      //running = true;

      //try
      //{
      //  while(true)
      //  {
      //    using(restartSource = new CancellationTokenSource())
      //    {
      //      try
      //      {
      //        using(var cancellationSource = 
      //          CancellationTokenSource.CreateLinkedTokenSource(
      //            restartSource.Token, 
      //            cancellationToken))
      //        {
      //          cancellationToken = cancellationSource.Token;

                var watch = new Stopwatch();

                Trace.TraceInformation("Starting EWS listener.");
                Trace.TraceInformation("Start discover mailboxes.");
                watch.Start();
                await DiscoverMailboxes();
                watch.Stop();
                Trace.TraceInformation("End discover mailboxes; elasped: {0}.", watch.Elapsed);

                Trace.TraceInformation("Listen mailboxes.");
                watch.Restart();
                await ListenMailboxes();
                watch.Stop();
                Trace.TraceInformation("Listener is started; elapsed: {0}.", watch.Elapsed);
      //        }
      //      }
      //      catch(OperationCanceledException e)
      //      {
      //        if (e.CancellationToken == restartSource.Token)
      //        {
      //          continue;
      //        }

      //        throw;
      //      }
      //      catch(Exception e)
      //      {
      //        Trace.TraceError("Failed to start EWS listener. " + e);

      //        throw;
      //      }
      //    }
      //  }
      //}
      //finally
      //{
      //  restartSource = null;
      //  running = false;
      //}
    }

    /// <summary>
    /// Discovers all mailboxes.
    /// </summary>
    /// <returns>A task that completes when all mail boxes are in sync.</returns>
    private async Task DiscoverMailboxes()
    {
      var parallelism = Settings.EWSMaxConcurrency;

      using(var semaphore = new SemaphoreSlim(parallelism))
      {
        Func<string, Task> discover = async email =>
        {
          try
          {
            var mailbox = await DiscoverMailbox(email);

            using(var model = CreateModel())
            {
              if (mailbox.ExternalEwsUrl != null)
              {
                model.Entry(mailbox).State = EntityState.Added;
              }
              else
              {
                var invalid = new InvalidMailbox { Email = mailbox.Email };

                model.Entry(invalid).State = EntityState.Added;
              }

              await model.SaveChangesAsync(cancellationToken);
            }
          }
          finally
          {
            semaphore.Release();
          }
        };

        using(var model = CreateModel())
        {
          while(true)
          {
            var mailboxes = model.MailboxAffinities.Take(1000).ToArray();

            if (mailboxes.Length == 0)
            {
              break;
            }

            model.MailboxAffinities.RemoveRange(mailboxes);
            await model.SaveChangesAsync();
          }

          await model.BankSystemMailboxes.
            Select(item => item.Email).
            Distinct().
            Except(model.MailboxAffinities.Select(item => item.Email)).
            Except(model.InvalidMailboxes.Select(item => item.Email)).
            AsNoTracking().
            ForEachAsync(
              async email =>
              {
                await semaphore.WaitAsync(cancellationToken);
                
                var task = discover(email);
              },
              cancellationToken);
        }

        // Wait to complete pending tasks.
        for(var i = 0;  semaphore.CurrentCount + i < parallelism; ++i)
        {
          await semaphore.WaitAsync(cancellationToken);
        }
      }
    }

    /// <summary>
    /// Discovers a mailbox instance.
    /// </summary>
    /// <param name="email">A email.</param>
    /// <returns>A MailboxAffinity instance.</returns>
    private async Task<MailboxAffinity> DiscoverMailbox(string email)
    {
      try
      {
        var user = Settings.DefaultApplicationUser;

        var userInfo = await AutoDiscovery.GetUserSettings(
          Settings.AutoDiscoveryUrl,
          user.Email,
          user.Password,
          Settings.AttemptsToDiscoverUrl,
          email,
          cancellationToken);

        return new MailboxAffinity
        {
          Email = email,
          ExternalEwsUrl = 
            userInfo.Settings[UserSettingName.ExternalEwsUrl] as string,
          GroupingInformation =
            userInfo.Settings[UserSettingName.GroupingInformation] as string
        };
      }
      catch(Exception e)
      {
        Trace.TraceError(
          "Cannot resolve affinity for a mailbox: {0}. {1}", 
          email, 
          e);

        return new MailboxAffinity { Email = email };
      }
    }

    /// <summary>
    /// Synchronizes mailboxes.
    /// </summary>
    /// <param name="user">An application user.</param>
    /// <param name="mailboxes">A enumeration of mailboxes.</param>
    /// <returns>A task instance.</returns>
    private Task SyncMailboxes(
      ApplicationUser user,
      IEnumerable<MailboxAffinity> mailboxes)
    {
      return Task.WhenAll(
        mailboxes.Select(mailbox => SyncMailbox(user, mailbox)));
    }

    /// <summary>
    /// Syncs a mail box.
    /// </summary>
    /// <param name="user">An application user.</param>
    /// <param name="mailbox">A mailbox to synchronize.</param>
    /// <returns>Synced mail box, or null if mail box is up to date.</returns>
    private async Task SyncMailbox(
      ApplicationUser user, 
      MailboxAffinity mailbox)
    {
      if ((mailbox == null) || 
        (mailbox.ExternalEwsUrl == null) || 
        (mailbox.GroupingInformation == null))
      {
        return;
      }

      var service = GetService(mailbox, user);
      var state = null as BankMailbox;

      using(var model = CreateModel())
      {
        state = await model.BankMailboxes.
          AsNoTracking().
          Where(item => item.Email == mailbox.Email).
          FirstOrDefaultAsync();
      }

      var changed = false;
      var isNew = state == null;

      if (isNew)
      {
        state = new BankMailbox();
      }

      var syncState = await SyncMailbox(
        mailbox,
        service,
        Office365.WellKnownFolderName.Inbox,
        state.InboxSyncState);

      if (state.InboxSyncState != syncState)
      {
        state.InboxSyncState = syncState;
        changed = true;
      }

      syncState = await SyncMailbox(
        mailbox,
        service,
        Office365.WellKnownFolderName.Calendar,
        state.CalendarSyncState);

      if (state.CalendarSyncState != syncState)
      {
        state.CalendarSyncState = syncState;
        changed = true;
      }

      if (changed)
      {
        using(var model = CreateModel())
        {
          model.Entry(state).State =
            isNew ? EntityState.Added : EntityState.Modified;

          await model.SaveChangesAsync(cancellationToken);
        }
      }
    }

    /// <summary>
    /// Syncs and updates a mail box.
    /// </summary>
    /// <param name="email">An email address to sync.</param>
    /// <param name="events">A enumeration of events.</param>
    /// <returns>Task instance.</returns>
    private async Task SyncAndUpdateMailbox(
      string email,
      IEnumerable<Office365.NotificationEvent> events)
    {
      using(var model = CreateModel())
      {
        model.BankNotifications.AddRange(
          events.
            OfType<Office365.ItemEvent>().
            Select(
              item =>
                new BankNotification
                {
                  Timestamp = item.TimeStamp,
                  ItemID = item.ItemId.UniqueId,
                  Email = email,
                  ChangeType = 
                    (item.EventType == Office365.EventType.NewMail) ||
                    (item.EventType == Office365.EventType.Created) ?
                      Office365.ChangeType.Create.ToString() :
                      item.EventType == Office365.EventType.Deleted ?
                      Office365.ChangeType.Delete.ToString() :
                      Office365.ChangeType.Update.ToString()
                }));

        await model.SaveChangesAsync(cancellationToken);
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
      MailboxAffinity mailbox,
      Office365.ExchangeService service, 
      Office365.FolderId folderId,
      string syncState)
    {
      var state = syncState;
      var hasMore = false;
      var attempt = 0;
      var wait = false;

      do
      {
        cancellationToken.ThrowIfCancellationRequested();

        if (wait)
        {
          wait = false;
          await Task.Delay(1000);
        }

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
          SyncProperties,
          null,
          512,
          Office365.SyncFolderItemsScope.NormalItems,
          state);

        try
        {
          var changes = await taskSource.Task;

          if (changes.Count > 0)
          {
            using(var model = CreateModel())
            {
              model.BankNotifications.AddRange(
                changes.Select(
                  change => new BankNotification
                  {
                    Timestamp = change.Item.LastModifiedTime,
                    ItemID = change.ItemId.UniqueId,
                    Email = mailbox.Email,
                    ChangeType = change.ChangeType.ToString()
                  }).
                Where(
                  outer => !model.BankNotifications.
                    Any(
                      inner => 
                        (outer.Timestamp == inner.Timestamp) &&
                        (outer.Email == inner.Email) &&
                        (outer.ItemID == inner.ItemID))));

              await model.SaveChangesAsync(cancellationToken);
            }
          }

          state = changes.SyncState;
          hasMore = changes.MoreChangesAvailable;
        }
        catch(Exception e)
        {
          if (attempt < 1)
          {
            ++attempt;
            state = syncState;
            hasMore = true;

            continue;
          }

          Trace.TraceError(
            "Cannot synchronize a mailbox: {0}, filderId: {1}. {2}", 
            mailbox.Email, 
            folderId, 
            e);

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
      var groupSize = 200;
      var index = 0;
      var prev = null as MailboxAffinity;
      var group = new List<MailboxAffinity>(groupSize);

      using(var semaphore = new SemaphoreSlim(groupSize))
      using(var model = CreateModel())
      {
        Func<int, MailboxAffinity[], Task> listen = async (i, mailboxes) =>
        {
          try
          {
            var user = Settings.ApplicationUsers[
              (i / Settings.HangingConnectionLimit) %
                Settings.ApplicationUsers.Length];

            await ListenMailboxes(user, mailboxes);
          }
          finally
          {
            semaphore.Release(mailboxes.Length);
          }
        };

        await model.BankSystemMailboxes.
          Select(item => item.Email).
          Except(model.InvalidMailboxes.Select(item => item.Email)).
          Distinct().
          Join(
            model.MailboxAffinities,
            outer => outer,
            inner => inner.Email,
            (outer, inner) => inner).
          OrderBy(item => item.ExternalEwsUrl).
          ThenBy(item => item.GroupingInformation).
          ThenBy(item => item.Email).
          AsNoTracking().
          ForEachAsync(
            async item =>
            {
              if ((prev != null) &&
                ((prev.ExternalEwsUrl != item.ExternalEwsUrl) ||
                  (prev.GroupingInformation != item.GroupingInformation) ||
                  (group.Count >= groupSize)))
              {
                var task = listen(index, group.ToArray());

                group.Clear();
                ++index;
              }

              await semaphore.WaitAsync(cancellationToken);
              group.Add(item);
              prev = item;
            },
            cancellationToken);

        if (group.Count > 0)
        {
          var task = listen(index, group.ToArray());
        }

        // Wait to complete pending tasks.
        for (var i = 0; semaphore.CurrentCount + i < groupSize; ++i)
        {
          await semaphore.WaitAsync(cancellationToken);
        }
      }
    }

    /// <summary>
    /// Syncs and subscribes a group of mail boxes.
    /// </summary>
    /// <param name="user">An application user.</param>
    /// <param name="mailboxes">A group of mailboxes to listen.</param>
    /// <returns>A task instance.</returns>
    private async Task ListenMailboxes(
      ApplicationUser user,
      MailboxAffinity[] mailboxes)
    {
      var primaryMailbox = mailboxes.FirstOrDefault();

      if (primaryMailbox == null)
      {
        return;
      }

      Trace.TraceInformation(
        "Subscribe to a group with primary mailbox: {0}, group size is: {1}", 
        primaryMailbox.Email,
        mailboxes.Length);

      var primaryService = GetService(primaryMailbox, user);

      Func<
        Office365.ExchangeService, 
        MailboxAffinity, 
        Task<Office365.StreamingSubscription>> subscribe =
        (service, mailbox) =>
        {
          service.HttpHeaders.Add("X-AnchorMailbox", primaryMailbox.Email);
          service.HttpHeaders.Add("X-PreferServerAffinity", "true");

          var folderIds = new List<Office365.FolderId>();

          folderIds.Add(Office365.WellKnownFolderName.Calendar);
          folderIds.Add(Office365.WellKnownFolderName.Inbox);

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
              catch(Office365.ServiceResponseException e)
              {
                Trace.TraceError(
                  "Cannot subscribe on mailbox events at: {0}, " +
                    "errorCode = {1}. {2}",
                  mailbox.Email,
                  e.ErrorCode,
                  e);

                mailbox.ExternalEwsUrl = null;
                mailbox.GroupingInformation = null;
                taskSource.SetResult(null);
              }
              catch(Exception e)
              {
                Trace.TraceError(
                  "Cannot subscribe on mailbox events at: {0}. {1}",
                  mailbox.Email,
                  e);

                taskSource.SetResult(null);
              }
            },
            null,
            folderIds,
            Office365.EventType.NewMail,
            Office365.EventType.Created,
            Office365.EventType.Deleted,
            Office365.EventType.Modified);

          return taskSource.Task;
        };

      var primarySubscription = 
        await subscribe(primaryService, primaryMailbox);
      var backEndOverrideCookie = primaryService.CookieContainer.
        GetCookies(primaryService.Url)["X-BackEndOverrideCookie"];

      var subscriptions = 
        new[] { primarySubscription }.
        Concat(
          await Task.WhenAll(
            mailboxes.
              Skip(1).
              Select(
                mailbox =>
                {
                  var service = GetService(mailbox, user);

                  service.CookieContainer.Add(service.Url, backEndOverrideCookie);

                  return subscribe(service, mailbox);
                }))).
        Where(item => item != null).
        ToArray();

      using(var model = CreateModel())
      {
        foreach(var mailbox in mailboxes)
        {
          if (mailbox.ExternalEwsUrl == null)
          {
            model.Entry(mailbox).State = EntityState.Deleted;
          }
        }

        await model.SaveChangesAsync(cancellationToken);
      }

      if (subscriptions.Length == 0)
      {
        return;
      }

      var connection = new Office365.StreamingSubscriptionConnection(
        primaryService,
        subscriptions.Where(subscription => subscription != null),
        Settings.ExchangeListenerRecyclePeriod);

      connection.OnNotificationEvent += (sender, args) =>
      {
        if (args.Subscription == null)
        {
          return;
        }

        var email = args.Subscription.Service.ImpersonatedUserId.Id;
        // Note: fire and forget task.
        var syncTask = SyncAndUpdateMailbox(email, args.Events);
      };

      connection.OnSubscriptionError += (sender, args) =>
      {
        // TODO: implement this.
        //restartSource.Cancel();
      };

      connection.OnDisconnect += (sender, args) =>
      {
        // TODO: implement this.
        //restartSource.Cancel();
      };

      cancellationToken.ThrowIfCancellationRequested();

      connection.Open();

      // NOTE: run and forget.
      var syncMailBoxesTask = SyncMailboxes(user, mailboxes);

      subscriptions = null;
      mailboxes = null;
    }

    /// <summary>
    /// Gets an Exchange service instance.
    /// </summary>
    /// <param name="mailbox">A MailboxAffinity instance.</param>
    /// <param name="user">Optional application user.</param>
    /// <returns>a ExchangeService instance.</returns>
    private Office365.ExchangeService GetService(
      MailboxAffinity mailbox,
      ApplicationUser user = null)
    {
      var service = new Office365.ExchangeService(
        Office365.ExchangeVersion.Exchange2013);

      if (user == null)
      {
        user = Settings.DefaultApplicationUser;
      }

      service.Credentials = 
        new Office365.WebCredentials(user.Email, user.Password);
      service.UseDefaultCredentials = false;
      service.PreAuthenticate = true;

      service.ImpersonatedUserId = new Office365.ImpersonatedUserId(
        Office365.ConnectingIdType.SmtpAddress,
        mailbox.Email);

      service.Url = new Uri(mailbox.ExternalEwsUrl);

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
    /// Running indicator.
    /// </summary>
//    private bool running;

    /// <summary>
    /// A cancellation token.
    /// </summary>
    private CancellationToken cancellationToken;

    /// <summary>
    /// Restart cancellation source.
    /// </summary>
//    private CancellationTokenSource restartSource;

    /// <summary>
    /// A properies to retrieve during Sync.
    /// </summary>
    private static readonly Office365.PropertySet SyncProperties =  
      new Office365.PropertySet(
        Office365.ItemSchema.Id, 
        Office365.ItemSchema.LastModifiedTime);
  }
}