namespace Bnhp.RunitChanel
{
  using System;
  using System.Globalization;
  using System.ServiceModel.Channels;
  using System.ServiceModel.Configuration;

  public class RunitTransportBindingConfigurationElement : StandardBindingElement
  {
    protected override Type BindingElementType
    {
      get { return typeof(RunitTransportBinding); }
    }

    protected override void OnApplyConfiguration(Binding binding)
    {
      if (binding == null)
      {
        throw new ArgumentNullException("binding");
      }

      if (binding.GetType() != typeof(RunitTransportBinding))
      {
        throw new ArgumentException(string.Format(CultureInfo.CurrentCulture,
            "Invalid type for binding. Expected type: {0}. Type passed in: {1}.",
            typeof(RunitTransportBinding).AssemblyQualifiedName,
            binding.GetType().AssemblyQualifiedName));
      }
    }
  }

  public class RunitTransportBindingCollectionElement :
      StandardBindingCollectionElement<RunitTransportBinding, RunitTransportBindingConfigurationElement> { }
}
