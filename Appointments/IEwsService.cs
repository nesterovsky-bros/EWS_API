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
    #region Appointment and meeting
    /// <summary>
    /// Creates a new appointment/meeting and sends notifications to attendees.
    /// </summary>
    /// <param name="email">An e-mail address of the organizer.</param>
    /// <param name="appointment">
    /// an Appointment instance with data for the appointment.
    /// </param>
    /// <returns>An unique ID of the new appointment.</returns>
    /// <exception cref="IOException">in case of error.</exception>
    [OperationContract]
    string Create(string email, Appointment appointment);

    /// <summary>
    /// Retrieves all appointments' IDs that belongs to the specified range of dates.
    /// </summary>
    /// <param name="email">a target user's e-mail.</param>
    /// <param name="start">a start date.</param>
    /// <param name="end">an optional parameter, determines an end date.</param>
    /// <param name="maxResults">
    /// an optional parameter, determines maximum results in resonse.
    /// </param>
    /// <returns>a list of appointments' IDs.</returns>
    [OperationContract]
    IEnumerable<string> Find(string email,
      DateTime start,
      DateTime? end,
      int? maxResults);

    /// <summary>
    /// Gets an appointment by its unique ID.
    /// </summary>
    /// <param name="email">a target user's e-mail.</param>
    /// <param name="ID">an appointment's unique ID</param>
    /// <returns>
    /// an Appointment instance or null if the appointment with the specified ID
    /// was not found.
    /// </returns>
    [OperationContract]
    Appointment Get(string email, string ID);

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
    [OperationContract]
    bool Update(string email, Appointment appointment);

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
    [OperationContract]
    bool Cancel(string email, string ID, string reason);

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
    [OperationContract]
    bool Delete(string email, string ID);

    /// <summary>
    /// Accepts the specified appointment.
    /// </summary>
    /// <param name="email">an e-mail of the organizer of the appointment.</param>
    /// <param name="ID">the appointment unique ID.</param>
    /// <returns>
    /// true when the operation succeseed, and false otherwise.
    /// </returns>
    [OperationContract]
    bool Accept(string email, string ID);

    /// <summary>
    /// Declines the specified appointment.
    /// </summary>
    /// <param name="email">an e-mail of the organizer of the appointment.</param>
    /// <param name="ID">the appointment unique ID.</param>
    /// <returns>
    /// true when the operation succeseed, and false otherwise.
    /// </returns>
    [OperationContract]
    bool Decline(string email, string ID);
    #endregion

    #region E-Mail
   /// <summary>
    /// Sends the specified e-mail message to receivers.
    /// </summary>
    /// <param name="email">An e-mail address of the sender.</param>
    /// <param name="message">
    /// an EMailMessage instance with data to send.
    /// </param>
    /// <returns>An unique ID of the sent e-mail message.</returns>
    /// <exception cref="IOException">in case of error.</exception>
    [OperationContract]
    string Send(string email, EMailMessage message);

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
    [OperationContract]
    IEnumerable<string> FindMessages(string email, int? pageSize, int? offset);

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
    EMailMessage GetMessage(string email, string ID);

    /// <summary>
    /// Deletes an e-mail message specified by unique ID.
    /// </summary>
    /// <param name="email">an user's e-mail box.</param>
    /// <param name="ID">the e-mail message's unique ID.</param>
    /// <returns>
    /// true when the message was successfully deleted, and false otherwise.
    /// </returns>
    [OperationContract]
    bool DeleteMessage(string email, string ID);

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
    bool MoveTo(string email, string ID, string folder);

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
    bool CopyTo(string email, string ID, string folder);
    #endregion

    #region Notification
    /// <summary>
    /// Notifies about a change in a specified mail box.
    /// </summary>
    /// <param name="email">A mail box where change has occured.</param>
    /// <param name="ID">An ID of item changed.</param>
    /// <param name="changeType">A change type: delete, create, modify.</param>
    [OperationContract]
    bool Notification(string email, string ID, string changeType);

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
    [OperationContract]
    IEnumerable<Change> GetChanges(
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
    /// Optional number of record to skip in result.
    /// </param>
    /// <param name="take">
    /// Optional number of records to return from result.
    /// </param>
    /// <returns>A enumeration of changes.</returns>
    [OperationContract]
    IEnumerable<ChangeStats> GetChangeStats(
      string systemName,
      string email,
      string folderID,
      DateTime? startDate,
      DateTime? endDate,
      int? skip = 0,
      int? take = 0);
    #endregion
  }
}
