﻿namespace Bnhp.Office365
{
  using System;
  using System.Collections.Generic;
  using System.Runtime.Serialization;

  /// <summary>
  /// Describes a change in the monitored mailbox.
  /// </summary>
  [DataContract(Namespace = "https://www.bankhapoalim.co.il/")]
  public class Change
  {
    /// <summary>
    /// Change timestamp.
    /// </summary>
    [DataMember]
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// Milbox address.
    /// </summary>
    [DataMember]
    public string MailAddress { get; set; }

    /// <summary>
    /// Items ID.
    /// </summary>
    [DataMember]
    public string ItemId { get; set; }

    /// <summary>
    /// A change type.
    /// </summary>
    [DataMember]
    public ChangeType ChangeType { get; set; }
  }

  /// <summary>
  /// A change type.
  /// </summary>
  /// <remarks>
  /// Enumeration values are in sync with a enumeration 
  /// <see cref="Microsoft.Exchange.WebServices.Data.ChangeType"/>.
  /// </remarks>
  [DataContract(Namespace = "https://www.bankhapoalim.co.il/")]
  public enum ChangeType
  {
    [EnumMember]
    Created = 0,
    [EnumMember]
    Updated = 1,
    [EnumMember]
    Deleted = 2
  }
}