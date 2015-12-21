using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Threading;
using System.Configuration;

using CsQuery;

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

      long WaitPeriod = (long.TryParse(
        ConfigurationManager.AppSettings["PrinterHandlerWaitPeriod"], out WaitPeriod) ?
        WaitPeriod * 1000 : 30000) * TimeSpan.TicksPerMillisecond;

      var tempFile = Path.GetTempFileName();

      File.Delete(tempFile);

      tempFile = tempFile + ".html";

      using (var file = File.CreateText(tempFile))
      {
        var to = new StringBuilder();
        var attachments = new StringBuilder();

        foreach (var toRecipient in message.ToRecipients)
        {
          if (to.Length != 0)
          {
            to.Append(';');
          }

          to.AppendFormat(ToTemplate, toRecipient.Name, toRecipient.Address);
        }

        if (message.Attachments != null)
        {
          foreach (var attachment in message.Attachments)
          {
            attachments.AppendFormat(AttachmentTemplate, attachment.Name);
          }

          attachments = new StringBuilder().
            AppendFormat(AttachmentsTemplate, attachments.ToString());
        }

        var messageTemplate = string.Format(MessageHeaderTemplate,
          message.Subject,
          message.From != null ? message.From.Name : "",
          message.From != null ? message.From.Address : "",
          message.DateTimeSent.ToString(),
          to.ToString());

        if (IsHtml.IsMatch(message.TextBody))
        {
          var content = new CQ(message.TextBody);
          var body = content.Find("body");
          var children = body.Children();
          var template = new CQ(messageTemplate);

          template.InsertBefore(children[0]);

          template = attachments.ToString();

          template.InsertAfter(children[children.Length - 1]);

          messageTemplate = content.Render();
        }
        else
        {
          messageTemplate = string.Format(MessageTemplate,
            message.Subject,
            messageTemplate,
            string.IsNullOrEmpty(message.TextBody) ? "" : message.TextBody,
            attachments.ToString());
        }


        file.Write(messageTemplate);
      }
      
      // print content
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

        var startTime = DateTime.Now.Ticks;

        printer.Start();
        printer.WaitForInputIdle();

        while(true)
        {
          Thread.Sleep(500);

          if (printer.HasExited || (DateTime.Now.Ticks - startTime >= WaitPeriod))
          {
            break;
          }
        }

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

    /// <summary>
    /// HTML pattern.
    /// </summary>
    private static Regex IsHtml = new Regex(@"\<html.*/html\>",
      RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.Singleline);

    private const string MessageTemplate =
      "<html><head><title>{0}</title></head><body>" +
      "<div>{1}</div>" +
      "<p>&nbsp;</p>" +
      "<div>{2}</div>" +
      "<p>&nbsp;</p>" +
      "<div>{3}</div>" +
      "</body></html>";

    private const string MessageHeaderTemplate =
      "<p><span><b>From:</b>&nbsp;<span>{1} &lt;{2}&gt;</span></p>" +
      "<p><span><b>Sent:</b>&nbsp;<span>{3}</span></p>" +
      "<p><span><b>To:</b>&nbsp;<span>{4}</span></p>" +
      "<p><span><b>Subject:</b>&nbsp;<span>{0}</span></p>" +
      "<p>&nbsp;</p>";

    private const string ToTemplate =
      "{0} &lt;{1}&gt;";

    private const string AttachmentTemplate =
      "<li>{0}</li>";

    private const string AttachmentsTemplate =
      "<div style='font-weight: bold'>Attachments:</div><ul>{0}</ul>";
    #endregion
  }
}
