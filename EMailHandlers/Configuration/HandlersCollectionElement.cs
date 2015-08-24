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
  /// Defines a collection of handlers' configuration elements.
  /// </summary>
  public class HandlersCollectionElement : ConfigurationElementCollection
  {
    /// <summary>
    /// Gets the type of the collection element.
    /// </summary>
    public override ConfigurationElementCollectionType CollectionType
    {
      get
      {
        return ConfigurationElementCollectionType.AddRemoveClearMap;
      }
    }

    /// <summary>
    /// Gets an HandlerElement at the specified index location.
    /// </summary>
    /// <param actionName="index">
    /// The index location of the HandlerElement to return.
    /// </param>
    /// <returns>A HandlerElement at the specified index.</returns>
    public HandlerElement this[int index]
    {
      get { return (HandlerElement)BaseGet(index); }
      set
      {
        if (BaseGet(index) != null)
        {
          BaseRemoveAt(index);
        }

        BaseAdd(index, value);
      }
    }

    /// <summary>
    /// Gets the configuration element by the specified action actionName.
    /// </summary>
    /// <param actionName="action">
    /// An action actionName of an HandlerElement to return.
    /// </param>
    /// <returns>A HandlerElement with the specified action actionName, if any.</returns>
    new public HandlerElement this[string action]
    {
      get { return (HandlerElement)BaseGet(action); }
    }

    protected override ConfigurationElement CreateNewElement()
    {
      return new HandlerElement();
    }

    protected override object GetElementKey(ConfigurationElement element)
    {
      return ((HandlerElement)element).Action;
    }
  }
}
