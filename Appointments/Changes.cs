namespace Bnhp.Office365
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
    /// Mailbox address.
    /// </summary>
    [DataMember]
    public string Email { get; set; }

    /// <summary>
    /// A folder id.
    /// </summary>
    [DataMember]
    public string FolderID { get; set; }

    /// <summary>
    /// Items ID.
    /// </summary>
    [DataMember]
    public string ItemID { get; set; }

    /// <summary>
    /// A change type.
    /// </summary>
    [DataMember]
    public ChangeType ChangeType { get; set; }
  }

  /// <summary>
  /// Describes a change stats in the monitored mailbox.
  /// </summary>
  [DataContract(Namespace = "https://www.bankhapoalim.co.il/")]
  public class ChangeStats
  {
    /// <summary>
    /// Mailbox address.
    /// </summary>
    [DataMember]
    public string Email { get; set; }

    /// <summary>
    /// Number of changes.
    /// </summary>
    [DataMember]
    public int Count { get; set; }
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