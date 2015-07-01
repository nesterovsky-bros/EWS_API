namespace Bnhp.RunitChanel
{
  using System;
  using System.Threading;
  using System.Threading.Tasks;

  public class RunitMessage
  {
    public RunitMessage(string request)
    {
      Request = request;
      ResponseSource = new TaskCompletionSource<string>();
    }

    public string Request { get; private set; }
    public Task<string> Response { get { return ResponseSource.Task; } }
    public TaskCompletionSource<string> ResponseSource { get; private set; }
  }
}
