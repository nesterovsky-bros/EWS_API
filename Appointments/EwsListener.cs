namespace Bnhp.Office365
{
  using System;
  using System.Linq;
  using System.Collections.Generic;
  using System.Data.Entity;
  using System.Threading.Tasks;
  using Microsoft.Practices.Unity;
  using System.Threading;

  using Office365 = Microsoft.Exchange.WebServices.Data;
  using System.Diagnostics;
  using System.Collections.Concurrent;
  using System.Net.Http;

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
    public IEwsService Service { get; set; }

    /// <summary>
    /// Starts the listener.
    /// </summary>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>A task associated with the listener task.</returns>
    public async Task Start(
      CancellationToken cancellationToken = default(CancellationToken))
    {
      while (true)
      {
        using (var cancellation =
          CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
        {
          cancellation.CancelAfter(
            TimeSpan.FromMinutes(Settings.ExchangeListenerRecyclePeriod));

          try
          {
            var watch = new Stopwatch();

            Trace.TraceInformation("Starting EWS listener.");

            Trace.TraceInformation("Start expanding groups.");
            watch.Start();
            await ExpandGroups(cancellation);
            watch.Stop();
            Trace.TraceInformation(
              "End expanding groups; elasped: {0}.",
              watch.Elapsed);

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
          catch (OperationCanceledException)
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
    /// Expand groups.
    /// </summary>
    /// <param name="cancellation">A cancellation token source.</param>
    /// <returns>
    /// A task that completes when expand groups completes.
    /// </returns>
    private async Task ExpandGroups(CancellationTokenSource cancellation)
    {
      // Discover application users.
      var users = Settings.ApplicationUsers.Select(item => item.Email).ToArray();
      var usersAffinities = null as MailboxAffinity[];

      await DiscoverMailboxes(
        users,
        cancellation);

      // GetAppointment their affinity.
      using (var model = CreateModel())
      {
        usersAffinities = await model.MailboxAffinities.
          Where(item => users.Contains(item.Email)).
          ToArrayAsync(cancellation.Token);
      }

      if (usersAffinities.Length != users.Length)
      {
        throw new InvalidOperationException(
          "No all application users are discovered.");
      }

      var parallelism = Math.Max(1, Settings.EWSMaxConcurrency / 2);
      var index = 0;

      using (var semaphore = new SemaphoreSlim(parallelism))
      {
        Func<int, BankSystem, Task> expand = async (i, bankSystem) =>
        {
          try
          {
            var user = GetUser(i);
            var service =
              GetService(user, usersAffinities[i % usersAffinities.Length]);

            await EwsUtils.TryAction(
              "ExpandGroup",
              bankSystem.GroupName,
              service,
              async attempt =>
              {
                await Task.Yield();

                var results = service.ExpandGroup(bankSystem.GroupName).
                  Select(item => item.Address).
                  ToDictionary(item => item);

                using (var model = CreateModel())
                {
                  var existing = model.BankSystemMailboxes.
                    Where(item => item.GroupName == bankSystem.GroupName).
                    ToDictionary(item => item.Email);

                  model.BankSystemMailboxes.RemoveRange(
                    existing.Values.
                      Where(item => !results.ContainsKey(item.Email)));

                  model.BankSystemMailboxes.AddRange(
                    results.Values.
                      Where(email => !existing.ContainsKey(email)).
                      Select(
                        email =>
                          new BankSystemMailbox
                          {
                            GroupName = bankSystem.GroupName,
                            Email = email
                          }));

                  await model.SaveChangesAsync(cancellation.Token);
                }

                return true;
              },
              Settings,
              cancellation.Token);
          }
          catch (Exception e)
          {
            EwsUtils.Log(true, "ExpandGroup", null, e);

            throw;
          }
          finally
          {
            semaphore.Release();
          }
        };

        var bankSystems = null as BankSystem[];

        using (var model = CreateModel())
        {
          bankSystems = await model.BankSystems.
            Where(item => !item.Local).
            ToArrayAsync(cancellation.Token);
        }

        foreach(var bankSystem in bankSystems)
        {
          await semaphore.WaitAsync(cancellation.Token);

          var task = expand(index++, bankSystem);
        }

        // Wait to complete pending tasks.
        for (var i = 0; semaphore.CurrentCount + i < parallelism; ++i)
        {
          await semaphore.WaitAsync(cancellation.Token);
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
      var parallelism = Math.Max(1, Settings.EWSMaxConcurrency / 2);
      var index = 0;

      using(var semaphore = new SemaphoreSlim(parallelism))
      {
        using (var model = CreateModel())
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

                  var task = Discover(
                    index++,
                    group.ToArray(),
                    semaphore,
                    cancellation);

                  group.Clear();
                }

                group.Add(email);
              },
              cancellation.Token);
        }

        if (group.Count > 0)
        {
          await semaphore.WaitAsync(cancellation.Token);

          var task = Discover(
            index++,
            group.ToArray(),
            semaphore,
            cancellation);
        }

        // Wait to complete pending tasks.
        for (var i = 0; semaphore.CurrentCount + i < parallelism; ++i)
        {
          await semaphore.WaitAsync(cancellation.Token);
        }
      }
    }

    /// <summary>
    /// Discovers all mailboxes.
    /// </summary>
    /// <param name="emails">A list of unique emails to autodiscover.</param>
    /// <param name="cancellation">A cancellation token source.</param>
    /// <returns>A task that completes after discovery.</returns>
    private async Task DiscoverMailboxes(
      IEnumerable<string> emails,
      CancellationTokenSource cancellation)
    {
      var groupSize = Settings.UsersPerUsersSettins;
      var group = new List<string>(groupSize);
      var parallelism = Math.Max(1, Settings.EWSMaxConcurrency / 2);
      var index = 0;

      using (var semaphore = new SemaphoreSlim(parallelism))
      {
        foreach (var email in emails)
        {
          if (group.Count >= groupSize)
          {
            await semaphore.WaitAsync(cancellation.Token);

            var task = Discover(
              index++,
              group.ToArray(),
              semaphore,
              cancellation);

            group.Clear();
          }

          group.Add(email);
        }

        if (group.Count > 0)
        {
          await semaphore.WaitAsync(cancellation.Token);

          var task = Discover(
            index++,
            group.ToArray(),
            semaphore,
            cancellation);
        }

        // Wait to complete pending tasks.
        for (var i = 0; semaphore.CurrentCount + i < parallelism; ++i)
        {
          await semaphore.WaitAsync(cancellation.Token);
        }
      }
    }

    /// <summary>
    /// Disvocers an array of mailboxes.
    /// </summary>
    /// <param name="index">Iteration index.</param>
    /// <param name="emails">Array of emails.</param>
    /// <param name="semaphore">Async semaphore.</param>
    /// <param name="cancellation">Cancellation source.</param>
    /// <returns>Tasks that complete after discover.</returns>
    private async Task Discover(
      int index,
      string[] emails,
      SemaphoreSlim semaphore,
      CancellationTokenSource cancellation)
    {
      try
      {
        var user = GetUser(index);

        var mailboxes = (await EwsUtils.TryAction(
          "Discover",
          emails[0],
          null,
          async attempt =>
          {
            await Task.Yield();

            return EwsUtils.GetMailboxAffinities(user, Settings.AutoDiscoveryUrl, emails);
          },
          Settings,
          cancellation.Token)).
          ToDictionary(item => item.Email);

        using (var model = CreateModel())
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
      catch (Exception e)
      {
        EwsUtils.Log(true, "Discovery", null, e);

        throw;
      }
      finally
      {
        semaphore.Release();
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
      var parallelism = Math.Min(
        100,
        Math.Max(
          1, 
          Settings.EWSMaxConcurrency * Settings.ApplicationUsers.Length / 2));

      var index = 0;

      using (var semaphore = new SemaphoreSlim(parallelism))
      {
        Func<int, MailboxAffinity, Task> sync = async (i, mailbox) =>
        {
          try
          {
            await SyncMailbox(GetUser(i), mailbox, cancellation);
          }
          catch (Exception e)
          {
            EwsUtils.Log(true, "Sync", null, e);

            throw;
          }
          finally
          {
            semaphore.Release();
          }
        };

        using (var model = CreateModel())
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

      var folderIDs =
        await GetNotificationFolders(mailbox.Email, cancellation);

      if (folderIDs.Length == 0)
      {
        return;
      }

      var service = GetService(user, mailbox);

      foreach (var folderID in folderIDs)
      {
        var folderName = folderID.FolderName.ToString();
        string state;

        using (var model = CreateModel())
        {
          state = await model.MailboxSyncs.
            Where(
              item =>
                (item.Email == mailbox.Email) &&
                (item.FolderID == folderName)).
            Select(item => item.SyncState).
            FirstOrDefaultAsync(cancellation.Token);
        }

        var newState = await SyncMailbox(
          mailbox,
          service,
          folderID,
          state,
          cancellation);

        if (state != newState)
        {
          using (var model = CreateModel())
          {
            var item = new MailboxSync
            {
              Email = mailbox.Email,
              FolderID = folderName,
              SyncState = newState
            };

            model.Entry(item).State =
              newState == null ? EntityState.Deleted :
              state == null ? EntityState.Added :
              EntityState.Modified;

            await model.SaveChangesAsync(cancellation.Token);
          }
        }
      }
    }

    /// <summary>
    /// Syncs and updates a mail box.
    /// </summary>
    /// <param name="service">A service instance.</param>
    /// <param name="events">A enumeration of events.</param>
    /// <param name="cancellation">A cancellation token source.</param>
    /// <returns>Task instance.</returns>
    private async Task SyncAndUpdateMailbox(
      Office365.ExchangeService service,
      IEnumerable<Office365.NotificationEvent> events,
      CancellationTokenSource cancellation)
    {
      await Task.Yield();

      var folders = new Dictionary<string, Office365.Folder>();

      var notifications = events.OfType<Office365.ItemEvent>().
        GroupBy(item => item.ParentFolderId).
        Select(group => group.First()).
        Select(
          item =>
          {
            var parentID = item.ParentFolderId.UniqueId;

            var folder = folders.Get(parentID) ??
              (folders[parentID] =
                Office365.Folder.Bind(service, parentID, FolderProperties));

            return new MailboxNotification
            {
              Timestamp = item.TimeStamp,
              ItemID = item.ItemId.UniqueId,
              FolderID = folder.WellKnownFolderName.ToString(),
              Email = service.ImpersonatedUserId.Id,
              ChangeType =
                (item.EventType == Office365.EventType.NewMail) ||
                (item.EventType == Office365.EventType.Created) ?
                  ChangeType.Created.ToString() :
                  item.EventType == Office365.EventType.Deleted ?
                  ChangeType.Deleted.ToString() :
                  ChangeType.Updated.ToString()
            };
          }).
        ToArray();

      var callbacks = null as string[];

      using (var model = CreateModel())
      {
        model.MailboxNotifications.AddRange(notifications);

        await model.SaveChangesAsync(cancellation.Token);

        callbacks = await GetCallbacks(model, notifications, cancellation);
      }

      TriggerCallbacks(callbacks, cancellation);
    }

    /// <summary>
    /// Syncs a mail box.
    /// </summary>
    /// <param name="mailbox">A mailbox to synchronize.</param>
    /// <param name="service">An Exchange service.</param>
    /// <param name="folderID">A folder id.</param>
    /// <param name="syncState">A folder SyncState.</param>
    /// <param name="cancellation">A cancellation token source.</param>
    /// <returns>A new syncState value.</returns>
    private async Task<string> SyncMailbox(
      MailboxAffinity mailbox,
      Office365.ExchangeService service,
      Office365.FolderId folderID,
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
            service,
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
                  catch (Exception e)
                  {
                    source.SetException(e);
                  }
                },
                null,
                folderID,
                SyncProperties,
                null,
                512,
                Office365.SyncFolderItemsScope.NormalItems,
                state);

              return source.Task;
            },
            Settings,
            cancellation.Token);

          if (changes.Count > 0)
          {
            var notifications = changes.Select(
              change => new MailboxNotification
              {
                Timestamp = change.Item.LastModifiedTime,
                Email = mailbox.Email,
                FolderID = folderID.FolderName.ToString(),
                ItemID = change.ItemId.UniqueId,
                ChangeType =
                  change.ChangeType == Office365.ChangeType.Create ?
                    ChangeType.Created.ToString() :
                  change.ChangeType == Office365.ChangeType.Delete ?
                    ChangeType.Deleted.ToString() :
                    ChangeType.Updated.ToString()
              }).
              ToArray();

            var callbacks = null as string[];

            using (var model = CreateModel())
            {
              notifications = notifications.
                Where(
                  outer => !model.MailboxNotifications.
                    Any(
                      inner =>
                        (outer.Timestamp == inner.Timestamp) &&
                        (outer.Email == inner.Email) &&
                        (outer.ItemID == inner.ItemID))).
                ToArray();

              model.MailboxNotifications.AddRange(notifications);

              await model.SaveChangesAsync(cancellation.Token);

              callbacks =
                await GetCallbacks(model, notifications, cancellation);
            }

            TriggerCallbacks(callbacks, cancellation);
          }

          state = changes.SyncState;
          hasMore = changes.MoreChangesAvailable;
        }
        catch
        {
          return state == syncState ? null : state;
        }
      }
      while (hasMore);

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

      using (var semaphore = new SemaphoreSlim(parallelism))
      using (var model = CreateModel())
      {
        Func<int, MailboxAffinity[], Task> listen = async (i, mailboxes) =>
        {
          try
          {
            await ListenMailboxes(GetUser(i), mailboxes, cancellation);
          }
          catch (Exception e)
          {
            EwsUtils.Log(true, "Listen", null, e);

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
        for (var i = 0; semaphore.CurrentCount + i < parallelism; ++i)
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

          var folderIDs =
            await GetNotificationFolders(mailbox.Email, cancellation);

          try
          {
            return await EwsUtils.TryAction(
              "Subscribe",
              mailbox.Email,
              service,
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
                  folderIDs,
                  Office365.EventType.NewMail,
                  Office365.EventType.Created,
                  Office365.EventType.Deleted,
                  Office365.EventType.Modified);

                return source.Task;
              },
              Settings,
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

      var groupingInformation = null as string;
      var primaryEmail = null as string;
      var primaryService = null as Office365.ExchangeService;
      var primarySubscription = null as Office365.StreamingSubscription;
      var primaryIndex = 0;

      for (var i = 0; i < mailboxes.Length; ++i)
      {
        var mailbox = mailboxes[i];
        var service = GetService(user, mailbox);
        var subscription = await subscribe(service, mailbox, mailbox.Email);

        if (subscription != null)
        {
          groupingInformation = mailbox.GroupingInformation;
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
          "Subscribe to a group {0} with primary mailbox: {1}, group size is: {2}",
          groupingInformation,
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

      using (var model = CreateModel())
      {
        foreach (var mailbox in mailboxes)
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

        // Note: fire and forget task.
        var syncTask = SyncAndUpdateMailbox(
          args.Subscription.Service,
          args.Events,
          cancellation);
      };

      connection.OnSubscriptionError += (sender, args) =>
      {
        var subscription = args.Subscription;
        var service = subscription == null ? null : subscription.Service;
        var email = service == null ? null : service.ImpersonatedUserId.Id;

        if (email != null)
        {
          Trace.TraceWarning(
            "Subscription error for a mailbox: {0}. {1}",
            email,
            args.Exception);
        }
        else
        {
          Trace.TraceWarning(
            "Subscription error for a group {0} with primary mailbox: {1}. {2}",
            groupingInformation,
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
    /// Gets an array of folders to notify for a email.
    /// </summary>
    /// <param name="email">A email.</param>
    /// <param name="cancellation">A cancellation source.</param>
    /// <returns>A task producing an array of folder ids.</returns>
    private async Task<Office365.FolderId[]> GetNotificationFolders(
      string email,
      CancellationTokenSource cancellation)
    {
      string[] folderNames;

      using (var model = CreateModel())
      {
        folderNames = await model.BankSystemNotifications.
          Join(
            model.BankSystemMailboxes.
              Where(item => item.Email == email),
            outer => outer.GroupName,
            inner => inner.GroupName,
            (outer, inner) => outer.FolderID).
          Distinct().
          AsNoTracking().
          ToArrayAsync(cancellation.Token);
      }

      var folderIDs = new List<Office365.FolderId>(folderNames.Length);

      foreach (var folderName in folderNames)
      {
        Office365.WellKnownFolderName folderID;

        if (Enum.TryParse(folderName, out folderID))
        {
          folderIDs.Add(folderID);
        }
      }

      return folderIDs.ToArray();
    }

    private static async Task<string[]> GetCallbacks(
      EWSQueueEntities model,
      MailboxNotification[] notifications,
      CancellationTokenSource cancellation)
    {
      if ((notifications == null) || (notifications.Length == 0))
      {
        return new string[0];
      }

      return await model.BankSystemMailboxes.
        Join(
          notifications.Select(item => item.Email),
          outer => outer.Email,
          inner => inner,
          (outer, inner) => outer).
        Join(
          model.BankSystems,
          outer => outer.GroupName,
          inner => inner.GroupName,
          (outer, inner) => inner).
        Select(item => item.CallbackURL).
        Where(item => item != null).
        Distinct().
        AsNoTracking().
        ToArrayAsync(cancellation.Token);
    }

    private void TriggerCallbacks(
      string[] callbacks, 
      CancellationTokenSource cancellation)
    {
      if ((callbacks == null) || (callbacks.Length == 0))
      {
        return;
      }

      var delay = 1000;

      foreach (var callback in callbacks)
      {
        var callbackCancellation = null as CancellationTokenSource;

        if (pendingCallbacks.TryRemove(callback, out cancellation))
        {
          callbackCancellation.Cancel();
        }

        pendingCallbacks.GetOrAdd(
          callback,
          url =>
          {
            callbackCancellation =
              CancellationTokenSource.CreateLinkedTokenSource(cancellation.Token);

            Task.Run(
              async () =>
              {
                await Task.Delay(delay);

                using(var client = new HttpClient())
                {
                  await client.GetAsync(url, callbackCancellation.Token);
                }
              },
              callbackCancellation.Token);

            return callbackCancellation;
          });
      }
    }

    /// <summary>
    /// Pending callbacks.
    /// </summary>
    public ConcurrentDictionary<string, CancellationTokenSource> pendingCallbacks = 
      new ConcurrentDictionary<string, CancellationTokenSource>();

    /// <summary>
    /// A properies to retrieve during Sync.
    /// </summary>
    private static readonly Office365.PropertySet SyncProperties =  
      new Office365.PropertySet(
        Office365.ItemSchema.Id, 
        Office365.ItemSchema.LastModifiedTime);

    /// <summary>
    /// A properies to retrieve during notification.
    /// </summary>
    private static readonly Office365.PropertySet FolderProperties =
      new Office365.PropertySet(Office365.FolderSchema.WellKnownFolderName);
  }
}