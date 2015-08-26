using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Threading;

using Bnhp.Office365.EwsServiceReference;

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

      if (string.IsNullOrEmpty(recipient))
      {
        throw new ArgumentNullException("recipient");
      }

      if (client == null)
      {
        throw new ArgumentNullException("client");
      }

      // save e-mail content to a temporary file
      var eml = await client.GetMessageContentAsync(recipient, message.Id);

      if (eml == null)
      {
        // cannot print this e-mail
        return false;
      }

      var tempFile = Path.GetTempFileName();

      File.Delete(tempFile);

      tempFile = tempFile + ".eml";

      using (var file = File.Create(tempFile))
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
      catch(Exception e)
      {
        Trace.TraceError(e.ToString());

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
