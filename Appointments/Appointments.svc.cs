namespace Bnhp.Office365
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
    /// Creates a new appointment/meeting and sends notifications to attendees.
    /// </summary>
    /// <param name="email">An e-mail address of the organizer.</param>
    /// <param name="appointment">
    /// an AppointmentProxy instance with data for the appointment.
    /// </param>
    /// <returns>An unique ID of the new appointment.</returns>
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
        throw new ArgumentNullException("request.appointment");
      }
      
      var email = request.email;
      var service = GetService(email);
      var meeting = new Office365.Appointment(service);
      var appointment = request.appointment;

      // Set the properties on the meeting object to create the meeting.
      meeting.Subject = appointment.Subject;

      if (!string.IsNullOrEmpty(appointment.TextBody))
      {
        meeting.Body = new Office365.MessageBody(
          IsHtml.IsMatch(appointment.TextBody) ?
            Office365.BodyType.HTML : Office365.BodyType.Text,
          appointment.TextBody);
      }

      meeting.Start = appointment.Start;
      meeting.End = appointment.End;
      meeting.Location = appointment.Location;
      meeting.AllowNewTimeProposal = true;
      meeting.Importance = (Office365.Importance)appointment.Importance;
      meeting.ReminderMinutesBeforeStart = appointment.ReminderMinutesBeforeStart;

      if ((appointment.Recurrence != null) && 
        (appointment.Recurrence.Type != RecurrenceType.Once))
      {
        var start = appointment.Recurrence.StartDate;

        switch (appointment.Recurrence.Type)
        {
          case RecurrenceType.Dayly:
          {
            meeting.Recurrence = new Office365.Recurrence.DailyPattern(
              start,
              appointment.Recurrence.NumberOfOccurrences);

            break;
          }
          case RecurrenceType.Weekly:
          {
            meeting.Recurrence = new Office365.Recurrence.WeeklyPattern(
              start,
              appointment.Recurrence.NumberOfOccurrences,
              (Office365.DayOfTheWeek)start.DayOfWeek);

            break;
          }
          case RecurrenceType.Monthly:
          {
            meeting.Recurrence = new Office365.Recurrence.MonthlyPattern(
              start,
              appointment.Recurrence.NumberOfOccurrences,
              start.Day);

            break;
          }
          case RecurrenceType.Yearly:
          {
            meeting.Recurrence =
              new Office365.Recurrence.YearlyPattern(
                start,
                (Office365.Month)start.Month,
                start.Day);

            break;
          }
        }

        if (appointment.Recurrence.HasEnd)
        {
          meeting.Recurrence.EndDate = appointment.Recurrence.EndDate;
        }
      }

      if (appointment.RequiredAttendees != null)
      {
        foreach (var attendee in appointment.RequiredAttendees)
        {
          meeting.RequiredAttendees.Add(attendee.Address);
        }
      }

      if (appointment.OptionalAttendees != null)
      {
        foreach (var attendee in appointment.OptionalAttendees)
        {
          meeting.OptionalAttendees.Add(attendee.Address);
        }
      }

      if (appointment.Resources != null)
      {
        foreach (var resource in appointment.Resources)
        {
          meeting.Resources.Add(resource.Address);
        }
      }

      meeting.ICalUid = 
        Guid.NewGuid().ToString() + email.Substring(email.IndexOf('@'));

      // Send the meeting request
      meeting.Save(Office365.SendInvitationsMode.SendToAllAndSaveCopy);

      return meeting.Id.ToString();
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
    /// Finds an appointment by its ID in the calendar of the specified user.
    /// </summary>
    /// <param name="email">a target user's e-mail.</param>
    /// <param name="ID">
    /// the appointment unique ID received on successful Create method call.
    /// </param>
    /// <returns>
    /// an AppointmentProxy instance or null if the appointment was not found.
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
    /// Starts Find method asynchronously.
    /// </summary>
    /// <param name="email">a target user's e-mail.</param>
    /// <param name="ID">
    /// the appointment unique ID received on successful Create method call.
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
    /// Finishes asynchronous Find method call.
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
    /// Updates the specified appointment.
    /// Note: 
    ///   All the specified properties will be overwritten in the origin 
    ///   appointment.
    /// </summary>
    /// <param name="email">
    /// An e-mail address of an organizer or a participant of the meeting.
    /// </param>
    /// <param name="appointment">
    /// An appointment to update. 
    /// The appointment ID must be not null.
    /// </param>
    /// <returns>
    /// true when the appointment was modified successfully, and false otherwise.
    /// </returns>
    /// <remarks>
    /// Only organizer can update an appointment.
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
    /// <param name="appointment">
    /// an Appointment instance with new data for the appointment.
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
    /// true when the appointment was modified successfully, false when appointment 
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
      var appointment = request.appointment;

      if (appointment == null)
      {
        throw new ArgumentNullException("request.appointment");
      }

      var item = GetAppointment(request.email, appointment.Id);

      // Note: only organizer may update the appointment.
      if ((item != null) &&
        (item.MyResponseType == Office365.MeetingResponseType.Organizer))
      {
        item.Start = appointment.Start;
        item.End = appointment.End;

        if (!string.IsNullOrEmpty(appointment.Location))
        {
          item.Location = appointment.Location;
        }

        if (!string.IsNullOrEmpty(appointment.Subject))
        {
          item.Subject = appointment.Subject;
        }

        if (item.ReminderMinutesBeforeStart != appointment.ReminderMinutesBeforeStart)
        {
          item.ReminderMinutesBeforeStart = appointment.ReminderMinutesBeforeStart;
        }

        //if (item.IsRecurring)
        //{
        //  if (appointment.StartRecurrence.HasValue)
        //  {
        //    item.Recurrence.StartDate = appointment.StartRecurrence.Value;
        //  }

        //  if (appointment.EndRecurrence.HasValue)
        //  {
        //    item.Recurrence.EndDate = appointment.EndRecurrence.Value;
        //  }
        //}

        // Unless explicitly specified, the default is to use SendToAllAndSaveCopy.
        // This can convert an appointment into a meeting. To avoid this,
        // explicitly set SendToNone on non-meetings.
        var mode = item.IsMeeting ?
          Office365.SendInvitationsOrCancellationsMode.SendToAllAndSaveCopy :
          Office365.SendInvitationsOrCancellationsMode.SendToNone;

        item.Update(Office365.ConflictResolutionMode.AlwaysOverwrite, mode);

        return true;
      }

      return false;
    } 
    #endregion

    #region Cancel method
    /// <summary>
    /// Cancels an appointment specified by unique ID.
    /// Sends corresponding notifications to all participants.
    /// </summary>
    /// <param name="email">an e-mail of the organizer of the appointment.</param>
    /// <param name="ID">the appointment unique ID.</param>
    /// <param name="reason">a text message to be sent to all participants.</param>
    /// <returns>
    /// true when the appointment was canceled successfully, and false otherwise.
    /// </returns>
    /// <remarks>Only the appointment organizer may cancel it.</remarks>
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
    /// <param name="email">an e-mail of the organizer of the appointment.</param>
    /// <param name="ID">the appointment unique ID.</param>
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
    /// true when the appointment was canceled successfully, false when appointment 
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
    /// Delete an appointment specified by unique ID from organizer's e-mail box and
    /// sends cancel notifications to all participants.
    /// </summary>
    /// <param name="email">an e-mail of the organizer of the appointment.</param>
    /// <param name="ID">the appointment unique ID.</param>
    /// <returns>
    /// true when the appointment was successfully deleted, and false otherwise.
    /// </returns>
    /// <remarks>Only the appointment organizer may delete it.</remarks>
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
    /// <param name="email">an e-mail of the organizer of the appointment.</param>
    /// <param name="ID">the appointment unique ID.</param>
    /// <returns>a request ID.</returns>
    /// <remarks>Only the appointment organizer may delete it.</remarks>
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
    /// <remarks>Only the appointment organizer may delete it.</remarks>
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
    /// Accepts the specified appointment.
    /// </summary>
    /// <param name="email">an e-mail of the organizer of the appointment.</param>
    /// <param name="ID">the appointment unique ID.</param>
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
    /// <param name="email">an e-mail of the organizer of the appointment.</param>
    /// <param name="ID">the appointment unique ID.</param>
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
    /// Declines the specified appointment.
    /// </summary>
    /// <param name="email">an e-mail of the organizer of the appointment.</param>
    /// <param name="ID">the appointment unique ID.</param>
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
    /// <param name="email">an e-mail of the organizer of the appointment.</param>
    /// <param name="ID">the appointment unique ID.</param>
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
    /// <param name="ID">An ID of item changed.</param>
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

        var stats = query.GroupBy(item => item.Email).
          Select(
            item =>
              new ChangeStats { Email = item.Key, Count = item.Count() }).
          OrderBy(item => item.Email) as IQueryable<ChangeStats>;

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
      var user = Settings.DefaultApplicationUser;

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
    /// Gets the specified appointment. 
    /// </summary>
    /// <param name="email">
    /// an e-mail address of an organizer or a participant of the appointment.
    /// </param>
    /// <param name="ID">an unique appointment ID to search.</param>
    /// <returns>
    /// an Appointment instance or null when the appointment was not found.
    /// </returns>
    private Office365.Appointment GetAppointment(string email, string ID)
    {
      //var filter = 
      //  new Office365.SearchFilter.IsEqualTo(Office365.ItemSchema.Id, ID);

      //Office365.ItemView view = new Office365.ItemView(1);

      //view.Traversal = Office365.ItemTraversal.Shallow;

      //var service = GetService(email);
      //var appointments = service.FindItems(
      //  Office365.WellKnownFolderName.Calendar,
      //  filter,
      //  view);

      //if (appointments != null)
      //{
      //  return appointments.FirstOrDefault() as Office365.Appointment;
      //}

      //return null;

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

    //public OccurrenceInfo FirstOccurrence { get; internal set; }
    //public OccurrenceInfo LastOccurrence { get; internal set; }
    //public Attendee Organizer { get; internal set; }
    //public Recurrence Recurrence { get; set; }


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

      //if (proxy.IsRecurring)
      //{
      //  if (appointment.Recurrence is Office365.Recurrence.DailyPattern)
      //  {
      //    proxy.RecurrenceType = RecurrenceType.Dayly;
      //    proxy.RecurrenceInterval =
      //      ((Office365.Recurrence.DailyPattern)appointment.Recurrence).Interval;
      //  }
      //  else if (appointment.Recurrence is Office365.Recurrence.WeeklyPattern)
      //  {
      //    proxy.RecurrenceType = RecurrenceType.Weekly;
      //    proxy.RecurrenceInterval =
      //      ((Office365.Recurrence.WeeklyPattern)appointment.Recurrence).Interval;
      //  }
      //  else if (appointment.Recurrence is Office365.Recurrence.MonthlyPattern)
      //  {
      //    proxy.RecurrenceType = RecurrenceType.Monthly;
      //    proxy.RecurrenceInterval =
      //      ((Office365.Recurrence.MonthlyPattern)appointment.Recurrence).Interval;
      //  }
      //  else if (appointment.Recurrence is Office365.Recurrence.YearlyPattern)
      //  {
      //    proxy.RecurrenceType = RecurrenceType.Yearly;
      //    proxy.RecurrenceInterval =
      //      (int)((Office365.Recurrence.YearlyPattern)appointment.Recurrence).Month;
      //  }

      //  proxy.StartRecurrence = appointment.Recurrence.StartDate;
      //  proxy.EndRecurrence = appointment.Recurrence.EndDate;         
      //}

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
              Name = attendee.Name
            });
        }
      }

      return list;
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
          ResponseNotifier.Notify(requestID, request, response, e);
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
    private static Regex IsHtml = new Regex(@"\<html\>.*\</html\>", 
      RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.Singleline);
    #endregion
  }
}
