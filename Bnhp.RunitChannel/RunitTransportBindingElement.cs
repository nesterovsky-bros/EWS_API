namespace Bnhp.RunitChanel
{
  using System;
  using System.ServiceModel.Channels;

  public class RunitTransportBindingElement : TransportBindingElement
  {
    public RunitTransportBindingElement() { }

    public RunitTransportBindingElement(RunitTransportBindingElement other) { }

    public override string Scheme
    {
      get { return "runit"; }
    }

    public override BindingElement Clone()
    {
      return new RunitTransportBindingElement(this);
    }

    public override bool CanBuildChannelFactory<TChannel>(BindingContext context)
    {
      return typeof(TChannel) == typeof(IRequestChannel);
    }

    public override bool CanBuildChannelListener<TChannel>(BindingContext context)
    {
      return typeof(TChannel) == typeof(IReplyChannel);
    }

    public override IChannelListener<TChannel> BuildChannelListener<TChannel>(BindingContext context)
    {
      if (context == null)
      {
        throw new ArgumentNullException("context");
      }
      if (!CanBuildChannelListener<TChannel>(context))
      {
        throw new ArgumentException(String.Format("Unsupported channel type: {0}.", typeof(TChannel).Name));
      }
      return (IChannelListener<TChannel>)(object)new RunitReplyChannelListener(this, context);
    }
  }
}
