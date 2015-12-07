using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.ServiceModel;
using System.ServiceModel.Web;
using System.Text;
using System.Threading.Tasks;

namespace Bnhp.Office365
{
  /// <summary>
  /// The interface-wrapper for CRUD operations for Office365 appointments/e-mails.
  /// </summary>
  [ServiceContract(Namespace = "https://www.bankhapoalim.co.il/")]
  public interface IEwsService
  {
    #region Appointment and proxy
    /// <summary>
    /// Creates a new appointment and sends notifications to attendees.
    /// </summary>
    /// <param name="email">An e-mail address of the organizer.</param>
    /// <param name="appointment">
    /// an Appointment instance with data for the proxy.
    /// </param>
    /// <returns>An unique ID of the new proxy.</returns>
    /// <exception cref="IOException">in case of error.</exception>
    [OperationContract]
    Task<string> CreateAppointment(string email, Appointment appointment);

    /// <summary>
    /// Retrieves appointments that belongs to the specified range of dates.
    /// </summary>
    /// <param name="email">a target user's e-mail.</param>
    /// <param name="start">a start date.</param>
    /// <param name="end">an optional parameter, determines an end date.</param>
    /// <param name="maxResults">
    /// an optional parameter, determines maximum results in resonse.
    /// </param>
    /// <returns>a list of Appointment instances.</returns>
    [OperationContract]
    Task<IEnumerable<Appointment>> FindAppointments(string email,
      DateTime start,
      DateTime? end,
      int? maxResults);

    /// <summary>
    /// Retrieves appointments that belongs to the specified range of dates.
    /// </summary>
    /// <param name="email">a target user's e-mail.</param>
    /// <param name="start">a start date.</param>
    /// <param name="end">an optional parameter, determines an end date.</param>
    /// <param name="maxResults">
    /// an optional parameter, determines maximum results in resonse.
    /// </param>
    /// <returns>a list of Appointment ids.</returns>
    [OperationContract]
    Task<IEnumerable<string>> FindAppointmentsEx(
      string email,
      DateTime start,
      DateTime? end,
      int? maxResults);

    /// <summary>
    /// Gets an proxy by its unique ID.
    /// </summary>
    /// <param name="email">a target user's e-mail.</param>
    /// <param name="ID">an appointment's unique ID</param>
    /// <returns>
    /// an Appointment instance or null if the proxy with the specified ID
    /// was not found.
    /// </returns>
    [OperationContract]
    Task<Appointment> GetAppointment(string email, string ID);

    /// <summary>
    /// Updates the specified appointment.
    /// Note: 
    ///   All the specified properties will be overwritten in the origin 
    ///   proxy.
    /// </summary>
    /// <param name="email">
    /// An e-mail address of an organizer or a participant of the appointment.
    /// </param>
    /// <param name="appointment">
    /// An appointment to update. 
    /// The appointment ID must be not null.
    /// </param>
    /// <returns>
    /// true when the proxy was modified successfully, and false otherwise.
    /// </returns>
    /// <remarks>
    /// Only organizer can update an proxy.
    /// </remarks>
    [OperationContract]
    Task<bool> UpdateAppointment(string email, Appointment appointment);

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
    [OperationContract]
    Task<bool> CancelAppointment(string email, string ID, string reason);

    /// <summary>
    /// Delets an appointment specified by unique ID from organizer's e-mail box and
    /// sends cancel notifications to all participants.
    /// </summary>
    /// <param name="email">an e-mail of the organizer of the proxy.</param>
    /// <param name="ID">the appointment unique ID.</param>
    /// <returns>
    /// true when the appointment was successfully deleted, and false otherwise.
    /// </returns>
    /// <remarks>Only the appointment organizer may delete it.</remarks>
    [OperationContract]
    Task<bool> DeleteAppointment(string email, string ID);

    /// <summary>
    /// Accepts the specified appointment.
    /// </summary>
    /// <param name="email">an e-mail of the organizer of the appointment.</param>
    /// <param name="ID">the appointment unique ID.</param>
    /// <returns>
    /// true when the operation succeseed, and false otherwise.
    /// </returns>
    [OperationContract]
    Task<bool> AcceptAppointment(string email, string ID);

    /// <summary>
    /// Declines the specified appointment.
    /// </summary>
    /// <param name="email">an e-mail of the organizer of the appointment.</param>
    /// <param name="ID">the appointment unique ID.</param>
    /// <returns>
    /// true when the operation succeseed, and false otherwise.
    /// </returns>
    [OperationContract]
    Task<bool> DeclineAppointment(string email, string ID);
    #endregion

    #region E-Mail
    /// <summary>
    /// Creates a new e-mail message and stores it to Draft folder.
    /// Later this message may be sent by the SendMessage method.
    /// </summary>
    /// <param name="email">An e-mail address of the sender.</param>
    /// <param name="message">
    /// an EMailMessage instance with data (subject, recipients, body etc.).
    /// </param>
    /// <returns>An unique ID of the stored e-mail message.</returns>
    /// <exception cref="IOException">in case of error.</exception>
    [OperationContract]
    Task<string> CreateMessage(string email, EMailMessage message);

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
    [OperationContract]
    Task<bool> AddAttachment(
      string email, 
      string ID, 
      string name, 
      byte[] content);

    /// <summary>
    /// Sends the specified e-mail message to receivers.
    /// </summary>
    /// <param name="email">An e-mail address of the sender.</param>
    /// <param name="ID">an e-mail message's unique ID to send.</param>
    /// <returns>
    /// true when the message was successfully sent, and false otherwise.
    /// </returns>
    /// <exception cref="IOException">in case of error.</exception>
    [OperationContract]
    Task<bool> SendMessage(string email, string ID);

    /// <summary>
    /// Retrieves e-mail messages' IDs from Inbox.
    /// </summary>
    /// <param name="email">a target user's e-mail.</param>
    /// <param name="pageSize">
    /// determines how much records from Inbox to return in resonse.
    /// The default pageSize is 1000 first e-mails.
    /// </param>
    /// <param name="offset">
    /// an optional parameter, determines start offset in Inbox.
    /// </param>
    /// <returns>a list of EMailMessage instances.</returns>
    [OperationContract]
    Task<IEnumerable<EMailMessage>> FindMessages(
      string email, 
      int? pageSize, 
      int? offset);

    /// <summary>
    /// Gets an e-mail message by its ID.
    /// </summary>
    /// <param name="email">a target user's e-mail.</param>
    /// <param name="ID">an e-mail message's unique ID.</param>
    /// <returns>
    /// an EMailMessage instance or null if the e-mail with 
    /// the specified ID was not found.
    /// </returns>
    [OperationContract]
    Task<EMailMessage> GetMessage(string email, string ID);

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
    [OperationContract]
    Task<byte[]> GetAttachmentByName(string email, string ID, string name);

    /// <summary>
    /// Gets a file attachment by an e-mail ID and the attachment's index.
    /// </summary>
    /// <param name="email">a target user's e-mail.</param>
    /// <param name="ID">an e-mail message's unique ID.</param>
    /// <param name="index">an attachment's index to get.</param>
    /// <returns>
    /// the attachment's content or null when there is no an attachment with such index.
    /// </returns>
    [OperationContract]
    Task<byte[]> GetAttachmentByIndex(string email, string ID, int index);

    /// <summary>
    /// Deletes a file attachment from the specified message.
    /// </summary>
    /// <param name="email">a target user's e-mail.</param>
    /// <param name="ID">an e-mail message's unique ID.</param>
    /// <param name="name">an attachment's name to delete.</param>
    /// <returns>
    /// true when the specified attachment was successfully deleted, 
    /// and false otherwise.
    /// </returns>
    [OperationContract]
    Task<bool> DeleteAttachmentByName(string email, string ID, string name);

    /// <summary>
    /// Gets an e-mail message content by its ID.
    /// </summary>
    /// <param name="email">a target user's e-mail.</param>
    /// <param name="ID">an e-mail message's unique ID.</param>
    /// <returns>
    /// an MimeContent instance or null if the e-mail with 
    /// the specified ID was not found.
    /// </returns>
    [OperationContract]
    Task<MimeContent> GetMessageContent(string email, string ID);

    /// <summary>
    /// Deletes an e-mail message specified by unique ID.
    /// </summary>
    /// <param name="email">an user's e-mail box.</param>
    /// <param name="ID">the e-mail message's unique ID.</param>
    /// <returns>
    /// true when the message was successfully deleted, and false otherwise.
    /// </returns>
    [OperationContract]
    Task<bool> DeleteMessage(string email, string ID);
    
    /// <summary>
    /// Updates an e-mail message specified by unique ID.
    /// </summary>
    /// <param name="email">an user's e-mail box.</param>
    /// <param name="ID">the e-mail message's unique ID.</param>
    /// <returns>
    /// true when the message was successfully deleted, and false otherwise.
    /// </returns>
    [OperationContract]
    Task<bool> UpdateMessage(string email, EMailMessage changedMessage);

    /// <summary>
    /// Moves the specified e-mail message to a folder.
    /// </summary>
    /// <param name="email">an user's e-mail box.</param>
    /// <param name="ID">the e-mail message's unique ID.</param>
    /// <param name="folder">a target folder where to move the message.</param>
    /// <returns>
    /// true when the message was successfully moved, and false otherwise.
    /// </returns>
    [OperationContract]
    Task<bool> MoveTo(string email, string ID, string folder);

    /// <summary>
    /// Copies the specified e-mail message to a folder.
    /// </summary>
    /// <param name="email">an user's e-mail box.</param>
    /// <param name="ID">the e-mail message's unique ID.</param>
    /// <param name="folder">a target folder where to copy the message.</param>
    /// <returns>
    /// true when the message was successfully copied, and false otherwise.
    /// </returns>
    [OperationContract]
    Task<bool> CopyTo(string email, string ID, string folder);
    #endregion

    #region Notification
    /// <summary>
    /// Notifies about a change in a specified mail box.
    /// </summary>
    /// <param name="email">A mail box where change has occured.</param>
    /// <param name="ID">An ID of proxy changed.</param>
    /// <param name="changeType">A change type: delete, create, modify.</param>
    [OperationContract]
    Task<bool> Notification(string email, string ID, string changeType);

    /// <summary>
    /// Gets a set of changes.
    /// </summary>
    /// <param name="systemName">An optional system name.</param>
    /// <param name="email">Optional email address.</param>
    /// <param name="folderID">Optional filder id.</param>
    /// <param name="startDate">Optional start date.</param>
    /// <param name="endDate">Optional end date.</param>
    /// <param name="skip">
    /// Optional number of records to skip in result.
    /// </param>
    /// <param name="take">
    /// Optional number of records to return from result.
    /// </param>
    /// <returns>A enumeration of changes.</returns>
    [OperationContract]
    Task<IEnumerable<Change>> GetChanges(
      string systemName,
      string email,
      string folderID,
      DateTime? startDate,
      DateTime? endDate,
      int? skip = 0,
      int? take = 0);

    /// <summary>
    /// Gets change stats.
    /// </summary>
    /// <param name="systemName">An optional system name.</param>
    /// <param name="email">Optional email address.</param>
    /// <param name="folderID">Optional filder id.</param>
    /// <param name="startDate">Optional start date.</param>
    /// <param name="endDate">Optional end date.</param>
    /// <param name="skip">
    /// Optional number of records to skip in result.
    /// </param>
    /// <param name="take">
    /// Optional number of records to return from result.
    /// </param>
    /// <returns>A enumeration of changes.</returns>
    [OperationContract]
    Task<IEnumerable<ChangeStats>> GetChangeStats(
      string systemName,
      string email,
      string folderID,
      DateTime? startDate,
      DateTime? endDate,
      int? skip = 0,
      int? take = 0);
    #endregion

    #region Manipulations with mailbox groups.
    /// <summary>
    /// Enumerates a mailboxes of a specified bank system.
    /// </summary>
    /// <param name="systemName">A system name.</param>
    /// <param name="skip">
    /// Optional number of record to skip in result.
    /// </param>
    /// <param name="take">
    /// Optional number of records to return from result.
    /// </param>
    /// <returns>A enumeration of mailboxes.</returns>
    [OperationContract]
    Task<IEnumerable<string>> GetBankSystemMailboxes(
      string systemName,
      int? skip = null,
      int? take = null);

    /// <summary>
    /// Adds mailboxes to a local bank system.
    /// </summary>
    /// <param name="systemName">A system name.</param>
    /// <param name="mailboxes">Mailboxes to add.</param>
    /// <returns>Action task.</returns>
    [OperationContract]
    Task<bool> AddBankSystemMailboxes(
      string systemName, 
      string[] mailboxes);

    /// <summary>
    /// Removes mailboxes from a local bank system.
    /// </summary>
    /// <param name="systemName">A system name.</param>
    /// <param name="mailboxes">Mailboxes to remove.</param>
    /// <returns>Action task.</returns>
    [OperationContract]
    Task<bool> RemoveBankSystemMailboxes(
      string systemName,
      string[] mailboxes);
    #endregion
  }
}
