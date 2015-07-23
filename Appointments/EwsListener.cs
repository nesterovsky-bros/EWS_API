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
    /// <returns>A task associated with the listener task.</returns>
    public async Task Start(
      CancellationToken cancellationToken = default(CancellationToken))
    {
      while(true)
      {
        using(var cancellation = 
          CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
        {
          try
          {
            var watch = new Stopwatch();

            Trace.TraceInformation("Starting EWS listener.");
            Trace.TraceInformation("Start discover mailboxes.");
            watch.Start();
            await DiscoverMailboxes(cancellation);
            watch.Stop();

            Trace.TraceInformation(
              "End discover mailboxes; elasped: {0}.",
              watch.Elapsed);

            Trace.TraceInformation("Listen mailboxes.");
            watch.Restart();
            await ListenMailboxes(cancellation);
            watch.Stop();
            
            Trace.TraceInformation(
              "Listener has started; elapsed: {0}.", 
              watch.Elapsed);

            await Task.Delay(int.MaxValue, cancellation.Token);
          }
          catch(OperationCanceledException)
          {
            cancellationToken.ThrowIfCancellationRequested();
          }
          finally
          {
            cancellation.Cancel();
          }
        }
      }
    }

    /// <summary>
    /// Discovers all mailboxes.
    /// </summary>
    /// <param name="cancellation">A cancellation token source.</param>
    /// <returns>A task that completes when all mail boxes are in sync.</returns>
    private async Task DiscoverMailboxes(CancellationTokenSource cancellation)
    {
      var parallelism = Math.Min(
        Settings.EWSMaxConcurrency * Settings.ApplicationUsers.Length, 
        1000);

      using(var semaphore = new SemaphoreSlim(parallelism))
      {
        Func<int, string, Task> discover = async (index, email) =>
        {
          try
          {
            var users = Settings.ApplicationUsers;
            var user = users[index % users.Length];
            var mailbox = await DiscoverMailbox(user, email, cancellation);

            using(var model = CreateModel())
            {
              var prev = await model.MailboxAffinities.AsNoTracking().
                Where(item => item.Email == mailbox.Email).
                FirstOrDefaultAsync(cancellation.Token);

              if (mailbox.ExternalEwsUrl != null)
              {
                if (prev != null)
                {
                  if ((prev.ExternalEwsUrl != mailbox.ExternalEwsUrl) ||
                    (prev.GroupingInformation != mailbox.GroupingInformation))
                  {
                    model.Entry(mailbox).State = EntityState.Modified;

                    Trace.TraceInformation(
                      "Affinity has changed for a mailbox {0}.", 
                      mailbox.Email);
                  }
                }
                else
                {
                  model.Entry(mailbox).State = EntityState.Added;
                }
              }
              else
              {
                var invalid = new InvalidMailbox { Email = mailbox.Email };

                model.Entry(invalid).State = EntityState.Added;
              }

              await model.SaveChangesAsync(cancellation.Token);
            }
          }
          finally
          {
            semaphore.Release();
          }
        };

        using(var model = CreateModel())
        {
          var index = 0;

          await model.BankSystemMailboxes.
            Select(item => item.Email).
            Distinct().
            Except(model.InvalidMailboxes.Select(item => item.Email)).
            AsNoTracking().
            ForEachAsync(
              async email =>
              {
                await semaphore.WaitAsync(cancellation.Token);

                var task = discover(index++, email);
              },
              cancellation.Token);
        }

        // Wait to complete pending tasks.
        for(var i = 0; semaphore.CurrentCount + i < parallelism; ++i)
        {
          await semaphore.WaitAsync(cancellation.Token);
        }
      }
    }

    /// <summary>
    /// Discovers a mailbox instance.
    /// </summary>
    /// <param name="user">An application user.</param>
    /// <param name="email">A email.</param>
    /// <param name="cancellation">A cancellation token source.</param>
    /// <returns>A MailboxAffinity instance.</returns>
    private async Task<MailboxAffinity> DiscoverMailbox(
      ApplicationUser user,
      string email,
      CancellationTokenSource cancellation)
    {
      try
      {
        return await TryAction(
          "discover",
          email,
          async attempt =>
          {
            var userInfo = await AutoDiscovery.GetUserSettings(
              Settings.AutoDiscoveryUrl,
              user,
              Settings.AttemptsToDiscoverUrl,
              email,
              cancellation.Token);

            return new MailboxAffinity
            {
              Email = email,
              ExternalEwsUrl =
                userInfo.Settings[UserSettingName.ExternalEwsUrl] as string,
              GroupingInformation =
                userInfo.Settings[UserSettingName.GroupingInformation] as string
            };
          },
          cancellation);
      }
      catch
      { 
        return new MailboxAffinity { Email = email };
      }
    }

    /// <summary>
    /// Synchronizes mailboxes.
    /// </summary>
    /// <param name="user">An application user.</param>
    /// <param name="mailboxes">A enumeration of mailboxes.</param>
    /// <param name="cancellation">A cancellation token source.</param>
    /// <returns>A task instance.</returns>
    private Task SyncMailboxes(
      ApplicationUser user,
      IEnumerable<MailboxAffinity> mailboxes,
      CancellationTokenSource cancellation)
    {
      return Task.WhenAll(
        mailboxes.Select(mailbox => SyncMailbox(user, mailbox, cancellation)));
    }

    /// <summary>
    /// Syncs a mail box.
    /// </summary>
    /// <param name="user">An application user.</param>
    /// <param name="mailbox">A mailbox to synchronize.</param>
    /// <param name="cancellation">A cancellation token source.</param>
    /// <returns>Synced mail box, or null if mail box is up to date.</returns>
    private async Task SyncMailbox(
      ApplicationUser user, 
      MailboxAffinity mailbox,
      CancellationTokenSource cancellation)
    {
      if ((mailbox == null) || 
        (mailbox.ExternalEwsUrl == null) || 
        (mailbox.GroupingInformation == null))
      {
        return;
      }

      var service = GetService(user, mailbox);
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
        state = new BankMailbox { Email = mailbox.Email };
      }

      var syncState = await SyncMailbox(
        mailbox,
        service,
        Office365.WellKnownFolderName.Inbox,
        state.InboxSyncState,
        cancellation);

      if (state.InboxSyncState != syncState)
      {
        state.InboxSyncState = syncState;
        changed = true;
      }

      syncState = await SyncMailbox(
        mailbox,
        service,
        Office365.WellKnownFolderName.Calendar,
        state.CalendarSyncState,
        cancellation);

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

          await model.SaveChangesAsync(cancellation.Token);
        }
      }
    }

    /// <summary>
    /// Syncs and updates a mail box.
    /// </summary>
    /// <param name="email">An email address to sync.</param>
    /// <param name="events">A enumeration of events.</param>
    /// <param name="cancellation">A cancellation token source.</param>
    /// <returns>Task instance.</returns>
    private async Task SyncAndUpdateMailbox(
      string email,
      IEnumerable<Office365.NotificationEvent> events,
      CancellationTokenSource cancellation)
    {
      await Task.Yield();

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

        await model.SaveChangesAsync(cancellation.Token);
      }
    }

    /// <summary>
    /// Syncs a mail box.
    /// </summary>
    /// <param name="mailbox">A mailbox to synchronize.</param>
    /// <param name="service">An Exchange service.</param>
    /// <param name="folderId">A folder id.</param>
    /// <param name="syncState">A folder SyncState.</param>
    /// <param name="cancellation">A cancellation token source.</param>
    /// <returns>A new syncState value.</returns>
    private async Task<string> SyncMailbox(
      MailboxAffinity mailbox,
      Office365.ExchangeService service, 
      Office365.FolderId folderId,
      string syncState,
      CancellationTokenSource cancellation)
    {
      var state = syncState;
      var hasMore = false;

      do
      {
        cancellation.Token.ThrowIfCancellationRequested();

        try
        {
          var changes = await TryAction(
            "sync",
            mailbox.Email,
            attempt =>
            {
              if ((attempt > 0) && (state == syncState))
              {
                state = null;
              }

              var source = new TaskCompletionSource<
                Office365.ChangeCollection<Office365.ItemChange>>();

              service.BeginSyncFolderItems(
                asyncResult =>
                {
                  try
                  {
                    source.SetResult(service.EndSyncFolderItems(asyncResult));
                  }
                  catch(Exception e)
                  {
                    source.SetException(e);
                  }
                },
                null,
                folderId,
                SyncProperties,
                null,
                512,
                Office365.SyncFolderItemsScope.NormalItems,
                state);

              return source.Task;
            },
            cancellation);

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

              await model.SaveChangesAsync(cancellation.Token);
            }
          }

          state = changes.SyncState;
          hasMore = changes.MoreChangesAvailable;
        }
        catch 
        {
          return state == syncState ? null : state;
        }
      }
      while(hasMore);

      return state;
    }

    /// <summary>
    /// Listens for mailboxes.
    /// </summary>
    /// <param name="cancellation">A cancellation token source.</param>
    private async Task ListenMailboxes(CancellationTokenSource cancellation)
    {
      var groupSize = 200;
      var index = 0;
      var prev = null as MailboxAffinity;
      var group = new List<MailboxAffinity>(groupSize);

      var parallelism = Math.Min(
        groupSize * Settings.ApplicationUsers.Length,
        1000);

      using(var semaphore = new SemaphoreSlim(parallelism))
      using(var model = CreateModel())
      {
        Func<int, MailboxAffinity[], Task> listen = async (i, mailboxes) =>
        {
          try
          {
            var users = Settings.ApplicationUsers;
            var user = users[i % users.Length];

            await ListenMailboxes(user, mailboxes, cancellation);
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

              await semaphore.WaitAsync(cancellation.Token);
              group.Add(item);
              prev = item;
            },
            cancellation.Token);

        if (group.Count > 0)
        {
          var task = listen(index, group.ToArray());
        }

        // Wait to complete pending tasks.
        for(var i = 0; semaphore.CurrentCount + i < parallelism; ++i)
        {
          await semaphore.WaitAsync(cancellation.Token);
        }
      }
    }

    /// <summary>
    /// Syncs and subscribes a group of mail boxes.
    /// </summary>
    /// <param name="user">An application user.</param>
    /// <param name="mailboxes">A group of mailboxes to listen.</param>
    /// <param name="cancellation">A cancellation token source.</param>
    /// <returns>A task instance.</returns>
    private async Task ListenMailboxes(
      ApplicationUser user,
      MailboxAffinity[] mailboxes,
      CancellationTokenSource cancellation)
    {
      var primaryMailbox = mailboxes.FirstOrDefault();

      if (primaryMailbox == null)
      {
        return;
      }

      var primaryEmail = primaryMailbox.Email;

      Trace.TraceInformation(
        "Subscribe to a group with primary mailbox: {0}, group size is: {1}", 
        primaryMailbox.Email,
        mailboxes.Length);

      var primaryService = GetService(user, primaryMailbox);

      Func<
        Office365.ExchangeService, 
        MailboxAffinity, 
        Task<Office365.StreamingSubscription>> subscribe =
        async (service, mailbox) =>
        {
          service.HttpHeaders.Add("X-AnchorMailbox", primaryEmail);
          service.HttpHeaders.Add("X-PreferServerAffinity", "true");

          var folderIds = new List<Office365.FolderId>();

          folderIds.Add(Office365.WellKnownFolderName.Calendar);
          folderIds.Add(Office365.WellKnownFolderName.Inbox);

          try
          {
            return await TryAction(
              "subscribe",
              mailbox.Email,
              attempt =>
              {
                var source =
                  new TaskCompletionSource<Office365.StreamingSubscription>();

                service.BeginSubscribeToStreamingNotifications(
                  asyncResult =>
                  {
                    try
                    {
                      source.SetResult(service.
                        EndSubscribeToStreamingNotifications(asyncResult));
                    }
                    catch(Exception e)
                    {
                      source.SetException(e);
                    }
                  },
                  null,
                  folderIds,
                  Office365.EventType.NewMail,
                  Office365.EventType.Created,
                  Office365.EventType.Deleted,
                  Office365.EventType.Modified);

                return source.Task;
              },
              cancellation);
          }
          catch(OperationCanceledException)
          {
            throw;
          }
          catch(Office365.ServiceResponseException)
          {
            mailbox.ExternalEwsUrl = null;
            mailbox.GroupingInformation = null;

            return null;
          }
          catch
          {
            return null;
          }
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
                  var service = GetService(user, mailbox);

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

        await model.SaveChangesAsync(cancellation.Token);
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
        var syncTask = SyncAndUpdateMailbox(email, args.Events, cancellation);
      };

      connection.OnSubscriptionError += (sender, args) =>
      {
        var subscription = args.Subscription;
        var service = subscription == null ? null : subscription.Service;
        var email = service == null ? null : service.ImpersonatedUserId.Id;

        if (email != null)
        {
          Trace.TraceInformation(
            "Subscription error for a mailbox: {0}. {1}",
            email,
            args.Exception);
        }
        else
        {
          Trace.TraceInformation(
            "Subscription error for a group with primary mailbox: {0}. {1}",
            primaryEmail,
            args.Exception);
        }

        if (!cancellation.IsCancellationRequested)
        {
          cancellation.Cancel();
        }
      };

      connection.OnDisconnect += (sender, args) =>
      {
        Trace.TraceInformation(
          "Disconnection for a group with primary mailbox: {0}. {1}",
          primaryEmail, 
          args.Exception);

        if (!cancellation.IsCancellationRequested)
        {
          cancellation.Cancel();
        }
      };

      cancellation.Token.ThrowIfCancellationRequested();

      // NOTE: run and forget.
      var syncMailBoxesTask = SyncMailboxes(user, mailboxes, cancellation);

      cancellation.Token.Register(() =>
      {
        try
        {
          connection.Close();
        }
        catch
        { 
        }
      });

      connection.Open();
    }

    /// <summary>
    /// Gets an Exchange service instance.
    /// </summary>
    /// <param name="user">An application user.</param>
    /// <param name="mailbox">A MailboxAffinity instance.</param>
    /// <returns>a ExchangeService instance.</returns>
    private Office365.ExchangeService GetService(
      ApplicationUser user,
      MailboxAffinity mailbox)
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
    /// <returns>An model instance.</returns>
    private EWSQueueEntities CreateModel()
    {
      var model = new EWSQueueEntities();

      model.Configuration.ProxyCreationEnabled = false;

      return model;
    }

    /// <summary>
    /// Performs an action a specified number of times.
    /// </summary>
    /// <typeparam name="T">A result type.</typeparam>
    /// <param name="name">Action name.</param>
    /// <param name="email">A mailbox.</param>
    /// <param name="action">Action function.</param>
    /// <param name="cancellation">A cancellation source.</param>
    /// <returns>Actio result.</returns>
    private static async Task<T> TryAction<T>(
      string name,
      string email,
      Func<int, Task<T>> action,
      CancellationTokenSource cancellation)
    {
      const int retryCount = 2;

      for(var i = 0; i < retryCount; ++i)
      {
        try
        {
          return await action(i);
        }
        catch(OperationCanceledException)
        {
          throw;
        }
        catch(Office365.ServiceResponseException e)
        {
          switch(e.ErrorCode)
          {
            case Office365.ServiceError.ErrorMailboxStoreUnavailable:
            case Office365.ServiceError.ErrorInternalServerError:
            {
              if (i + 1 < retryCount)
              {
                Trace.TraceWarning(
                  "Cannot perform {0} for a mailbox: {1}, eventCode = {2}. {3}",
                  name,
                  email,
                  e.ErrorCode,
                  e);

                break;
              }

              goto default;
            }
            default:
            {
              Trace.TraceError(
                "Cannot perform {0} for a mailbox: {1}, eventCode = {2}. {3}",
                name,
                email,
                e.ErrorCode,
                e);

              throw;
            }
          }
        }
        catch(Office365.ServiceRequestException e)
        {
          var webException = e.InnerException as WebException;
          var webResponse = webException == null ? null : 
            webException.Response as HttpWebResponse;

          if ((webResponse != null) && 
            (webResponse.StatusCode == HttpStatusCode.Unauthorized))
          {
            Trace.TraceError(
              "Cannot perform {0} for a mailbox: {1}; Unauthorized. {2}",
              name,
              email,
              e);

            throw;
          }

          if (i + 1 < retryCount)
          {
            Trace.TraceWarning(
              "Cannot perform {0} for a mailbox: {1}. {2}",
              name,
              email,
              e);

            break;
          }

          Trace.TraceError(
            "Cannot perform {0} for a mailbox: {1}. {2}",
            name,
            email,
            e);

          throw;
        }
        catch(Exception e)
        {
          Trace.TraceError(
            "Cannot perform {0} for a mailbox: {1}. {2}",
            name,
            email,
            e);

          throw;
        }

        await Task.Delay(Random(500, 1500), cancellation.Token);
      }

      return default(T);
    }

    /// <summary>
    /// Returns a random number within a specified range.
    /// </summary>
    /// <param name="minValue">
    /// The inclusive lower bound of the random number returned.
    /// </param>
    /// <param name="maxValue">
    /// The exclusive upper bound of the random number returned.
    /// </param>
    /// <returns>A random value within requested range,</returns>
    private static int Random(int minValue, int maxValue)
    { 
      lock(sync)
      {
        return random.Next(minValue, maxValue);
      }
    }

    /// <summary>
    /// A properies to retrieve during Sync.
    /// </summary>
    private static readonly Office365.PropertySet SyncProperties =  
      new Office365.PropertySet(
        Office365.ItemSchema.Id, 
        Office365.ItemSchema.LastModifiedTime);

    /// <summary>
    /// Global lock.
    /// </summary>
    private static object sync = new object();

    /// <summary>
    /// Random used to generate delays.
    /// </summary>
    private static Random random = new Random();
  }
}