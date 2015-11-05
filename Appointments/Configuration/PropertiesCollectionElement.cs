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
  /// Defines a collection of extended properties' configuration elements.
  /// </summary>
  public class PropertiesCollectionElement : ConfigurationElementCollection
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
    /// Gets an ExtendedPropertyElement at the specified index location.
    /// </summary>
    /// <param actionName="index">
    /// The index location of the ExtendedPropertyElement to return.
    /// </param>
    /// <returns>An ExtendedPropertyElement at the specified index.</returns>
    public ExtendedPropertyElement this[int index]
    {
      get { return (ExtendedPropertyElement)BaseGet(index); }
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
    /// Gets the configuration element by the specified name.
    /// </summary>
    /// <param name="name">a name of ExtendedPropertyElement.</param>
    /// <returns>A ExtendedPropertyElement with the specified name, if any.</returns>
    new public ExtendedPropertyElement this[string name]
    {
      get { return (ExtendedPropertyElement)BaseGet(name); }
    }

    protected override ConfigurationElement CreateNewElement()
    {
      return new ExtendedPropertyElement();
    }

    protected override object GetElementKey(ConfigurationElement element)
    {
      return ((ExtendedPropertyElement)element).Name;
    }
  }
}
