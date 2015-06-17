namespace Bphx.Tracers
{
  using System;
  using System.Collections.Generic;
  using System.Linq;
  using System.Web;
  using System.ServiceModel.Dispatcher;
  using System.ServiceModel.Configuration;
  using System.ServiceModel.Description;
  using System.ServiceModel.Web;

  /// <summary>
  /// Web Http behaviour element for Web.Config.
  /// See details at http://www.nesterovsky-bros.com/weblog/2013/05/13/ErrorHandlingInWCFBasedWebApplications.aspx
  /// </summary>
  public class JsonWebHttpElement : BehaviorExtensionElement
  {
    public override Type BehaviorType
    {
      get { return typeof(JsonWebHttpBehavior); }
    }

    protected override object CreateBehavior()
    {
      return new JsonWebHttpBehavior
      {
        DefaultOutgoingResponseFormat = WebMessageFormat.Json,
        AutomaticFormatSelectionEnabled = true
      };
    }
  }

  public class JsonWebHttpBehavior : WebHttpBehavior
  {
    protected override QueryStringConverter GetQueryStringConverter(
      OperationDescription operationDescription)
    {
      return new JsonQueryStringConverter();
    }

    protected override void AddServerErrorHandlers(
      ServiceEndpoint endpoint,
      EndpointDispatcher endpointDispatcher)
    {
      // clear default error handlers.
      endpointDispatcher.ChannelDispatcher.ErrorHandlers.Clear();

      // add our own error handler.
      endpointDispatcher.ChannelDispatcher.ErrorHandlers.Add(new JsonErrorHandler());
    }
  }
}