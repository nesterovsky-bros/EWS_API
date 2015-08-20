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
  public class HandlersConfigurationSection : ConfigurationSection
  {
    /// <summary>
    /// Default constructor.
    /// </summary>
    public HandlersConfigurationSection()
    {
    }
    
    /// <summary>
    /// Declare a collection element represented in the configuration file by the sub-section 
    /// &lt;handlers&gt; &lt;add ...&gt; &lt;/handlers&gt;.
    /// </summary>
    /// <remarks>
    /// the "IsDefaultCollection = false" instructs the .NET Framework to build 
    /// a nested section like &lt;handlers&gt; ... &lt;/handlers&gt;.
    /// </remarks>
    [ConfigurationProperty("handlers", IsDefaultCollection = true)]
    public HandlersCollectionElement Handlers
    {
      get
      {
        return (HandlersCollectionElement)base["handlers"];
      }
    }
  }
}
