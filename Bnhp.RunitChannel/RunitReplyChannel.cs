namespace Bnhp.RunitChanel
{
  using System;
  using System.IO;
  using System.ServiceModel;
  using System.ServiceModel.Channels;
  using System.Threading;
  using System.Threading.Tasks;

  partial class RunitReplyChannel : RunitChannelBase, IReplyChannel
  {
    readonly EndpointAddress localAddress;
    readonly RunitReplyChannelListener parent;

    public System.ServiceModel.EndpointAddress LocalAddress
    {
      get { return this.localAddress; }
    }

    public RunitReplyChannel(BufferManager bufferManager, MessageEncoderFactory encoderFactory, EndpointAddress address,
        RunitReplyChannelListener parent)
      : base(bufferManager, encoderFactory, address, parent, parent.MaxReceivedMessageSize)
    {
      this.localAddress = address;
      this.parent = parent;
    }

    public RequestContext ReceiveRequest(TimeSpan timeout)
    {
      ThrowIfDisposedOrNotOpen();

      var runitMessage = null as RunitMessage;

      if (timeout == TimeSpan.MaxValue)
      {
        runitMessage = parent.Queue.Take();
      }
      else if (!parent.Queue.TryTake(out runitMessage, timeout))
      {
        return null;
      }

      return new RunitRequestContext(this, runitMessage);
    }

    public RequestContext ReceiveRequest()
    {
      return ReceiveRequest(DefaultReceiveTimeout);
    }

    public bool TryReceiveRequest(TimeSpan timeout, out RequestContext context)
    {
      ThrowIfDisposedOrNotOpen();

      var runitMessage = null as RunitMessage;

      if (timeout == TimeSpan.MaxValue)
      {
        runitMessage = parent.Queue.Take();
      }
      else if (!parent.Queue.TryTake(out runitMessage, timeout))
      {
        context = null;

        return false;
      }

      context = new RunitRequestContext(this, runitMessage);

      return true;
    }

    public IAsyncResult BeginTryReceiveRequest(TimeSpan timeout, AsyncCallback callback, object state)
    {
      var taskCompletion = new TaskCompletionSource<RequestContext>(state);

      Task.Factory.StartNew(() =>
      {
        RequestContext context = null;

        try
        {
          TryReceiveRequest(timeout, out context);
          taskCompletion.SetResult(context);
        }
        catch(Exception e)
        {
          taskCompletion.SetException(e);
        }

        if (context != null)
        {
          callback(taskCompletion.Task);
        }
      });

      return taskCompletion.Task;
    }

    public bool EndTryReceiveRequest(IAsyncResult result, out RequestContext context)
    {
      var task = result as Task<RequestContext>;

      context = task == null ? null : task.Result;

      return context != null;
    }

    public IAsyncResult BeginReceiveRequest(TimeSpan timeout, AsyncCallback callback, object state)
    {
      throw new NotImplementedException();
    }

    public IAsyncResult BeginReceiveRequest(AsyncCallback callback, object state)
    {
      throw new NotImplementedException();
    }

    public RequestContext EndReceiveRequest(IAsyncResult result)
    {
      throw new NotImplementedException();
    }

    public bool WaitForRequest(TimeSpan timeout)
    {
      throw new NotImplementedException();
    }

    public IAsyncResult BeginWaitForRequest(TimeSpan timeout, AsyncCallback callback, object state)
    {
      throw new NotImplementedException();
    }

    public bool EndWaitForRequest(IAsyncResult asyncResult)
    {
      throw new NotImplementedException();
    }
  }
}
