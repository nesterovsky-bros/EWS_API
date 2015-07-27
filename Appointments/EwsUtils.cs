﻿namespace Bnhp.Office365
{
  using System;
  using System.Linq;
  using System.Threading;
  using System.Diagnostics;
  using System.Net;
  using System.Collections.Generic;
  using System.Runtime.Serialization;
  using System.Threading.Tasks;
  using Microsoft.Exchange.WebServices.Autodiscover;

  using Office365 = Microsoft.Exchange.WebServices.Data;
  using System.Runtime.ExceptionServices;
  
  /// <summary>
  /// EWS utility API.
  /// </summary>
  public class EwsUtils
  {
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
    /// <param name="action">Action function.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <param name="retryCount">Optional retry count. Default is 3.</param>
    /// <returns>Actio result.</returns>
    public static async Task<T> TryAction<T>(
      string name,
      string email,
      Func<int, Task<T>> action,
      CancellationToken cancellationToken,
      int retryCount = 3)
    {
      if (retryCount <= 0)
      {
        throw new ArgumentException("retryCount");
      }

      for(var i = 0; i < retryCount; ++i)
      {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
          return await action(i);
        }
        catch(OperationCanceledException)
        {
          throw;
        }
        catch(ObjectDisposedException)
        {
          throw;
        }
        catch(Office365.ServiceResponseException e)
        {
          switch(e.ErrorCode)
          {
            case Office365.ServiceError.ErrorMailboxStoreUnavailable:
            case Office365.ServiceError.ErrorInternalServerError:
            case Office365.ServiceError.ErrorInternalServerTransientError:
            case Office365.ServiceError.ErrorNoRespondingCASInDestinationSite:
            {
              if (Log(name, email, e, e.ErrorCode, true, i, retryCount))
              {
                throw;
              }

              break;
            }
            default:
            {
              Log(name, email, e, e.ErrorCode);

              throw;
            }
          }
        }
        catch(Office365.ServiceRequestException e)
        {
          var webException = e.InnerException as WebException;
          var webResponse = webException == null ? null :
            webException.Response as HttpWebResponse;

          if (webResponse != null)
          {
            if (Log(
              name, 
              email, 
              e, 
              webResponse.StatusCode, 
              webResponse.StatusCode != HttpStatusCode.Unauthorized, 
              i, 
              retryCount))
            {
              throw;
            }
          }
          else
          {
            if (Log(name, email, e, null, true, i, retryCount))
            {
              throw;
            }
          }
        }
        catch(AutodiscoverResponseException e)
        {
          if (Log(
            name, 
            email, 
            e, 
            e.ErrorCode, 
            (e.ErrorCode == AutodiscoverErrorCode.ServerBusy) ||
              (e.ErrorCode == AutodiscoverErrorCode.InternalServerError), 
            i, 
            retryCount))
          {
            throw;
          }
        }
        catch(Exception e)
        {
          Log(name, email, e, null);

          throw;
        }

        await Task.Delay(Random(500, 2000), cancellationToken);
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
    /// Logs an error.
    /// </summary>
    /// <param name="name">Action name.</param>
    /// <param name="email">A mailbox.</param>
    /// <param name="exception">An exception instance.</param>
    /// <param name="errorCode">Optional error code.</param>
    /// <param name="warning">
    /// Optional warning indicator. Default is false.
    /// </param>
    /// <param name="attempt">Optional try attempt. Default is 0.</param>
    /// <param name="retryCount">
    /// Optional number of attempts. Default is 2.
    /// </param>
    /// <returns>
    /// true to throw an error; and false to continue attempts.
    /// </returns>
    private static bool Log(
      string name,
      string email,
      Exception exception,
      object errorCode,
      bool warning = false,
      int attempt = 0,
      int retryCount = 1)
    {
      var message = errorCode == null ?
        "{0} failed for a mailbox: {1}. {2}" :
        "{0} failed for a mailbox: {1}, errorCode = {3}. {2}";

      if (warning && (attempt + 1 < retryCount))
      {
        Trace.TraceWarning(
          message,
          name,
          email,
          exception,
          errorCode);

        return false;
      }
      else
      {
        Trace.TraceError(
          message,
          name,
          email,
          exception,
          errorCode);

        return true;
      }
    }

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