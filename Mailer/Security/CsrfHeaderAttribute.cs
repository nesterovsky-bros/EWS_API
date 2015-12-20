using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Web.Http;
using System.Web.Http.Controllers;

namespace Mailer.Security
{
  /// <summary>
  /// Descendant of AuthorizeAttribute that checks XSRF token.
  /// See http://stackoverflow.com/questions/15574486/angular-against-asp-net-webapi-implement-csrf-on-the-server
  /// </summary>
  public class CsrfHeaderAttribute: AuthorizeAttribute
  {
    protected override bool IsAuthorized(HttpActionContext context)
    {
      if (!base.IsAuthorized(context))
      {
        return false;
      }

      var token = CsrfTokenHelper.GetCookieValue(
        context.Request, 
        CsrfTokenHelper.CSRFHeaderName);

      if (string.IsNullOrEmpty(token))
      {
        return false;
      }

      var userName = context.RequestContext.Principal.Identity.Name;

      //context.Request.Headers.

      // Verify that csrf token was generated from auth token
      // Since the csrf token should have gone out as a cookie, 
      // only our site should have been able to get it (via javascript) and return it 
      // in a header. This proves that our site made the request.
      return CsrfTokenHelper.IsMatch(token, userName);
    }
  }
}
