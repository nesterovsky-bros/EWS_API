namespace Mailer.Controllers
{
  using System;
  using System.Collections.Generic;
  using System.Linq;
  using System.Threading;
  using System.Threading.Tasks;
  using System.Web.Http;
  using System.Net;
  using System.Net.Http;
  using System.Runtime.Serialization;
  using System.Xml;
  using System.Text;
  using System.IO;
  using System.Data.Entity;

  using Mailer.Code;
  using Mailer.EwsServiceReference;
  using Mailer.Models;
  using System.Text.RegularExpressions;

  /// <summary>
  /// Declares API for cloud mailer application.
  /// </summary>
  public class MailerController : ApiController
  {
    /// <summary>
    /// Retrieves list of potential senders.
    /// </summary>
    /// <param name="filter">a search filter.</param>
    /// <param name="take">how match items to return (maximum).</param>
    /// <returns>an enumeration of Addressee instances.</returns>
    [Authorize]
    public async Task<IEnumerable<BankUser>> GetSenders(
      string filter, 
      int take = 50)
    {
      using (var context = new Taxonomy())
      {
        return await Task.FromResult(
          context.GetUsersOrGroups(filter, take, 1).
          ToList());
      }
    }

    /// <summary>
    /// Retrieves list of bank's units that suits the specified filter.
    /// </summary>
    /// <param name="units">
    /// determines type of units. 
    /// Possible values are: branches, administrations, departments and groups.
    /// </param>
    /// <param name="filter">a search filter.</param>
    /// <returns>an enumeration of BankUnit instances.</returns>
    [Authorize]
    public async Task<IEnumerable<BankUnit>> GetBankUnits(
      string units = "branches", 
      string filter = null, 
      int take = 50)
    {
      units = units.ToLower();

      var level = units == "groups" ? 
        4 : units == "departments" ? 
          5 : units == "administrations" ? 
            6 : 7;

      using (var context = new Taxonomy())
      {
        return await Task.FromResult(
          context.GetBranches(filter, int.MaxValue).
            Where(item => item.HierarchyID.Split('/').Length == level).
            Take(take).
            ToList());
      }
    }

    /// <summary>
    /// Retrieves the bank's taxonomy.
    /// </summary>
    /// <returns>an enumeration of BankUnit instances.</returns>
    [Authorize]
    public IEnumerable<BankUnit> GetTaxonomy()
    {
      return BankUnits.Get().Values;
    }

    /// <summary>
    /// Retrieves enumeration of bank's roles.
    /// </summary>
    /// <param name="filter">a search filter.</param>
    /// <param name="take">how match items to return (maximum).</param>
    /// <returns>an enumeration of Role instances.</returns>
    [Authorize]
    public async Task<IEnumerable<BankUser>> GetRoles(
      string filter, 
      int take = 50)
    {
      using (var context = new Taxonomy())
      {
        return await Task.FromResult(
          context.GetUsersOrGroups(filter, take, 2).
          ToList());
      }
    }

    /// <summary>
    /// Retrieves enumeration of recipients.
    /// </summary>
    /// <param name="filter">a search filter.</param>
    /// <param name="take">how match items to return (maximum).</param>
    /// <returns>an enumeration of Role instances.</returns>
    [HttpPost]
    [ActionName("GetRecipients")]
    [Authorize]
    public async Task<IEnumerable<BankUser>> GetRecipients(
      RecipientsRequest request)
    {
      var hierarchyIDs = "";
      var itemNames = "";

      foreach (var item in request.HierarchyIDs)
      {
        hierarchyIDs += item + ",";
      }

      if (hierarchyIDs.Length > 0)
      {
        hierarchyIDs = hierarchyIDs.Substring(0, hierarchyIDs.Length - 1);
      }

      foreach (var item in request.Roles)
      {
        itemNames += item + ",";
      }

      if (itemNames.Length > 0)
      {
        itemNames = itemNames.Substring(0, itemNames.Length - 1);
      }

      using (var context = new Taxonomy())
      {
        return await Task.FromResult(
          context.GetUsersEx(hierarchyIDs, itemNames).
            Take(1000). // maximum allowed recipients
            ToList()); 
      }
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
    public async Task<IEnumerable<BankUser>> UploadIdentities()
    {
      var list = new List<BankUser>();

      await UploadAction(
        async provider =>
        {
          var file = provider.FileData.FirstOrDefault();

          if (file == null)
          {
            throw new HttpResponseException(HttpStatusCode.BadRequest);
          }

          var bankUsers = BankUsers.Get().
            Where(item => !string.IsNullOrEmpty(item.EmployeeCode)).
            ToDictionary(item => item.EmployeeCode);

          using (var reader = File.OpenText(file.LocalFileName))
          {
            var line = null as string;
            var bankUser = null as BankUser;

            while ((line = await reader.ReadLineAsync()) != null)
            {
              if (bankUsers.TryGetValue(line.Trim(), out bankUser))
              {
                list.Add(bankUser);
              }
            }
          }
        });

      return list;
    }

    /// <summary>
    /// Creates a new draft message.
    /// </summary>
    /// <returns>a newly created message ID.</returns>
    [HttpPost]
    [ActionName("CreateDraftMessage")]
    [Authorize]
    public async Task<string> CreateDraftMessage(EMailMessage message)
    {
      var client = new EwsServiceClient();
      var sender = ResolveEmail(RequestContext.Principal.Identity.Name);

      message.Sender = sender;
      message.From = sender;

      return await client.CreateMessageAsync(sender.Address, message);
    }

    /// <summary>
    /// Retrieves asynchronously a whole e-mail message by its ID.
    /// </summary>
    /// <param name="messageId">the message ID.</param>
    /// <returns>a Message instance.</returns>
    [Authorize]
    public async Task<Message> GetMessage(string messageId)
    {
      var client = new EwsServiceClient();
      var sender = ResolveEmail(RequestContext.Principal.Identity.Name);
      var message = await client.GetMessageAsync(sender.Address, messageId);

      if (message == null)
      {
        throw new ArgumentException("Wrong messageId.");
      }

      var result = new Message
      {
        Subject = message.Subject,
        From = message.From == null ?
          new BankUser
          {
            FirstName = sender.Name,
            Email = sender.Address
          } :
          //FindBankUser(sender.Address) : 
          FindBankUser(message.From.Address),
        Content = message.TextBody == null ? null :
          message.TextBody.Replace("<html><body>", "").
          Replace("</body></html>", "")
      };

      var to = new List<BankUser>();

      foreach (var recipient in message.ToRecipients)
      {
        var bankUser = FindBankUser(recipient.Address ?? recipient.Name);

        if (bankUser != null)
        {
          to.Add(bankUser);
        }
      }

      result.To = to.ToArray();
      
      result.Attachments = message.Attachments == null ?
        null :
        message.Attachments.Select(
          attachment =>
          {
            var content = client.GetAttachmentByName(
              sender.Address,
              messageId,
              attachment.Name);

            return new Code.Attachment
            {
              Name = attachment.Name,
              Size = attachment.Size.GetValueOrDefault(),
              Content = Convert.ToBase64String(content)
            };
          }).
        ToArray();

      return result;
    }

    /// <summary>
    /// Update message.
    /// </summary>
    /// <param name="message">a message's parts to update. 
    /// At least an unique ID must be presented in the specified message.</param>
    /// <returns>true when the message was successfully updated, false otherwise.</returns>
    [HttpPost]
    [ActionName("UpdateMessage")]
    [Authorize]
    public async Task<bool> UpdateMessage(EMailMessage message)
    {
      var client = new EwsServiceClient();
      var sender = ResolveEmail(RequestContext.Principal.Identity.Name);

      if (!string.IsNullOrEmpty(message.TextBody))
      {
        message.TextBody = "<html><body>" + message.TextBody + "</body></html>";
      }

      return await client.UpdateMessageAsync(sender.Address, message);
    }

    /// <summary>Delete the specified message.</summary>
    /// <param name="messageId">the message ID to delete.</param>
    /// <returns>true when the message was successfully deleted, false otherwise.</returns>
    [HttpPost]
    [ActionName("DeleteMessage")]
    [Authorize]
    public async Task<bool> DeleteMessage(string messageID)
    {
      var client = new EwsServiceClient();
      var sender = ResolveEmail(RequestContext.Principal.Identity.Name);

      return await client.DeleteMessageAsync(sender.Address, messageID);
    }

    [HttpPost]
    [ActionName("AddAttachment")]
    [Authorize]
    public async Task<bool> AddAttachment(AttachmentRequest attachment)
    {
      var client = new EwsServiceClient();
      var sender = ResolveEmail(RequestContext.Principal.Identity.Name);

      return await client.AddAttachmentAsync(
        sender.Address,
        attachment.MessageID, 
        attachment.Name, 
        Convert.FromBase64String(attachment.Content));
    }

    [HttpPost]
    [ActionName("DeleteAttachment")]
    [Authorize]
    public async Task<bool> DeleteAttachment(AttachmentRequest attachment)
    {
      var client = new EwsServiceClient();
      var sender = ResolveEmail(RequestContext.Principal.Identity.Name);

      return await client.DeleteAttachmentByNameAsync(
        sender.Address,
        attachment.MessageID,
        attachment.Name);
    }

    /// <summary>
    /// Sends an e-mail message to recipients.
    /// </summary>
    /// <param name="messageID">an unique ID of message to be sent.</param>
    /// <returns>true when the message was sent successfully, false otherwise.</returns>
    [HttpPost]
    [ActionName("SendDraftMessage")]
    [Authorize]
    public async Task<bool> SendDraftMessage(string messageID)
    {
      var client = new EwsServiceClient();
      var sender = ResolveEmail(RequestContext.Principal.Identity.Name);

      return await client.SendMessageAsync(sender.Address, messageID);
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
      var sender = ResolveEmail(RequestContext.Principal.Identity.Name);

      if ((message.To == null) || (message.To.Length == 0))
      {
        throw new ArgumentException("There are no recipients, message.To is null or empty.");
      }

      if ((message.From == null) || string.IsNullOrEmpty(message.From.Email))
      {
        emailMessage.From = sender;
      }
      else
      {
        emailMessage.From = new EMailAddress
        {
          Address = message.From.Email,
          Name = message.From.FirstName + " " + message.From.SecondName
        };
      }

      var text = message.Content;

      if (!string.IsNullOrWhiteSpace(text) && text.Trim().StartsWith("<"))
      {
        text = "<html><body>" + text + "</body></html>";
      }

      emailMessage.TextBody = text;
      emailMessage.Subject = message.Subject;
      emailMessage.ToRecipients = message.To.Select(
        item => new EMailAddress
        {
          Name = item.FirstName + " " + item.SecondName,
          Address = item.Email
        }).
        ToArray();

      var messageId = 
        await client.CreateMessageAsync(sender.Address, emailMessage);

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

      return await client.SendMessageAsync(sender.Address, messageId);
    }
    
    /// <summary>
    /// Read fake data from the App_Data/test_data.xml
    /// </summary>
    /// <returns></returns>
    private async Task<IEnumerable<BankUser>> ReadAddresses(
      string filter = null,
      int take = 100)
    {
      if (filter == null)
      {
        return await Task.FromResult(new BankUser[0]);
      }

      var text1 = filter;
      var text2 = null as string;
      var tokens = SplitPattern.Split(filter).
        Where(item => item.Length > 1).ToArray();
      var separator = SeparatorPattern.Match(filter);

      if (separator.Success)
      {
        text1 = filter.Substring(0, separator.Index);
        text2 = filter.Substring(separator.Index + separator.Length);
      }

      return (await ReadRecipients(text1, text2)).
        OrderByDescending(
          item =>
            tokens.
              Where(token => BuildName(item).Contains(token)).
              Sum(token => token.Length)).
        Take(take);
    }

    /// <summary>
    /// Reads all recipients according with the specified filters.
    /// </summary>
    /// <param name="text1">a first filter.</param>
    /// <param name="text2">a second filter.</param>
    /// <returns>
    /// a collection of Addressee instances that suit to the specified filter.
    /// </returns>
    private async Task<IEnumerable<BankUser>> ReadRecipients(
      string text1,
      string text2)
    {
      using (var context = new Taxonomy())
      {
        return await Task.FromResult(
          context.GetRecipients(text1, text2, int.MaxValue).
            ToArray().
            Select(
              item => new BankUser
              {
                EmployeeCode = item.EmployeeCode,
                //Name = BuildName(item),
                FirstName = item.FirstName,
                SecondName = item.SecondName,
                Email = item.EMail,
                ItemName = item.ItemName,
                HierarchyID = item.HierarchyID,
                Title = item.Title
              }));
      }
    }

    private static string BuildName(BankUser item)
    {
      var result = new StringBuilder();

      if (item.FirstName != null)
      {
        result.Append(item.FirstName);
      }

      if (item.SecondName != null)
      {
        if (result.Length > 0)
        {
          result.Append(" ");
        }

        result.Append(item.SecondName);
      }

      if ((item.Title != null) && (result.Length == 0))
      {
        result.Append(item.Title);
      }

      var bankUnit = BankUnits.Get()[item.HierarchyID];

      if (bankUnit.BranchName != null)
      {
        result.Append("/").
          Append(bankUnit.BranchID).
          Append(" ").
          Append(bankUnit.BranchName);
      }
      else
      {
        var parts = item.HierarchyID != null ? 
          item.HierarchyID.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries) :
          new string[0];

        if (!string.IsNullOrWhiteSpace(bankUnit.GroupName))
        {
          result.Append("/");

          if (parts.Length > 1)
          {
            var delta = 3 - parts[1].Length;

            if (delta > 0)
            {
              result.Append(new string('0', delta));
            }

            result.Append(parts[1]).Append(" ");
          }

          result.Append(bankUnit.GroupName);
        }

        if (!string.IsNullOrWhiteSpace(bankUnit.DepartmentName))
        {
          result.Append("/");

          if (parts.Length > 2)
          {
            var delta = 3 - parts[2].Length;

            if (delta > 0)
            {
              result.Append(new string('0', delta));
            }

            result.Append(parts[2]).Append(" ");
          }

          result.Append(bankUnit.DepartmentName);
        }

        if (!string.IsNullOrWhiteSpace(bankUnit.AdministrationName))
        {
          result.Append("/");

          if (parts.Length > 3)
          {
            var delta = 3 - parts[3].Length;

            if (delta > 0)
            {
              result.Append(new string('0', delta));
            }

            result.Append(parts[3]).Append(" ");
          }

          result.Append(bankUnit.AdministrationName);
        }
      }

      return result.ToString();
    }

    private EMailAddress ResolveEmail(string name)
    {
      var bankUser = FindBankUser(name);

      return bankUser != null ?
        new EMailAddress
        {
          Name = bankUser.FirstName + " " + bankUser.SecondName,
          Address = bankUser.Email
        } :
        //null;
        new EMailAddress
        {
          Name = "EWS User 1",
          Address = "ewsuser1@poalimdev.onmicrosoft.com"
        }; // for debug purposes only
    }

    private BankUser FindBankUser(string name)
    {
      if (!string.IsNullOrEmpty(name))
      {
        foreach (var user in BankUsers.Get())
        {
          if ((user.ItemName == name) ||
            (user.Email == name) ||
            (user.Title == name) ||
            (user.FirstName + " " + user.SecondName == name))
          {
            return user;
          }
        }
      }

      return null;
    }

    //private static T FromXmlString<T>(string xml)
    //{
    //  if (string.IsNullOrEmpty(xml))
    //  {
    //    return default(T);
    //  }

    //  var serializer = new NetDataContractSerializer();
    //  var reader = new StringReader(xml);

    //  return (T)serializer.ReadObject(XmlReader.Create(reader));
    //}

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

    /// <summary>
    /// Defines cache of bank units.
    /// </summary>
    private static Cache<Dictionary<string, BankUnit>> BankUnits = 
      new Cache<Dictionary<string, BankUnit>>.Builder
      {
        Key = "BankUnits",
        Expiration = Cache.LongDelay,
        Factory = () => 
        {
          using (var context = new Taxonomy())
          {
            return context.GetBranches(null, int.MaxValue).
              ToDictionary(item => item.HierarchyID);
          }
        }
    };

    /// <summary>
    /// Defines cache of bank users.
    /// </summary>
    private static Cache<List<BankUser>> BankUsers =
      new Cache<List<BankUser>>.Builder
      {
        Key = "BankUsers",
        Expiration = Cache.LongDelay,
        Factory = () =>
        {
          using (var context = new Taxonomy())
          {
            return context.GetUsersOrGroups("", int.MaxValue, 1).ToList();
          }
        }
      };

    private static Regex SeparatorPattern = new Regex(@"[/\\,;:&|#^@~!]|של");
    private static Regex SplitPattern = new Regex(@"\sשל(?:\s|$)|[^\d\w]+0*");
  }
}
