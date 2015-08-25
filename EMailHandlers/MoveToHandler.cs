using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

using Bnhp.Office365.EwsServiceReference;

namespace Bnhp.Office365
{
  /// <summary>
  /// An implementation of IEMailHandler interface that moves
  /// an e-mail message to the specified folder in the recipient's 
  /// mail box.
  /// </summary>
  public class MoveToHandler: IEMailHandler
  {
    #region IEMailHandler Members
    /// <summary>
    /// Handles the specified e-mail message.
    /// </summary>
    /// <param actionName="client">An EwsService client.</param>
    /// <param actionName="message">an EMailMessage instance to handle.</param>
    /// <param actionName="recipient">a recipient of this message.</param>
    /// <param actionName="args">optional params for this handler.</param>
    /// <returns>
    /// true when the message was successfully handled, and false otherwise.
    /// </returns>
    public async Task<bool> Handle(
      EwsServiceClient client,
      EMailMessage message, 
      string recipient, 
      params string[] args)
    {
      if (message == null)
      {
        throw new ArgumentNullException("message");
      }

      if (client == null)
      {
        throw new ArgumentNullException("client");
      }

      if (string.IsNullOrEmpty(recipient))
      {
        throw new ArgumentNullException("recipient");
      }

      if ((args == null) || (args.Length == 0))
      {
        throw new ArgumentNullException("args");
      }

      return await client.MoveToAsync(recipient, message.Id, args[0]);
    }
    #endregion
  }
}
