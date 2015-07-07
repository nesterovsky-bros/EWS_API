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
  [ServiceContract(Namespace = "https://www.bankhapoalim.co.il/")]
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
    string Create(string email, Appointment appointment);
    
    /// <summary>
    /// Starts Create method asynchronously.
    /// </summary>
    /// <param name="email">An e-mail address of the organizer.</param>
    /// <param name="appointment">
    /// an Appointment instance with data for the appointment.
    /// </param>
    /// <returns>a request ID.</returns>
    [OperationContract]
    long CreateBegin(string email, Appointment appointment);

    /// <summary>
    /// Finishes asynchronous Create method call.
    /// </summary>
    /// <param name="requestID">
    /// a request ID obtained in result of CreateBegin call.
    /// </param>
    /// <returns>
    /// An unique ID of the new appointment, or null when task not finished yet.
    /// </returns>
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
    /// <returns>a list of Appointment instances.</returns>
    [OperationContract]
    IEnumerable<Appointment> Get(string email,
      DateTime start,
      DateTime? end,
      int? maxResults);

    /// <summary>
    /// Starts Get method asynchronously.
    /// </summary>
    /// <param name="email">a target user's e-mail.</param>
    /// <param name="start">a start date.</param>
    /// <param name="end">an optional parameter, determines an end date.</param>
    /// <param name="maxResults">
    /// an optional parameter, determines maximum results in resonse.
    /// </param>
    /// <returns>a request ID.</returns>
    [OperationContract]
    long GetBegin(
      string email, 
      DateTime start, 
      DateTime? end, 
      int? maxResults);

    /// <summary>
    /// Finishes asynchronous Get method call.
    /// </summary>
    /// <param name="requestID">
    /// a request ID obtained in result of GetBegin call.
    /// </param>
    /// <returns>
    /// a list of Appointment instances, or null when task not finished yet.
    /// </returns>
    [OperationContract]
    IEnumerable<Appointment> GetEnd(long requestID);

    /// <summary>
    /// Finds an appointment by its ID in the calendar of the specified user.
    /// </summary>
    /// <param name="email">a target user's e-mail.</param>
    /// <param name="ID">
    /// the appointment unique ID received on successful Create method call.
    /// </param>
    /// <returns>
    /// an Appointment instance or null if the appointment was not found.
    /// </returns>
    [OperationContract]
    Appointment Find(string email, string ID);
    
    /// <summary>
    /// Starts Find method asynchronously.
    /// </summary>
    /// <param name="email">a target user's e-mail.</param>
    /// <param name="ID">
    /// the appointment unique ID received on successful Create method call.
    /// </param>
    /// <returns>a request ID.</returns>
    [OperationContract]
    long FindBegin(string email, string ID);

    /// <summary>
    /// Finishes asynchronous Find method call.
    /// </summary>
    /// <param name="requestID">
    /// a request ID obtained in result of FindBegin call.
    /// </param>
    /// <returns>
    /// a list of Appointment instances, or null when task not finished yet.
    /// </returns>
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
    /// Starts Update method asynchronously.
    /// </summary>
    /// <param name="email">a target user's e-mail.</param>
    /// <param name="appointment">
    /// an Appointment instance with new data for the appointment.
    /// </param>
    /// <returns>a request ID.</returns>
    [OperationContract]
    long UpdateBegin(string email, Appointment appointment);

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
    [OperationContract]
    bool? UpdateEnd(long requestID);

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
    /// Starts Cancel method asynchronously.
    /// </summary>
    /// <param name="email">an e-mail of the organizer of the appointment.</param>
    /// <param name="ID">the appointment unique ID.</param>
    /// <param name="reason">a text message to be sent to all participants.</param>
    /// <returns>a request ID.</returns>
    [OperationContract]
    long CancelBegin(string email, string ID, string reason);

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
    [OperationContract]
    bool? CancelEnd(long requestID);

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
    /// Starts Delete method asynchronously.
    /// </summary>
    /// <param name="email">an e-mail of the organizer of the appointment.</param>
    /// <param name="ID">the appointment unique ID.</param>
    /// <returns>a request ID.</returns>
    /// <remarks>Only the appointment organizer may delete it.</remarks>
    [OperationContract]
    long DeleteBegin(string email, string ID);

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
    [OperationContract]
    bool? DeleteEnd(long requestID);

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
    /// Starts Accept method asynchronously.
    /// </summary>
    /// <param name="email">an e-mail of the organizer of the appointment.</param>
    /// <param name="ID">the appointment unique ID.</param>
    /// <returns>a request ID.</returns>
    [OperationContract]
    long AcceptBegin(string email, string ID);

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
    [OperationContract]
    bool? AcceptEnd(long requestID);

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

    /// <summary>
    /// Starts Decline method asynchronously.
    /// </summary>
    /// <param name="email">an e-mail of the organizer of the appointment.</param>
    /// <param name="ID">the appointment unique ID.</param>
    /// <returns>a request ID.</returns>
    [OperationContract]
    long DeclineBegin(string email, string ID);

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
    [OperationContract]
    bool? DeclineEnd(long requestID);

    /// <summary>
    /// Notifies about a change in a specified mail box.
    /// </summary>
    /// <param name="email">A mail box where change has occured.</param>
    /// <param name="ID">An ID of item changed.</param>
    /// <param name="changeType">A change type: delete, create, modify.</param>
    [OperationContract]
    bool Notification(string email, string ID, string changeType);
  }
}
