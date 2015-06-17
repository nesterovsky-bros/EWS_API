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
using Bnhp.Tracers;
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
  [ServiceLoggingBehavior]
  public class Appointments : IAppointments
  {
    #region IAppointments Members
    /// <summary>
    /// Creates a new appointment/meeting and sends notifications to attendees.
    /// </summary>
    /// <param name="email">An e-mail address of the organizer.</param>
    /// <param name="appointment">
    /// an AppointmentProxy instance with data for the appointment.
    /// </param>
    /// <returns>An unique ID of the new appointment.</returns>
    /// <exception cref="Exception">in case of error.</exception>
    public long CreateBegin(string email, Appointment appointment)
    {
      var requestID = StoreInputParams("Create", email, appointment);

      Threading.Task.Factory.StartNew(
        () =>
        {
          var result = null as string;
          var error = null as string;

          try
          {
            var service = GetService(email);
            var meeting = new MSOffice365.Appointment(service);

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

            result = meeting.ICalUid;
          }
          catch (Exception e)
          {
            error = e.ToString();
          }

          StoreResult(requestID, result, error);
        });

      return requestID;
    }

    public string CreateEnd(long requestID)
    {
      return ReadResult<string>(requestID);
    }

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
    public long GetBegin(
      string email, 
      DateTime start, 
      DateTime? end,
      int? maxResults)
    {
      var startDate = start;
      var endDate = end.GetValueOrDefault(DateTime.Now);
      var requestedResults = maxResults.GetValueOrDefault(int.MaxValue - 1);

      var requestID = 
        StoreInputParams("Get", email, startDate, endDate, requestedResults);

      Threading.Task.Factory.StartNew(
        () =>
        {
          var result = new List<Appointment>();
          var error = null as string;

          try
          {
            MSOffice365.CalendarView view = new MSOffice365.CalendarView(
              startDate,
              endDate,
              requestedResults);

            // Item searches do not support Deep traversal.
            view.Traversal = MSOffice365.ItemTraversal.Shallow;

            var service = GetService(email);
            var appointments = service.FindAppointments(
              MSOffice365.WellKnownFolderName.Calendar,
              view);

            if (appointments != null)
            {
              foreach (var appointment in appointments)
              {
                result.Add(ConvertAppointment(appointment));
              }
            }
          }
          catch (Exception e)
          {
            error = e.ToString();
          }

          StoreResult(requestID, result, error);
        });

      return requestID;
    }

    public IEnumerable<Appointment> GetEnd(long requestID)
    {
      return ReadResult<List<Appointment>>(requestID);
    }

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
    public long FindBegin(string email, string UID)
    {
      var requestID = StoreInputParams("Find", email, UID);

      Threading.Task.Factory.StartNew(
        () =>
        {
          var result = null as Appointment;
          var error = null as string;

          try
          {
            var appointment = GetAppointment(email, UID);

            result = ConvertAppointment(appointment);
          }
          catch (Exception e)
          {
            error = e.ToString();
          }

          StoreResult(requestID, result, error);
        });

      return requestID;
    }

    public Appointment FindEnd(long requestID)
    { 
      return ReadResult<Appointment>(requestID);
    }

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
    public long UpdateBegin(string email, Appointment appointment)
    {
      var requestID = StoreInputParams("Update", email, appointment);

      Threading.Task.Factory.StartNew(
        () =>
        {
          var result = false;
          var error = null as string;

          try
          {
            var item = GetAppointment(email, appointment.UID);

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

              result = true;
            }
          }
          catch (Exception e)
          {
            error = e.ToString();
          }

          StoreResult(requestID, result, error);
        });

      return requestID;
    }

    public bool UpdateEnd(long requestID)
    {
      return ReadResult<bool>(requestID);
    }

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
      var appointment = GetAppointment(email, UID);

      if (appointment != null)
      {
        appointment.CancelMeeting(reason);

        return true;
      }

      return false;
    }

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
      var appointment = GetAppointment(email, UID);

      if (appointment != null)
      {
        appointment.Delete(MSOffice365.DeleteMode.MoveToDeletedItems, true);

        return true;
      }

      return false;
    }

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
      var appointment = GetAppointment(email, UID);

      if (appointment != null)
      {
        appointment.Accept(true);

        return true;
      }

      return false;
    }

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
      var appointment = GetAppointment(email, UID);

      if (appointment != null)
      {
        appointment.Decline(true);

        return true;
      }

      return false;
    }
    #endregion

    /// <summary>
    /// Gets a service instance.
    /// </summary>
    /// <returns>a ExchangeService instance.</returns>
    private ExchangeService GetService(string impersonatedUserId)
    {
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

      if (!string.IsNullOrEmpty(impersonatedUserId) &&
        (ExchangeUserName != impersonatedUserId))
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
    
    private static long StoreInputParams(
      string actionName,
      params object[] inputParams)
    {
      var data = new StringBuilder();
      var serializer = new NetDataContractSerializer();
      var writer = XmlWriter.Create(data);

      serializer.WriteObject(writer, inputParams);

      writer.Flush();

      using (EWSQueueEntities model = new EWSQueueEntities())
      {
        var item = new Queue
        {
          Operation = actionName,
          Request = data.ToString(),
          CreatedAt = DateTime.Now,
          ExpiresAt = DateTime.Now.AddDays(1)
        };

        model.Queues.Add(item);

        model.SaveChanges();

        return item.ID;
      }
    }

    private static void StoreResult(
      long requestID,
      object result,
      string error)
    {
      if (result == null)
      {
        return;
      }

      using (var model = new EWSQueueEntities())
      {
        var item = model.Queues.
          Where(request => request.ID == requestID).
          FirstOrDefault();

        if (item != null)
        {
          if (string.IsNullOrEmpty(error))
          {
            var data = new StringBuilder();
            var serializer = new NetDataContractSerializer();
            var writer = XmlWriter.Create(data);
            
            serializer.WriteObject(writer, result);

            writer.Flush();

            item.Response = data.ToString();
          }
          else
          {
            //item.Error = error;
          }

          model.SaveChanges();
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

        if ((item == null) || string.IsNullOrEmpty(item.Response))
        {
          return default(T);
        }

        var serializer = new NetDataContractSerializer();
        var reader = new StringReader(item.Response);

        return (T)serializer.ReadObject(XmlReader.Create(reader));
      }
    }

    #region private fields
    private static string ExchangeUserName;
    private static string ExchangePassword;
    private static string ExchangeUrl;
    private static Regex IsHtml = new Regex(@"\<html\>.*\</html\>", 
      RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.Singleline);
    #endregion
  }
}
