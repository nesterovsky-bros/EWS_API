﻿namespace Mailer.Controllers
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
      int take = 100)
    {
      using (var context = new Taxonomy())
      {
        var result = await Task.FromResult(
          context.GetUsersOrGroups(filter, take, 1).
            ToArray());

        return result;
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
    public async Task<IEnumerable<BankUnit>> GetBankUnits(
      string units = "branches", 
      string filter = null, 
      int take = 100)
    {
      units = units.ToLower();

      var level = units == "groups" ? 
        4 : units == "departments" ? 
          5 : units == "administrations" ? 
            6 : 7;

      using (var context = new Taxonomy())
      {
        var result = await Task.FromResult(
          context.GetBranches(filter, int.MaxValue).
            Where(item => item.HierarchyID.Split('/').Length == level).
            Take(take).
            ToArray());

        return result;
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
      int take = 100)
    {
      using (var context = new Taxonomy())
      {
        var result = await Task.FromResult(
          context.GetUsersOrGroups(filter, take, 2).
            ToArray());

        return result;
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
      var hierarchyIDs = new StringBuilder();
      var itemNames = new StringBuilder();

      foreach (var item in request.HierarchyIDs)
      {
        if (hierarchyIDs.Length > 0)
        {
          hierarchyIDs.Append(',').Append(item);
        }
        else
        {
          hierarchyIDs.Append(item);
        }
      }

      foreach (var item in request.Roles)
      {
        if (itemNames.Length > 0)
        {
          itemNames.Append(',').Append(item);
        }
        else
        {
          itemNames.Append(item);
        }
      }

      using (var context = new Taxonomy())
      {
        var result = await Task.FromResult(
          context.GetUsersEx(
            hierarchyIDs.ToString(), 
            itemNames.ToString()).
          ToArray());

        return result;
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
    public async Task<IEnumerable<Addressee>> UploadIdentities()
    {
      var list = new List<Addressee>();

      var addresses = (await ReadAddresses("")).
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

      var text = message.Content;

      if (!string.IsNullOrWhiteSpace(text) && text.Trim().StartsWith("<"))
      {
        text = "<html>" + text + "</html>";
      }

      emailMessage.TextBody = text;
      emailMessage.Subject = message.Subject;
      emailMessage.ToRecipients = await ResolveRecipients(message.To);

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
      string filter = null,
      int take = 100)
    {
      if (filter == null)
      {
        return await Task.FromResult<IEnumerable<Addressee>>(new Addressee[0]);
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
              Where(token => item.Name.Contains(token)).
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
    private async Task<IEnumerable<Addressee>> ReadRecipients(
      string text1,
      string text2)
    {
      using (var context = new Taxonomy())
      {
        return await Task.FromResult<IEnumerable<Addressee>>(
          context.GetRecipients(text1, text2, int.MaxValue).
            ToArray().
            Select(
              item => new Addressee
              {
                Id = item.EmployeeCode,
                Name = BuildName(item),
                Email = item.EMail,
                ItemName = item.ItemName,
                HierarchyID = item.HierarchyID
              }).
            Where(item => !string.IsNullOrWhiteSpace(item.Name)));
      }
    }

    private static Regex SeparatorPattern = new Regex(@"[/\\,;:&|#^@~!]|של");
    private static Regex SplitPattern = new Regex(@"\sשל(?:\s|$)|[^\d\w]+0*");

    private static string BuildName(ExtendedRecipient item)
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

      if (item.BranchName != null)
      {
        result.Append("/").
          Append(item.BranchID).
          Append(" ").
          Append(item.BranchName);
      }
      else
      {
        var parts = item.HierarchyID != null ? 
          item.HierarchyID.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries) :
          new string[0];

        if (!string.IsNullOrWhiteSpace(item.GroupName))
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

          result.Append(item.GroupName);
        }

        if (!string.IsNullOrWhiteSpace(item.DepartmentName))
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

          result.Append(item.DepartmentName);
        }

        if (!string.IsNullOrWhiteSpace(item.AdministrationName))
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

          result.Append(item.AdministrationName);
        }
      }

      return result.ToString();
    }

    private async Task<EMailAddress[]> ResolveRecipients(Addressee[] addresses)
    {
      // TODO: replace the following code with the real life e-mail resolver

      if ((addresses == null) || (addresses.Length == 0))
      {
        return null;
      }

      var dictionary = (await ReadAddresses("")).
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
                Name = a.Name,
                Address = "contact@nesterovsky-bros.com"
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
  }
}
