namespace Bnhp.Office365
{
  using Microsoft.Exchange.WebServices.Data;

  /// <summary>
  /// A <see cref="ITraceListener"/> that writes to the 
  /// <see cref="System.Diagnostics.Trace"/>.
  /// </summary>
  public class EwsTraceListener: ITraceListener
  {
    public void Trace(string traceType, string traceMessage)
    {
      System.Diagnostics.Trace.TraceInformation(
        "{0}. {1}", 
        traceType, 
        traceMessage);
    }
  }
}