namespace Bnhp.Office365
{
  using System;
  using System.Linq;
  using System.Threading;
  using System.Diagnostics;
  using System.Net;
  using System.Collections.Generic;
  using System.Threading.Tasks;
  using System.Collections.Concurrent;
  using Microsoft.Exchange.WebServices.Autodiscover;

  using Office365 = Microsoft.Exchange.WebServices.Data;
  using System.Data.Entity;
  using System.Security.Principal;

  /// <summary>
  /// EWS utility API.
  /// </summary>
  public class EwsUtils
  {
    /// <summary>
    /// Verifies that a member is authorized to access a mail box.
    /// </summary>
    /// <param name="principal">A principal instance.</param>
    /// <param name="email">A mail box email.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A <see cref="Task"/> instance.</returns>
    /// <exception cref="UnauthorizedAccessException">
    /// In case a principal is not authorized.
    /// </exception>
    public static async Task VerifyMailboxAuthorized(
      IPrincipal principal,
      string email,
      CancellationToken cancellationToken = default(CancellationToken))
    {
      if (!await IsMailboxAuthorized(principal, email, cancellationToken))
      {
        throw new UnauthorizedAccessException();
      }
    }

    /// <summary>
    /// Tests whether the principal is authorized to access a mail box.
    /// </summary>
    /// <param name="principal">A principal instance.</param>
    /// <param name="email">A mail box email.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>true is action is authorized, and false othewise.</returns>
    public static async Task<bool> IsMailboxAuthorized(
      IPrincipal principal, 
      string email,
      CancellationToken cancellationToken = default(CancellationToken))
    {
      if (email == null)
      {
        return true;
      }

      var members = 
        await GetMailboxAuthorizedMembers(email, cancellationToken);

      if ((members.Length == 0) ||
        (principal == null) || 
        (principal.Identity == null) || 
        !principal.Identity.IsAuthenticated)
      {
        return false;
      }

      return members.Any(
        member => member.Name == "*" ? true :
          member.IsGroup ?
          principal.IsInRole(member.Name) :
          string.Compare(principal.Identity.Name, member.Name, true) == 0);
    }

    public class MemberImpl : Member { }

    /// <summary>
    /// Gets members authorized to access a mail box.
    /// </summary>
    /// <param name="email">A mail box to check.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A enumeration of users or groups authorized to access a mail box.</returns>
    public static async Task<Member[]> GetMailboxAuthorizedMembers(
      string email,
      CancellationToken cancellationToken = default(CancellationToken))
    {
      using(var model = new EWSQueueEntities())
      {
        return await model.BankSystemMailboxes.
          Where(item => item.Email == email).
          Select(item => item.GroupName).
          Distinct().
          GroupJoin(
            model.BankSystemRights,
            groupName => groupName,
            item => item.GroupName,
            (groupName, items) => items).
          SelectMany(
            items => items.DefaultIfEmpty(),
            (items, item) =>
              new MemberImpl
              {
                Name = item == null ? "*" : item.MemberName,
                IsGroup = item == null ? true : item.IsGroup
              }).
          Distinct().
          ToArrayAsync(cancellationToken);
      }
    }

    /// <summary>
    /// Gets user settings using AutoDiscovery service.
    /// </summary>
    /// <param name="user">
    /// An application user.
    /// </param>
    /// <param name="url">
    /// An AutoDiscovery service url.
    /// </param>
    /// <param name="emails">Emails to get users' settings for.</param>
    /// <param name="settings">Settings to request.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>
    /// Enumerable of mailbox affinities. 
    /// Items returned are not necessary in the order of input emails.
    /// Only resolved emails are returned.
    /// </returns>
    public static IEnumerable<MailboxAffinity> GetMailboxAffinities(
      ApplicationUser user,
      string url,
      IEnumerable<string> emails)
    {
      var service = new AutodiscoverService
      {
        Url = new Uri(url),
        Credentials = new Office365.WebCredentials(user.Email, user.Password),
        //EnableScpLookup = false,
        RedirectionUrlValidationCallback = value => true
      };

      var results = service.GetUsersSettings(
        emails,
        UserSettingName.GroupingInformation,
        UserSettingName.ExternalEwsUrl);

      return results.
        Where(item => item.ErrorCode == AutodiscoverErrorCode.NoError).
        Select(
          item => new MailboxAffinity
          {
            Email = item.SmtpAddress,
            ExternalEwsUrl =
              item.Settings[UserSettingName.ExternalEwsUrl] as string,
            GroupingInformation =
              item.Settings[UserSettingName.GroupingInformation] as string
          });
    }

    /// <summary>
    /// Performs an action a specified number of times.
    /// </summary>
    /// <typeparam name="T">A result type.</typeparam>
    /// <param name="name">Action name.</param>
    /// <param name="email">A mailbox.</param>
    /// <param name="service">An exchange service.</param>
    /// <param name="action">Action function.</param>
    /// <param name="settings">A Settings instance.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>Actio result.</returns>
    public static async Task<T> TryAction<T>(
      string name,
      string email,
      Office365.ExchangeService service,
      Func<int, Task<T>> action,
      Settings settings,
      CancellationToken cancellationToken = default(CancellationToken))
    {
      var key = "";

      if (service != null)
      {
        var webCredentials = service.Credentials as Office365.WebCredentials;
        var networkCredential = webCredentials.Credentials as NetworkCredential;

        key = networkCredential.UserName;
      }

      var semaphore = GetSemaphore(key, settings.EWSMaxConcurrency);

      var retryCount = settings.RetryCount;

      if (retryCount <= 0)
      {
        retryCount = 1;
      }

      for(var i = 0; i < retryCount; ++i)
      {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
          await semaphore.WaitAsync(
            TimeSpan.FromMinutes(settings.RequestTimeout), 
            cancellationToken);

          try
          {
            return await action(i);
          }
          finally
          {
            semaphore.Release();
          }
        }
        catch(Exception e)
        {
          var warning = (i + 1 < retryCount) && IsRetryable(e);

          Log(warning, name, email, e);

          if (!warning)
          {
            throw;
          }
        }

        if (i == 0)
        {
          await Task.Delay(Random(500, 2000), cancellationToken);
        }
        else if (i == 1)
        {
          await Task.Delay(Random(2000, 5000), cancellationToken);
        }
        else
        {
          await Task.Delay(Random(5000, 10000), cancellationToken);
        }
      }

      throw new InvalidOperationException();
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
    public static int Random(int minValue, int maxValue)
    {
      lock (sync)
      {
        return random.Next(minValue, maxValue);
      }
    }

    /// <summary>
    /// Tests whether an error is retryable.
    /// </summary>
    /// <param name="exception">An exception instance.</param>
    /// <returns>True if error is retryable, and false otherwise.</returns>
    public static bool IsRetryable(Exception exception)
    { 
      var serviceResponseException = 
        exception as Office365.ServiceResponseException;

      if (serviceResponseException != null)
      {
        if (serviceResponseException is Office365.ServerBusyException)
        {
          return true;
        }

        switch (serviceResponseException.ErrorCode)
        {
          case Office365.ServiceError.ErrorMailboxStoreUnavailable:
          case Office365.ServiceError.ErrorInternalServerError:
          case Office365.ServiceError.ErrorInternalServerTransientError:
          case Office365.ServiceError.ErrorNoRespondingCASInDestinationSite:
          case Office365.ServiceError.ErrorTooManyObjectsOpened:
          {
            return true;
          }
        }

        return false;
      }

      var serviceRequestException = 
        exception as Office365.ServiceRequestException;

      if (serviceRequestException != null)
      {
        var webException = 
          serviceRequestException.InnerException as WebException;
        var webResponse = webException == null ? null :
          webException.Response as HttpWebResponse;

        if (webResponse != null)
        {
          return webResponse.StatusCode != HttpStatusCode.Unauthorized;
        }

        return true;
      }

      var autodiscoverResponseException = 
        exception as AutodiscoverResponseException;

      if (autodiscoverResponseException != null)
      {
        switch(autodiscoverResponseException.ErrorCode)
        {
          case AutodiscoverErrorCode.ServerBusy:
          case AutodiscoverErrorCode.InternalServerError:
          {
            return true;
          }
        }

        return false;
      }

      return false;
    }

    /// <summary>
    /// Gets an error code for an exception, if available.
    /// </summary>
    /// <param name="exception">An exception instance.</param>
    /// <returns>
    /// An error code, or null if no error code is available.
    /// </returns>
    public static object GetErrorCode(Exception exception)
    {
      var serviceResponseException = 
        exception as Office365.ServiceResponseException;

      if (serviceResponseException != null)
      {
        return serviceResponseException.ErrorCode;
      }

      var serviceRequestException = 
        exception as Office365.ServiceRequestException;

      if (serviceRequestException != null)
      {
        var webException =
          serviceRequestException.InnerException as WebException;
        var webResponse = webException == null ? null :
          webException.Response as HttpWebResponse;

        if (webResponse != null)
        {
          return webResponse.StatusCode;
        }

        return null;
      }

      var autodiscoverResponseException = 
        exception as AutodiscoverResponseException;

      if (autodiscoverResponseException != null)
      {
        return autodiscoverResponseException.ErrorCode;
      }

      return null;
    }

    /// <summary>
    /// Logs an error.
    /// </summary>
    /// <param name="warning">
    /// Warning indicator.
    /// </param>
    /// <param name="name">Action name.</param>
    /// <param name="email">A mailbox.</param>
    /// <param name="exception">An exception instance.</param>
    public static void Log(
      bool warning,
      string name,
      string email,
      Exception exception)
    {
      if ((exception is OperationCanceledException) ||
        (exception is ObjectDisposedException) ||
        (exception is ThreadAbortException))
      {
        return;
      }

      var errorCode = GetErrorCode(exception);
      var message = email == null ?
        errorCode == null ?
          "{0} failed. {2}" :
          "{0} failed, errorCode = {3}. {2}" :
        errorCode == null ?
          "{0} failed for a mailbox: {1}. {2}" :
          "{0} failed for a mailbox: {1}, errorCode = {3}. {2}";

      if (warning)
      {
        Trace.TraceWarning(
          message,
          name,
          email,
          exception,
          errorCode);
      }
      else
      {
        Trace.TraceError(
          message,
          name,
          email,
          exception,
          errorCode);
      }
    }

    /// <summary>
    /// Gets a global semaphore.
    /// </summary>
    /// <param name="key">A semaphore id.</param>
    /// <param name="maxCount">
    /// A maximum number of requests for the semaphore that 
    /// can be granted concurrently.
    /// </param>
    /// <returns></returns>
    public static SemaphoreSlim GetSemaphore(string key, int maxCount)
    {
      return semaphores.GetOrAdd(key, item => new SemaphoreSlim(maxCount));
    }

    /// <summary>
    /// Global lock.
    /// </summary>
    private static object sync = new object();

    /// <summary>
    /// Random used to generate delays.
    /// </summary>
    private static Random random = new Random();

    /// <summary>
    /// Global semaphores.
    /// </summary>
    private static ConcurrentDictionary<string, SemaphoreSlim> semaphores = 
      new ConcurrentDictionary<string, SemaphoreSlim>();
  }
}