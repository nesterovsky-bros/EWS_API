namespace Bnhp.Office365.RunIT
{
  using System;
  using System.Collections.Generic;
  using System.Linq;
  using System.IO;
  using System.Text;
  using System.Threading.Tasks;
  using System.Xml;
  using System.Xml.Serialization;
  using System.Runtime.Serialization;

  using BNHP.HT.RunIT.Interfaces;
  
  using Bnhp.Office365.RunIT.Operations;
  using Microsoft.Practices.Unity;

  /// <summary>
  /// An entry point for RunIT API.
  /// </summary>
  /// <seealso cref="https://msdn.microsoft.com/en-us/library/microsoft.exchange.webservices.data.appointment_properties(v=exchg.80).aspx"/>
  public class RunITService : RequestReplyBeanBase
  {
    /// <summary>
    /// Creates RunIT service.
    /// </summary>
    public RunITService() 
    {
      WcfServiceFactory.Configure(container);
    }

    /* 
     * A request example:
     
        <Get xmlns="https://www.bankhapoalim.co.il/">
          <email>anesterovsky@bphx.com</email>
          <start>2015-06-30T12:38:00</start>
          <end>2015-06-30T12:38:00</end>
          <maxResults>1000</maxResults>
        </Get>
     
     */
    public override string Request(IBeanRequestMessage message)
    {
      var service = GetService();

      switch (message.OperationName)
      {
        case "Create":
        {
          var request = FromXmlString<Create>(message.OperationArgument);
          var response = new CreateResponse
          {
            CreateResult = service.Create(
              request.email,
              request.appointment)
          };

          return ToXmlString(response);
        }
        case "Find":
        {
          var request = FromXmlString<Find>(message.OperationArgument);
          var response = new FindResponse
          {
            FindResult = service.Find(
              request.email, 
              request.start, 
              request.end, 
              request.maxResults).ToList()
          };

          return ToXmlString(response);
        }
        case "Get":
        {
          var request = FromXmlString<Get>(message.OperationArgument);
          var response = new GetResponse
          {
            GetResult = service.Get(
              request.email,
              request.UID)
          };

          return ToXmlString(response);
        }
        case "Delete":
        {
          var request = FromXmlString<Delete>(message.OperationArgument);
          var response = new DeleteResponse
          {
            DeleteResult = service.Delete(
              request.email,
              request.UID)
          };

          return ToXmlString(response);
        }
        case "Cancel":
        {
          var request = FromXmlString<Cancel>(message.OperationArgument);
          var response = new CancelResponse
          {
            CancelResult = service.Cancel(
              request.email,
              request.UID,
              request.reason)
          };

          return ToXmlString(response);
        }
        case "Update":
        {
          var request = FromXmlString<Update>(message.OperationArgument);
          var response = new UpdateResponse
          {
            UpdateResult = service.Update(
              request.email,
              request.appointment)
          };

          return ToXmlString(response);
        }
        case "Accept":
        {
          var request = FromXmlString<Accept>(message.OperationArgument);
          var response = new AcceptResponse
          {
            AcceptResult = service.Accept(
              request.email,
              request.UID)
          };

          return ToXmlString(response);
        }
        case "Decline":
        {
          var request = FromXmlString<Decline>(message.OperationArgument);
          var response = new DeclineResponse
          {
            DeclineResult = service.Decline(
              request.email,
              request.UID)
          };

          return ToXmlString(response);
        }
        default:
        {
          throw new NotSupportedException(
            "Operation '" + message.OperationName + "' is not supported.");
        }
      }
    }

    private static string ToXmlString<T>(T result)
    {
      var data = new StringBuilder();
      var serializer = new DataContractSerializer(result.GetType());

      using (var writer = XmlWriter.Create(data))
      {
        serializer.WriteObject(writer, result);
      }

      return data.ToString();
    }

    private static T FromXmlString<T>(string xml)
    {
      if (string.IsNullOrEmpty(xml))
      {
        return default(T);
      }

      var serializer = new DataContractSerializer(typeof(T));
      var reader = new StringReader(xml);

      return (T)serializer.ReadObject(XmlReader.Create(reader));
    }

    private IEwsService GetService()
    {
      return container.Resolve<IEwsService>();
    }

    private UnityContainer container = new UnityContainer();
  }
}