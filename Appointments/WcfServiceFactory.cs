namespace Bnhp.Office365
{
  using System;
  using System.Configuration;
  using System.ServiceModel;
  using Microsoft.Practices.Unity;
  using Unity.Wcf;
  using System.Net;

  public class WcfServiceFactory : UnityServiceHostFactory
  {
    public ServiceHost Create<T>(params Uri[] baseAddresses)
    {
      return CreateServiceHost(typeof(T), baseAddresses);
    }

    protected override void ConfigureContainer(IUnityContainer container)
    {
      Configure(container);
    }

    public static void Configure(IUnityContainer container)
    {
      var settings = new Settings
      {
        ExchangeUserName =
          ConfigurationManager.AppSettings["ExchangeUserName"],
        ExchangePassword =
          ConfigurationManager.AppSettings["ExchangePassword"],
        RequestTimeout =
          double.Parse(ConfigurationManager.AppSettings["RequestTimeout"]),
        AutoDiscoveryUrl =
          ConfigurationManager.AppSettings["AutoDiscoveryUrl"],
        AttemptsToDiscoverUrl =
          int.Parse(ConfigurationManager.AppSettings["AttemptsToDiscoverUrl"]),
        ExchangeListenerRecyclePeriod =
          int.Parse(ConfigurationManager.AppSettings["ExchangeListenerRecyclePeriod"])
      };

      var listener = new EwsListener();

      container.
        RegisterInstance(settings).
        RegisterInstance<IResponseNotifier>(new ResponseNotifier()).
        RegisterInstance(listener).
        RegisterType<IAppointments, Appointments>();

      container.BuildUp(listener);

      var startTask = listener.Start();
    }
  }
}