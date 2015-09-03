namespace Bnhp.Office365
{
  using System;
  using System.Collections.Generic;
  using System.IO;
  using System.Linq;
  using System.Runtime.Serialization;
  using System.ServiceModel;
  using System.Text;
  using System.Text.RegularExpressions;
  using System.Xml;
  using System.Data.Entity;
  
  using Microsoft.Practices.Unity;

  using Office365 = Microsoft.Exchange.WebServices.Data;
  using System.Threading;
  
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
      var service = GetService(email);
      var officeAppointment = new Office365.Appointment(service);
      var proxy = appointment;

      // Set the properties on the proxy object to create the proxy.
      officeAppointment.Subject = proxy.Subject;

      if (!string.IsNullOrEmpty(proxy.TextBody))
      {
        officeAppointment.Body = new Office365.MessageBody(
          IsHtml.IsMatch(proxy.TextBody) ?
            Office365.BodyType.HTML : Office365.BodyType.Text,
          proxy.TextBody);
      }

      officeAppointment.Start = proxy.Start;
      officeAppointment.End = proxy.End;
      officeAppointment.Location = proxy.Location;
      officeAppointment.AllowNewTimeProposal = true;
      officeAppointment.Importance = (Office365.Importance)proxy.Importance;
      officeAppointment.ReminderMinutesBeforeStart = proxy.ReminderMinutesBeforeStart;

      if ((proxy.Recurrence != null) && 
        (proxy.Recurrence.Type != RecurrenceType.Unknown))
      {
        var start = proxy.Recurrence.StartDate;

        switch (proxy.Recurrence.Type)
        {
          case RecurrenceType.Daily:
          {
            officeAppointment.Recurrence = new Office365.Recurrence.DailyPattern(
              start,
              proxy.Recurrence.Interval);

            break;
          }
          case RecurrenceType.Weekly:
          {
            officeAppointment.Recurrence = new Office365.Recurrence.WeeklyPattern(
              start,
              proxy.Recurrence.Interval,
              (Office365.DayOfTheWeek)start.DayOfWeek);

            break;
          }
          case RecurrenceType.Monthly:
          {
            officeAppointment.Recurrence = new Office365.Recurrence.MonthlyPattern(
              start,
              proxy.Recurrence.Interval,
              start.Day);

            break;
          }
          case RecurrenceType.Yearly:
          {
            officeAppointment.Recurrence =
              new Office365.Recurrence.YearlyPattern(
                start,
                (Office365.Month)start.Month,
                start.Day);

            break;
          }
        }

        if (proxy.Recurrence.HasEnd)
        {
          officeAppointment.Recurrence.EndDate = proxy.Recurrence.EndDate;
        }
        else
        {
          officeAppointment.Recurrence.NumberOfOccurrences = proxy.Recurrence.NumberOfOccurrences;
        }
      }

      if (proxy.RequiredAttendees != null)
      {
        foreach (var attendee in proxy.RequiredAttendees)
        {
          officeAppointment.RequiredAttendees.Add(attendee.Address);
        }
      }

      if (proxy.OptionalAttendees != null)
      {
        foreach (var attendee in proxy.OptionalAttendees)
        {
          officeAppointment.OptionalAttendees.Add(attendee.Address);
        }
      }

      if (proxy.Resources != null)
      {
        foreach (var resource in proxy.Resources)
        {
          officeAppointment.Resources.Add(resource.Address);
        }
      }

      SetExtendedProperties(officeAppointment, proxy.ExtendedProperties);
      SetCategories(officeAppointment, proxy.Categories);

      officeAppointment.ICalUid = 
        Guid.NewGuid().ToString() + email.Substring(email.IndexOf('@'));

      // SendMessage the proxy request
      Sync(() => officeAppointment.Save(Office365.SendInvitationsMode.SendToAllAndSaveCopy));

      return officeAppointment.Id.ToString();
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
      Office365.CalendarView view = new Office365.CalendarView(
        start,
        end.GetValueOrDefault(DateTime.Now),
        maxResults.GetValueOrDefault(int.MaxValue - 1));

      // Item searches do not support Deep traversal.
      view.Traversal = Office365.ItemTraversal.Shallow;

      var service = GetService(email);
      var appointments = Sync(() =>
        service.FindAppointments(
        Office365.WellKnownFolderName.Calendar,
        view));

      var result = new List<string>();

      if (appointments != null)
      {
        foreach(var appointment in appointments)
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
      var appointment = RetrieveAppointment(email, ID);

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
      var proxy = appointment;

      if (proxy == null)
      {
        throw new ArgumentNullException("appointment");
      }

      var officeAppointment = RetrieveAppointment(email, proxy.Id);

      // Note: only organizer may update the proxy.
      if ((officeAppointment != null) &&
        (officeAppointment.MyResponseType == Office365.MeetingResponseType.Organizer))
      {
        if (!proxy.Start.Equals(DateTime.MinValue))
        {
          officeAppointment.Start = proxy.Start;
        }

        if (!proxy.End.Equals(DateTime.MinValue))
        {
          officeAppointment.End = proxy.End;
        }

        if (!string.IsNullOrEmpty(proxy.Location))
        {
          officeAppointment.Location = proxy.Location;
        }

        if (!string.IsNullOrEmpty(proxy.Subject))
        {
          officeAppointment.Subject = proxy.Subject;
        }

        if ((proxy.ReminderMinutesBeforeStart > 0) &&
          (officeAppointment.ReminderMinutesBeforeStart != proxy.ReminderMinutesBeforeStart))
        {
          officeAppointment.ReminderMinutesBeforeStart = proxy.ReminderMinutesBeforeStart;
        }

        if (!string.IsNullOrEmpty(proxy.TextBody))
        {
          officeAppointment.Body = new Office365.MessageBody(
            IsHtml.IsMatch(proxy.TextBody) ?
              Office365.BodyType.HTML : Office365.BodyType.Text,
            proxy.TextBody);
        }

        SetExtendedProperties(officeAppointment, proxy.ExtendedProperties);
        SetCategories(officeAppointment, proxy.Categories);

        // TODO: update more properties

        // Unless explicitly specified, the default is to use SendToAllAndSaveCopy.
        // This can convert an proxy into a proxy. To avoid this,
        // explicitly set SendToNone on non-meetings.
        var mode = officeAppointment.IsMeeting ?
          Office365.SendInvitationsOrCancellationsMode.SendToAllAndSaveCopy :
          Office365.SendInvitationsOrCancellationsMode.SendToNone;

        Sync(() => officeAppointment.Update(Office365.ConflictResolutionMode.AlwaysOverwrite, mode));

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
      var appointment = RetrieveAppointment(email, ID);

      if (appointment != null)
      {
        Sync(() => appointment.CancelMeeting(reason));

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
      var appointment = RetrieveAppointment(email, ID);

      if (appointment != null)
      {
        Sync(() => appointment.Delete(Office365.DeleteMode.MoveToDeletedItems, true));

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
      var appointment = RetrieveAppointment(email, ID);

      if (appointment != null)
      {
        Sync(() => appointment.Accept(true));

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
      var appointment = RetrieveAppointment(email, ID);

      if (appointment != null)
      {
        Sync(() => appointment.Decline(true));

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
      if (message == null)
      {
        throw new ArgumentNullException("message");
      }

      var service = GetService(email);
      var emailMessage = new Office365.EmailMessage(service);

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

      Sync(() => emailMessage.Save(Office365.WellKnownFolderName.Drafts));

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
      if (string.IsNullOrEmpty(ID))
      {
        throw new ArgumentNullException("ID");
      }

      var service = GetService(email);
      var message = Sync(() => Office365.EmailMessage.Bind(service, ID));

      if (message == null)
      {
        return false;
      }

      var attachment =
        Sync(() => message.Attachments.AddFileAttachment(name, content));

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
      return ProcessEMailImpl(email, ID, "send");
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
      var view = new Office365.ItemView(
        pageSize.HasValue && (pageSize.Value > 0) ? pageSize.Value : 1000);

      if (offset.HasValue)
      {
        view.Offset = offset.Value;
      }

      view.Traversal = Office365.ItemTraversal.Shallow;

      var service = GetService(email);
      var items = Sync(() => service.FindItems(
        Office365.WellKnownFolderName.Inbox,
        view));
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
      var service = GetService(email);
      var message = Sync(() => Office365.EmailMessage.Bind(service, ID));

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
      return GetFileAttachmentImpl(email, ID, name);
    }

    /// <summary>
    /// Gets a file attachment by an e-mail ID and the attachment's name or 
    /// by index.
    /// </summary>
    /// <param name="email">a target user's e-mail.</param>
    /// <param name="ID">an e-mail message's unique ID.</param>
    /// <param name="name">an attachment's name to get.</param>
    /// <param name="index">optional attachment's index.</param>
    /// <returns>
    /// the attachment's content or null when there is no 
    /// an attachment with such name.
    /// </returns>
    private byte[] GetFileAttachmentImpl(
      string email, 
      string ID, 
      string name,
      int? index = 0)
    {
      if (string.IsNullOrEmpty(ID))
      {
        throw new ArgumentNullException("ID");
      }

      var service = GetService(email);
      var message = Sync(() => Office365.EmailMessage.Bind(service, ID));
      var attachment = null as Office365.FileAttachment;

      if (message.HasAttachments)
      {
        if (index != null)
        {
          if ((index >= 0) || (index < message.Attachments.Count))
          {
            attachment = message.Attachments[index.Value] as Office365.FileAttachment;
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
              (attachment.Name != name))
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
      return GetFileAttachmentImpl(email, ID, null, index);
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
      var service = GetService(email);
      var message = Sync(() => Office365.EmailMessage.Bind(
        service,
        ID,
        new Office365.PropertySet(Office365.ItemSchema.MimeContent)));

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
      return ProcessEMailImpl(email, ID, "delete");
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
      return ProcessEMailImpl(email, ID, "move", folder);
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
      return ProcessEMailImpl(email, ID, "copy", folder);
    }
    #endregion

    #region Notify method
    /// <summary>
    /// Notifies about a change in a specified mail box.
    /// </summary>
    /// <param name="email">A mail box where change has occured.</param>
    /// <param name="ID">An ID of proxy changed.</param>
    /// <param name="changeType">A change type: delete, create, modify.</param>
    public bool Notification(string email, string ID, string changeType)
    {
      return true;
    }
    #endregion

    #region GetChanges and GetChangeStats
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
      using(var model = new EWSQueueEntities())
      {
        var query = GetChangesQuery(
          model,
          systemName,
          email,
          folderID,
          startDate,
          endDate);

        query = query.
          OrderBy(item => item.Timestamp).
          ThenBy(item => item.Email).
          ThenBy(item => item.ItemID);

        if (skip != null)
        {
          query = query.Skip(skip.Value);
        }

        if (take != null)
        {
          query = query.Take(take.Value);
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
      using(var model = new EWSQueueEntities())
      {
        var query = GetChangesQuery(
          model, 
          systemName,
          email,
          folderID,
          startDate,
          endDate);

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

        if (skip != null)
        {
          stats = stats.Skip(skip.Value);
        }

        if (take != null)
        {
          stats = stats.Take(take.Value);
        }

        return stats.ToList();
      }
    }

    private IQueryable<MailboxNotification> GetChangesQuery(
      EWSQueueEntities model,
      string systemName,
      string email,
      string folderID,
      DateTime? startDate,
      DateTime? endDate)
    {
      var query = systemName == null ?
        model.MailboxNotifications.AsNoTracking() :
        model.MailboxNotifications.AsNoTracking().Join(
          model.BankSystems.
            Where(item => item.GroupName == systemName).
            Join(
              model.BankSystemMailboxes,
              outer => outer.GroupName,
              inner => inner.GroupName,
              (outer, inner) => inner),
          outer => outer.Email,
          inner => inner.Email,
          (outer, inner) => outer);

      if (email != null)
      {
        query = query.Where(item => item.Email == email);
      }

      if (folderID != null)
      {
        query = query.Where(item => item.FolderID == folderID);
      }

      if (startDate != null)
      {
        query = query.Where(item => item.Timestamp >= startDate);
      }

      if (endDate != null)
      {
        query = query.Where(item => item.Timestamp <= endDate);
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

      var users = Settings.ApplicationUsers;
      var index = Interlocked.Increment(ref accessCount) % users.Length;
      var user = users[index];
      var service = new Office365.ExchangeService(
        Office365.ExchangeVersion.Exchange2013);

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

      return Sync(() => Office365.Appointment.Bind(service, new Office365.ItemId(ID)));
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
        result.TextBody = content.Text;
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
              ResponseType = (MeetingResponseType?)attendee.ResponseType
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
        if (result != null)
        {
          serializer.WriteObject(writer, result);
        }
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

    private bool ProcessEMailImpl(
      string email, 
      string ID, 
      string action, 
      string folder = null)
    {
      var service = GetService(email);
      var message = Office365.EmailMessage.Bind(service, ID);

      if (message != null)
      {
        if (string.Compare(action, "delete", true) == 0)
        {
          message.Delete(Office365.DeleteMode.MoveToDeletedItems);
        }
        else if (string.Compare(action, "send", true) == 0)
        {
          message.SendAndSaveCopy();
        }
        else if (!string.IsNullOrEmpty(folder))
        {
          var folderID = FindFolder(service, folder);

          if (!string.IsNullOrEmpty(folderID))
          {
            if (string.Compare(action, "move", true) == 0)
            {
              message.Move(folderID);
            }
            else if (string.Compare(action, "copy", true) == 0)
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

    /// <summary>
    /// Runs action in context of AccessSemaphore.
    /// </summary>
    /// <param name="action"></param>
    private void Sync(Action action)
    {
      var semaphore = Settings.AccessSemaphore;

      semaphore.Wait(TimeSpan.FromMinutes(Settings.RequestTimeout));

      try
      {
        action();
      }
      finally
      {
        semaphore.Release();
      }
    }
    private T Sync<T>(Func<T> action)
    {
      var semaphore = Settings.AccessSemaphore;

      semaphore.Wait(TimeSpan.FromMinutes(Settings.RequestTimeout));

      try
      {
        return action();
      }
      finally
      {
        semaphore.Release();
      }
    }

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

    /// <summary>
    /// Internal counter of number of accesses to GetService() method.
    /// </summary>
    private static int accessCount;
    #endregion
  }
}
