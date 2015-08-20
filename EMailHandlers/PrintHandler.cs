using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

using Bnhp.Office365.References;
using System.Diagnostics;
using System.Threading;

namespace Bnhp.Office365
{
  /// <summary>
  /// An implementation of IEMailHandler interface that prints
  /// an e-mail message.
  /// </summary>
  public class PrintHandler: IEMailHandler
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

      // save e-mail content to a temporary file
      var eml = client.GetMessageContent(recipient, message.Id);
      var tempFile = Path.GetTempFileName();

      File.Delete(tempFile);

      using (var file = File.Create(tempFile + ".eml"))
      {
        file.Write(eml.Content, 0, eml.Content.Length);
      }

      // print this .eml file
      try
      {
        var printer = new Process();

        printer.StartInfo = new ProcessStartInfo
        {
          UseShellExecute = true,
          Verb = "print",
          FileName = tempFile,
          CreateNoWindow = true,
          WindowStyle = ProcessWindowStyle.Hidden
        };

        printer.Start();
        printer.WaitForInputIdle();

        Thread.Sleep(3000);

        if (!printer.CloseMainWindow())
        {
          printer.Kill();
        }
      }
      catch
      {
        return false;
      }
      finally 
      {
        File.Delete(tempFile);
      }

      return true;
    }
    #endregion
  }
}
