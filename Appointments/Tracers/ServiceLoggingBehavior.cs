using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.ServiceModel.Description;
using System.Web;
using System.ServiceModel.Channels;

namespace Bnhp.Tracers
{
  public class ServiceLoggingBehavior : Attribute, IServiceBehavior
  {
    public void ApplyDispatchBehavior(
      ServiceDescription serviceDescription, 
      ServiceHostBase serviceHostBase)
    {
      foreach (ServiceEndpoint endpoint in serviceDescription.Endpoints)
      {
        foreach (OperationDescription operation in endpoint.Contract.Operations)
        {
          if (!operation.Behaviors.Contains(typeof(LoggingOperationBehavior)))
          {
            operation.Behaviors.Add(new LoggingOperationBehavior());
          }
        }
      }
    }

    public void AddBindingParameters(
      ServiceDescription serviceDescription, 
      ServiceHostBase serviceHostBase, 
      System.Collections.ObjectModel.Collection<ServiceEndpoint> endpoints, 
      BindingParameterCollection bindingParameters)
    {
      // Do nothing
    }

    public void Validate(
      ServiceDescription serviceDescription, 
      ServiceHostBase serviceHostBase)
    {
      // Do nothing
    }
  }
}