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
          cancellation.CancelAfter(
            TimeSpan.FromMinutes(Settings.ExchangeListenerRecyclePeriod));

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

            Trace.TraceInformation("Sync mailboxes.");
            watch.Restart();
            await SyncMailboxes(cancellation);
            watch.Stop();

            Trace.TraceInformation(
              "Sync mailboxes has completed; elapsed: {0}.",
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
    /// <returns>
    /// A task that completes when all mail boxes are discovered.
    /// </returns>
    private async Task DiscoverMailboxes(CancellationTokenSource cancellation)
    {
      var groupSize = Settings.UsersPerUsersSettins;
      var group = new List<string>(groupSize);
      var parallelism = Settings.EWSMaxConcurrency;
      var index = 0;

      using(var semaphore = new SemaphoreSlim(parallelism))
      {
        Func<int, string[], Task> discover = async (i, emails) =>
        {
          try
          {
            var user = GetUser(i);

            var mailboxes = (await EwsUtils.TryAction(
              "Discover",
              emails[0],
              async attempt =>
              {
                await Task.Yield();

                return EwsUtils.GetMailboxAffinities(user, Settings.AutoDiscoveryUrl, emails);
              },
              cancellation.Token)).
              ToDictionary(item => item.Email);

            using(var model = CreateModel())
            {
              foreach (var email in emails)
              {
                var mailbox = mailboxes.Get(email);

                if (mailbox == null)
                {
                  var invalid = new InvalidMailbox { Email = mailbox.Email };

                  model.Entry(invalid).State = EntityState.Added;
                }
                else
                {
                  var prev = await model.MailboxAffinities.AsNoTracking().
                    Where(item => item.Email == email).
                    FirstOrDefaultAsync(cancellation.Token);

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

                await model.SaveChangesAsync(cancellation.Token);
              }
            }
          }
          catch(OperationCanceledException)
          {
            throw;
          }
          catch(ObjectDisposedException)
          {
            throw;
          }
          catch(Exception e)
          {
            Trace.TraceError("Discovery error. {0}", e);

            throw;
          }
          finally
          {
            semaphore.Release();
          }
        };

        using(var model = CreateModel())
        {
          await model.BankSystemMailboxes.
            Select(item => item.Email).
            Distinct().
            Except(model.InvalidMailboxes.Select(item => item.Email)).
            AsNoTracking().
            ForEachAsync(
              async email =>
              {
                if (group.Count >= groupSize)
                {
                  await semaphore.WaitAsync(cancellation.Token);

                  var task = discover(index++, group.ToArray());

                  group.Clear();
                }

                group.Add(email);
              },
              cancellation.Token);
        }

        if (group.Count > 0)
        {
          await semaphore.WaitAsync(cancellation.Token);

          var task = discover(index++, group.ToArray());
        }

        // Wait to complete pending tasks.
        for(var i = 0; semaphore.CurrentCount + i < parallelism; ++i)
        {
          await semaphore.WaitAsync(cancellation.Token);
        }
      }
    }

    /// <summary>
    /// Syncs all mailboxes.
    /// </summary>
    /// <param name="cancellation">A cancellation token source.</param>
    /// <returns>
    /// A task that completes when all mail boxes are in sync.
    /// </returns>
    private async Task SyncMailboxes(CancellationTokenSource cancellation)
    {
      var parallelism = 
        Settings.EWSMaxConcurrency * Settings.ApplicationUsers.Length;
      var index = 0;

      using(var semaphore = new SemaphoreSlim(parallelism))
      {
        Func<int, MailboxAffinity, Task> sync = async (i, mailbox) =>
        {
          try
          {
            await SyncMailbox(GetUser(i), mailbox, cancellation);
          }
          catch(OperationCanceledException)
          {
            throw;
          }
          catch(ObjectDisposedException)
          {
            throw;
          }
          catch(Exception e)
          {
            Trace.TraceError("Sync error. {0}", e);

            throw;
          }
          finally
          {
            semaphore.Release();
          }
        };

        using(var model = CreateModel())
        {
          await model.BankSystemMailboxes.
            Select(item => item.Email).
            Distinct().
            Except(model.InvalidMailboxes.Select(item => item.Email)).
            Join(
              model.MailboxAffinities,
              outer => outer,
              inner => inner.Email,
              (outer, inner) => inner).
            AsNoTracking().
            ForEachAsync(
              async mailbox =>
              {
                await semaphore.WaitAsync(cancellation.Token);

                var task = sync(index++, mailbox);
              },
              cancellation.Token);
        }

        // Wait to complete pending tasks.
        for (var i = 0; semaphore.CurrentCount + i < parallelism; ++i)
        {
          await semaphore.WaitAsync(cancellation.Token);
        }
      }
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
          var changes = await EwsUtils.TryAction(
            "Sync",
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
            cancellation.Token);

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
            await ListenMailboxes(GetUser(i), mailboxes, cancellation);
          }
          catch(OperationCanceledException)
          {
            throw;
          }
          catch(ObjectDisposedException)
          {
            throw;
          }
          catch(Exception e)
          {
            Trace.TraceError("Listen error. {0}", e);

            throw;
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
      Func<
        Office365.ExchangeService,
        MailboxAffinity,
        string,
        Task<Office365.StreamingSubscription>> subscribe =
        async (service, mailbox, anchorMailbox) =>
        {
          service.HttpHeaders.Add("X-AnchorMailbox", anchorMailbox);
          service.HttpHeaders.Add("X-PreferServerAffinity", "true");

          var folderIds = new List<Office365.FolderId>();

          folderIds.Add(Office365.WellKnownFolderName.Calendar);
          folderIds.Add(Office365.WellKnownFolderName.Inbox);

          try
          {
            return await EwsUtils.TryAction(
              "Subscribe",
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
                    catch (Exception e)
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
              cancellation.Token);
          }
          catch (OperationCanceledException)
          {
            throw;
          }
          catch (ObjectDisposedException)
          {
            throw;
          }
          catch (Office365.ServiceResponseException)
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

      var primaryEmail = null as string;
      var primaryService = null as Office365.ExchangeService;
      var primarySubscription = null as Office365.StreamingSubscription;
      var primaryIndex = 0;

      for(var i = 0; i < mailboxes.Length; ++i)
      {
        var mailbox = mailboxes[i];
        var service = GetService(user, mailbox);
        var subscription = await subscribe(service, mailbox, mailbox.Email);

        if (subscription != null)
        {
          primaryEmail = mailbox.Email;
          primaryService = service;
          primarySubscription = subscription;
          primaryIndex = i;

          break;
        }
      }

      var subscriptions = new Office365.StreamingSubscription[0];

      if (primarySubscription != null)
      {
        Trace.TraceInformation(
          "Subscribe to a group with primary mailbox: {0}, group size is: {1}",
          primaryEmail,
          mailboxes.Length - primaryIndex);

        var backEndOverrideCookie = primaryService.CookieContainer.
          GetCookies(primaryService.Url)["X-BackEndOverrideCookie"];

        subscriptions =
          new[] { primarySubscription }.
          Concat(
            await Task.WhenAll(
              mailboxes.
                Skip(primaryIndex + 1).
                Select(
                  mailbox =>
                  {
                    var service = GetService(user, mailbox);

                    service.CookieContainer.Add(
                      service.Url, 
                      backEndOverrideCookie);

                    return subscribe(service, mailbox, primaryEmail);
                  }))).
          Where(item => item != null).
          ToArray();
      }

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

        //if (!cancellation.IsCancellationRequested)
        //{
        //  cancellation.CancelAfter(60000);
        //}
      };

      connection.OnDisconnect += (sender, args) =>
      {
        Trace.TraceInformation(
          "Disconnection for a group with primary mailbox: {0}. {1}",
          primaryEmail, 
          args.Exception);

        if (!cancellation.IsCancellationRequested)
        {
          connection.Open();
        }
      };

      cancellation.Token.ThrowIfCancellationRequested();

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
    /// Gets a user for an index.
    /// </summary>
    /// <remarks>
    /// Distributes available users among multiple operations.
    /// </remarks>
    /// <param name="index">An index value.</param>
    /// <returns>Returns a user instance.</returns>
    private ApplicationUser GetUser(int index)
    {
      var users = Settings.ApplicationUsers;

      return users[index % users.Length];
    }

    /// <summary>
    /// A properies to retrieve during Sync.
    /// </summary>
    private static readonly Office365.PropertySet SyncProperties =  
      new Office365.PropertySet(
        Office365.ItemSchema.Id, 
        Office365.ItemSchema.LastModifiedTime);
  }
}