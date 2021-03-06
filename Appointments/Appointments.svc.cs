﻿namespace Bnhp.Office365
{
  using System;
  using System.Collections.Generic;
  using System.Configuration;
  using System.IO;
  using System.Linq;
  using System.Threading;
  using System.Runtime.Serialization;
  using System.ServiceModel;
  using System.ServiceModel.Web;
  using System.Text;
  using System.Text.RegularExpressions;
  using System.Xml;
  using System.Threading.Tasks;
  using System.Data.Entity;
  
  using Microsoft.Practices.Unity;
  using Microsoft.Exchange.WebServices.Autodiscover;

  using Office365 = Microsoft.Exchange.WebServices.Data;
  
  /// <summary>
  /// An implementation of IAppointments interface for CRUD operations with
  /// appointments for Office365.
  /// </summary>
  [Obsolete]
  [ServiceBehavior(Namespace = "https://www.bankhapoalim.co.il/")]
  public class Appointments : IAppointments
  {
    /// <summary>
    /// A settings instance.
    /// </summary>
    [Dependency]
    public Settings Settings { get; set; }

    /// <summary>
    /// A response notifier.
    /// </summary>
    [Dependency]
    public IResponseNotifier ResponseNotifier { get; set; }

    #region Create method
    /// <summary>
    /// Creates a new proxy/proxy and sends notifications to attendees.
    /// </summary>
    /// <param name="email">An e-mail address of the organizer.</param>
    /// <param name="proxy">
    /// an AppointmentProxy instance with data for the proxy.
    /// </param>
    /// <returns>An unique ID of the new proxy.</returns>
    /// <exception cref="Exception">in case of error.</exception>
    public string Create(string email, Appointment appointment)
    {
      return Call(
        "Create",
        new CreateRequest
        {
          email = email,
          appointment = appointment
        },
        CreateImpl);
    }

    public long CreateBegin(string email, Appointment appointment)
    {
      return CallAsync(
        "Create",
        new CreateRequest
        {
          email = email,
          appointment = appointment
        },
        CreateImpl);
    }

    public string CreateEnd(long requestID)
    {
      return ReadResult<string>(requestID);
    }

    public struct CreateRequest
    {
      public string email;
      public Appointment appointment;
    }

    private string CreateImpl(CreateRequest request)
    {
      if (request.appointment == null)
      {
        throw new ArgumentNullException("request.proxy");
      }
      
      var email = request.email;
      var service = GetService(email);
      var appointment = new Office365.Appointment(service);
      var proxy = request.appointment;

      // Set the properties on the proxy object to create the proxy.
      appointment.Subject = proxy.Subject;

      if (!string.IsNullOrEmpty(proxy.TextBody))
      {
        appointment.Body = new Office365.MessageBody(
          IsHtml.IsMatch(proxy.TextBody) ?
            Office365.BodyType.HTML : Office365.BodyType.Text,
          proxy.TextBody);
      }

      appointment.Start = proxy.Start;
      appointment.End = proxy.End;
      appointment.Location = proxy.Location;
      appointment.AllowNewTimeProposal = true;
      appointment.Importance = (Office365.Importance)proxy.Importance;
      appointment.ReminderMinutesBeforeStart = proxy.ReminderMinutesBeforeStart;

      if ((proxy.Recurrence != null) && 
        (proxy.Recurrence.Type != RecurrenceType.Unknown))
      {
        var start = proxy.Recurrence.StartDate;

        switch (proxy.Recurrence.Type)
        {
          case RecurrenceType.Daily:
          {
            appointment.Recurrence = new Office365.Recurrence.DailyPattern(
              start,
              proxy.Recurrence.Interval);

            break;
          }
          case RecurrenceType.Weekly:
          {
            appointment.Recurrence = new Office365.Recurrence.WeeklyPattern(
              start,
              proxy.Recurrence.Interval,
              (Office365.DayOfTheWeek)start.DayOfWeek);

            break;
          }
          case RecurrenceType.Monthly:
          {
            appointment.Recurrence = new Office365.Recurrence.MonthlyPattern(
              start,
              proxy.Recurrence.Interval,
              start.Day);

            break;
          }
          case RecurrenceType.Yearly:
          {
            appointment.Recurrence =
              new Office365.Recurrence.YearlyPattern(
                start,
                (Office365.Month)start.Month,
                start.Day);

            break;
          }
        }

        if (proxy.Recurrence.HasEnd)
        {
          appointment.Recurrence.EndDate = proxy.Recurrence.EndDate;
        }
        else
        {
          appointment.Recurrence.NumberOfOccurrences = proxy.Recurrence.NumberOfOccurrences;
        }
      }

      if (proxy.RequiredAttendees != null)
      {
        foreach (var attendee in proxy.RequiredAttendees)
        {
          appointment.RequiredAttendees.Add(attendee.Address);
        }
      }

      if (proxy.OptionalAttendees != null)
      {
        foreach (var attendee in proxy.OptionalAttendees)
        {
          appointment.OptionalAttendees.Add(attendee.Address);
        }
      }

      if (proxy.Resources != null)
      {
        foreach (var resource in proxy.Resources)
        {
          appointment.Resources.Add(resource.Address);
        }
      }

      SetExtendedProperties(appointment, proxy.ExtendedProperties);

      appointment.ICalUid = 
        Guid.NewGuid().ToString() + email.Substring(email.IndexOf('@'));

      // SendMessage the proxy request
      appointment.Save(Office365.SendInvitationsMode.SendToAllAndSaveCopy);

      return appointment.Id.ToString();
    }
    #endregion

    #region Get method
    /// <summary>
    /// Retrieves all appointments in the specified range of dates.
    /// </summary>
    /// <param name="email">a target user's e-mail.</param>
    /// <param name="start">a start date.</param>
    /// <param name="end">an optional parameter, determines an end date.</param>
    /// <param name="maxResults">
    /// an optional parameter, determines maximum results in resonse.
    /// </param>
    /// <returns>
    /// a list of Appointment instances.
    /// </returns>
    public IEnumerable<Appointment> Get(
      string email,
      DateTime start,
      DateTime? end,
      int? maxResults)
    {
      return Call(
        "Get",
        new GetRequest
        {
          email = email,
          start = start,
          end = end,
          maxResults = maxResults
        },
        GetImpl);
    }

    public long GetBegin(
      string email, 
      DateTime start, 
      DateTime? end,
      int? maxResults)
    {
      return CallAsync(
        "Get",
        new GetRequest
        {
          email = email,
          start = start,
          end = end,
          maxResults = maxResults
        },
        GetImpl);
    }

    public IEnumerable<Appointment> GetEnd(long requestID)
    {
      return ReadResult<IEnumerable<Appointment>>(requestID);
    }

    public struct GetRequest
    {
      public string email;
      public DateTime start;
      public DateTime? end;
      public int? maxResults;
    }

    private IEnumerable<Appointment> GetImpl(GetRequest request)
    {
      Office365.CalendarView view = new Office365.CalendarView(
        request.start,
        request.end.GetValueOrDefault(DateTime.Now),
        request.maxResults.GetValueOrDefault(int.MaxValue - 1));

      // Item searches do not support Deep traversal.
      view.Traversal = Office365.ItemTraversal.Shallow;

      var service = GetService(request.email);
      var appointments = service.FindAppointments(
        Office365.WellKnownFolderName.Calendar,
        view);

      var result = new List<Appointment>();

      if (appointments != null)
      {
        foreach (var appointment in appointments)
        {
          result.Add(ConvertAppointment(appointment));
        }
      }

      return result;
    }
    #endregion

    #region Find method
    /// <summary>
    /// Finds an proxy by its ID in the calendar of the specified user.
    /// </summary>
    /// <param name="email">a target user's e-mail.</param>
    /// <param name="ID">
    /// the proxy unique ID received on successful CreateAppointment method call.
    /// </param>
    /// <returns>
    /// an AppointmentProxy instance or null if the proxy was not found.
    /// </returns>
    public Appointment Find(string email, string ID)
    {
      return Call(
        "Find",
        new FindRequest
        {
          email = email,
          ID = ID
        },
        FindImpl);
    }

    /// <summary>
    /// Starts FindAppointments method asynchronously.
    /// </summary>
    /// <param name="email">a target user's e-mail.</param>
    /// <param name="ID">
    /// the proxy unique ID received on successful CreateAppointment method call.
    /// </param>
    /// <returns>a request ID.</returns>
    public long FindBegin(string email, string ID)
    {
      return CallAsync(
        "Find",
        new FindRequest
        {
          email = email,
          ID = ID
        },
        FindImpl);
    }

    /// <summary>
    /// Finishes asynchronous FindAppointments method call.
    /// </summary>
    /// <param name="requestID">
    /// a request ID obtained in result of FindBegin call.
    /// </param>
    /// <returns>
    /// a list of Appointment instances, or null when task not finished yet.
    /// </returns>
    public Appointment FindEnd(long requestID)
    { 
      return ReadResult<Appointment>(requestID);
    }

    public struct FindRequest
    {
      public string email;
      public string ID;
    }

    private Appointment FindImpl(FindRequest request)
    {
      var appointment = GetAppointment(request.email, request.ID);

      return ConvertAppointment(appointment);
    }
    #endregion

    #region Update method
    /// <summary>
    /// Updates the specified proxy.
    /// Note: 
    ///   All the specified properties will be overwritten in the origin 
    ///   proxy.
    /// </summary>
    /// <param name="email">
    /// An e-mail address of an organizer or a participant of the proxy.
    /// </param>
    /// <param name="proxy">
    /// An proxy to update. 
    /// The proxy ID must be not null.
    /// </param>
    /// <returns>
    /// true when the proxy was modified successfully, and false otherwise.
    /// </returns>
    /// <remarks>
    /// Only organizer can update an proxy.
    /// </remarks>
    public bool Update(string email, Appointment appointment)
    {
      return Call(
        "Update",
        new UpdateRequest
        {
          email = email,
          appointment = appointment
        },
        UpdateImpl).GetValueOrDefault(false);
    }

    /// <summary>
    /// Starts Update method asynchronously.
    /// </summary>
    /// <param name="email">a target user's e-mail.</param>
    /// <param name="proxy">
    /// an Appointment instance with new data for the proxy.
    /// </param>
    /// <returns>a request ID.</returns>
    public long UpdateBegin(string email, Appointment appointment)
    {
      return CallAsync(
        "Update",
        new UpdateRequest
        {
          email = email,
          appointment = appointment
        },
        UpdateImpl);
    }

    /// <summary>
    /// Finishes asynchronous Update method call.
    /// </summary>
    /// <param name="requestID">
    /// a request ID obtained in result of UpdateBegin call.
    /// </param>
    /// <returns>
    /// true when the proxy was modified successfully, false when proxy 
    /// wasn't modified, and null when task not finished yet.
    /// </returns>
    public bool? UpdateEnd(long requestID)
    {
      return ReadResult<bool?>(requestID);
    }

    public struct UpdateRequest
    {
      public string email;
      public Appointment appointment;
    }

    private bool? UpdateImpl(UpdateRequest request)
    { 
      var proxy = request.appointment;

      if (proxy == null)
      {
        throw new ArgumentNullException("request.proxy");
      }

      var appointment = GetAppointment(request.email, proxy.Id);

      // Note: only organizer may update the proxy.
      if ((appointment != null) &&
        (appointment.MyResponseType == Office365.MeetingResponseType.Organizer))
      {
        appointment.Start = proxy.Start;
        appointment.End = proxy.End;

        if (!string.IsNullOrEmpty(proxy.Location))
        {
          appointment.Location = proxy.Location;
        }

        if (!string.IsNullOrEmpty(proxy.Subject))
        {
          appointment.Subject = proxy.Subject;
        }

        if (appointment.ReminderMinutesBeforeStart != proxy.ReminderMinutesBeforeStart)
        {
          appointment.ReminderMinutesBeforeStart = proxy.ReminderMinutesBeforeStart;
        }

        //if (proxy.IsRecurring)
        //{
        //  if (proxy.StartRecurrence.HasValue)
        //  {
        //    proxy.Recurrence.StartDate = proxy.StartRecurrence.Value;
        //  }

        //  if (proxy.EndRecurrence.HasValue)
        //  {
        //    proxy.Recurrence.EndDate = proxy.EndRecurrence.Value;
        //  }
        //}

        SetExtendedProperties(appointment, proxy.ExtendedProperties);

        // Unless explicitly specified, the default is to use SendToAllAndSaveCopy.
        // This can convert an proxy into a proxy. To avoid this,
        // explicitly set SendToNone on non-meetings.
        var mode = appointment.IsMeeting ?
          Office365.SendInvitationsOrCancellationsMode.SendToAllAndSaveCopy :
          Office365.SendInvitationsOrCancellationsMode.SendToNone;

        appointment.Update(Office365.ConflictResolutionMode.AlwaysOverwrite, mode);

        return true;
      }

      return false;
    } 
    #endregion

    #region Cancel method
    /// <summary>
    /// Cancels an proxy specified by unique ID.
    /// Sends corresponding notifications to all participants.
    /// </summary>
    /// <param name="email">an e-mail of the organizer of the proxy.</param>
    /// <param name="ID">the proxy unique ID.</param>
    /// <param name="reason">a text message to be sent to all participants.</param>
    /// <returns>
    /// true when the proxy was canceled successfully, and false otherwise.
    /// </returns>
    /// <remarks>Only the proxy organizer may cancel it.</remarks>
    public bool Cancel(string email, string ID, string reason)
    {
      return Call(
        "Cancel",
        new CancelRequest
        {
          email = email,
          ID = ID,
          reason = reason
        },
        CancelImpl).GetValueOrDefault(false);
    }

    /// <summary>
    /// Starts Cancel method asynchronously.
    /// </summary>
    /// <param name="email">an e-mail of the organizer of the proxy.</param>
    /// <param name="ID">the proxy unique ID.</param>
    /// <param name="reason">a text message to be sent to all participants.</param>
    /// <returns>a request ID.</returns>
    public long CancelBegin(string email, string ID, string reason)
    {
      return CallAsync(
        "Cancel",
        new CancelRequest
        {
          email = email,
          ID = ID,
          reason = reason
        },
        CancelImpl);
    }

    /// <summary>
    /// Finishes asynchronous Cancel method call.
    /// </summary>
    /// <param name="requestID">
    /// a request ID obtained in result of CancelBegin call.
    /// </param>
    /// <returns>
    /// true when the proxy was canceled successfully, false when proxy 
    /// wasn't canceled, and null when task not finished yet.
    /// </returns>
    public bool? CancelEnd(long requestID)
    {
      return ReadResult<bool?>(requestID);
    }

    public struct CancelRequest
    {
      public string email;
      public string ID;
      public string reason;
    }

    private bool? CancelImpl(CancelRequest request)
    {
      var appointment = GetAppointment(request.email, request.ID);

      if (appointment != null)
      {
        appointment.CancelMeeting(request.reason);

        return true;
      }

      return false;
    }
    #endregion

    #region Delete method
    /// <summary>
    /// Delete an proxy specified by unique ID from organizer's e-mail box and
    /// sends cancel notifications to all participants.
    /// </summary>
    /// <param name="email">an e-mail of the organizer of the proxy.</param>
    /// <param name="ID">the proxy unique ID.</param>
    /// <returns>
    /// true when the proxy was successfully deleted, and false otherwise.
    /// </returns>
    /// <remarks>Only the proxy organizer may delete it.</remarks>
    public bool Delete(string email, string ID)
    {
      return Call(
        "Delete",
        new DeleteRequest
        {
          email = email,
          ID = ID
        },
        DeleteImpl).GetValueOrDefault(false);
    }

    /// <summary>
    /// Starts Delete method asynchronously.
    /// </summary>
    /// <param name="email">an e-mail of the organizer of the proxy.</param>
    /// <param name="ID">the proxy unique ID.</param>
    /// <returns>a request ID.</returns>
    /// <remarks>Only the proxy organizer may delete it.</remarks>
    public long DeleteBegin(string email, string ID)
    {
      return CallAsync(
        "Delete",
        new DeleteRequest
        {
          email = email,
          ID = ID
        },
        DeleteImpl);
    }

    /// <summary>
    /// Finishes asynchronous Delete method call.
    /// </summary>
    /// <param name="requestID">
    /// a request ID obtained in result of DeleteBegin call.
    /// </param>
    /// <returns>
    /// true when the operation succeeded, false when failed,
    /// and null when task not finished yet.
    /// </returns>
    /// <remarks>Only the proxy organizer may delete it.</remarks>
    public bool? DeleteEnd(long requestID)
    {
      return ReadResult<bool?>(requestID);
    }

    public struct DeleteRequest
    {
      public string email;
      public string ID;
    }

    private bool? DeleteImpl(DeleteRequest request)
    {
      var appointment = GetAppointment(request.email, request.ID);

      if (appointment != null)
      {
        appointment.Delete(Office365.DeleteMode.MoveToDeletedItems, true);

        return true;
      }

      return false;
    }
    #endregion

    #region Accept method
    /// <summary>
    /// Accepts the specified proxy.
    /// </summary>
    /// <param name="email">an e-mail of the organizer of the proxy.</param>
    /// <param name="ID">the proxy unique ID.</param>
    /// <returns>
    /// true when the operation succeseed, and false otherwise.
    /// </returns>
    public bool Accept(string email, string ID)
    {
      return Call(
        "Accept",
        new AcceptRequest
        {
          email = email,
          ID = ID
        },
        AcceptImpl).GetValueOrDefault(false);
    }

    /// <summary>
    /// Starts Accept method asynchronously.
    /// </summary>
    /// <param name="email">an e-mail of the organizer of the proxy.</param>
    /// <param name="ID">the proxy unique ID.</param>
    /// <returns>a request ID.</returns>
    public long AcceptBegin(string email, string ID)
    {
      return CallAsync(
        "Accept",
        new AcceptRequest
        {
          email = email,
          ID = ID
        },
        AcceptImpl);
    }

    /// <summary>
    /// Finishes asynchronous Accept method call.
    /// </summary>
    /// <param name="requestID">
    /// a request ID obtained in result of AcceptBegin call.
    /// </param>
    /// <returns>
    /// true when the operation succeeded, false when operation failed,
    /// and null when task not finished yet.
    /// </returns>
    public bool? AcceptEnd(long requestID)
    {
      return ReadResult<bool?>(requestID);
    }

    public struct AcceptRequest
    {
      public string email;
      public string ID;
    }

    private bool? AcceptImpl(AcceptRequest request)
    {
      var appointment = GetAppointment(request.email, request.ID);

      if (appointment != null)
      {
        appointment.Accept(true);

        return true;
      }

      return false;
    }
    #endregion

    #region Decline method
    /// <summary>
    /// Declines the specified proxy.
    /// </summary>
    /// <param name="email">an e-mail of the organizer of the proxy.</param>
    /// <param name="ID">the proxy unique ID.</param>
    /// <returns>
    /// true when the operation succeseed, and false otherwise.
    /// </returns>
    public bool Decline(string email, string ID)
    {
      return Call(
        "Decline",
        new DeclineRequest
        {
          email = email,
          ID = ID
        },
        DeclineImpl).GetValueOrDefault(false);
    }

    /// <summary>
    /// Starts Decline method asynchronously.
    /// </summary>
    /// <param name="email">an e-mail of the organizer of the proxy.</param>
    /// <param name="ID">the proxy unique ID.</param>
    /// <returns>a request ID.</returns>
    public long DeclineBegin(string email, string ID)
    {
      return CallAsync(
        "Decline",
        new DeclineRequest
        {
          email = email,
          ID = ID
        },
        DeclineImpl);
    }

    /// <summary>
    /// Finishes asynchronous Decline method call.
    /// </summary>
    /// <param name="requestID">
    /// a request ID obtained in result of DeclineBegin call.
    /// </param>
    /// <returns>
    /// true when the operation succeeded, false when operation failed,
    /// and null when task not finished yet.
    /// </returns>
    public bool? DeclineEnd(long requestID)
    {
      return ReadResult<bool?>(requestID);
    }

    public struct DeclineRequest
    {
      public string email;
      public string ID;
    }

    private bool? DeclineImpl(DeclineRequest request)
    {
      var appointment = GetAppointment(request.email, request.ID);

      if (appointment != null)
      {
        appointment.Decline(true);

        return true;
      }

      return false;
    }
    #endregion

    #region Notify method

    public struct NotifyRequest
    {
      public string email;
      public string ID;
      public string changeType;
    }

    /// <summary>
    /// Notifies about a change in a specified mail box.
    /// </summary>
    /// <param name="email">A mail box where change has occured.</param>
    /// <param name="ID">An ID of proxy changed.</param>
    /// <param name="changeType">A change type: delete, create, modify.</param>
    public bool Notification(string email, string ID, string changeType)
    {
      return Call(
        "Notify",
        new NotifyRequest
        {
          email = email,
          ID = ID,
          changeType = changeType
        },
        request => true);
    }
    #endregion

    #region GetChanges and GetChangeStats

    public struct GetChangesRequest
    {
      public string systemName;
      public string email;
      public string folderID;
      public DateTime? startDate;
      public DateTime? endDate;
      public int? skip;
      public int? take;
    }
    
    /// <summary>
    /// Gets a set of changes.
    /// </summary>
    /// <param name="systemName">An optional system name.</param>
    /// <param name="email">Optional email address.</param>
    /// <param name="folderID">Optional filder id.</param>
    /// <param name="startDate">Optional start date.</param>
    /// <param name="endDate">Optional end date.</param>
    /// <param name="skip">
    /// Optional number of record to skip in result.
    /// </param>
    /// <param name="take">
    /// Optional number of records to return from result.
    /// </param>
    /// <returns>A enumeration of changes.</returns>
    public IEnumerable<Change> GetChanges(
      string systemName,
      string email,
      string folderID,
      DateTime? startDate,
      DateTime? endDate,
      int? skip = 0,
      int? take = 0)
    {
      return Call(
        "GetChanges",
        new GetChangesRequest
        {
          systemName = systemName,
          email = email,
          folderID = folderID,
          startDate = startDate,
          endDate = endDate,
          skip = skip,
          take = take
        },
        GetChangesImpl);
    }

    /// <summary>
    /// Gets change stats.
    /// </summary>
    /// <param name="systemName">An optional system name.</param>
    /// <param name="email">Optional email address.</param>
    /// <param name="folderID">Optional filder id.</param>
    /// <param name="startDate">Optional start date.</param>
    /// <param name="endDate">Optional end date.</param>
    /// <param name="skip">
    /// Optional number of record to skip in result.
    /// </param>
    /// <param name="take">
    /// Optional number of records to return from result.
    /// </param>
    /// <returns>A enumeration of changes.</returns>
    public IEnumerable<ChangeStats> GetChangeStats(
      string systemName,
      string email,
      string folderID,
      DateTime? startDate,
      DateTime? endDate,
      int? skip = 0,
      int? take = 0)
    {
      return Call(
        "GetChangeStats",
        new GetChangesRequest
        {
          systemName = systemName,
          email = email,
          folderID = folderID,
          startDate = startDate,
          endDate = endDate,
          skip = skip,
          take = take
        },
        GetChangeStatsImpl);
    }

    private IEnumerable<Change> GetChangesImpl(GetChangesRequest request)
    {
      using(var model = new EWSQueueEntities())
      {
        var query = GetChangesQuery(model, request);

        query = query.
          OrderBy(item => item.Timestamp).
          ThenBy(item => item.Email).
          ThenBy(item => item.ItemID);

        if (request.skip != null)
        {
          query = query.Skip(request.skip.Value);
        }

        if (request.take != null)
        {
          query = query.Take(request.take.Value);
        }

        return query.ToList().
          Select(
            item => new Change 
            {
              Timestamp = item.Timestamp,
              Email = item.Email,
              FolderID = item.FolderID,
              ItemID = item.ItemID,
              ChangeType =  
                (ChangeType)Enum.Parse(typeof(ChangeType), item.ChangeType)
            }).
          ToArray();
      }
    }

    private IEnumerable<ChangeStats> GetChangeStatsImpl(
      GetChangesRequest request)
    {
      using(var model = new EWSQueueEntities())
      {
        var query = GetChangesQuery(model, request);

        var stats = query.GroupBy(item => new { item.Email, item.FolderID }).
          Select(
            item =>
              new ChangeStats 
              { 
                Email = item.Key.Email, 
                FolderID = item.Key.FolderID, 
                Count = item.Count() 
              }).
          OrderBy(item => item.Email).
          ThenBy(item => item.FolderID) as IQueryable<ChangeStats>;

        if (request.skip != null)
        {
          stats = stats.Skip(request.skip.Value);
        }

        if (request.take != null)
        {
          stats = stats.Take(request.take.Value);
        }

        return stats.ToList();
      }
    }

    private IQueryable<MailboxNotification> GetChangesQuery(
      EWSQueueEntities model,
      GetChangesRequest request)
    {
      var query = request.systemName == null ?
        model.MailboxNotifications.AsNoTracking() :
        model.MailboxNotifications.AsNoTracking().Join(
          model.BankSystems.
            Where(item => item.GroupName == request.systemName).
            Join(
              model.BankSystemMailboxes,
              outer => outer.GroupName,
              inner => inner.GroupName,
              (outer, inner) => inner),
          outer => outer.Email,
          inner => inner.Email,
          (outer, inner) => outer);

      if (request.email != null)
      {
        query = query.Where(item => item.Email == request.email);
      }

      if (request.folderID != null)
      {
        query = query.Where(item => item.FolderID == request.folderID);
      }

      if (request.startDate != null)
      {
        query = query.Where(item => item.Timestamp >= request.startDate);
      }

      if (request.endDate != null)
      {
        query = query.Where(item => item.Timestamp <= request.endDate);
      }

      return query;
    }
    #endregion

    #region Private methods
    /// <summary>
    /// Gets a service instance.
    /// </summary>
    /// <returns>a ExchangeService instance.</returns>
    private Office365.ExchangeService GetService(string impersonatedUserId)
    {
      if (impersonatedUserId == null)
      {
        throw new ArgumentNullException("impersonatedUserId");
      }

      var service = new Office365.ExchangeService(
        Office365.ExchangeVersion.Exchange2013);
      var user = Settings.ApplicationUsers[0];

      service.Credentials = 
        new Office365.WebCredentials(user.Email, user.Password);
      service.UseDefaultCredentials = false;
      service.PreAuthenticate = true;

      service.ImpersonatedUserId = new Office365.ImpersonatedUserId(
        Office365.ConnectingIdType.SmtpAddress,
        impersonatedUserId);

      var url = GetServiceUrl(impersonatedUserId);

      if (url == null)
      {
        var mailbox = EwsUtils.GetMailboxAffinities(
          user,
          Settings.AutoDiscoveryUrl,
          new [] { impersonatedUserId }).
          FirstOrDefault();

        if (mailbox != null)
        {
          SaveServiceUrl(mailbox);
        }

        throw new ArgumentException("Invalid user: " + impersonatedUserId);
      }

      service.Url = new Uri(url);

      return service;
    }

    private bool RedirectionUrlValidationCallback(string url)
    {
      return url.StartsWith("https");
    }

    /// <summary>
    /// Gets the specified proxy. 
    /// </summary>
    /// <param name="email">
    /// an e-mail address of an organizer or a participant of the proxy.
    /// </param>
    /// <param name="ID">an unique proxy ID to search.</param>
    /// <returns>
    /// an Appointment instance or null when the proxy was not found.
    /// </returns>
    private Office365.Appointment GetAppointment(string email, string ID)
    {
      var service = GetService(email);

      return Office365.Appointment.Bind(service, new Office365.ItemId(ID));
    }

    private Appointment ConvertAppointment(Office365.Appointment appointment)
    {
      if (appointment == null)
      {
        return null;
      }

      var proxy = new Appointment
      {
        Id = appointment.Id.ToString()
      };

      var target = proxy.GetType();
      var source = appointment.GetType();

      foreach (Office365.PropertyDefinition definition in
        appointment.GetLoadedPropertyDefinitions())
      {
        var name = definition.Name;
        var property = source.GetProperty(name);
        var targetProperty = target.GetProperty(name);

        if (targetProperty == null)
        {
          continue;
        }

        if (property.CanRead && targetProperty.CanWrite)
        {
          if ((property.PropertyType == typeof(string)) ||
            (property.PropertyType == typeof(int)) ||
            (property.PropertyType == typeof(DateTime)) ||
            (property.PropertyType == typeof(DateTime?)) ||
            (property.PropertyType == typeof(TimeSpan)) ||
            (property.PropertyType == typeof(TimeSpan?)) ||
            (property.PropertyType == typeof(bool)))
          {
            targetProperty.SetValue(proxy, property.GetValue(appointment));
          }
          else if (name == "Importance")
          {
            targetProperty.SetValue(proxy, (Importance)property.GetValue(appointment));
          }
          else if (name == "MeetingResponseType")
          {
            targetProperty.SetValue(proxy, (MeetingResponseType)property.GetValue(appointment));
          }
          else if (name == "Sensitivity")
          {
            targetProperty.SetValue(proxy, (Sensitivity)property.GetValue(appointment));
          }
        }
      }

      var message = null as Office365.TextBody;

      if (appointment.TryGetProperty(
        Office365.AppointmentSchema.TextBody,
        out message))
      {
        proxy.TextBody = message.ToString();
      }

      proxy.RequiredAttendees = GetAttendees(
        appointment,
        Office365.AppointmentSchema.RequiredAttendees);

      proxy.OptionalAttendees = GetAttendees(
        appointment,
        Office365.AppointmentSchema.OptionalAttendees);

      proxy.Resources = GetAttendees(
        appointment,
        Office365.AppointmentSchema.Resources);

      var emailAddress = null as Office365.EmailAddress;

      if (appointment.TryGetProperty(
        Office365.AppointmentSchema.Organizer,
        out emailAddress))
      {
        proxy.Organizer = new Attendee
        {
          Address = emailAddress.Address,
          Name = emailAddress.Name
        };
      }

      if (appointment.IsRecurring)
      {
        Office365.Recurrence recurrence;

        if (appointment.TryGetProperty(
          Office365.AppointmentSchema.Recurrence,
          out recurrence))
        {
          proxy.Recurrence = new Recurrence
          {
            StartDate = recurrence.StartDate,
            EndDate = recurrence.EndDate,
            NumberOfOccurrences = recurrence.NumberOfOccurrences,
            OriginalTypeName = recurrence.GetType().FullName
          };

          if (recurrence is Office365.Recurrence.IntervalPattern)
          {
            proxy.Recurrence.Interval =
              ((Office365.Recurrence.IntervalPattern)recurrence).Interval;

            if ((recurrence is Office365.Recurrence.DailyPattern) ||
              (recurrence is Office365.Recurrence.DailyRegenerationPattern))
            {
              proxy.Recurrence.Type = RecurrenceType.Daily;
            }
            else if ((recurrence is Office365.Recurrence.MonthlyPattern) ||
              (recurrence is Office365.Recurrence.MonthlyRegenerationPattern))
            {
              proxy.Recurrence.Type = RecurrenceType.Monthly;
            }
            else if ((recurrence is Office365.Recurrence.WeeklyPattern) ||
              (recurrence is Office365.Recurrence.WeeklyRegenerationPattern))
            {
              proxy.Recurrence.Type = RecurrenceType.Weekly;
            }
            else if ((recurrence is Office365.Recurrence.YearlyPattern) ||
              (recurrence is Office365.Recurrence.YearlyRegenerationPattern))
            {
              proxy.Recurrence.Type = RecurrenceType.Yearly;
            }
          }
        }

        Office365.OccurrenceInfo occurence;

        if (appointment.TryGetProperty(
          Office365.AppointmentSchema.FirstOccurrence,
          out occurence))
        {
          proxy.FirstOccurrence = new OccurrenceInfo
          {
            Start = occurence.Start,
            End = occurence.End
          };
        }

        if (appointment.TryGetProperty(
          Office365.AppointmentSchema.LastOccurrence,
          out occurence))
        {
          proxy.LastOccurrence = new OccurrenceInfo
          {
            Start = occurence.Start,
            End = occurence.End
          };
        }
      }

      proxy.ExtendedProperties = GetExtendedProperties(appointment);

      return proxy;
    }

    private static List<Attendee> GetAttendees(
      Office365.Appointment appointment,
      Office365.PropertyDefinitionBase property)
    {
      var list = new List<Attendee>();
      var attendees = null as Office365.AttendeeCollection;

      if (appointment.TryGetProperty(property, out attendees))
      {
        foreach (var attendee in attendees)
        {
          list.Add(
            new Attendee
            {
              Address = attendee.Address,
              Name = attendee.Name,
              ResponseType = (MeetingResponseType)attendee.ResponseType
            });
        }
      }

      return list;
    }

    private static List<ExtendedProperty> GetExtendedProperties(
      Office365.Appointment appointment)
    {
      var properties = null as Office365.ExtendedPropertyCollection;
      var result = null as List<ExtendedProperty>;

      if (appointment.TryGetProperty(
        Office365.ItemSchema.ExtendedProperties,
        out properties))
      {
        foreach (var property in properties)
        {
          if (property.PropertyDefinition.PropertySetId !=
            Appointments.ExtendedPropertySetId)
          {
            // not our extended property, skip it
            continue;
          }

          if (result == null)
          {
            result = new List<ExtendedProperty>();
          }

          result.Add(
            new ExtendedProperty
            {
              Name = property.PropertyDefinition.Name,
              Value = property.Value as string
            });
        }
      }

      return result;
    }

    private static void SetExtendedProperties(
      Office365.Appointment appointment,
      List<ExtendedProperty> properties)
    {
      if (properties != null)
      {
        foreach (var property in properties)
        {
          appointment.SetExtendedProperty(
            new Office365.ExtendedPropertyDefinition(
              Appointments.ExtendedPropertySetId,
              property.Name,
              Office365.MapiPropertyType.String),
            property.Value);
        }
      }
    }

    private O Call<I, O>(string actionName, I request, Func<I, O> action)
    {
      var requestID = StoreRequest(actionName, request);
      var response = default(O);
      var error = null as Exception;

      try
      {
        response = action(request);
        error = StoreResult(requestID, response);
      }
      catch(Exception e)
      {
        error = e;
        StoreError(requestID, e);

        throw e;
      }
      finally
      {
        Notify(requestID, request, response, error);
      }

      return response;
    }

    private long CallAsync<I, O>(string actionName, I request, Func<I, O> action)
    {
      var requestID = StoreRequest(actionName, request);

      Task.Factory.StartNew(
        () =>
        {
          var response = default(O);
          var error = null as Exception;

          try
          {
            response = action(request);
            error = StoreResult(requestID, response);
          }
          catch(Exception e)
          {
            error = e;
            StoreError(requestID, e);
          }
          finally
          {
            Notify(requestID, request, response, error);
          }
        });

      return requestID;
    }

    private long StoreRequest<T>(string actionName, T request)
    {
      using(var model = new EWSQueueEntities())
      {
        var item = new Queue
        {
          Operation = actionName,
          Request = ToXmlString(request),
          CreatedAt = DateTime.Now,
          ExpiresAt = DateTime.Now.AddMinutes(Settings.RequestTimeout)
        };

        model.Queues.Add(item);

        model.SaveChanges();

        return item.ID;
      }
    }

    private Exception StoreResult<O>(long requestID, O result)
    {
      Exception error = null;

      using(var model = new EWSQueueEntities())
      {
        var item = model.Queues.
          Where(request => request.ID == requestID).
          FirstOrDefault();

        if (item != null)
        {
          if (item.ExpiresAt.HasValue)
          {
            if (item.ExpiresAt.Value < DateTime.Now)
            {
              error = new TimeoutException(
                  "Operation " + item.Operation + " is timed out.");

              item.Error = ToXmlString(error);
            }
          }

          item.Response = ToXmlString(result);

          model.Entry(item).State = EntityState.Modified;
          model.SaveChanges();
        }
      }

      return error;
    }

    private void StoreError(long requestID, Exception error)
    {
      using(var model = new EWSQueueEntities())
      {
        var item = model.Queues.
          Where(request => request.ID == requestID).
          FirstOrDefault();

        if (item != null)
        {
          item.Error = ToXmlString(error);
          model.Entry(item).State = EntityState.Modified;
          model.SaveChanges();
        }
      }
    }

    private void Notify<I, O>(
      long requestID, 
      I request, 
      O response, 
      Exception e)
    {
      if (ResponseNotifier != null)
      {
        try
        {
//          ResponseNotifier.Notify(requestID, request, response, e);
        }
        catch
        { 
          // Notifier should not interrupt us.
        }
      }
    }

    private static T ReadResult<T>(long requestID)
    {
      using (var model = new EWSQueueEntities())
      {
        var item = model.Queues.
          Where(request => request.ID == requestID).
          FirstOrDefault();

        if (item == null)
        {
          return default(T);
        }

        if (item.Error != null)
        {
          throw FromXmlString<Exception>(item.Error);
        }

        if ((item.Response == null) && item.ExpiresAt.HasValue)
        {
          if (item.ExpiresAt.Value < DateTime.Now)
          {
            throw new TimeoutException(
              "Operation " + item.Operation + " is timed out.");
          }
        }

        return FromXmlString<T>(item.Response);
      }
    }

    private static string GetServiceUrl(string email)
    {
      using (var model = new EWSQueueEntities())
      {
        return model.MailboxAffinities.
          Where(item => item.Email == email).
          Select(item => item.ExternalEwsUrl).
          FirstOrDefault();
      }
    }

    private static void SaveServiceUrl(MailboxAffinity mailbox)
    {
      using(var model = new EWSQueueEntities())
      {
        model.Entry(mailbox).State = 
          mailbox.ExternalEwsUrl == null ?  EntityState.Deleted :
          model.MailboxAffinities.AsNoTracking().
            Any(item => item.Email == mailbox.Email) ? EntityState.Added :
          EntityState.Modified;

        model.SaveChanges();
      }
    }

    private static string ToXmlString(object result)
    {
      var data = new StringBuilder();
      var serializer = new NetDataContractSerializer();

      using (var writer = XmlWriter.Create(data))
      {
        serializer.WriteObject(writer, result);
      }

      return data.ToString();
    }

    private static T FromXmlString<T>(string xml)
    {
      if (string.IsNullOrEmpty(xml))
      {
        return default(T);
      }

      var serializer = new NetDataContractSerializer();
      var reader = new StringReader(xml);

      return (T)serializer.ReadObject(XmlReader.Create(reader));
    }
    #endregion

    #region private fields
    /// <summary>
    /// HTML pattern.
    /// </summary>
    private static Regex IsHtml = new Regex(@"\<html\>.*\</html\>", 
      RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.Singleline);

    /// <summary>
    /// The GUID for the extended property set.
    /// </summary>
    private static Guid ExtendedPropertySetId = 
      new Guid("{DD12CD36-DB49-4002-A809-56B40E6B60E9}");
    #endregion
  }
}
