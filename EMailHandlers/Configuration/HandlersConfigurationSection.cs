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
  /// Defines a configuration section for handlers.
  /// </summary>
  public class HandlersConfigurationSection : ConfigurationSection
  {
    /// <summary>
    /// Gets and sets a collection of handlers. 
    /// </summary>
    [ConfigurationProperty("handlers", IsDefaultCollection = false)]
    public HandlersCollectionElement Handlers
    {
      get
      {
        return (HandlersCollectionElement)base["handlers"];
      }
    }
  }
}
