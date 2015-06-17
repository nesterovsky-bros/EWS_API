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
  /// The interface-wrapper for CRUD operations for Office365 appointments.
  /// </summary>
  [ServiceContract]
  public interface IAppointments
  {
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
    long CreateBegin(string email, Appointment appointment);

    [OperationContract]
    string CreateEnd(long requestID);

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
    [OperationContract]
    long GetBegin(
      string email, 
      DateTime start, 
      DateTime? end, 
      int? maxResults);

    [OperationContract]
    IEnumerable<Appointment> GetEnd(long requestID);

    /// <summary>
    /// Finds an appointment by its ID in the calendar of the specified user.
    /// </summary>
    /// <param name="email">a target user's e-mail.</param>
    /// <param name="UID">
    /// the appointment unique ID received on successful Create method call.
    /// </param>
    /// <returns>
    /// an Appointment instance or null if the appointment was not found.
    /// </returns>
    [OperationContract]
    long FindBegin(string email, string UID);

    [OperationContract]
    Appointment FindEnd(long requestID);

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
    [OperationContract]
    long UpdateBegin(string email, Appointment appointment);
    
    [OperationContract]
    bool UpdateEnd(long requestID);

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
    [OperationContract]
    bool Cancel(string email, string UID, string reason);

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
    [OperationContract]
    bool Delete(string email, string UID);

    /// <summary>
    /// Accepts the specified appointment.
    /// </summary>
    /// <param name="email">an e-mail of the organizer of the appointment.</param>
    /// <param name="UID">the appointment unique ID.</param>
    /// <returns>
    /// true when the operation succeseed, and false otherwise.
    /// </returns>
    [OperationContract]
    bool Accept(string email, string UID);

    /// <summary>
    /// Declines the specified appointment.
    /// </summary>
    /// <param name="email">an e-mail of the organizer of the appointment.</param>
    /// <param name="UID">the appointment unique ID.</param>
    /// <returns>
    /// true when the operation succeseed, and false otherwise.
    /// </returns>
    [OperationContract]
    bool Decline(string email, string UID);
  }
}
