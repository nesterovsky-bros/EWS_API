using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.Serialization;
using System.Web;

namespace Bnhp.Office365
{
  /// <summary>
  /// A proxy class for Office 365 appointment.
  /// </summary>
  /// <seealso cref="https://msdn.microsoft.com/en-us/library/microsoft.exchange.webservices.data.appointment_properties(v=exchg.80).aspx"/>
  [DataContract(Namespace = "https://www.bankhapoalim.co.il/")]
  public class Appointment
  {
    /// <summary>
    /// Gets the ID of this item. 
    /// </summary>
    [DataMember]
    public string Id { get; internal set; }

    /// <summary>
    /// Gets and sets a value that indicates whether the associated object is an appointment, 
    /// a meeting, a response to a meeting, or a cancelled meeting.
    /// The possible values are:
    /// 0 - No response is required for this object. This is the case for appointment 
    ///     objects and meeting response objects.
    /// 1 - This meeting belongs to the organizer.
    /// 2 - This value on the attendee's meeting indicates that the attendee has 
    ///     tentatively accepted the meeting request.
    /// 3 - This value on the attendee's meeting t indicates that the attendee has 
    ///     accepted the meeting request.
    /// 4 - This value on the attendee's meeting indicates that the attendee has 
    ///     declined the meeting request.
    /// 5 - This value on the attendee's meeting indicates the attendee has not 
    ///     yet responded. This value is on the meeting request, meeting update, and meeting cancelation.
    /// </summary>
    [DefaultValue(0)]
    [DataMember]
    public int AppointmentState { get; internal set; }

    ///// <summary>
    ///// Gets a list of meetings that conflict with this appointment in the authenticated
    ////  user's calendar.
    ///// </summary>
    //[DataMember]
    //public List<Appointment> AdjacentMeetings { get; internal set; }

    /// <summary>
    /// Gets or sets a value indicating whether new time proposals are allowed for
    /// attendees of this meeting.
    /// </summary>
    [DefaultValue(true)]
    [DataMember]
    public bool AllowNewTimeProposal { get; set; }
    
    /// <summary>
    /// Gets the time when the attendee replied to the meeting request.
    /// </summary>
    [DataMember]
    public DateTime? AppointmentReplyTime { get; internal set; }
    
    //
    // Summary:
    //     Gets the sequence number of this appointment.
    [DataMember]
    public int AppointmentSequenceNumber { get; internal set; }
      
    /// <summary>
    /// Gets a value indicating the type of this appointment.
    /// The correct values are:
    //     0 - The appointment is non-recurring.
    //     1 - The appointment is an occurrence of a recurring appointment.
    //     2 - The appointment is an exception of a recurring appointment.
    //     3 - The appointment is the recurring master of a series.
    /// </summary>
    [DefaultValue(0)]
    [DataMember]
    public int AppointmentType { get; internal set; }
    
    /// <summary>
    /// Gets or sets the type of conferencing that will be used during the meeting.
    /// </summary>
    [DataMember]
    public int ConferenceType { get; set; }

    ///// <summary>
    ///// Gets a list of meetings that conflict with this appointment in the authenticated
    ///// user's calendar.
    ///// </summary>
    //[DataMember]
    //public List<Appointment> ConflictingMeetings { get; internal set; }

    /// <summary>
    /// Gets a text summarizing the To recipients of this item.
    /// </summary>
    [DataMember]
    public string DisplayTo { get; internal set; }
  
    /// <summary>
    /// Gets the date and time this item was created.
    /// </summary>
    [DataMember]
    public DateTime? DateTimeCreated { get; internal set; }

    /// <summary>
    /// Gets the time when this item was received.
    /// </summary>
    [DataMember]
    public DateTime? DateTimeReceived { get; internal set; }
    
    /// <summary>
    /// Gets the date and time this item was sent.
    /// </summary>
    [DataMember]
    public DateTime? DateTimeSent { get; internal set; }
    
    /// <summary>
    /// Gets a text summarizing the Cc receipients of this item.
    /// </summary>
    [DataMember]
    public string DisplayCc { get; internal set; }

    /// <summary>
    /// Gets the duration of this appointment.
    /// </summary>
    [DataMember]
    public TimeSpan? Duration { get; internal set; }
    
    /// <summary>
    /// Gets or sets the end time of the appointment.
    /// </summary>
    [DataMember]
    public DateTime End { get; set; }
    
    ///// <summary>
    ///// Gets or sets time zone of the end property of this appointment.
    ///// </summary>
    //[DataMember]
    //public TimeZoneInfo EndTimeZone { get; set; }
    
    /// <summary>
    /// Gets or sets the Enhanced location object.
    /// </summary>
    [DataMember]
    public string EnhancedLocation { get; set; }

    /// <summary>
    /// Gets an OccurrenceInfo identifying the first occurrence of this meeting.
    /// </summary>
    [DataMember]
    public OccurrenceInfo FirstOccurrence { get; internal set; }
    
    /// <summary>
    /// Gets the ICalendar DateTimeStamp.
    /// </summary>
    [DataMember]
    public DateTime? ICalDateTimeStamp { get; internal set; }
    
    /// <summary>
    /// Gets the ICalendar RecurrenceId.
    /// </summary>
    [DataMember]
    public DateTime? ICalRecurrenceId { get; internal set; }
    
    /// <summary>
    /// Gets or sets the ICalendar Uid.
    /// </summary>
    [DataMember]
    public string ICalUid { get; set; }

    /// <summary>
    /// Gets a value indicating whether the item has been modified since it was created.
    /// </summary>
    [DefaultValue(false)]
    [DataMember]
    public bool IsUnmodified { get; internal set;  }

    /// <summary>
    /// Gets and sets importance of the appointment.
    /// </summary>
    [DefaultValue(Importance.Normal)]
    [DataMember]
    public Importance Importance { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this appointment is an all day event.
    /// </summary>
    [DefaultValue(false)]
    [DataMember]
    public bool IsAllDayEvent { get; set; }
    
    /// <summary>
    /// Gets a value indicating whether the appointment has been cancelled.
    /// </summary>
    [DefaultValue(false)]
    [DataMember]
    public bool IsCancelled { get; internal set; }
    
    /// <summary>
    /// Gets a value indicating whether the appointment is a meeting.
    /// </summary>
    [DefaultValue(false)]
    [DataMember]
    public bool IsMeeting { get; internal set; }
    
    /// <summary>
    /// Gets or sets a value indicating whether this is an online meeting.
    /// </summary>
    [DefaultValue(false)]
    [DataMember]
    public bool IsOnlineMeeting { get; set; }
    
    /// <summary>
    /// Gets a value indicating whether the appointment is recurring.
    /// </summary>
    [DefaultValue(false)]
    [DataMember]
    public bool IsRecurring { get; set; }
       
    /// <summary>
    /// Gets or sets a value indicating whether responses are requested when invitations
    /// are sent for this meeting.
    /// </summary>
    [DefaultValue(false)]
    [DataMember]
    public bool IsResponseRequested { get; set; }
    
    /// <summary>
    /// Gets the Url for joining an online meeting
    /// </summary>
    [DataMember]
    public string JoinOnlineMeetingUrl { get; internal set; }
    
    /// <summary>
    /// Gets an OccurrenceInfo identifying the last occurrence of this meeting.
    /// </summary>
    [DataMember]
    public OccurrenceInfo LastOccurrence { get; internal set; }
    
    ////
    //// Summary:
    ////     Gets or sets a value indicating the free/busy status of the owner of this
    ////     appointment.
    //[DataMember]
    //public LegacyFreeBusyStatus LegacyFreeBusyStatus { get; set; }
    
    /// <summary>
    /// Gets or sets the location of this appointment.
    /// </summary>
    [DataMember]
    public string Location { get; set; }

    /// <summary>
    /// Gets the name of the user who last modified this item.
    /// </summary>
    [DataMember]
    public string LastModifiedName { get; internal set; }

    /// <summary>
    /// Gets the date and time this item was last modified.
    /// </summary>
    [DataMember]
    public DateTime LastModifiedTime { get; internal set; }

    /// <summary>
    /// Gets a value indicating whether the meeting request has already been sent.
    /// </summary>
    [DataMember]
    public bool MeetingRequestWasSent { get; internal set; }
    
    /// <summary>
    /// Gets or sets the URL of the meeting workspace. A meeting workspace is a shared
    /// Web site for planning meetings and tracking results.
    /// </summary>
    [DataMember]
    public string MeetingWorkspaceUrl { get; set; }
    
    ///// <summary>
    ///// Gets a list of modified occurrences for this meeting.
    ///// </summary>
    //[DataMember]
    //public List<OccurrenceInfo> ModifiedOccurrences { get; internal set; }
       
    /// <summary>
    /// Gets a value indicating what was the last response of the user that loaded
    /// this meeting.
    /// </summary>
    [DataMember]
    public MeetingResponseType MyResponseType { get; internal set; }
    
    /// <summary>
    /// Gets or sets the URL of the Microsoft NetShow online meeting.
    /// </summary>
    [DataMember]
    public string NetShowUrl { get; set; }
    
    ///// <summary>
    ///// Gets the Online Meeting Settings
    ///// </summary>
    //[DataMember]
    //public OnlineMeetingSettings OnlineMeetingSettings { get; internal set; }
    
    /// <summary>
    /// Gets a list of optional attendeed for this meeting.
    /// </summary>
    [DataMember]
    public List<Attendee> OptionalAttendees { get; internal set; }
    
    /// <summary>
    /// Gets the organizer of this meeting. The Organizer property is read-only and
    /// is only relevant for attendees.  The organizer of a meeting is automatically
    /// set to the user that created the meeting.
    /// </summary>
    [DataMember]
    public Attendee Organizer { get; internal set; }
    
    /// <summary>
    /// Gets the original start time of this appointment.
    /// </summary>
    [DataMember]
    public DateTime OriginalStart { get; internal set; }
       
    /// <summary>
    /// Gets or sets the recurrence pattern for this appointment. Available recurrence
    /// pattern classes include Recurrence.DailyPattern, Recurrence.MonthlyPattern
    /// and Recurrence.YearlyPattern.
    /// </summary>
    [DataMember]
    public Recurrence Recurrence { get; set; }

    /// <summary>
    /// Gets or sets the number of minutes before the start
    /// of this item that the reminder should be triggered.
    /// </summary>
    [DataMember]
    public int ReminderMinutesBeforeStart { get; set; }

    /// <summary>
    /// Gets a list of required attendees for this meeting.
    /// </summary>
    [DataMember]
    public List<Attendee> RequiredAttendees { get; internal set; }
    
    /// <summary>
    /// Gets a list of resources for this meeting.
    /// </summary>
    [DataMember]
    public List<Attendee> Resources { get; internal set; }

    /// <summary>
    /// Gets the date until which an item must be preserved.
    /// </summary>
    [DataMember]
    public DateTime? RetentionDate { get; set;  }
    
    /// <summary>
    /// Gets or sets the start time of the appointment.
    /// </summary>
    [DataMember]
    public DateTime Start { get; set; }

    /// <summary>
    /// Gets or sets the subject of this item.
    /// </summary>
    [DataMember]
    public string Subject { get; set; }

    /// <summary>
    /// Gets the sensitivity of this item.
    /// </summary>
    [DataMember]
    public Sensitivity Sensitivity { get; set; }

    ///// <summary>
    ///// Gets or sets time zone of the start property of this appointment.
    ///// </summary>
    //[DataMember]
    //public TimeZoneInfo StartTimeZone { get; set; }
    
    /// <summary>
    /// Gets the name of the time zone this appointment is defined in.
    /// </summary>
    [DataMember]
    public string TimeZone { get; internal set; }

    /// <summary>
    /// Gets the text body of the item as a string value.
    /// </summary>
    [DataMember]
    public string TextBody { get; set; }

    /// <summary>
    /// Gets a text indicating when this appointment occurs. The text returned by
    /// When is localized using the Exchange Server culture or using the culture
    /// specified in the PreferredCulture property of the ExchangeService object
    /// this appointment is bound to.
    /// </summary>
    [DataMember]
    public string When { get; internal set; }
  }

  /// <summary>
  /// Defines recurrence pattern for the appointment.
  /// </summary>
  [DataContract(Namespace = "https://www.bankhapoalim.co.il/")]
  public enum RecurrenceType
  {
    [EnumMember]
    Once,
    [EnumMember]
    Dayly,
    [EnumMember]
    Weekly,
    [EnumMember]
    Monthly,
    [EnumMember]
    Yearly
  }

  /// <summary>
  /// Defines recurrence pattern for the appointment.
  /// </summary>
  [DataContract(Namespace = "https://www.bankhapoalim.co.il/")]
  public class OccurrenceInfo
  {
    /// <summary>
    /// Gets the start date and time of the occurrence.
    /// </summary>
    [DataMember]
    public DateTime Start { get; set; }

    /// <summary>
    /// Gets the end date and time of the occurrence.
    /// </summary>
    [DataMember]
    public DateTime End { get; set; }
  }

  /// <summary>
  /// Specifies constants that define the type of response given to a meeting request.
  /// </summary>
  [DataContract(Namespace = "https://www.bankhapoalim.co.il/")]
  public enum MeetingResponseType
  { 
    /// <summary>
    /// An unknown response.
    /// </summary>
    [EnumMember]
    Unknown,
    /// <summary>
    /// No response; the authenticated user is the organizer of the meeting.
    /// </summary>
    [EnumMember]
    Organizer,
    /// <summary>
    /// A tentatively accept response.
    /// </summary>
    [EnumMember]
    Tentative,
    /// <summary>
    /// An accept response.
    /// </summary>
    [EnumMember]
    Accept,
    /// <summary>
    /// A decline response.
    /// </summary>
    [EnumMember]
    Decline,
    /// <summary>
    /// No response has been received.
    /// </summary>
    [EnumMember]
    NoResponseReceived
  }

  /// <summary>
  /// Represents a meeting attendee.
  /// </summary>
  [DataContract(Namespace = "https://www.bankhapoalim.co.il/")]
  public class Attendee
  {
    /// <summary>
    /// Gets or sets the name that is associated with the email address. 
    /// </summary>
    [DataMember]
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the email address.
    /// </summary>
    [DataMember]
    public string Address { get; set; }

    /// <summary>
    /// Converts Attendee instance to a string value.
    /// </summary>
    /// <returns>a string value that represents this Attendee instance.</returns>
    public override string ToString()
    {
      return Name + " <" + Address + ">";
    }
  }

  [DataContract(Namespace = "https://www.bankhapoalim.co.il/")]
  public class Recurrence
  {
    /// <summary>
    /// Gets or sets the date and time when the recurrence start.
    /// </summary>
    [DataMember]
    public DateTime StartDate { get; set; }

    /// <summary>
    /// Gets a value indicating whether the pattern has a fixed number 
    /// of occurrences or an end date.
    /// </summary>
    public bool HasEnd { get { return EndDate.HasValue;  } }

    /// <summary>
    /// Gets or sets the date after which the recurrence ends.
    /// </summary>
    [DataMember]
    public DateTime? EndDate { get; set; }

    /// <summary>
    /// Gets or sets the number of occurrences after which the recurrence ends. 
    /// </summary>
    [DefaultValue(0)]
    [DataMember]
    public int NumberOfOccurrences { get; set; }

    /// <summary>
    /// Gets or sets recurrence type.
    /// </summary>
    [DefaultValue(RecurrenceType.Once)]
    [DataMember]
    public RecurrenceType Type { get; set; }
  }

  /// <summary>
  /// Defines an appointment sensitivity.
  /// </summary>
  [DataContract(Namespace = "https://www.bankhapoalim.co.il/")]
  public enum Sensitivity
  {
    /// <summary>
    /// The item has a normal sensitivity.
    /// </summary>
    [EnumMember]
    Normal,

    /// <summary>
    /// The item is personal.
    /// </summary>
    [EnumMember]
    Personal,

    /// <summary>
    /// The item is private.
    /// </summary>
    [EnumMember]
    Private,

    /// <summary>
    /// The item is confidential.
    /// </summary>
    [EnumMember]
    Confidential
  }

  /// <summary>
  /// 
  /// </summary>
  [DataContract(Namespace = "https://www.bankhapoalim.co.il/")]
  public enum Importance
  {
    /// <summary>
    /// Low importance.
    /// </summary>
    [EnumMember]
    Low,

    /// <summary>
    /// Normal importance.
    /// </summary>
    [EnumMember]
    Normal,

    /// <summary>
    /// High importance.
    /// </summary>
    [EnumMember]
    High
  }
}