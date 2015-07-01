namespace Bnhp.RunitChanel
{
  using System;
  using System.IO;
  using System.ServiceModel;
  using System.ServiceModel.Channels;
  using System.Text;
  using System.Xml;

  abstract class RunitChannelBase : ChannelBase
  {
    const int MaxBufferSize = 64 * 1024;
    const int MaxSizeOfHeaders = 4 * 1024;

    readonly EndpointAddress address;
    readonly BufferManager bufferManager;
    readonly MessageEncoder encoder;
    readonly long maxReceivedMessageSize;

    public EndpointAddress RemoteAddress
    {
      get { return this.address; }
    }

    public RunitChannelBase(BufferManager bufferManager, MessageEncoderFactory encoderFactory, EndpointAddress address, ChannelManagerBase parent,
     long maxReceivedMessageSize)
      : base(parent)
    {
      this.address = address;
      this.bufferManager = bufferManager;
      this.encoder = encoderFactory.CreateSessionEncoder();
      this.maxReceivedMessageSize = maxReceivedMessageSize;
    }

    protected static Exception ConvertException(Exception exception)
    {
      return new CommunicationException(exception.Message, exception);
    }

    protected override void OnAbort()
    {
    }

    protected override void OnOpen(TimeSpan timeout)
    {
    }

    protected override void OnClose(TimeSpan timeout)
    {
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

    protected Message ReadMessage(string request)
    {
      var stream = new MemoryStream();
      var writer = new StreamWriter(stream);

      writer.Write(request);
      writer.Flush();
      stream.Position = 0;

      return this.encoder.ReadMessage(stream, int.MaxValue);
    }

    protected string WriteMessage(Message message)
    {
      var builder = new StringBuilder();
      var xmlWriter = XmlWriter.Create(builder);

      message.WriteMessage(xmlWriter);
      xmlWriter.Flush();

      return builder.ToString();
    }
  }
}
