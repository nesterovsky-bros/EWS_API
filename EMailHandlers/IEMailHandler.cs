using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Bnhp.Office365.EwsServiceReference;

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
      /// <param actionName="client">An EwsService client.</param>
      /// <param actionName="message">an EMailMessage instance to handle.</param>
      /// <param actionName="recipient">a recipient of this message.</param>
      /// <param actionName="args">optional params for this handler.</param>
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
