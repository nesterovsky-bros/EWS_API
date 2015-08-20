using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Bnhp.Office365.References;

namespace Bnhp.Office365
{
    /// <summary>
    /// Defines an interface of e-mail handler.
    /// </summary>
    public interface IEMailHandler
    {
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
      bool Handle(
        EwsServiceClient client,
        EMailMessage message, 
        string recipient, 
        params string[] args);
    }
}
