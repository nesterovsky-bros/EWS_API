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
  /// Defines a &lt;handler&gt; configuration element.
  /// </summary>
  public class HandlerElement : ConfigurationElement
  {
    /// <summary>
    /// Default constructor.
    /// </summary>
    public HandlerElement()
    {
    }

    /// <summary>
    /// Creates a HandlerElement instance.
    /// </summary>
    /// <param name="action">an action name.</param>
    /// <param name="type">a handler type.</param>
    public HandlerElement(string action, Type type)
    {
      if (!typeof(IEMailHandler).IsAssignableFrom(type))
      {
        throw new ArgumentException("type");
      }

      Action = action;
      Type = type;
    }

    /// <summary>
    /// Gets and sets an action name, processed by this handler.
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
    [ConfigurationProperty("type", IsRequired = true)]
    [TypeConverter(typeof(TypeNameConverter))]
    [SubclassTypeValidator(typeof(IEMailHandler))]
    public Type Type
    {
      get { return this["type"] as Type; }
      set { this["type"] = value; }
    }
  }
}
