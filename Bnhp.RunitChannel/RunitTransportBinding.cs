namespace Bnhp.RunitChanel
{
  using System.Configuration;
  using System.Globalization;
  using System.ServiceModel.Channels;

  public class RunitTransportBinding : Binding
  {
    readonly MessageEncodingBindingElement messageElement;
    readonly RunitTransportBindingElement transportElement;

    public RunitTransportBinding()
    {
      this.messageElement = new TextMessageEncodingBindingElement();
      this.transportElement = new RunitTransportBindingElement();
    }

    public RunitTransportBinding(string configurationName)
      : this()
    {
      RunitTransportBindingCollectionElement section = (RunitTransportBindingCollectionElement)ConfigurationManager.GetSection(
          "system.serviceModel/bindings/runitTransportBinding");
      RunitTransportBindingConfigurationElement element = section.Bindings[configurationName];
      if (element == null)
      {
        throw new ConfigurationErrorsException(string.Format(CultureInfo.CurrentCulture,
            "There is no binding named {0} at {1}.", configurationName, section.BindingName));
      }
      else
      {
        element.ApplyConfiguration(this);
      }
    }

    public override BindingElementCollection CreateBindingElements()
    {
      return new BindingElementCollection(
        new BindingElement[] 
        {
            this.messageElement,
            this.transportElement
        });
    }

    public override string Scheme
    {
      get { return this.transportElement.Scheme; }
    }
  }
}
