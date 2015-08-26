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
  using Microsoft.Exchange.WebServices.Data;
  
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

    #region CreateAppointment method
    /// <summary>
    /// Creates a new appointment and sends notifications to attendees.
    /// </summary>
    /// <param name="email">An e-mail address of the organizer.</param>
    /// <param name="appointment">
    /// an AppointmentProxy instance with data for the appointment.
    /// </param>
    /// <returns>An unique ID of the new appointment.</returns>
    /// <exception cref="Exception">in case of error.</exception>
    public string CreateAppointment(string email, Appointment appointment)
    {
      return Call(
        "CreateAppointment",
        new CreateAppointmentRequest
        {
          email = email,
          appointment = appointment
        },
        CreateAppointmentImpl);
    }

    public struct CreateAppointmentRequest
    {
      public string email;
      public Appointment appointment;
    }

    private string CreateAppointmentImpl(CreateAppointmentRequest request)
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
      SetCategories(appointment, proxy.Categories);

      appointment.ICalUid = 
        Guid.NewGuid().ToString() + email.Substring(email.IndexOf('@'));

      // SendMessage the proxy request
      appointment.Save(Office365.SendInvitationsMode.SendToAllAndSaveCopy);

      return appointment.Id.ToString();
    }
    #endregion

    #region FindAppointments method
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
    public IEnumerable<string> FindAppointments(
      string email,
      DateTime start,
      DateTime? end,
      int? maxResults)
    {
      return Call(
        "FindAppointments",
        new FindAppointmentsRequest
        {
          email = email,
          start = start,
          end = end,
          maxResults = maxResults
        },
        FindAppointmentsImpl);
    }

    public struct FindAppointmentsRequest
    {
      public string email;
      public DateTime start;
      public DateTime? end;
      public int? maxResults;
    }

    private IEnumerable<string> FindAppointmentsImpl(FindAppointmentsRequest request)
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

    #region GetAppointment method
    /// <summary>
    /// Gets an appointment by its ID in the calendar of the specified user.
    /// </summary>
    /// <param name="email">a target user's e-mail.</param>
    /// <param name="ID">
    /// the appointment unique ID received on successful CreateAppointment method call.
    /// </param>
    /// <returns>
    /// an Appointment instance or null if the appointment was not found.
    /// </returns>
    public Appointment GetAppointment(string email, string ID)
    {
      return Call(
        "GetAppointment",
        new ProcessAppointmentRequest
        {
          email = email,
          ID = ID
        },
        GetAppointmentImpl);
    }

    public struct ProcessAppointmentRequest
    {
      public string email;
      public string ID;
    }

    private Appointment GetAppointmentImpl(ProcessAppointmentRequest request)
    {
      var appointment = RetrieveAppointment(request.email, request.ID);

      return ConvertAppointment(appointment);
    }
    #endregion

    #region UpdateAppointment method
    /// <summary>
    /// Updates the specified appointment.
    /// Note: 
    ///   All the specified properties will be overwritten in the origin 
    ///   appointment.
    /// </summary>
    /// <param name="email">
    /// An e-mail address of an organizer or a participant of the appointment.
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
    public bool UpdateAppointment(string email, Appointment appointment)
    {
      return Call(
        "UpdateAppointment",
        new UpdateAppointmentRequest
        {
          email = email,
          appointment = appointment
        },
        UpdateAppointmentImpl).GetValueOrDefault(false);
    }

    public struct UpdateAppointmentRequest
    {
      public string email;
      public Appointment appointment;
    }

    private bool? UpdateAppointmentImpl(UpdateAppointmentRequest request)
    { 
      var proxy = request.appointment;

      if (proxy == null)
      {
        throw new ArgumentNullException("request.appointment");
      }

      var appointment = RetrieveAppointment(request.email, proxy.Id);

      // Note: only organizer may update the proxy.
      if ((appointment != null) &&
        (appointment.MyResponseType == Office365.MeetingResponseType.Organizer))
      {
        if (!proxy.Start.Equals(DateTime.MinValue))
        {
          appointment.Start = proxy.Start;
        }

        if (!proxy.End.Equals(DateTime.MinValue))
        {
          appointment.End = proxy.End;
        }
        
        if (!string.IsNullOrEmpty(proxy.Location))
        {
          appointment.Location = proxy.Location;
        }

        if (!string.IsNullOrEmpty(proxy.Subject))
        {
          appointment.Subject = proxy.Subject;
        }

        if ((proxy.ReminderMinutesBeforeStart > 0) && 
          (appointment.ReminderMinutesBeforeStart != proxy.ReminderMinutesBeforeStart))
        {
          appointment.ReminderMinutesBeforeStart = proxy.ReminderMinutesBeforeStart;
        }

        SetExtendedProperties(appointment, proxy.ExtendedProperties);
        SetCategories(appointment, proxy.Categories);

        // TODO: update more properties

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

    #region CancelAppointment method
    /// <summary>
    /// Cancels an appointment specified by unique ID.
    /// Sends corresponding notifications to all participants.
    /// </summary>
    /// <param name="email">an e-mail of the organizer of the proxy.</param>
    /// <param name="ID">the appointment unique ID.</param>
    /// <param name="reason">a text message to be sent to all participants.</param>
    /// <returns>
    /// true when the appointment was canceled successfully, and false otherwise.
    /// </returns>
    /// <remarks>Only the appointment organizer may cancel it.</remarks>
    public bool CancelAppointment(string email, string ID, string reason)
    {
      return Call(
        "CancelAppointment",
        new CancelAppointmentRequest
        {
          email = email,
          ID = ID,
          reason = reason
        },
        CancelAppointmentImpl).GetValueOrDefault(false);
    }

    public struct CancelAppointmentRequest
    {
      public string email;
      public string ID;
      public string reason;
    }

    private bool? CancelAppointmentImpl(CancelAppointmentRequest request)
    {
      var appointment = RetrieveAppointment(request.email, request.ID);

      if (appointment != null)
      {
        appointment.CancelMeeting(request.reason);

        return true;
      }

      return false;
    }
    #endregion

    #region DeleteAppointment method
    /// <summary>
    /// Deletes an appointment specified by unique ID from organizer's e-mail box and
    /// sends cancel notifications to all participants.
    /// </summary>
    /// <param name="email">an e-mail of the organizer of the appointment.</param>
    /// <param name="ID">the appointment unique ID.</param>
    /// <returns>
    /// true when the appointment was successfully deleted, and false otherwise.
    /// </returns>
    /// <remarks>Only the appointment organizer may delete it.</remarks>
    public bool DeleteAppointment(string email, string ID)
    {
      return Call(
        "DeleteAppointment",
        new ProcessAppointmentRequest
        {
          email = email,
          ID = ID
        },
        DeleteAppointmentImpl).GetValueOrDefault(false);
    }

    private bool? DeleteAppointmentImpl(ProcessAppointmentRequest request)
    {
      var appointment = RetrieveAppointment(request.email, request.ID);

      if (appointment != null)
      {
        appointment.Delete(Office365.DeleteMode.MoveToDeletedItems, true);

        return true;
      }

      return false;
    }
    #endregion

    #region AcceptAppointment method
    /// <summary>
    /// Accepts the specified appointment.
    /// </summary>
    /// <param name="email">an e-mail of the organizer of the appointment.</param>
    /// <param name="ID">the appointment unique ID.</param>
    /// <returns>
    /// true when the operation succeseed, and false otherwise.
    /// </returns>
    public bool AcceptAppointment(string email, string ID)
    {
      return Call(
        "AcceptAppointment",
        new ProcessAppointmentRequest
        {
          email = email,
          ID = ID
        },
        AcceptAppointmentImpl).GetValueOrDefault(false);
    }

    private bool? AcceptAppointmentImpl(ProcessAppointmentRequest request)
    {
      var appointment = RetrieveAppointment(request.email, request.ID);

      if (appointment != null)
      {
        appointment.Accept(true);

        return true;
      }

      return false;
    }
    #endregion

    #region DeclineAppointment method
    /// <summary>
    /// Declines the specified appointment.
    /// </summary>
    /// <param name="email">an e-mail of the organizer of the appointment.</param>
    /// <param name="ID">the appointment unique ID.</param>
    /// <returns>
    /// true when the operation succeseed, and false otherwise.
    /// </returns>
    public bool DeclineAppointment(string email, string ID)
    {
      return Call(
        "DeclineAppointment",
        new ProcessAppointmentRequest
        {
          email = email,
          ID = ID
        },
        DeclineAppointmentImpl).GetValueOrDefault(false);
    }

    private bool? DeclineAppointmentImpl(ProcessAppointmentRequest request)
    {
      var appointment = RetrieveAppointment(request.email, request.ID);

      if (appointment != null)
      {
        appointment.Decline(true);

        return true;
      }

      return false;
    }
    #endregion

    #region CreateMessage method
    /// <summary>
    /// Creates a new e-mail message and stores it to Draft folder.
    /// Later to this message one may add attachments and then send it 
    /// to recipients by the SendMessage method.
    /// </summary>
    /// <param name="email">An e-mail address of the sender.</param>
    /// <param name="message">
    /// an EMailMessage instance with data (subject, recipients, body etc.).
    /// </param>
    /// <returns>An unique ID of the stored e-mail message.</returns>
    /// <exception cref="IOException">in case of error.</exception>
    public string CreateMessage(string email, EMailMessage message)
    {
      return Call(
        "CreateMessage",
        new CreateMessageRequest
        {
          email = email,
          message = message
        },
        CreateMessageImpl);
    }

    public struct CreateMessageRequest
    {
      public string email;
      public EMailMessage message;
    }

    private string CreateMessageImpl(CreateMessageRequest request)
    {
      if (request.message == null)
      {
        throw new ArgumentNullException("request.message");
      }

      var email = request.email;
      var service = GetService(email);
      var emailMessage = new Office365.EmailMessage(service);
      var message = request.message;

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

      SetCategories(emailMessage, message.Categories);
      SetExtendedProperties(emailMessage, message.ExtendedProperties);

      emailMessage.Save(Office365.WellKnownFolderName.Drafts);

      return emailMessage.Id.ToString();
    }
    #endregion

    #region AddAttachment method
    /// <summary>
    /// Add a file attachment that to the specified e-mail message.
    /// </summary>
    /// <param name="email">a target user's e-mail.</param>
    /// <param name="ID">an e-mail message's unique ID.</param>
    /// <param name="name">an attachment's name to add.</param>
    /// <param name="content">the attachment's content itself.</param>
    /// <returns>
    /// true when the attachment was added successfully, and false otherwise.
    /// </returns>
    public bool AddAttachment(
      string email,
      string ID,
      string name,
      byte[] content)
    {
      return Call(
        "AddAttachment",
        new AddAttachmentRequest
        {
          email = email,
          ID = ID,
          name = name,
          content = content
        },
        AddAttachmentImpl).GetValueOrDefault(false);
    }

    public struct AddAttachmentRequest
    {
      public string email;
      public string ID;
      public string name;
      public byte[] content;
    }

    private bool? AddAttachmentImpl(AddAttachmentRequest request)
    {
      if (string.IsNullOrEmpty(request.ID))
      {
        throw new ArgumentNullException("request.ID");
      }

      var service = GetService(request.email);
      var message = Office365.EmailMessage.Bind(service, request.ID);

      if (message == null)
      {
        return false;
      }

      var attachment = 
        message.Attachments.AddFileAttachment(request.name, request.content);

      return attachment != null;
    }
    #endregion
    
    #region SendMessage method
    /// <summary>
    /// Sends the specified e-mail message to receivers.
    /// </summary>
    /// <param name="email">An e-mail address of the sender.</param>
    /// <param name="ID">an e-mail message's unique ID to send.</param>
    /// <returns>
    /// true when the message was successfully sent, and false otherwise.
    /// </returns>
    /// <exception cref="IOException">in case of error.</exception>
    public bool SendMessage(string email, string ID)
    {
      return Call(
        "SendMessage",
        new EMailProcessRequest
        {
          email = email,
          ID = ID,
          action = "send"
        },
        ProcessEMailImpl).GetValueOrDefault(false);
    }
    #endregion

    #region FindMessages method
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
          pageSize = 
            pageSize.HasValue && (pageSize.Value > 0) ? pageSize.Value : 1000,
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

    #region GetMessage method
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
        new EMailProcessRequest
        {
          email = email,
          ID = ID
        },
        GetMessageImpl);
    }

    private EMailMessage GetMessageImpl(EMailProcessRequest request)
    {
      var service = GetService(request.email);
      var message = Office365.EmailMessage.Bind(service, request.ID);
      
      return ConvertMessage(message);
    }
    #endregion

    #region GetAttachmentByName method
    /// <summary>
    /// Gets a file attachment by an e-mail ID and the attachment's name.
    /// </summary>
    /// <param name="email">a target user's e-mail.</param>
    /// <param name="ID">an e-mail message's unique ID.</param>
    /// <param name="name">an attachment's name to get.</param>
    /// <returns>
    /// the attachment's content or null when there is no 
    /// an attachment with such name.
    /// </returns>
    public byte[] GetAttachmentByName(string email, string ID, string name)
    {
      return Call(
          "GetAttachmentByName",
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
      public int? index;
    }

    private byte[] GetFileAttachmentImpl(GetFileAttachmentRequest request)
    {
      if (string.IsNullOrEmpty(request.ID))
      {
        throw new ArgumentNullException("request.ID");
      }

      var service = GetService(request.email);
      var message = Office365.EmailMessage.Bind(service, request.ID);
      var attachment = null as Office365.FileAttachment;

      if (message.HasAttachments)
      {
        if (request.index.HasValue)
        {
          var index = request.index.Value;

          if ((index >= 0) || (index < message.Attachments.Count))
          {
            attachment = message.Attachments[index] as Office365.FileAttachment;
          }
        }
        else
        {
          foreach (var item in message.Attachments)
          {
            attachment = item as Office365.FileAttachment;

            if ((attachment == null) || 
              attachment.IsInline || 
              attachment.IsContactPhoto ||
              (attachment.Name != request.name))
            {
              attachment = null;

              continue;
            }

            break;
          }
        }
      }

      if (attachment != null)
      {
        using (var stream = new MemoryStream())
        {
          attachment.Load(stream);

          return stream.ToArray();
        }
      }

      return null;
    }
    #endregion

    #region GetAttachmentByIndex method
    /// <summary>
    /// Gets a file attachment by an e-mail ID and the attachment's index.
    /// </summary>
    /// <param name="email">a target user's e-mail.</param>
    /// <param name="ID">an e-mail message's unique ID.</param>
    /// <param name="index">an attachment's index to get.</param>
    /// <returns>
    /// the attachment's content or null when there is no an attachment with such index.
    /// </returns>
    public byte[] GetAttachmentByIndex(string email, string ID, int index)
    {
      return Call(
          "GetAttachmentByIndex",
          new GetFileAttachmentRequest
          {
            email = email,
            ID = ID,
            index = index
          },
          GetFileAttachmentImpl);
    }
    #endregion

    #region GetMessageContent method
    /// <summary>
    /// Gets an e-mail message content by its ID.
    /// </summary>
    /// <param name="email">a target user's e-mail.</param>
    /// <param name="ID">an e-mail message's unique ID.</param>
    /// <returns>
    /// an MimeContent instance or null if the e-mail with 
    /// the specified ID was not found.
    /// </returns>
    public MimeContent GetMessageContent(string email, string ID)
    {
      return Call(
        "GetMessageContent",
        new EMailProcessRequest
        {
          email = email,
          ID = ID
        },
        GetMessageContentImpl);
    }

    private MimeContent GetMessageContentImpl(EMailProcessRequest request)
    {
      var service = GetService(request.email);
      var message = Office365.EmailMessage.Bind(service, request.ID);
      var mimeContent = message.MimeContent;

      return new MimeContent 
      {
        CharacterSet = mimeContent.CharacterSet,
        Content = mimeContent.Content
      };
    }
    #endregion

    #region DeleteMessage method
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

    #region MoveTo method
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

    #region CopyTo method
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
      using (var model = new EWSQueueEntities())
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
    /// Gets the specified proxy. 
    /// </summary>
    /// <param name="email">
    /// an e-mail address of an organizer or a participant of the proxy.
    /// </param>
    /// <param name="ID">an unique proxy ID to search.</param>
    /// <returns>
    /// an Appointment instance or null when the proxy was not found.
    /// </returns>
    private Office365.Appointment RetrieveAppointment(string email, string ID)
    {
      var service = GetService(email);

      return Office365.Appointment.Bind(service, new Office365.ItemId(ID));
    }

    private O ConvertItem<I, O>(I item)
      where I : Office365.Item
      where O : Item, new()
    {
      if (item == null)
      {
        return default(O);
      }

      var result = new O()
      {
        Id = item.Id.ToString()
      };

      var target = result.GetType();
      var source = item.GetType();

      foreach (Office365.PropertyDefinition definition in
        item.GetLoadedPropertyDefinitions())
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
            targetProperty.SetValue(result, property.GetValue(item));
          }
          else if (name == "Importance")
          {
            targetProperty.SetValue(result, (Importance)property.GetValue(item));
          }
          else if (name == "MeetingResponseType")
          {
            targetProperty.SetValue(result, (MeetingResponseType)property.GetValue(item));
          }
          else if (name == "Sensitivity")
          {
            targetProperty.SetValue(result, (Sensitivity)property.GetValue(item));
          }
        }
      }

      var content = null as Office365.MessageBody;

      if (item.TryGetProperty(Office365.ItemSchema.Body, out content))
      {
        switch (content.BodyType)
        {
          case BodyType.HTML:
          {
            result.TextBody =
              Encoding.UTF8.GetString(Convert.FromBase64String(content.Text));

            break;
          }
          case BodyType.Text:
          {
            result.TextBody = content.Text;

            break;
          }
        }
      }

      result.ExtendedProperties = GetExtendedProperties(item);
      result.Categories = GetCategories(item);

      return result;
    }

    private Appointment ConvertAppointment(Office365.Appointment appointment)
    {
      var result = ConvertItem<Office365.Appointment, Appointment>(appointment);

      if (result == null)
      {
        return null;
      }

      result.RequiredAttendees = GetAttendees(
        appointment, 
        Office365.AppointmentSchema.RequiredAttendees);

      result.OptionalAttendees = GetAttendees(
        appointment, 
        Office365.AppointmentSchema.OptionalAttendees);

      result.Resources = GetAttendees(
        appointment,
        Office365.AppointmentSchema.Resources);

      var emailAddress = null as Office365.EmailAddress;

      if (appointment.TryGetProperty(
        Office365.AppointmentSchema.Organizer,
        out emailAddress))
      {
        result.Organizer = new Attendee 
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
          result.Recurrence = new Recurrence
          {
            StartDate = recurrence.StartDate,
            EndDate = recurrence.EndDate,
            NumberOfOccurrences = recurrence.NumberOfOccurrences,
            OriginalTypeName = recurrence.GetType().FullName
          };

          if (recurrence is Office365.Recurrence.IntervalPattern)
          {
            result.Recurrence.Interval = 
              ((Office365.Recurrence.IntervalPattern)recurrence).Interval;

            if ((recurrence is Office365.Recurrence.DailyPattern) ||
              (recurrence is Office365.Recurrence.DailyRegenerationPattern))
            {
              result.Recurrence.Type = RecurrenceType.Daily;
            }
            else if ((recurrence is Office365.Recurrence.MonthlyPattern) ||
              (recurrence is Office365.Recurrence.MonthlyRegenerationPattern))
            {
              result.Recurrence.Type = RecurrenceType.Monthly;
            }
            else if ((recurrence is Office365.Recurrence.WeeklyPattern) ||
              (recurrence is Office365.Recurrence.WeeklyRegenerationPattern))
            {
              result.Recurrence.Type = RecurrenceType.Weekly;
            }
            else if ((recurrence is Office365.Recurrence.YearlyPattern) ||
              (recurrence is Office365.Recurrence.YearlyRegenerationPattern))
            {
              result.Recurrence.Type = RecurrenceType.Yearly;
            }
          }
        }

        Office365.OccurrenceInfo occurence;

        if (appointment.TryGetProperty(
          Office365.AppointmentSchema.FirstOccurrence, 
          out occurence))
        {
          result.FirstOccurrence = new OccurrenceInfo
          {
            Start = occurence.Start,
            End = occurence.End
          };
        }

        if (appointment.TryGetProperty(
          Office365.AppointmentSchema.LastOccurrence,
          out occurence))
        {
          result.LastOccurrence = new OccurrenceInfo
          {
            Start = occurence.Start,
            End = occurence.End
          };
        }
      }

      return result;
    }

    private EMailMessage ConvertMessage(Office365.EmailMessage message)
    {
      var result = ConvertItem<Office365.EmailMessage, EMailMessage>(message);

      if (result == null)
      {
        return null;
      };

      result.BccRecipients = GetRecipients(message, "BccRecipients");
      result.CcRecipients = GetRecipients(message, "CcRecipients");
      result.ToRecipients = GetRecipients(message, "ToRecipients");

      if (message.HasAttachments)
      {
        result.Attachments = new List<Attachment>();

        foreach (var attachment in message.Attachments)
        {
          result.Attachments.Add(
            new Attachment
            {
              Name = attachment.Name,
              ContentType = attachment.ContentType,
              Size = attachment.Size
            });
        }
      }

      return result;
    }

    private static List<ExtendedProperty> GetExtendedProperties(
      Office365.Item item)
    {
      var properties = null as Office365.ExtendedPropertyCollection;
      var result = null as List<ExtendedProperty>;

      if (item.TryGetProperty(
        Office365.ItemSchema.ExtendedProperties,
        out properties))
      {
        foreach (var property in properties)
        {
          if (property.PropertyDefinition.PropertySetId !=
            EwsService.ExtendedPropertySetId)
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
      Office365.Item item,
      List<ExtendedProperty> properties)
    {
      if (properties != null)
      {
        foreach (var property in properties)
        {
          item.SetExtendedProperty(
            new Office365.ExtendedPropertyDefinition(
              EwsService.ExtendedPropertySetId,
              property.Name,
              Office365.MapiPropertyType.String),
            property.Value);
        }
      }
    }

    private static List<string> GetCategories(Office365.Item item)
    {
      var categories = null as Office365.StringList;
      var result = null as List<string>;

      if (item.TryGetProperty(
        Office365.ItemSchema.Categories,
        out categories))
      {
        foreach (var category in categories)
        {
          if (result == null)
          {
            result = new List<string>();
          }

          result.Add(category);
        }
      }

      return result;
    }

    private static void SetCategories(
      Office365.Item item,
      List<string> categories)
    {
      if (categories != null)
      {
        item.Categories.AddRange(categories);
      }
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
        else if (string.Compare(request.action, "send", true) == 0)
        {
          message.SendAndSaveCopy();
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
