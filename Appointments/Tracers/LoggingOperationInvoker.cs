using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.ServiceModel;
using System.ServiceModel.Dispatcher;
using System.Web;

namespace Bnhp.Tracers
{
  public class LoggingOperationInvoker : IOperationInvoker
  {
    public LoggingOperationInvoker(
      IOperationInvoker baseInvoker, 
      DispatchOperation operation)
    {
      this.baseInvoker = baseInvoker;
      this.operationName = operation.Name;
    }

    #region IOperationInvoker Members
    public object Invoke(object instance, object[] inputs, out object[] outputs)
    {
      var operationID = Guid.NewGuid().ToString();

      LogStart(operationID, operationName);

      try
      {
        var result = baseInvoker.Invoke(instance, inputs, out outputs);

        LogEnd(operationID, operationName);

        return result;
      }
      catch (Exception ex)
      {
        LogError(operationID, operationName, ex);
        
        throw ex;
      }
    }

    public IAsyncResult InvokeBegin(
      object instance, 
      object[] inputs, 
      AsyncCallback callback, 
      object state)
    {
      var operationID = Guid.NewGuid().ToString();
      var stateHolder = new LoggingOperationInvoker.AsyncStateHolder
      {
        State = state,
        OperationID =  operationID
      };

      LogStart(operationID, operationName);

      try
      {
        return baseInvoker.InvokeBegin(instance, inputs, callback, stateHolder);
      }
      catch (Exception ex)
      {
        LogError(operationID, operationName, ex);
        
        throw ex;
      } 
    }

    public object InvokeEnd(
      object instance, 
      out object[] outputs, 
      IAsyncResult result)
    {
      var state = 
        result.AsyncState as LoggingOperationInvoker.AsyncStateHolder;

      try
      {
        var output = baseInvoker.InvokeEnd(instance, out outputs, result);

        LogEnd(state.OperationID, operationName);

        return output; 
      }
      catch (Exception ex)
      {
        LogError(state.OperationID, operationName, ex);
        
        throw ex;
      } 
    }

    public object[] AllocateInputs()
    {
      return baseInvoker.AllocateInputs();
    }

    public bool IsSynchronous
    {
      get { return baseInvoker.IsSynchronous; }
    }
    #endregion

    private void LogError(string operationID, string operationName, Exception error)
    {
      try
      {
        var request = OperationContext.Current.RequestContext.RequestMessage;

        Trace.TraceError(
          "\nOperation: " + operationName +
          "\nID: " + operationID +
          "\nError: " + error.ToString() +
          "\nRequest:\n" + request + "\n");
      }
      catch
      {
        // do nothing
      }
    }

    private void LogStart(string operationID, string operationName)
    {
      var context = ServiceSecurityContext.Current;
      var identity = context == null ? null : context.WindowsIdentity;

      Trace.TraceInformation(
        "\nStart of operation: " + operationName +
        "\nID: " + operationID +
        "\nUser: " + (identity == null ? "" : identity.Name) + "\n");
    }

    private void LogEnd(string operationID, string operationName)
    {
      Trace.TraceInformation(
        "\nEnd of operation: " + operationName +
        "\nID: " + operationID + "\n");
    }

    private IOperationInvoker baseInvoker;
    private string operationName;

    private class AsyncStateHolder
    {
      public object State { get; set; }
      public string OperationID { get; set; }
    }
  }
}