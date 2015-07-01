namespace Bnhp.RunitChanel
{
  using System;
  using System.ServiceModel;
  using BNHP.HT.RunIT.Interfaces;
  using System.ServiceModel.Description;
  using System.Xml.Linq;
  using System.IO;

  // Runit Service point
  public abstract class RunitService<T>: RequestReplyBeanBase, IDisposable
  {
    public RunitService()
    {
      ServiceHost = CreateServiceHost();
      ServiceHost.Open();

      foreach(var channel in ServiceHost.ChannelDispatchers)
      {
        var listener = channel.Listener as RunitReplyChannelListener;

        if (listener != null)
        {
          ChannelListener = listener;

          break;
        }
      }

      foreach(var endpoint in ServiceHost.Description.Endpoints)
      {
        var contract = endpoint.Contract;
        var url = contract.Namespace;

        if (!url.EndsWith("/"))
        {
          url += "/";
        }

        ActionUrl = url + contract.Name + "/";

        break;
      }
    }

    public void Dispose()
    {
      if (ServiceHost != null)
      {
        var disposable = ServiceHost as IDisposable;

        ServiceHost = null;
        disposable.Dispose();
      }
    }

    public override string Request(IBeanRequestMessage message)
    {
      return Request(message.OperationName, message.OperationArgument);
    }

    public virtual string Request(string operation, string request)
    {
      request =
@"<s:Envelope xmlns:s='http://www.w3.org/2003/05/soap-envelope' xmlns:a='http://www.w3.org/2005/08/addressing'>
  <s:Header>
    <a:Action>" + ActionUrl + operation + @"</a:Action>
    <a:To>runit:</a:To>
  </s:Header>
  <s:Body>" + request + @"</s:Body>
</s:Envelope>";

      var message = new RunitMessage(request);

      ChannelListener.Queue.Add(message);

      var document = XDocument.Load(new StringReader(message.Response.Result));
      var ns = (XNamespace)"http://www.w3.org/2003/05/soap-envelope";
      var body = document.Element(ns + "Envelope").Element(ns + "Body");
      var reader = body.CreateReader();

      reader.MoveToContent();

      return reader.ReadInnerXml();
    }

    protected virtual ServiceHost CreateServiceHost()
    {
      return new ServiceHost(typeof(T));
    }

    public ServiceHost ServiceHost { get; private set; }
    public RunitReplyChannelListener ChannelListener { get; private set; }
    public string ActionUrl { get; private set; }
  }
}
