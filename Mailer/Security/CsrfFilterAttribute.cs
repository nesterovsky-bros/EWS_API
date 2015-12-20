using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using System.Web.Http.Filters;
using System.Net.Http;
using System.Net.Http.Headers;

namespace Mailer.Security
{
  /// <summary>
  /// Implements an IAuthenticationFilter that checks XSRF-TOKEN.
  /// See http://www.asp.net/web-api/overview/security/authentication-filters
  /// </summary>
  public class CsrfFilterAttribute : FilterAttribute, IAuthenticationFilter
  {
    public Task AuthenticateAsync(
      HttpAuthenticationContext context, 
      CancellationToken cancellationToken)
    {
      // get auth token from cookie
      var request = context.Request;
      var headers = request.Headers;
      var authCookie = 
        headers.GetCookies(AuthenticationTokenName).FirstOrDefault();
      var error = false;

      // there is no authentication cookies, do nothing.
      if (authCookie != null)
      {
        var authToken = authCookie[AuthenticationTokenName].Value;

        // get CSRF header
        var csrfToken = headers.GetValues(CSRFHeaderName).FirstOrDefault();

        if (string.IsNullOrEmpty(csrfToken))
        {
          // is there a CSRF cookies?
          var csrfCookie = headers.GetCookies(CSRFTokenName).FirstOrDefault();

          // when there is no CSRF header then it is very probably
          // an attempt to hack the service
          error = ((csrfCookie != null) ||
            (string.Compare(request.Method.Method, "PUT") == 0) ||
            (string.Compare(request.Method.Method, "POST") == 0) ||
            (string.Compare(request.Method.Method, "DELETE") == 0));
        }
        else
        {
          // Verify that CSRF token was generated from auth token
          // Since the CSRF token should have gone out as a cookie, only 
          // our site should have been able to get it (via javascript) and return it in a header. 
          // This proves that our site made the request.
          error = !CsrfTokenHelper.DoesCsrfTokenMatchAuthToken(csrfToken, authToken);
        }
      }

      if (error)
      {
        context.ErrorResult =
          new AuthenticationFailureResult(CSRFAttack, request);
      }

      return Task.FromResult(0);
    }

    public async Task ChallengeAsync(
      HttpAuthenticationChallengeContext context,
      CancellationToken cancellationToken)
    {
      var headers = context.Request.Headers;
      var cookie =
        headers.GetCookies(AuthenticationTokenName).FirstOrDefault();
      var authToken = 
        cookie == null ? null : cookie[AuthenticationTokenName].Value;

      if (!string.IsNullOrEmpty(authToken))
      {
        // is there a CSRF cookies?
        cookie = headers.GetCookies(CSRFTokenName).FirstOrDefault();

        if (cookie == null)
        {
          var token = 
            CsrfTokenHelper.GenerateCsrfTokenFromAuthToken(authToken);

          cookie = new CookieHeaderValue(CSRFTokenName, token)
          {
            HttpOnly = false
          };
        
          var response = await context.Result.ExecuteAsync(cancellationToken);

          response.Headers.AddCookies(new CookieHeaderValue[] { cookie });
        }
      }
      else
      {
        cookie = new CookieHeaderValue(AuthenticationTokenName, Guid.NewGuid().ToString())
        {
          HttpOnly = true
        };

        var response = await context.Result.ExecuteAsync(cancellationToken);

        response.Headers.AddCookies(new CookieHeaderValue[] { cookie });
      }
    }

    private const string CSRFTokenName = "XSRF-TOKEN";
    private const string CSRFHeaderName = "X-XSRF-TOKEN";
    private const string AuthenticationTokenName = "BNHP-AUTH-TOKEN";
    private const string CSRFAttack = "An attempt of CSRF attack detected.";
  }
}
