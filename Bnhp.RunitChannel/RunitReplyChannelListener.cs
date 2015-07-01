namespace Bnhp.RunitChanel
{
  using System;
  using System.Collections.Concurrent;
  using System.ServiceModel;
  using System.ServiceModel.Channels;
  using System.Threading;

  public class RunitReplyChannelListener : ChannelListenerBase<IReplyChannel>
  {
    public readonly long MaxReceivedMessageSize;
    readonly BufferManager bufferManager;
    readonly MessageEncoderFactory encoderFactory;
    readonly Uri uri;
    RunitReplyChannel replyChannel;
    AcceptChannelAsyncResult acceptChannelAsyncResult;

    private static ManualResetEvent acceptChannelWaitHandle = new ManualResetEvent(false);

    public BlockingCollection<RunitMessage> Queue { get; private set; }

    public override Uri Uri
    {
      get { return this.uri; }
    }

    public RunitReplyChannelListener(RunitTransportBindingElement transportElement, BindingContext context)
      : base(context.Binding)
    {
      this.MaxReceivedMessageSize = transportElement.MaxReceivedMessageSize;
      MessageEncodingBindingElement messageElement = context.BindingParameters.Remove<MessageEncodingBindingElement>();
      this.bufferManager = BufferManager.CreateBufferManager(transportElement.MaxBufferPoolSize, (int)this.MaxReceivedMessageSize);
      this.encoderFactory = messageElement.CreateMessageEncoderFactory();
      this.uri = new Uri(context.ListenUriBaseAddress, context.ListenUriRelativeAddress);
      this.Queue = new BlockingCollection<RunitMessage>();
    }

    protected override void OnOpen(TimeSpan timeout)
    {
    }

    protected override void OnClose(TimeSpan timeout)
    {
      if (this.acceptChannelAsyncResult != null)
      {
        RunitReplyChannelListener.acceptChannelWaitHandle.Set();
        if (this.acceptChannelAsyncResult.Callback != null)
          this.acceptChannelAsyncResult.Callback(this.acceptChannelAsyncResult);
      }
    }

    protected override IReplyChannel OnAcceptChannel(TimeSpan timeout)
    {
      if (this.replyChannel == null)
      {
        EndpointAddress address = new EndpointAddress(Uri);
        this.replyChannel = new RunitReplyChannel(this.bufferManager, this.encoderFactory, address, this);
      }

      return this.replyChannel;
    }

    protected override IAsyncResult OnBeginAcceptChannel(TimeSpan timeout, AsyncCallback callback, object state)
    {
      if (this.replyChannel != null)
        return this.acceptChannelAsyncResult = new AcceptChannelAsyncResult() { AsyncState = state, Callback = callback };

      CompletedAsyncResult asyncResult = new CompletedAsyncResult() { AsyncState = state, Timeout = timeout };
      if (callback != null)
        callback(asyncResult);
      return asyncResult;
    }

    protected override IReplyChannel OnEndAcceptChannel(IAsyncResult result)
    {
      if (result is CompletedAsyncResult)
        return this.OnAcceptChannel((result as CompletedAsyncResult).Timeout);
      return null;
    }

    protected override IAsyncResult OnBeginWaitForChannel(TimeSpan timeout, AsyncCallback callback, object state)
    {
      throw new NotImplementedException();
    }

    protected override bool OnEndWaitForChannel(IAsyncResult result)
    {
      throw new NotImplementedException();
    }

    protected override bool OnWaitForChannel(TimeSpan timeout)
    {
      throw new NotImplementedException();
    }

    protected override void OnAbort()
    {
      throw new NotImplementedException();
    }

    protected override IAsyncResult OnBeginClose(TimeSpan timeout, AsyncCallback callback, object state)
    {
      throw new NotImplementedException();
    }

    protected override IAsyncResult OnBeginOpen(TimeSpan timeout, AsyncCallback callback, object state)
    {
      throw new NotImplementedException();
    }

    protected override void OnEndClose(IAsyncResult result)
    {
      throw new NotImplementedException();
    }

    protected override void OnEndOpen(IAsyncResult result)
    {
      throw new NotImplementedException();
    }

    class AcceptChannelAsyncResult : IAsyncResult
    {
      public AsyncCallback Callback { get; set; }

      public object AsyncState { get; set; }

      public System.Threading.WaitHandle AsyncWaitHandle
      {
        get { return RunitReplyChannelListener.acceptChannelWaitHandle; }
      }

      public bool CompletedSynchronously
      {
        get { return false; }
      }

      public bool IsCompleted { get; private set; }
    }

  }
}
