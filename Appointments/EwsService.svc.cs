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
  public class EwsService : IEwsService
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
        (appointment.Recurrence.Type != RecurrenceType.Unknown))
      {
        var start = appointment.Recurrence.StartDate;

        switch (appointment.Recurrence.Type)
        {
          case RecurrenceType.Daily:
          {
            meeting.Recurrence = new Office365.Recurrence.DailyPattern(
              start,
              appointment.Recurrence.Interval);

            break;
          }
          case RecurrenceType.Weekly:
          {
            meeting.Recurrence = new Office365.Recurrence.WeeklyPattern(
              start,
              appointment.Recurrence.Interval,
              (Office365.DayOfTheWeek)start.DayOfWeek);

            break;
          }
          case RecurrenceType.Monthly:
          {
            meeting.Recurrence = new Office365.Recurrence.MonthlyPattern(
              start,
              appointment.Recurrence.Interval,
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
        else
        {
          meeting.Recurrence.NumberOfOccurrences = appointment.Recurrence.NumberOfOccurrences;
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

    #region Find method
    /// <summary>
    /// Retrieves all appointments' IDs in the specified range of dates.
    /// </summary>
    /// <param name="email">a target user's e-mail.</param>
    /// <param name="start">a start date.</param>
    /// <param name="end">an optional parameter, determines an end date.</param>
    /// <param name="maxResults">
    /// an optional parameter, determines maximum results in resonse.
    /// </param>
    /// <returns>a collection of appointments' IDs.</returns>
    public IEnumerable<string> Find(
      string email,
      DateTime start,
      DateTime? end,
      int? maxResults)
    {
      return Call(
        "Find",
        new FindRequest
        {
          email = email,
          start = start,
          end = end,
          maxResults = maxResults
        },
        FindImpl);
    }

    public struct FindRequest
    {
      public string email;
      public DateTime start;
      public DateTime? end;
      public int? maxResults;
    }

    private IEnumerable<string> FindImpl(FindRequest request)
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

      var result = new List<string>();

      if (appointments != null)
      {
        foreach (var appointment in appointments)
        {
          result.Add(appointment.Id.ToString());
        }
      }

      return result;
    }
    #endregion

    #region Get method
    /// <summary>
    /// Gets an appointment by its ID in the calendar of the specified user.
    /// </summary>
    /// <param name="email">a target user's e-mail.</param>
    /// <param name="ID">
    /// the appointment unique ID received on successful Create method call.
    /// </param>
    /// <returns>
    /// an Appointment instance or null if the appointment was not found.
    /// </returns>
    public Appointment Get(string email, string ID)
    {
      return Call(
        "Get",
        new GetRequest
        {
          email = email,
          ID = ID
        },
        GetImpl);
    }

    public struct GetRequest
    {
      public string email;
      public string ID;
    }

    private Appointment GetImpl(GetRequest request)
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

        if ((appointment.ReminderMinutesBeforeStart > 0) && 
          (item.ReminderMinutesBeforeStart != appointment.ReminderMinutesBeforeStart))
        {
          item.ReminderMinutesBeforeStart = appointment.ReminderMinutesBeforeStart;
        }

        // TODO: update more properties, e.g. external properties

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

    #region Send method
    /// <summary>
    /// Creates and sends a new e-mail message to the recipients.
    /// </summary>
    /// <param name="email">An e-mail address of the creator.</param>
    /// <param name="message">
    /// an EMailMessage instance with data to send.
    /// </param>
    /// <returns>An unique ID of the saved e-mail message.</returns>
    /// <exception cref="IOException">in case of error.</exception>
    public string Send(string email, EMailMessage message)
    {
      return Call(
        "Send",
        new SendRequest
        {
          email = email,
          message = message
        },
        SendImpl);
    }

    public struct SendRequest
    {
      public string email;
      public EMailMessage message;
    }

    private string SendImpl(SendRequest request)
    {
      if (request.message == null)
      {
        throw new ArgumentNullException("request.message");
      }

      var email = request.email;
      var service = GetService(email);
      var emailMessage = new Office365.EmailMessage(service);
      var message = request.message;

      if (message.Attachments != null)
      {
        foreach (var path in message.Attachments)
        {
          if (File.Exists(path))
          {
            emailMessage.Attachments.AddFileAttachment(path);
          }
        }
      }

      if (message.BccRecipients != null)
      {
        foreach (var recipient in message.BccRecipients)
        {
          emailMessage.BccRecipients.Add(recipient.Name, recipient.Address);
        }
      }

      if (message.CcRecipients != null)
      {
        foreach (var recipient in message.CcRecipients)
        {
          emailMessage.CcRecipients.Add(recipient.Name, recipient.Address);
        }
      }

      if (message.ToRecipients != null)
      {
        foreach (var recipient in message.ToRecipients)
        {
          emailMessage.ToRecipients.Add(recipient.Name, recipient.Address);
        }
      }

      if (message.TextBody != null)
      {
        var bodyType = IsHtml.IsMatch(message.TextBody) ?
          Office365.BodyType.HTML : Office365.BodyType.Text;

        emailMessage.Body = 
          new Office365.MessageBody(bodyType, message.TextBody);
      }

      emailMessage.Importance = (Office365.Importance)message.Importance;
      emailMessage.From = emailMessage.Sender = 
        new Office365.EmailAddress(message.Sender.Name, message.Sender.Address);
      emailMessage.Sensitivity = (Office365.Sensitivity)message.Sensitivity;
      emailMessage.IsReadReceiptRequested = message.IsReadReceiptRequested;
      emailMessage.IsResponseRequested = message.IsResponseRequested;
      emailMessage.Subject = message.Subject;

      emailMessage.SendAndSaveCopy();

      return emailMessage.Id.ToString();
    }
    #endregion

    #region FindMessages
    /// <summary>
    /// Retrieves all e-mal messages' IDs from Inbox.
    /// </summary>
    /// <param name="email">a target user's e-mail.</param>
    /// <param name="pageSize">
    /// determines how much records from Inbox to return in resonse.
    /// The default pageSize is 1000 first e-mails.
    /// </param>
    /// <param name="offset">
    /// an optional parameter, determines start offset in Inbox.
    /// </param>
    /// <returns>a list of messages' IDs instances.</returns>
    public IEnumerable<string> FindMessages(
      string email, 
      int? pageSize, 
      int? offset)
    {
      return Call(
        "FindMessages",
        new FindMessagesRequest
        {
          email = email,
          pageSize = pageSize.HasValue ? pageSize.Value : 1000,
          offset = offset
        },
        FindMessagesImpl);
    }

    public struct FindMessagesRequest
    {
      public string email;
      public int pageSize;
      public int? offset;
    }

    private IEnumerable<string> FindMessagesImpl(FindMessagesRequest request)
    {
      var view = new Office365.ItemView(request.pageSize);

      if (request.offset.HasValue)
      {
        view.Offset = request.offset.Value;
      }

      view.Traversal = Office365.ItemTraversal.Shallow;

      var service = GetService(request.email);
      var items = service.FindItems(
        Office365.WellKnownFolderName.Inbox,
        view);
      var result = new List<string>();

      if (items != null)
      {
        foreach (var item in items)
        {
          result.Add(item.Id.ToString());
        }
      }

      return result;
    }
    #endregion

    #region GetMessage
    /// <summary>
    /// Gets an e-mail message by its ID.
    /// </summary>
    /// <param name="email">a target user's e-mail.</param>
    /// <param name="ID">an e-mail message's unique ID.</param>
    /// <returns>
    /// an EMailMessage instance or null if the e-mail with 
    /// the specified ID was not found.
    /// </returns>
    public EMailMessage GetMessage(string email, string ID)
    {
      return Call(
        "GetMessage",
        new GetMessageRequest
        {
          email = email,
          ID = ID
        },
        GetMessageImpl);
    }

    public struct GetMessageRequest
    {
      public string email;
      public string ID;
    }

    private EMailMessage GetMessageImpl(GetMessageRequest request)
    {
      var service = GetService(request.email);
      var message = Office365.EmailMessage.Bind(service, request.ID);

      return ConvertMessage(message);
    }
    #endregion

    #region GetFileAttachment
    /// <summary>
    /// Gets a file attachment by an e-mail ID and the attachment's name.
    /// </summary>
    /// <param name="email">a target user's e-mail.</param>
    /// <param name="ID">an e-mail message's unique ID.</param>
    /// <param name="name">an attachment's name to get.</param>
    /// <returns>
    /// an Attachment instance or null when there is no an attachment with such name.
    /// </returns>
    public Attachment GetFileAttachment(string email, string ID, string name)
    {
      return Call(
          "GetFileAttachment",
          new GetFileAttachmentRequest
          {
            email = email,
            ID = ID,
            name = name
          },
          GetFileAttachmentImpl);
    }

    public struct GetFileAttachmentRequest
    {
      public string email;
      public string ID;
      public string name;
    }

    private Attachment GetFileAttachmentImpl(GetFileAttachmentRequest request)
    {
      var service = GetService(request.email);
      var message = Office365.EmailMessage.Bind(service, request.ID);
      var result = null as Attachment;

      if (message.HasAttachments)
      {
        foreach (var attachment in message.Attachments)
        {
          if (attachment.IsInline)
          {
            continue;
          }

          var fileAttachment = attachment as Office365.FileAttachment;

          if ((fileAttachment == null) || fileAttachment.IsContactPhoto)
          {
            continue;
          }

          if (fileAttachment.Name != request.name)
          {
            continue;
          }

          result = new Attachment();

          result.ContentType = fileAttachment.ContentType;
          result.Name = fileAttachment.Name;

          using (var stream = new MemoryStream())
          {
            fileAttachment.Load(stream);

            result.Content = stream.ToArray();
          }
        }
      }

      return result;
    }
    #endregion

    #region DeleteMessage
    /// <summary>
    /// Deletes an e-mail message specified by unique ID.
    /// </summary>
    /// <param name="email">an user's e-mail box.</param>
    /// <param name="ID">the e-mail message's unique ID.</param>
    /// <returns>
    /// true when the message was successfully deleted, and false otherwise.
    /// </returns>
    public bool DeleteMessage(string email, string ID)
    {
      return Call(
        "DeleteMessage",
        new EMailProcessRequest
        {
          email = email,
          ID = ID,
          action = "delete"
        },
        ProcessEMailImpl).GetValueOrDefault(false);
    }
    #endregion

    #region MoveTo
    /// <summary>
    /// Moves the specified e-mail message to a folder.
    /// </summary>
    /// <param name="email">an user's e-mail box.</param>
    /// <param name="ID">the e-mail message's unique ID.</param>
    /// <param name="folder">a target folder where to move the message.</param>
    /// <returns>
    /// true when the message was successfully moved, and false otherwise.
    /// </returns>
    public bool MoveTo(string email, string ID, string folder)
    {
      return Call(
        "MoveTo",
        new EMailProcessRequest
        {
          email = email,
          ID = ID,
          folder = folder,
          action = "move"
        },
        ProcessEMailImpl).GetValueOrDefault(false);
    }
    #endregion

    #region CopyTo
    /// <summary>
    /// Copies the specified e-mail message to a folder.
    /// </summary>
    /// <param name="email">an user's e-mail box.</param>
    /// <param name="ID">the e-mail message's unique ID.</param>
    /// <param name="folder">a target folder where to copy the message.</param>
    /// <returns>
    /// true when the message was successfully copied, and false otherwise.
    /// </returns>
    public bool CopyTo(string email, string ID, string folder)
    {
      return Call(
        "CopyTo",
        new EMailProcessRequest
        {
          email = email,
          ID = ID,
          folder = folder,
          action = "copy"
        },
        ProcessEMailImpl).GetValueOrDefault(false);
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

    private EMailMessage ConvertMessage(Office365.EmailMessage message)
    {
      var result = new EMailMessage
      {
        Id = message.Id.ToString()
      };

      var target = result.GetType();
      var source = message.GetType();

      foreach (Office365.PropertyDefinition definition in
        message.GetLoadedPropertyDefinitions())
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
            targetProperty.SetValue(result, property.GetValue(message));
          }
          else if (name == "Importance")
          {
            targetProperty.SetValue(result, (Importance)property.GetValue(message));
          }
          else if (name == "Sensitivity")
          {
            targetProperty.SetValue(result, (Sensitivity)property.GetValue(message));
          }
        }
      }

      var content = null as Office365.TextBody;

      if (message.TryGetProperty(
        Office365.ItemSchema.TextBody,
        out content))
      {
        result.TextBody = content.ToString();
      }

      result.BccRecipients = GetRecipients(message, "BccRecipients");
      result.CcRecipients = GetRecipients(message, "CcRecipients");
      result.ToRecipients = GetRecipients(message, "ToRecipients");

      result.Attachments = new List<string>();

      if (message.HasAttachments)
      {
        foreach (var attachment in message.Attachments)
        {
          if (attachment.IsInline)
          {
            continue;
          }

          var fileAttachment = attachment as Office365.FileAttachment;

          if ((fileAttachment == null) || fileAttachment.IsContactPhoto)
          {
            continue;
          }

          result.Attachments.Add(fileAttachment.Name);
        }
      }

      return result;
    }

    private static List<EMailAddress> GetRecipients(
      Office365.EmailMessage message,
      string propertyName)
    {
      var list = new List<EMailAddress>();
      var property = typeof(Office365.EmailMessage).GetProperty(propertyName);

      if ((property != null) && property.CanRead)
      {
        var recipients = 
          property.GetValue(message) as Office365.EmailAddressCollection;

        if (recipients != null)
        {
          foreach (var recipient in recipients)
          {
            list.Add(
              new EMailAddress
              {
                Address = recipient.Address,
                Name = recipient.Name
              });
          }
        }
      }

      return list;
    }

    private static string FindFolder(
      Office365.ExchangeService service,
      string name)
    {
      var view = new Office365.FolderView(100000);
      var properties = new Office365.PropertySet(
        Office365.ItemSchema.Id,
        Office365.FolderSchema.DisplayName);

      view.Traversal = Office365.FolderTraversal.Deep;

      var folders = service.FindFolders(
        Office365.WellKnownFolderName.Root,
        view);

      return folders.
        Where(folder => folder.DisplayName == name).
        Select(folder => folder.Id.ToString()).
        FirstOrDefault();
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

    public struct EMailProcessRequest
    {
      public string email;
      public string ID;
      public string folder;
      public string action;
    }

    private bool? ProcessEMailImpl(EMailProcessRequest request)
    {
      var service = GetService(request.email);
      var message = Office365.EmailMessage.Bind(service, request.ID);

      if (message != null)
      {
        if (string.Compare(request.action, "delete", true) == 0)
        {
          message.Delete(Office365.DeleteMode.MoveToDeletedItems);
        }
        else if (!string.IsNullOrEmpty(request.folder))
        {
          var folderID = FindFolder(service, request.folder);

          if (string.IsNullOrEmpty(folderID))
          {
            if (string.Compare(request.action, "move", true) == 0)
            {
              message.Move(folderID);
            }
            else if (string.Compare(request.action, "copy", true) == 0)
            {
              message.Copy(folderID);
            }
            // else return false;
          }
        }
      }

      return false;
    }

    
    #endregion

    #region private fields
    private static Regex IsHtml = new Regex(@"\<html\>.*\</html\>", 
      RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.Singleline);
    #endregion
  }
}
