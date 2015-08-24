using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Configuration;
using System.ComponentModel;

namespace Bnhp.Office365.Configuration
{
  /// <summary>
  /// Defines handler configuration element.
  /// </summary>
  public class HandlerElement : ConfigurationElement
  {
    /// <summary>
    /// Gets and sets an action actionName, processed by this handler.
    /// </summary>
    [ConfigurationProperty("action", IsRequired = true, IsKey = true)]
    public string Action
    {
      get { return (string)this["action"]; }
      set { this["action"] = value; }
    }

    /// <summary>
    /// Gets and sets the handler's type.
    /// </summary>
    [ConfigurationProperty("handler", IsRequired = true)]
    [TypeConverter(typeof(TypeNameConverter))]
    [SubclassTypeValidator(typeof(IEMailHandler))]
    public Type Handler
    {
      get { return this["handler"] as Type; }
      set { this["handler"] = value; }
    }
  }
}
