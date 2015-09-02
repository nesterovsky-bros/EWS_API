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
    /// <param name="isFault">A fault indicator.</param>
    Task Notify(long ID, bool isFault);
  }
}