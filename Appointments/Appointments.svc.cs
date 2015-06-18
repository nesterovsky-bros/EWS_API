using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.ServiceModel;
using System.ServiceModel.Web;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml;
using Microsoft.Exchange.WebServices.Data;
using Multiconn.Experanto.Serializer;
using MSOffice365 = Microsoft.Exchange.WebServices.Data;
using Threading = System.Threading.Tasks;

namespace Bnhp.Office365
{
  /// <summary>
  /// An implementation of IAppointments interface for CRUD operations with
  /// appointments for Office365.
  /// </summary>
  public class Appointments : IAppointments
  {
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
      
      var service = GetService(request.email);
      var meeting = new MSOffice365.Appointment(service);
      var appointment = request.appointment;

      // Set the properties on the meeting object to create the meeting.
      meeting.Subject = appointment.Subject;

      if (!string.IsNullOrEmpty(appointment.Message))
      {
        meeting.Body = new MSOffice365.MessageBody(
          IsHtml.IsMatch(appointment.Message) ?
            MSOffice365.BodyType.HTML : MSOffice365.BodyType.Text,
          appointment.Message);
      }

      meeting.Start = appointment.Start.GetValueOrDefault(DateTime.Now);
      meeting.End = appointment.End.GetValueOrDefault(DateTime.Now.AddHours(1));
      meeting.Location = appointment.Location;
      meeting.AllowNewTimeProposal = true;
      meeting.Importance = MSOffice365.Importance.Normal;
      meeting.ReminderMinutesBeforeStart = appointment.ReminderMinutesBeforeStart;

      var IsRecurring = (appointment.RecurrenceType != RecurrenceType.Once);

      if (IsRecurring)
      {
        var start =
          appointment.StartRecurrence.GetValueOrDefault(DateTime.Now);

        // TODO: 
        switch (appointment.RecurrenceType)
        {
          case RecurrenceType.Dayly:
          {
            meeting.Recurrence = new MSOffice365.Recurrence.DailyPattern(
              start,
              appointment.RecurrenceInterval);

            break;
          }
          case RecurrenceType.Weekly:
          {
            meeting.Recurrence = new MSOffice365.Recurrence.WeeklyPattern(
              start,
              appointment.RecurrenceInterval,
              (MSOffice365.DayOfTheWeek)start.DayOfWeek);

            break;
          }
          case RecurrenceType.Monthly:
          {
            meeting.Recurrence = new MSOffice365.Recurrence.MonthlyPattern(
              start,
              appointment.RecurrenceInterval,
              start.Day);

            break;
          }
          case RecurrenceType.Yearly:
          {
            meeting.Recurrence =
              new MSOffice365.Recurrence.YearlyPattern(
                start,
                (MSOffice365.Month)start.Month,
                start.Day);

            break;
          }
        }

        if (appointment.EndRecurrence.HasValue)
        {
          meeting.Recurrence.EndDate = appointment.EndRecurrence.Value;
        }
      }

      // Note: currently only required attendees are supported.
      // TODO: support of optional attendees.
      foreach (var attendee in appointment.Attendees)
      {
        meeting.RequiredAttendees.Add(attendee);
      }

      //meeting.OptionalAttendees.Add("Magdalena.Kemp@contoso.com");

      // Send the meeting request
      meeting.Save(MSOffice365.SendInvitationsMode.SendToAllAndSaveCopy);

      return meeting.ICalUid;
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
      MSOffice365.CalendarView view = new MSOffice365.CalendarView(
        request.start,
        request.end.GetValueOrDefault(DateTime.Now),
        request.maxResults.GetValueOrDefault(int.MaxValue - 1));

      // Item searches do not support Deep traversal.
      view.Traversal = MSOffice365.ItemTraversal.Shallow;

      var service = GetService(request.email);
      var appointments = service.FindAppointments(
        MSOffice365.WellKnownFolderName.Calendar,
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
    /// <param name="UID">
    /// the appointment unique ID received on successful Create method call.
    /// </param>
    /// <returns>
    /// an AppointmentProxy instance or null if the appointment was not found.
    /// </returns>
    public Appointment Find(string email, string UID)
    {
      return Call(
        "Find",
        new FindRequest
        {
          email = email,
          UID = UID
        },
        FindImpl);
    }

    /// <summary>
    /// Starts Find method asynchronously.
    /// </summary>
    /// <param name="email">a target user's e-mail.</param>
    /// <param name="UID">
    /// the appointment unique ID received on successful Create method call.
    /// </param>
    /// <returns>a request ID.</returns>
    public long FindBegin(string email, string UID)
    {
      return CallAsync(
        "Find",
        new FindRequest
        {
          email = email,
          UID = UID
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
      public string UID;
    }

    private Appointment FindImpl(FindRequest request)
    {
      var appointment = GetAppointment(request.email, request.UID);

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
    /// The appointment UID must be not null.
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

      var item = GetAppointment(request.email, appointment.UID);

      // Note: only organizer may update the appointment.
      if ((item != null) &&
        (item.MyResponseType == MSOffice365.MeetingResponseType.Organizer))
      {
        var duration = item.End - item.Start;

        if (appointment.Start.HasValue)
        {
          item.Start = appointment.Start.Value;
        }

        if (appointment.End.HasValue)
        {
          item.End = appointment.End.Value;
        }
        else
        {
          item.End = item.Start + duration;
        }

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

        if (item.IsRecurring)
        {
          if (appointment.StartRecurrence.HasValue)
          {
            item.Recurrence.StartDate = appointment.StartRecurrence.Value;
          }

          if (appointment.EndRecurrence.HasValue)
          {
            item.Recurrence.EndDate = appointment.EndRecurrence.Value;
          }
        }

        // Unless explicitly specified, the default is to use SendToAllAndSaveCopy.
        // This can convert an appointment into a meeting. To avoid this,
        // explicitly set SendToNone on non-meetings.
        var mode = item.IsMeeting ?
          MSOffice365.SendInvitationsOrCancellationsMode.SendToAllAndSaveCopy :
          MSOffice365.SendInvitationsOrCancellationsMode.SendToNone;

        item.Update(MSOffice365.ConflictResolutionMode.AlwaysOverwrite, mode);

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
    /// <param name="UID">the appointment unique ID.</param>
    /// <param name="reason">a text message to be sent to all participants.</param>
    /// <returns>
    /// true when the appointment was canceled successfully, and false otherwise.
    /// </returns>
    /// <remarks>Only the appointment organizer may cancel it.</remarks>
    public bool Cancel(string email, string UID, string reason)
    {
      return Call(
        "Cancel",
        new CancelRequest
        {
          email = email,
          UID = UID,
          reason = reason
        },
        CancelImpl).GetValueOrDefault(false);
    }

    /// <summary>
    /// Starts Cancel method asynchronously.
    /// </summary>
    /// <param name="email">an e-mail of the organizer of the appointment.</param>
    /// <param name="UID">the appointment unique ID.</param>
    /// <param name="reason">a text message to be sent to all participants.</param>
    /// <returns>a request ID.</returns>
    public long CancelBegin(string email, string UID, string reason)
    {
      return CallAsync(
        "Cancel",
        new CancelRequest
        {
          email = email,
          UID = UID,
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
      public string UID;
      public string reason;
    }

    private bool? CancelImpl(CancelRequest request)
    {
      var appointment = GetAppointment(request.email, request.UID);

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
    /// <param name="UID">the appointment unique ID.</param>
    /// <returns>
    /// true when the appointment was successfully deleted, and false otherwise.
    /// </returns>
    /// <remarks>Only the appointment organizer may delete it.</remarks>
    public bool Delete(string email, string UID)
    {
      return Call(
        "Delete",
        new DeleteRequest
        {
          email = email,
          UID = UID
        },
        DeleteImpl).GetValueOrDefault(false);
    }

    /// <summary>
    /// Starts Delete method asynchronously.
    /// </summary>
    /// <param name="email">an e-mail of the organizer of the appointment.</param>
    /// <param name="UID">the appointment unique ID.</param>
    /// <returns>a request ID.</returns>
    /// <remarks>Only the appointment organizer may delete it.</remarks>
    public long DeleteBegin(string email, string UID)
    {
      return CallAsync(
        "Delete",
        new DeleteRequest
        {
          email = email,
          UID = UID
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
      public string UID;
    }

    private bool? DeleteImpl(DeleteRequest request)
    {
      var appointment = GetAppointment(request.email, request.UID);

      if (appointment != null)
      {
        appointment.Delete(MSOffice365.DeleteMode.MoveToDeletedItems, true);

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
    /// <param name="UID">the appointment unique ID.</param>
    /// <returns>
    /// true when the operation succeseed, and false otherwise.
    /// </returns>
    public bool Accept(string email, string UID)
    {
      return Call(
        "Accept",
        new AcceptRequest
        {
          email = email,
          UID = UID
        },
        AcceptImpl).GetValueOrDefault(false);
    }

    /// <summary>
    /// Starts Accept method asynchronously.
    /// </summary>
    /// <param name="email">an e-mail of the organizer of the appointment.</param>
    /// <param name="UID">the appointment unique ID.</param>
    /// <returns>a request ID.</returns>
    public long AcceptBegin(string email, string UID)
    {
      return CallAsync(
        "Accept",
        new AcceptRequest
        {
          email = email,
          UID = UID
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
      public string UID;
    }

    private bool? AcceptImpl(AcceptRequest request)
    {
      var appointment = GetAppointment(request.email, request.UID);

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
    /// <param name="UID">the appointment unique ID.</param>
    /// <returns>
    /// true when the operation succeseed, and false otherwise.
    /// </returns>
    public bool Decline(string email, string UID)
    {
      return Call(
        "Decline",
        new DeclineRequest
        {
          email = email,
          UID = UID
        },
        DeclineImpl).GetValueOrDefault(false);
    }

    /// <summary>
    /// Starts Decline method asynchronously.
    /// </summary>
    /// <param name="email">an e-mail of the organizer of the appointment.</param>
    /// <param name="UID">the appointment unique ID.</param>
    /// <returns>a request ID.</returns>
    public long DeclineBegin(string email, string UID)
    {
      return CallAsync(
        "Decline",
        new DeclineRequest
        {
          email = email,
          UID = UID
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
      public string UID;
    }

    private bool? DeclineImpl(DeclineRequest request)
    {
      var appointment = GetAppointment(request.email, request.UID);

      if (appointment != null)
      {
        appointment.Decline(true);

        return true;
      }

      return false;
    }
    #endregion

    #region Private methods
    /// <summary>
    /// Gets a service instance.
    /// </summary>
    /// <returns>a ExchangeService instance.</returns>
    private ExchangeService GetService(string impersonatedUserId)
    {
      if (impersonatedUserId == null)
      {
        throw new ArgumentNullException("impersonatedUserId");
      }

      var service =
        new ExchangeService(ExchangeVersion.Exchange2013);

      //---------------- TODO: replace with service account ----------------------------
      ExchangeUserName =
        ExchangeUserName ?? ConfigurationManager.AppSettings["ExchangeUserName"];
      ExchangePassword =
        ExchangePassword ?? ConfigurationManager.AppSettings["ExchangePassword"];
      ExchangeUrl =
        ExchangeUrl ?? ConfigurationManager.AppSettings["ExchangeUrl"];
      //-------------------------------------------------------------------

      service.Credentials =
        new MSOffice365.WebCredentials(ExchangeUserName, ExchangePassword);
      service.UseDefaultCredentials = false;
      service.PreAuthenticate = true;

      if (ExchangeUserName != impersonatedUserId)
      {
        service.ImpersonatedUserId = new MSOffice365.ImpersonatedUserId(
          MSOffice365.ConnectingIdType.SmtpAddress,
          impersonatedUserId);
      }

      if (string.IsNullOrEmpty(ExchangeUrl))
      {
        service.AutodiscoverUrl(ExchangeUserName, RedirectionUrlValidationCallback);

        ExchangeUrl = service.Url.ToString();
      }
      else
      {
        service.Url = new Uri(ExchangeUrl);
      }

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
    /// <param name="UID">an unique appointment ID to search.</param>
    /// <returns>
    /// an Appointment instance or null when the appointment was not found.
    /// </returns>
    private MSOffice365.Appointment GetAppointment(string email, string UID)
    {
      var property = new MSOffice365.ExtendedPropertyDefinition(
        MSOffice365.DefaultExtendedPropertySet.Meeting,
        0x23,
        MSOffice365.MapiPropertyType.Binary);
      var value =
        Convert.ToBase64String(HexEncoder.HexStringToArray(UID));
      var filter = new MSOffice365.SearchFilter.IsEqualTo(property, value);

      MSOffice365.ItemView view = new MSOffice365.ItemView(1);

      view.Traversal = MSOffice365.ItemTraversal.Shallow;

      var service = GetService(email);
      var appointments = service.FindItems(
        MSOffice365.WellKnownFolderName.Calendar,
        filter,
        view);

      if (appointments != null)
      {
        return appointments.FirstOrDefault() as MSOffice365.Appointment;
      }

      return null;
    }

    private Appointment ConvertAppointment(MSOffice365.Appointment appointment)
    {
      if (appointment == null)
      {
        return null;
      }

      var proxy = new Appointment
        {
          DisplayTo = appointment.DisplayTo,
          End = appointment.End,
          ID = appointment.Id.ToString(),
          IsMeeting = appointment.IsMeeting,
          IsOrganizer = 
            appointment.MyResponseType == MSOffice365.MeetingResponseType.Organizer,
          IsRecurring = appointment.IsRecurring,
          Location = appointment.Location,
          ReminderMinutesBeforeStart = appointment.ReminderMinutesBeforeStart,
          Start = appointment.Start,
          Subject = appointment.Subject,
          UID = appointment.ICalUid,
          RecurrenceType = RecurrenceType.Once
        };

      var message = null as TextBody;

      if (appointment.TryGetProperty(
        MSOffice365.AppointmentSchema.TextBody,
        out message))
      {
        proxy.Message = message.ToString();
      }

      proxy.Attendees = new List<string>();

      var attendees = null as AttendeeCollection;

      if (appointment.TryGetProperty(
        MSOffice365.AppointmentSchema.RequiredAttendees,
        out attendees))
      {
        foreach (var attendee in attendees)
        {
          proxy.Attendees.Add(attendee.Address);
        }
      }

      if (proxy.IsRecurring)
      {
        if (appointment.Recurrence is MSOffice365.Recurrence.DailyPattern)
        {
          proxy.RecurrenceType = RecurrenceType.Dayly;
          proxy.RecurrenceInterval =
            ((MSOffice365.Recurrence.DailyPattern)appointment.Recurrence).Interval;
        }
        else if (appointment.Recurrence is MSOffice365.Recurrence.WeeklyPattern)
        {
          proxy.RecurrenceType = RecurrenceType.Weekly;
          proxy.RecurrenceInterval =
            ((MSOffice365.Recurrence.WeeklyPattern)appointment.Recurrence).Interval;
        }
        else if (appointment.Recurrence is MSOffice365.Recurrence.MonthlyPattern)
        {
          proxy.RecurrenceType = RecurrenceType.Monthly;
          proxy.RecurrenceInterval =
            ((MSOffice365.Recurrence.MonthlyPattern)appointment.Recurrence).Interval;
        }
        else if (appointment.Recurrence is MSOffice365.Recurrence.YearlyPattern)
        {
          proxy.RecurrenceType = RecurrenceType.Yearly;
          proxy.RecurrenceInterval =
            (int)((MSOffice365.Recurrence.YearlyPattern)appointment.Recurrence).Month;
        }

        proxy.StartRecurrence = appointment.Recurrence.StartDate;
        proxy.EndRecurrence = appointment.Recurrence.EndDate;         
      }

      return proxy;
    }

    private O Call<I, O>(string actionName, I request, Func<I, O> action)
    {
      var requestID = StoreRequest(actionName, request);

      try
      {
        var result = action(request);

        StoreResult(requestID, result);

        return result;
      }
      catch (Exception e)
      {
        StoreError(requestID, e);

        throw e;
      }
    }

    private long CallAsync<I, O>(string actionName, I request, Func<I, O> action)
    {
      var requestID = StoreRequest(actionName, request);

      Threading.Task.Factory.StartNew(
        () =>
        {
          try
          {
            StoreResult(requestID, action(request));
          }
          catch (Exception e)
          {
            StoreError(requestID, e);
          }
        });

      return requestID;
    }

    private static long StoreRequest<T>(string actionName, T request)
    {
      using (var model = new EWSQueueEntities())
      {
        var timeout = 2.0;
        
        try
        {
          timeout = 
            double.Parse(ConfigurationManager.AppSettings["RequestTimeout"]);
        }
        catch
        {
          // use the default value, 2 mins.
        }
        
        var item = new Queue
        {
          Operation = actionName,
          Request = ToXmlString(request),
          CreatedAt = DateTime.Now,
          ExpiresAt = DateTime.Now.AddMinutes(timeout)
        };

        model.Queues.Add(item);

        model.SaveChanges();

        return item.ID;
      }
    }

    private static void StoreResult(long requestID, object result)
    {
      using (var model = new EWSQueueEntities())
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
              item.Error = ToXmlString(
                new TimeoutException(
                  "Operation " + item.Operation + " is timed out."));
            }
          }

          item.Response = ToXmlString(result);

          model.SaveChanges();
        }
      }
    }

    private static void StoreError(long requestID, Exception error)
    {
      using (var model = new EWSQueueEntities())
      {
        var item = model.Queues.
          Where(request => request.ID == requestID).
          FirstOrDefault();

        if (item != null)
        {
          item.Error = ToXmlString(error);
        }

        model.SaveChanges();
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
    private static string ExchangeUserName;
    private static string ExchangePassword;
    private static string ExchangeUrl;
    private static Regex IsHtml = new Regex(@"\<html\>.*\</html\>", 
      RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.Singleline);
    #endregion
  }
}
