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
  /// An implementation of IEMailHandler interface that saves
  /// an e-mail message's body to the specified location.
  /// </summary>
  public class SaveBodyHandler: IEMailHandler
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

      if ((args == null) || (args.Length == 0))
      {
        throw new ArgumentNullException("args");
      }

      if (string.IsNullOrEmpty(message.TextBody))
      {
        return false;
      }

      using (var file = File.CreateText(args[0]))
      {
        file.Write(message.TextBody);
      }

      return true;
    }
    #endregion
  }
}
