﻿using System;
using System.Collections.Generic;
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
    public string ID { get; internal set; }
    
    /// <summary>
    /// Gets unique ID for this appointment.
    /// Note: all recuring appointments have the same UID value.
    /// </summary>
    [DataMember]
    public string UID { get; internal set; }
    
    /// <summary>
    /// Gets or sets the subject of this item.
    /// </summary>
    [DataMember]
    public string Subject { get; set; }

    /// <summary>
    /// Gets or sets a body message.
    /// </summary>
    [DataMember]
    public string Message { get; set; }
    
    /// <summary>
    /// Gets or sets the location of this appointment.
    /// </summary>
    [DataMember]
    public string Location { get; set; }
    
    /// <summary>
    /// Gets whether the appointment is a meeting.
    /// </summary>
    [DataMember]
    public bool IsMeeting { get; set; }
    
    /// <summary>
    /// Gets or sets the start time of the appointment.
    /// </summary>
    [DataMember]
    public DateTime? Start { get; set; }
    
    /// <summary>
    /// Gets or sets the end time of the appointment.
    /// </summary>
    [DataMember]
    public DateTime? End { get; set; }
    
    /// <summary>
    /// Gets a text string that lists the To recipients of this item.
    /// </summary>
    [DataMember]
    public string DisplayTo { get; internal set; }
    
    /// <summary>
    /// Gets a list of required attendees for the meeting.
    /// </summary>
    [DataMember]
    public List<string> Attendees { get; set; }
    
    /// <summary>
    /// Gets whether the user requested this appointment is an organizer.
    /// </summary>
    [DataMember]
    public bool IsOrganizer { get; internal set; }
    
    /// <summary>
    /// Gets whether the appointment is part of a recurring series.
    /// </summary>
    [DataMember]
    public bool IsRecurring { get; internal set; }
    
    /// <summary>
    /// Gets and sets a start date of the recurrence, if any.
    /// </summary>
    [DataMember]
    public DateTime? StartRecurrence { get; set; }
    
    /// <summary>
    /// Gets and sets an end date of the recurrence, if any.
    /// </summary>
    [DataMember]
    public DateTime? EndRecurrence { get; set; }
    
    /// <summary>
    /// Gets or sets the recurrence pattern for the appointment.
    /// </summary>
    [DataMember]
    public RecurrenceType RecurrenceType { get; set; }

    /// <summary>
    /// Gets or sets the recurrence interval.
    /// </summary>
    [DataMember]
    public int RecurrenceInterval { get; set; }

    /// <summary>
    ///  Gets or sets the number of minutes before the start of this item
    ///  when the reminder should be triggered.
    /// </summary>
    [DataMember]
    public int ReminderMinutesBeforeStart { get; set; }
  }

  /// <summary>
  /// Defines recurrence pattern for the appointment.
  /// </summary>
  [DataContract(Namespace = "https://www.bankhapoalim.co.il/")]
  public enum RecurrenceType
  {
    Once,
    Dayly,
    Weekly,
    Monthly,
    Yearly
  }
}