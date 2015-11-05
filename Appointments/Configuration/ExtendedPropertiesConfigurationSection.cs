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
  /// Defines a configuration section for extended properties.
  /// </summary>
  public class ExtendedPropertiesConfigurationSection : ConfigurationSection
  {
    /// <summary>
    /// Gets and sets a collection of extended properties. 
    /// </summary>
    [ConfigurationProperty("properties", IsDefaultCollection = false)]
    public PropertiesCollectionElement PropertiesCollection
    {
      get
      {
        return (PropertiesCollectionElement)base["properties"];
      }
    }
  }
}
