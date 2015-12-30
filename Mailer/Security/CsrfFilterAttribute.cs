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
      var principal = context.RequestContext.Principal;
      var userName = principal == null ? null : principal.Identity.Name;

      // checks whether the user is authenticated
      if (!string.IsNullOrEmpty(userName))
      {
        // gets CSRF HTTP header
        if (request.Headers.TryGetValues(CSRFHeaderName, out headers))
        {
          var token = headers.FirstOrDefault();

          // checks CSRF header against a private security key
          if (IsMatch(token, userName))
          {
            return;
          }
        }
        // a CSRF header may be omitted for first HTTP GET methods only
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

      var request = context.Request;
      var headers = null as IEnumerable<string>;

      if (!request.Headers.TryGetValues(CSRFHeaderName, out headers))
      {
        // only GET method may be without CSRF method.
        // Note: the business logic must avoid to do CRUD actions on HTTP GET!!!
        if (string.Compare(request.Method.Method, "get", true) == 0)
        {
          var token = GenerateToken(userName);

          // sets CSRF cookie for subsequent calls
          context.Response.Headers.AddCookies(
            new CookieHeaderValue[]
            {
              new CookieHeaderValue(CSRFCookieName, token)
              {
                HttpOnly = false,
                Path = "/"
              }
            });
        }
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
    /// <param name="authToken">a base private token.</param>
    /// <returns></returns>
    protected static bool IsMatch(string csrfToken, string authToken)
    {
      return csrfToken == GenerateToken(authToken);
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
        var computedHash =
          sha.ComputeHash(Encoding.Unicode.GetBytes(value + ConstantSalt));
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
    private static string ConstantSalt = Guid.NewGuid().ToString();
  }
}
