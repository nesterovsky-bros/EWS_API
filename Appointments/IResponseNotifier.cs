namespace Bnhp.Office365
{
  using System;
  using System.Threading.Tasks;

  /// <summary>
  /// A service to notify about response.
  /// </summary>
  public interface IResponseNotifier
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
    Task Notify<I, O>(long ID, I request, O response, Exception error);
  }
}