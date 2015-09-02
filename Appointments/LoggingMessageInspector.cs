namespace Bnhp.Office365
{
  using Microsoft.Practices.Unity;
  using System;
  using System.Data.Entity;
  using System.Linq;
  using System.ServiceModel;
  using System.ServiceModel.Channels;
  using System.ServiceModel.Dispatcher;
  using System.Text;
  using System.Xml;

  public class LoggingMessageInspector : IDispatchMessageInspector
  {
    /// <summary>
    /// A settings instance.
    /// </summary>
    [Dependency]
    public Settings Settings { get; set; }

    /// <summary>
    /// A response notifier.
    /// </summary>
    [Dependency]
    public IResponseNotifier ResponseNotifier { get; set; }

    public object AfterReceiveRequest(ref Message request, IClientChannel channel, InstanceContext instanceContext)
    {
      var buffer = request.CreateBufferedCopy(int.MaxValue);

      request = buffer.CreateMessage();

      var copy = buffer.CreateMessage();
      var builder = new StringBuilder();
      var xmlWriter = XmlWriter.Create(builder);

      copy.WriteMessage(xmlWriter);
      xmlWriter.Flush();

      using(var model = new EWSQueueEntities())
      {
        var item = new Queue
        {
          Operation = copy.Headers.Action,
          Request = builder.ToString(),
          CreatedAt = DateTime.Now,
          //ExpiresAt = DateTime.Now.AddMinutes(Settings.RequestTimeout)
        };

        request.Properties[RequestIDName] = item.ID;

        model.Queues.Add(item);
        model.SaveChanges();

        return item.ID;
      }
    }

    public void BeforeSendReply(ref Message reply, object correlationState)
    {
      var buffer = reply.CreateBufferedCopy(int.MaxValue);

      reply = buffer.CreateMessage();

      var copy = buffer.CreateMessage();
      var requestID = (long)correlationState;

      var builder = new StringBuilder();
      var xmlWriter = XmlWriter.Create(builder);

      copy.WriteMessage(xmlWriter);
      xmlWriter.Flush();

      using(var model = new EWSQueueEntities())
      {
        var item = model.Queues.
          Where(request => request.ID == requestID).
          FirstOrDefault();

        if (item != null)
        {
          if (copy.IsFault)
          {
            item.Error = builder.ToString();
          }
          else
          {
            item.Response = builder.ToString();
          }

          model.Entry(item).State = EntityState.Modified;
          model.SaveChanges();
        }
      }

      if (ResponseNotifier != null)
      {
        try
        {
          ResponseNotifier.Notify(requestID, copy.IsFault);
        }
        catch
        {
          // Notifier should not interrupt us.
        }
      }

    }

    public const string RequestIDName = "Bnhp.Office365.RequestID";
  }
}
