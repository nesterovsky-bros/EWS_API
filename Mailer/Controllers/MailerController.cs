namespace Mailer.Controllers
{
  using System;
  using System.Collections.Generic;
  using System.Collections.Concurrent;
  using System.Linq;
  using System.Threading;
  using System.Threading.Tasks;
  using System.Web.Http;
  using System.Net;
  using System.Net.Http;
  using System.Diagnostics;
  using System.Runtime.Serialization;
  using System.Xml;
  using System.Text;
  using System.IO;
  using System.Web.Hosting;

  using Mailer.Code;
  using Mailer.EwsServiceReference;

  using NesterovskyBros.Code;

  /// <summary>
  /// Declares API for cloud mailer application.
  /// </summary>
  public class MailerController : ApiController
  {
    /// <summary>
    /// Retrieves list of addresses that suits the specified filter.
    /// </summary>
    /// <param name="filter">a search filter.</param>
    /// <returns>an enumeration of Addressee instances.</returns>
    public async Task<IEnumerable<Addressee>> GetAddresses(string filter)
    {
      var list = await ReadAddresses(filter);

      return list;
    }

    /// <summary>
    /// Retrieves list of potential senders.
    /// </summary>
    /// <param name="filter">a search filter.</param>
    /// <returns>an enumeration of Addressee instances.</returns>
    public async Task<IEnumerable<Addressee>> GetSenders(string filter)
    {
      // TODO: to implement this method

      var list = (await ReadAddresses(filter)).
        Where(a => !string.IsNullOrEmpty(a.Email));

      return list;
    }

    /// <summary>
    /// Uploads a file with identities.
    /// Arguments are passed as mime/multipart.
    /// 
    /// Content should contain a plain/text attached, and 
    /// optional description form field.
    /// </summary>
    /// <returns>
    /// an enumeration of Addressee instances that correspond to
    ///  the specified identities.
    /// </returns>
    [HttpPost]
    [ActionName("UploadIdentities")]
    public async Task<IEnumerable<Addressee>> UploadIdentities()
    {
      var list = new List<Addressee>();

      var addresses = (await ReadAddresses()).
        Where(a => !string.IsNullOrEmpty(a.Id)).
        ToDictionary(a => a.Id);
      
      await UploadAction(
        async provider =>
        {
          var file = provider.FileData.FirstOrDefault();

          if (file == null)
          {
            throw new HttpResponseException(HttpStatusCode.BadRequest);
          }

          using (var reader = File.OpenText(file.LocalFileName))
          {
            var line = null as string;
            var address = null as Addressee;

            while ((line = await reader.ReadLineAsync()) != null)
            {
              if (addresses.TryGetValue(line.Trim(), out address))
              {
                list.Add(address);
              }
            }
          }
        });

      return list;
    }

    /// <summary>
    /// Sends an e-mail message to recipients.
    /// </summary>
    /// <param name="message">a message to send.</param>
    /// <returns>true when the message was sent successfully.</returns>
    [HttpPost]
    [ActionName("SendMessage")]
    [Authorize]
    public async Task<bool> SendMessage(Message message)
    {
      var client = new EwsServiceClient();
      var emailMessage = new EMailMessage();

      if ((message.From == null) || string.IsNullOrEmpty(message.From.Email))
      {
        emailMessage.From =
          await ResolveEmail(RequestContext.Principal.Identity.Name);
      }
      else
      {
        emailMessage.From = new EMailAddress
        {
          Address = message.From.Email,
          Name = message.From.Name
        };
      }

      emailMessage.Subject = message.Subject;
      emailMessage.TextBody = message.Content;
      emailMessage.ToRecipients = await GetRecipients(message.To);
      emailMessage.CcRecipients = await GetRecipients(message.Cc);
      emailMessage.BccRecipients = await GetRecipients(message.Bcc);

      var messageId = 
        await client.CreateMessageAsync(emailMessage.From.Address, emailMessage);

      if (message.Attachments != null)
      {
        foreach (var attachment in message.Attachments)
        {
          await client.AddAttachmentAsync(
            emailMessage.From.Address,
            messageId,
            attachment.Name,
            Convert.FromBase64String(attachment.Content));
        }
      }

      return await client.SendMessageAsync(emailMessage.From.Address, messageId);
    }
    
    /// <summary>
    /// Read fake data from the App_Data/test_data.xml
    /// </summary>
    /// <returns></returns>
    private async Task<IEnumerable<Addressee>> ReadAddresses(
      string filter = null)
    {
      var path = HostingEnvironment.MapPath("~/App_Data/test_data.xml");
      var list = new List<Addressee>();

      using (var file = File.OpenText(path))
      {
        var content = await file.ReadToEndAsync();

        list = FromXmlString<List<Addressee>>(content);
      }

      if (!string.IsNullOrEmpty(filter))
      {
        return list.Where(a =>
          ((a.Id != null) && a.Id.Contains(filter)) ||
          ((a.Name != null) && a.Name.Contains(filter)));
      }

      return list.AsEnumerable();
    }

    private async Task<EMailAddress[]> GetRecipients(Addressee[] addresses)
    {
      // TODO: replace the following code with the real life e-mail resolver

      if ((addresses == null) || (addresses.Length == 0))
      {
        return null;
      }

      var dictionary = (await ReadAddresses()).
        Where(item => !string.IsNullOrEmpty(item.Email)).
        ToDictionary(item => item.Name);

      return addresses.Select(
        a =>
          {
            var address = null as Addressee;

            if (a.Name == "אדמיניסטרטור")
            {
              var admin = dictionary["Lior Ammar"];

              return new EMailAddress
              {
                Name = admin.Name,
                Address = admin.Email
              };
            }
            else if (dictionary.TryGetValue(a.Name, out address))
            {
              return new EMailAddress
              {
                Name = address.Name,
                Address = address.Email
              };
            }
            else
            {
              return new EMailAddress
              {
                Name = "Noname",
                Address = "postman@nesterovsky-bros.com"
              };
            }
          }).ToArray();
    }

    private async Task<EMailAddress> ResolveEmail(string userName)
    {
      // TODO: replace with e-mail resolver

      return await Task.FromResult(
        new EMailAddress
        {
          Name = "EWS User #1",
          Address = "ewsuser1@poalimdev.onmicrosoft.com"
        });
    } 

    private static T FromXmlString<T>(string xml)
    {
      if (string.IsNullOrEmpty(xml))
      {
        return default(T);
      }

      var serializer = new NetDataContractSerializer();
      var reader = new StringReader(xml);

      return (T)serializer.ReadObject(XmlReader.Create(reader));
    }

    /// <summary>
    /// Performs upload action.
    /// </summary>
    /// <param name="action">An action logic.</param>
    /// <returns>Async task.</returns>
    private async Task UploadAction(
      Func<MultipartFormDataStreamProvider, Task> action,
      CancellationToken CancellationToken = default(CancellationToken))
    {
      if (!Request.Content.IsMimeMultipartContent())
      {
        throw new HttpResponseException(HttpStatusCode.UnsupportedMediaType);
      }

      var timeout = 1000;
      var dir = default(TempDirectory);

      Func<Task> block = async () =>
      {
        using (var timeoutSource = new CancellationTokenSource(timeout))
        using (var cancellationSource =
          CancellationTokenSource.CreateLinkedTokenSource(
            timeoutSource.Token,
            CancellationToken))
        {
          dir = await TempDirectory.CreateAsync(null, cancellationSource.Token);
        }

        var root = dir.DirectoryPath;

        Directory.CreateDirectory(root);

        var provider = new MultipartFormDataStreamProvider(root);

        await Request.Content.ReadAsMultipartAsync(provider);

        await action(provider);
      };

      await await block().
        ContinueWith(
          async task =>
          {
            using (var timeoutSource = new CancellationTokenSource(timeout))
            {
              await dir.CloseAsync(timeoutSource.Token);
            }
          });
    }
  }
}
