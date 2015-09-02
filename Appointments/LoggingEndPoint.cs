namespace Bnhp.Office365
{
  using System;
  using System.ServiceModel.Channels;
  using System.ServiceModel.Dispatcher;
  using System.ServiceModel.Description;
  using System.ServiceModel.Configuration;
  using Microsoft.Practices.Unity;

  /// <summary>
  /// WCF endpoint to attacht trace inspector.
  /// </summary>
  public class LoggingEndPoint: IEndpointBehavior
  {
    public void AddBindingParameters(ServiceEndpoint endpoint, BindingParameterCollection bindingParameters)
    {
      return;
    }

    public void ApplyClientBehavior(ServiceEndpoint endpoint, ClientRuntime clientRuntime)
    {
    }

    public void ApplyDispatchBehavior(ServiceEndpoint endpoint, EndpointDispatcher endpointDispatcher)
    {
      var container = new UnityContainer();

      WcfServiceFactory.Configure(container);

      endpointDispatcher.DispatchRuntime.MessageInspectors.Add(container.Resolve<LoggingMessageInspector>());
    }

    public void Validate(ServiceEndpoint endpoint)
    {
      return;
    }
  }

  /// <summary>
  /// Defines the behavior
  /// </summary>
  public class LoggingEndPointBehaviorElement : BehaviorExtensionElement
  {
    public LoggingEndPointBehaviorElement()
    {
    }

    protected override object CreateBehavior()
    {
      return new LoggingEndPoint();
    }

    public override Type BehaviorType
    {
      get { return typeof(LoggingEndPoint); }
    }
  }

}
