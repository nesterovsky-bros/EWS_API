namespace Bnhp.Office365
{
  using Microsoft.Practices.Unity;
  using System.Configuration;
  using Unity.Wcf;

  public class WcfServiceFactory : UnityServiceHostFactory
  {
    protected override void ConfigureContainer(IUnityContainer container)
    {
      // register all your components with the container here
      // container
      //    .RegisterType<IService1, Service1>()
      //    .RegisterType<DataContext>(new HierarchicalLifetimeManager());

      var settings = new Settings
      {
        ExchangeUserName = 
          ConfigurationManager.AppSettings["ExchangeUserName"],
        ExchangePassword = 
          ConfigurationManager.AppSettings["ExchangePassword"],
        RequestTimeout = 
          double.Parse(ConfigurationManager.AppSettings["RequestTimeout"])
      };

      container.
        RegisterInstance<IResponseNotifier>(new ResponseNotifier()).
        RegisterInstance(settings).
        RegisterType<IAppointments, Appointments>();
    }
  }
}