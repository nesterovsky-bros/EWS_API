using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Http;

namespace Mailer.Security
{
  /// <summary>
  /// An action fault result.
  /// See http://www.asp.net/web-api/overview/security/authentication-filters
  /// </summary>
  public class AuthenticationFailureResult : IHttpActionResult
  {
    public AuthenticationFailureResult(string reasonPhrase, HttpRequestMessage request)
    {
      ReasonPhrase = reasonPhrase;
      Request = request;
    }

    public string ReasonPhrase { get; private set; }

    public HttpRequestMessage Request { get; private set; }

    public Task<HttpResponseMessage> ExecuteAsync(CancellationToken cancellationToken)
    {
      return Task.FromResult(Execute());
    }

    private HttpResponseMessage Execute()
    {
      return new HttpResponseMessage(HttpStatusCode.Forbidden)
      {
        RequestMessage = Request,
        ReasonPhrase = ReasonPhrase
      };
    }
  }
}
