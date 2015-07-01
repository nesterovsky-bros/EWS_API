namespace Bnhp.RunitChanel
{
  using System;
  using System.ServiceModel;
  using System.ServiceModel.Channels;

  partial class RunitReplyChannel
  {
    class RunitRequestContext : RequestContext
    {
      bool aborted;
      readonly RunitReplyChannel parent;
      readonly RunitMessage runitMessage;
      readonly Message message;
      CommunicationState state;

      public override Message RequestMessage
      {
        get { return this.message; }
      }

      public RunitRequestContext(RunitReplyChannel parent, RunitMessage runitMessage)
      {
        this.aborted = false;
        this.parent = parent;
        this.runitMessage = runitMessage;
        this.message = parent.ReadMessage(runitMessage.Request);
        this.state = CommunicationState.Opened;
      }

      public override void Abort()
      {
        if (this.aborted)
        {
          return;
        }
        this.aborted = true;
        this.state = CommunicationState.Faulted;
      }

      public override void Close(TimeSpan timeout)
      {
        this.state = CommunicationState.Closed;
      }

      public override void Close()
      {
        this.Close(this.parent.DefaultCloseTimeout);
      }

      public override void Reply(Message message, TimeSpan timeout)
      {
        if (this.aborted)
        {
          throw new CommunicationObjectAbortedException();
        }
        if (this.state == CommunicationState.Faulted)
        {
          throw new CommunicationObjectFaultedException();
        }
        if (this.state == CommunicationState.Closed)
        {
          throw new ObjectDisposedException("this");
        }

        this.parent.ThrowIfDisposedOrNotOpen();

        runitMessage.ResponseSource.SetResult(
          this.parent.WriteMessage(message));
      }

      public override void Reply(Message message)
      {
        this.Reply(message, this.parent.DefaultSendTimeout);
      }

      public override IAsyncResult BeginReply(Message message, TimeSpan timeout, AsyncCallback callback, object state)
      {
        throw new NotImplementedException();
      }

      public override IAsyncResult BeginReply(Message message, AsyncCallback callback, object state)
      {
        throw new NotImplementedException();
      }

      public override void EndReply(IAsyncResult result)
      {
        throw new NotImplementedException();
      }
    }
  }
}