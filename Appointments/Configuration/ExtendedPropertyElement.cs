using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Configuration;
using System.ComponentModel;
using Microsoft.Exchange.WebServices.Data;

namespace Bnhp.Office365.Configuration
{
  /// <summary>
  /// Defines configuration element for an extended property.
  /// </summary>
  public class ExtendedPropertyElement : ConfigurationElement
  {
    /// <summary>
    /// Gets and sets the property name.
    /// </summary>
    [ConfigurationProperty("name", IsRequired = true, IsKey = true)]
    public string Name
    {
      get { return (string)this["name"]; }
      set { this["name"] = value; }
    }

    /// <summary>
    /// Gets and sets the property's tag, if any.
    /// </summary>
    [ConfigurationProperty("tag")]
    public int? Tag
    {
      get { return this["tag"] as int?; }
      set { this["tag"] = value; }
    }

    /// <summary>
    /// Gets and sets the MAPI type of an extended property.
    /// The default type is "String".
    /// </summary>
    /// <seealso cref="Microsoft.Exchange.WebServices.Data.MapiPropertyType"/>
    [ConfigurationProperty("type")]
    public string Type
    {
      get { return this["type"] as string; }
      set { this["type"] = value; }
    }

    /// <summary>
    /// Gets and sets the extended property's type as a MAPI type.
    /// </summary>
    public MapiPropertyType MapiType
    {
      get
      {
        var value = Type;

        if (!string.IsNullOrWhiteSpace(value))
        {
          MapiPropertyType result;

          if (MapiPropertyType.TryParse(value, true, out result))
          {
            return result;
          }
        }

        return MapiPropertyType.String;
      }
      set
      {
        Type = value.ToString();
      }
    }
  }
}
