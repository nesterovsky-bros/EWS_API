using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

using Bnhp.Office365.References;

namespace Bnhp.Office365
{
  /// <summary>
  /// An implementation of IEMailHandler interface that saves
  /// an e-mail message's attachments to the specified location.
  /// </summary>
  public class SaveAttachmentsHandler: IEMailHandler
  {
    #region IEMailHandler Members
    /// <summary>
    /// Handles the specified e-mail message.
    /// </summary>
    /// <param name="client">An EwsService client.</param>
    /// <param name="message">an EMailMessage instance to handle.</param>
    /// <param name="recipient">a recipient of this message.</param>
    /// <param name="args">optional params for this handler.</param>
    /// <returns>
    /// true when the message was successfully handled, and false otherwise.
    /// </returns>
    public bool Handle(
      EwsServiceClient client,
      EMailMessage message, 
      string recipient, 
      params string[] args)
    {
      if (message == null)
      {
        throw new ArgumentNullException("message");
      }

      if (string.IsNullOrEmpty(recipient))
      {
        throw new ArgumentNullException("recipient");
      }

      if (client == null)
      {
        throw new ArgumentNullException("client");
      }

      if ((args == null) || (args.Length == 0))
      {
        throw new ArgumentNullException("args");
      }

      if ((message.Attachments == null) || (message.Attachments.Length == 0))
      {
        return false;
      }

      var result = false;
      var dir = args[0];

      if (!Directory.Exists(dir))
      {
        Directory.CreateDirectory(dir);
      }

      foreach (var attachment in message.Attachments)
      {
        var content = client.GetAttachmentByName(
          recipient, 
          message.Id, 
          attachment.Name);

        if (content != null)
        {
          using (var file = File.Create(Path.Combine(dir, attachment.Name)))
          {
            file.Write(content, 0, content.Length);
          }

          result = true;
        }
      }
      
      return result;
    }
    #endregion
  }
}
