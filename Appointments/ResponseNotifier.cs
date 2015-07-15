namespace Bnhp.Office365
{
  using System;
  using System.Diagnostics;
  using System.Threading.Tasks;

  /// <summary>
  /// A service to notify about response.
  /// </summary>
  public class ResponseNotifier: IResponseNotifier
  {
    /// <summary>
    /// Method called when response with a specified ID is ready.
    /// </summary>
    /// <typeparam name="I">A request type.</typeparam>
    /// <typeparam name="O">A response type.</typeparam>
    /// <param name="ID">A request ID.</param>
    /// <param name="request">A request instance.</param>
    /// <param name="response">A response intance, if any.</param>
    /// <param name="error">An error in case of error.</param>
    public virtual Task Notify<I, O>(
      long ID, 
      I request, 
      O response, 
      Exception error)
    {
      if (error != null)
      {
        Trace.TraceError("Request {0} failed with error: {1}", ID, error.Message);
      }
      else
      {
        Trace.TraceInformation("Response {0} is ready.", ID);
      }

      return Task.FromResult(true);
    }
  }
}