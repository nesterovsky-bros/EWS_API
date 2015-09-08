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
    /// <param name="ID">A request ID.</param>
    /// <param name="isFault">A fault indicator.</param>
    public virtual Task Notify(long ID, bool isFault)
    {
      if (isFault)
      {
        Trace.TraceError("Request {0} is failed.", ID);
      }
      else
      {
        Trace.TraceInformation("Response {0} is ready.", ID);
      }

      return Task.FromResult(true);
    }
  }
}