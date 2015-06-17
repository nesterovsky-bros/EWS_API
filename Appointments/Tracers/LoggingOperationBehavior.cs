using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel.Description;
using System.ServiceModel.Dispatcher;
using System.ServiceModel.Channels;
using System.Web;

namespace Bnhp.Tracers
{
  public class LoggingOperationBehavior : IOperationBehavior
  {
    #region IOperationBehavior Members
    
    public void ApplyDispatchBehavior(
      OperationDescription description, 
      DispatchOperation operation)
    {
      operation.Invoker = 
        new LoggingOperationInvoker(operation.Invoker, operation);
    }

    public void AddBindingParameters(
      OperationDescription operationDescription, 
      BindingParameterCollection bindingParameters)
    {
      // Do nothing
    }

    public void ApplyClientBehavior(
      OperationDescription operationDescription, 
      ClientOperation clientOperation)
    {
      // Do nothing
    }

    public void Validate(OperationDescription operationDescription)
    {
      // Do nothing
    }

    #endregion
  }
}