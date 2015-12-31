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
  /// Implements ActionFilterAttribute against CSRF attacks.
  /// </summary>
  public class CsrfFilterAttribute : ActionFilterAttribute
  {
    public override void OnActionExecuting(HttpActionContext context)
    {
      var principal = context.RequestContext.Principal;
      var userName = principal == null ? null : principal.Identity.Name;

      // checks whether the user is authenticated
      if (!string.IsNullOrEmpty(userName))
      {
        var request = context.Request;
        var headers = null as IEnumerable<string>;

        // gets CSRF HTTP header
        if (request.Headers.TryGetValues(CSRFHeaderName, out headers))
        {
          var header = headers.FirstOrDefault();
          var cookie = 
            request.Headers.GetCookies(AuthCookieName).FirstOrDefault();
          var authToken = cookie == null ? null : cookie[AuthCookieName].Value;
          
          // checks CSRF header against an authentication token
          if (IsMatch(header, userName + authToken))
          {
            return;
          }
        }
        // a CSRF header may be omitted for first HTTP GET calls only
        else if (string.Compare(request.Method.Method, "get", true) == 0)
        {
          return;
        }
        // else access forbiden.
      }

      context.Response =
        new HttpResponseMessage(HttpStatusCode.Forbidden)
        {
          ReasonPhrase = "Invalid CSRF token."
        };
    }

    public override void OnActionExecuted(HttpActionExecutedContext context)
    {
      var principal = context.ActionContext.RequestContext.Principal;
      var userName = principal == null ? null : principal.Identity.Name;

      // checks whether the user is authenticated
      if (string.IsNullOrEmpty(userName))
      {
        return;
      }

      // generates an unique CSRF cookie for the next request
      var authToken = Guid.NewGuid().ToString();
      var response = context.Response;
      var headers = response == null ? null : response.Headers;

      if (headers != null)
      {
        var csrfToken = GenerateToken(userName + authToken);

        headers.AddCookies(
          new[]
          {
            new CookieHeaderValue(AuthCookieName, authToken)
            {
              HttpOnly = true,
              Path = "/"
            },
            new CookieHeaderValue(CSRFCookieName, csrfToken)
            {
              HttpOnly = false,
              Path = "/"
            }
          });
      }
    }

    public override bool AllowMultiple
    {
      get { return false; }
    }

    /// <summary>
    /// Checks whether the specified CSRF token is match to the security
    /// key based on the providen token.
    /// </summary>
    /// <param name="csrfToken">a CSRF token to check.</param>
    /// <param name="token">a base private token.</param>
    /// <returns>
    /// true when CSRF token matches the base token and false otherwise.
    /// </returns>
    protected static bool IsMatch(string csrfToken, string token)
    {
      return csrfToken == GenerateToken(token);
    }

    /// <summary>
    /// Generates a security token based on the specified value.
    /// </summary>
    /// <param name="value">a base string value.</param>
    /// <returns>a security token.</returns>
    protected static string GenerateToken(string value)
    {
      using (var sha = SHA256.Create())
      {
        var hash = sha.ComputeHash(
          Encoding.Unicode.GetBytes(value + ApplicationSecret));

        return HttpServerUtility.UrlTokenEncode(hash);
      }
    }

    /// <summary>
    /// Gets a CSRF header name.
    /// </summary>
    protected static string CSRFHeaderName
    {
      get
      {
        return ConfigurationManager.AppSettings["CSRFHeaderName"] ??
          "X-XSRF-TOKEN";
      }
    }

    /// <summary>
    /// Gets a CSRF cookie name.
    /// </summary>
    protected static string CSRFCookieName
    {
      get
      {
        return ConfigurationManager.AppSettings["CSRFCookieName"] ??
          "XSRF-TOKEN";
      }
    }

    /// <summary>
    /// Gets an authentication cookie name.
    /// </summary>
    protected static string AuthCookieName
    {
      get
      {
        return ConfigurationManager.AppSettings["AuthCookieName"] ??
          "BNHP-AUTH-TOKEN";
      }
    }

    // application secret
    private static string ApplicationSecret = 
      "{33BE6648-7500-4976-A0CF-C9B834847282}";
  }
}
