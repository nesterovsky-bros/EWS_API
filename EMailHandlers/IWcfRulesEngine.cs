using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.ServiceModel;
using System.Text;

namespace Bnhp.Office365
{
  /// <summary>
  /// An interface for WCF service that wraps rules engine.
  /// </summary>
  [ServiceContract]
  public interface IWcfRulesEngine
  {
    /// <summary>
    /// Executes rules engine WCF service.
    /// </summary>
    [OperationContract]
    void Execute();
  }
}
