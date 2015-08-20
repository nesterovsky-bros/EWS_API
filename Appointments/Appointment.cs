using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.Serialization;
using System.Web;

namespace Bnhp.Office365
{
  /// <summary>
  /// A proxy class for Office 365 proxy.
  /// </summary>
  /// <seealso cref="https://msdn.microsoft.com/en-us/library/microsoft.exchange.webservices.data.appointment_properties(v=exchg.80).aspx"/>
  [DataContract(Namespace = "https://www.bankhapoalim.co.il/")]
  public class Appointment: Item
  {
    /// <summary>
    /// Gets and sets a value that indicates whether the associated object is an proxy, 
    /// a proxy, a response to a proxy, or a cancelled proxy.
    /// The possible values are:
    /// 0 - No response is required for this object. This is the case for proxy 
    ///     objects and proxy response objects.
    /// 1 - This proxy belongs to the organizer.
    /// 2 - This value on the attendee's proxy indicates that the attendee has 
    ///     tentatively accepted the proxy request.
    /// 3 - This value on the attendee's proxy t indicates that the attendee has 
    ///     accepted the proxy request.
    /// 4 - This value on the attendee's proxy indicates that the attendee has 
    ///     declined the proxy request.
    /// 5 - This value on the attendee's proxy indicates the attendee has not 
    ///     yet responded. This value is on the proxy request, proxy update, and proxy cancelation.
    /// </summary>
    [DefaultValue(0)]
    [DataMember]
    public int AppointmentState { get; internal set; }

    /// <summary>
    /// Gets or sets a value indicating whether new time proposals are allowed for
    /// attendees of this proxy.
    /// </summary>
    [DefaultValue(true)]
    [DataMember]
    public bool AllowNewTimeProposal { get; set; }
    
    /// <summary>
    /// Gets the time when the attendee replied to the proxy request.
    /// </summary>
    [DataMember]
    public DateTime? AppointmentReplyTime { get; internal set; }
    
    //
    // Summary:
    //     Gets the sequence number of this proxy.
    [DataMember]
    public int AppointmentSequenceNumber { get; internal set; }
      
    /// <summary>
    /// Gets a value indicating the type of this proxy.
    /// The correct values are:
    //     0 - The proxy is non-recurring.
    //     1 - The proxy is an occurrence of a recurring proxy.
    //     2 - The proxy is an exception of a recurring proxy.
    //     3 - The proxy is the recurring master of a series.
    /// </summary>
    [DefaultValue(0)]
    [DataMember]
    public int AppointmentType { get; internal set; }
    
    /// <summary>
    /// Gets or sets the type of conferencing that will be used during the proxy.
    /// </summary>
    [DataMember]
    public int ConferenceType { get; set; }

    /// <summary>
    /// Gets the duration of this proxy.
    /// </summary>
    [DataMember]
    public TimeSpan? Duration { get; internal set; }
    
    /// <summary>
    /// Gets or sets the end time of the proxy.
    /// </summary>
    [DataMember]
    public DateTime End { get; set; }
    
    /// <summary>
    /// Gets or sets the Enhanced location object.
    /// </summary>
    [DataMember]
    public string EnhancedLocation { get; set; }

    /// <summary>
    /// Gets an OccurrenceInfo identifying the first occurrence of this proxy.
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
    /// Gets or sets a value indicating whether this proxy is an all day event.
    /// </summary>
    [DefaultValue(false)]
    [DataMember]
    public bool IsAllDayEvent { get; set; }
    
    /// <summary>
    /// Gets a value indicating whether the proxy has been cancelled.
    /// </summary>
    [DefaultValue(false)]
    [DataMember]
    public bool IsCancelled { get; internal set; }
    
    /// <summary>
    /// Gets a value indicating whether the proxy is a proxy.
    /// </summary>
    [DefaultValue(false)]
    [DataMember]
    public bool IsMeeting { get; internal set; }
    
    /// <summary>
    /// Gets or sets a value indicating whether this is an online proxy.
    /// </summary>
    [DefaultValue(false)]
    [DataMember]
    public bool IsOnlineMeeting { get; set; }
    
    /// <summary>
    /// Gets a value indicating whether the proxy is recurring.
    /// </summary>
    [DefaultValue(false)]
    [DataMember]
    public bool IsRecurring { get; set; }
       
    /// <summary>
    /// Gets or sets a value indicating whether responses are requested.
    /// </summary>
    [DefaultValue(false)]
    [DataMember]
    public bool IsResponseRequested { get; set; }
    
    /// <summary>
    /// Gets the Url for joining an online proxy
    /// </summary>
    [DataMember]
    public string JoinOnlineMeetingUrl { get; internal set; }
    
    /// <summary>
    /// Gets an OccurrenceInfo identifying the last occurrence of this proxy.
    /// </summary>
    [DataMember]
    public OccurrenceInfo LastOccurrence { get; internal set; }
    
    /// <summary>
    /// Gets or sets the location of this proxy.
    /// </summary>
    [DataMember]
    public string Location { get; set; }

    /// <summary>
    /// Gets a value indicating whether the proxy request has already been sent.
    /// </summary>
    [DataMember]
    public bool MeetingRequestWasSent { get; internal set; }
    
    /// <summary>
    /// Gets or sets the URL of the proxy workspace. A proxy workspace is a shared
    /// Web site for planning meetings and tracking results.
    /// </summary>
    [DataMember]
    public string MeetingWorkspaceUrl { get; set; }
    
    ///// <summary>
    ///// Gets a list of modified occurrences for this proxy.
    ///// </summary>
    //[DataMember]
    //public List<OccurrenceInfo> ModifiedOccurrences { get; internal set; }
       
    /// <summary>
    /// Gets a value indicating what was the last response of the user that loaded
    /// this proxy.
    /// </summary>
    [DataMember]
    public MeetingResponseType MyResponseType { get; internal set; }
    
    /// <summary>
    /// Gets or sets the URL of the Microsoft NetShow online proxy.
    /// </summary>
    [DataMember]
    public string NetShowUrl { get; set; }
    
    /// <summary>
    /// Gets a list of optional attendeed for this proxy.
    /// </summary>
    [DataMember]
    public List<Attendee> OptionalAttendees { get; internal set; }
    
    /// <summary>
    /// Gets the organizer of this proxy. The Organizer property is read-only and
    /// is only relevant for attendees.  The organizer of a proxy is automatically
    /// set to the user that created the proxy.
    /// </summary>
    [DataMember]
    public Attendee Organizer { get; internal set; }
    
    /// <summary>
    /// Gets the original start time of this proxy.
    /// </summary>
    [DataMember]
    public DateTime OriginalStart { get; internal set; }
       
    /// <summary>
    /// Gets or sets the recurrence pattern for this proxy. Available recurrence
    /// pattern classes include Recurrence.DailyPattern, Recurrence.MonthlyPattern
    /// and Recurrence.YearlyPattern.
    /// </summary>
    [DataMember]
    public Recurrence Recurrence { get; set; }

    /// <summary>
    /// Gets or sets the number of minutes before the start
    /// of this proxy that the reminder should be triggered.
    /// </summary>
    [DataMember]
    public int ReminderMinutesBeforeStart { get; set; }

    /// <summary>
    /// Gets a list of required attendees for this proxy.
    /// </summary>
    [DataMember]
    public List<Attendee> RequiredAttendees { get; internal set; }
    
    /// <summary>
    /// Gets a list of resources for this proxy.
    /// </summary>
    [DataMember]
    public List<Attendee> Resources { get; internal set; }

    /// <summary>
    /// Gets the date until which an proxy must be preserved.
    /// </summary>
    [DataMember]
    public DateTime? RetentionDate { get; set;  }
    
    /// <summary>
    /// Gets or sets the start time of the proxy.
    /// </summary>
    [DataMember]
    public DateTime Start { get; set; }

    ///// <summary>
    ///// Gets or sets time zone of the start property of this proxy.
    ///// </summary>
    //[DataMember]
    //public TimeZoneInfo StartTimeZone { get; set; }
    
    /// <summary>
    /// Gets the name of the time zone this proxy is defined in.
    /// </summary>
    [DataMember]
    public string TimeZone { get; internal set; }

    /// <summary>
    /// Gets a text indicating when this proxy occurs. The text returned by
    /// When is localized using the Exchange Server culture or using the culture
    /// specified in the PreferredCulture property of the ExchangeService object
    /// this proxy is bound to.
    /// </summary>
    [DataMember]
    public string When { get; internal set; }
  }

  /// <summary>
  /// Defines recurrence pattern for the proxy.
  /// </summary>
  [DataContract(Namespace = "https://www.bankhapoalim.co.il/")]
  public enum RecurrenceType
  {
    [EnumMember]
    Unknown,
    [EnumMember]
    Daily,
    [EnumMember]
    Weekly,
    [EnumMember]
    Monthly,
    [EnumMember]
    Yearly
  }

  /// <summary>
  /// Defines recurrence pattern for the proxy.
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
  /// Specifies constants that define the type of response given to a proxy request.
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
    /// No response; the authenticated user is the organizer of the proxy.
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
  /// Represents a proxy attendee.
  /// </summary>
  [DataContract(Namespace = "https://www.bankhapoalim.co.il/")]
  public class Attendee: EMailAddress
  {
    /// <summary>
    /// Gets the type of response given to a proxy request.
    /// </summary>
    [DataMember]
    public MeetingResponseType? ResponseType { get; internal set; }
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
    [DataMember]
    public int? NumberOfOccurrences { get; set; }

    /// <summary>
    /// Gets or sets recurrence type.
    /// </summary>
    [DefaultValue(RecurrenceType.Unknown)]
    [DataMember]
    public RecurrenceType Type { get; set; }

    /// <summary>
    /// Gets recurrence type name.
    /// </summary>
    [DataMember]
    public string OriginalTypeName { get; internal set; }

    /// <summary>
    /// Gets or sets the interval between occurrences.
    /// </summary>
    [DefaultValue(0)]
    [DataMember]
    public int Interval { get; set; }
  }
}