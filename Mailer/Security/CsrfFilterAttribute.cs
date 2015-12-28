using System;
using System.Linq;
using System.Text;
using System.Web;
using System.Web.Http.Filters;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Web.Http.Controllers;
using System.Net;
using System.Security.Cryptography;
using System.Configuration;
using System.Collections.Generic;

namespace Mailer.Security
{
  /// <summary>
  /// Implements an IAuthenticationFilter that checks XSRF-TOKEN.
  /// See http://stackoverflow.com/questions/23339002/custom-authorization-attribute-not-working-in-webapi
  /// </summary>
  public class CsrfFilterAttribute : ActionFilterAttribute
  {
    public override void OnActionExecuting(HttpActionContext context)
    {
      var request = context.Request;
      var headers = null as IEnumerable<string>;

      if (request.Headers.TryGetValues(CSRFHeaderName, out headers))
      {
        var token = headers.FirstOrDefault();
        var cookie =
          request.Headers.GetCookies(AuthCookieName).FirstOrDefault();
        var authToken = cookie == null ? null : cookie[AuthCookieName].Value;

        if (IsMatch(token, authToken))
        {
          return;
        }
      }
      else if (string.Compare(request.Method.Method, "get", true) == 0)
      {
        return;
      }
      // else access forbiden.

      context.Response =
        new HttpResponseMessage(HttpStatusCode.Forbidden)
        {
          ReasonPhrase = "An attempt of CSRF attack was detected."
        };
    }

    public override void OnActionExecuted(HttpActionExecutedContext context)
    {
      var principal = context.ActionContext.RequestContext.Principal;
      var userName = principal == null ? null : principal.Identity.Name;
      var token = GenerateToken(userName);

      context.Response.Headers.AddCookies(
        new CookieHeaderValue[] 
        {
          new CookieHeaderValue(AuthCookieName, token)
          {
            HttpOnly = true
          },
          new CookieHeaderValue(CSRFCookieName, token)
          {
            HttpOnly = false
          }
        });
    }

    public override bool AllowMultiple
    {
      get { return false; }
    }

    protected static string GenerateToken(string authToken)
    {
      authToken += ":" + Guid.NewGuid().ToString();

      return GenerateHash(authToken);
    }

    protected static bool IsMatch(string csrfToken, string authToken)
    {
      return csrfToken == GenerateHash(authToken);
    }

    protected static string GenerateHash(string authToken)
    {
      using (var sha = SHA256.Create())
      {
        var computedHash =
          sha.ComputeHash(Encoding.Unicode.GetBytes(authToken + ConstantSalt));
        var cookieFriendlyHash = HttpServerUtility.UrlTokenEncode(computedHash);

        return cookieFriendlyHash;
      }
    }

    /// <summary>
    /// Gets a CSRF header name.
    /// </summary>
    protected static string CSRFHeaderName
    {
      get
      {
        if (string.IsNullOrEmpty(_CSRFHeaderName))
        {
          _CSRFHeaderName =
            ConfigurationManager.AppSettings["CSRFHeaderName"] ??
            "X-XSRF-TOKEN";
        }

        return _CSRFHeaderName;
      }
    }

    /// <summary>
    /// Gets a CSRF cookie name.
    /// </summary>
    protected static string CSRFCookieName
    {
      get
      {
        if (string.IsNullOrEmpty(_CSRFTokenName))
        {
          _CSRFTokenName =
            ConfigurationManager.AppSettings["CSRFTokenName"] ??
            "XSRF-TOKEN";
        }

        return _CSRFTokenName;
      }
    }

    private static string _CSRFHeaderName;
    private static string _CSRFTokenName;
    private static string ConstantSalt = "{D60F377A-F587-4323-AD4F-7F4539A60FCC}";
    private const string AuthCookieName = "BNHP_AUTH_TOKEN";
  }
}
