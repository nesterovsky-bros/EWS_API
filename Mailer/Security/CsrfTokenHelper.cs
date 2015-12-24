using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Web;

namespace Mailer.Security
{
  /// <summary>
  /// A helper class against CSRF attacks.
  /// See <a href="http://stackoverflow.com/questions/15574486/angular-against-asp-net-webapi-implement-csrf-on-the-server">"Angular against Asp.Net WebApi, implement CSRF on the server"</a>.
  /// </summary>
  public class CsrfTokenHelper
  {
    public static string GetCookieValue(HttpRequestMessage request, string name)
    {
      var cookie = request.Headers.GetCookies(name).FirstOrDefault();

      return cookie == null ? null : cookie[name].Value;
    }

    public static void SetCookieValue(
      HttpResponseMessage response,
      string name,
      string value,
      bool httpOnly = true)
    {
      var cookie = new CookieHeaderValue(name, value)
      {
        HttpOnly = httpOnly,
        MaxAge = TimeSpan.FromDays(1)
      };

      response.Headers.AddCookies(new CookieHeaderValue[] { cookie });
    }

    public static string GenerateToken(string authToken = null)
    {
      return string.IsNullOrEmpty(authToken) ? 
        Guid.NewGuid().ToString() : 
        GenerateHash(authToken);
    }

    public static bool IsMatch(string csrfToken, string authToken)
    {
      return csrfToken == GenerateHash(authToken);
    }

    private static string GenerateHash(string authToken)
    {
      using (var sha = SHA256.Create())
      {
        var computedHash = 
          sha.ComputeHash(Encoding.Unicode.GetBytes(authToken + ConstantSalt));
        var cookieFriendlyHash = HttpServerUtility.UrlTokenEncode(computedHash);

        return cookieFriendlyHash;
      }
    }

    public static string AuthCookieName = "BNHP-AUTH-TOKEN";
    public static string CSRFHeaderName = "X-XSRF-TOKEN";
    public static string CSRFTokenName = "XSRF-TOKEN";
    
    private const string ConstantSalt = "{D60F377A-F587-4323-AD4F-7F4539A60FCC}";
  }
}
