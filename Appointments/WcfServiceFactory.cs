namespace Bnhp.Office365
{
  using System;
  using System.Linq;
  using System.Configuration;
  using System.ServiceModel;
  using Microsoft.Practices.Unity;
  using Unity.Wcf;
  using System.Net;
  using System.Diagnostics;

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
      var users = GetApplicationUsers();

      if (users.Length == 0)
      {
        Trace.TraceError("No application users are defined.");

        throw new ApplicationException("No application users are defined.");
      }

      var settings = new Settings
      {
        HangingConnectionLimit =
          int.Parse(ConfigurationManager.AppSettings["HangingConnectionLimit"]),
        EWSMaxConcurrency =
          int.Parse(ConfigurationManager.AppSettings["EWSMaxConcurrency"]),
        RequestTimeout =
          double.Parse(ConfigurationManager.AppSettings["RequestTimeout"]),
        AutoDiscoveryUrl =
          ConfigurationManager.AppSettings["AutoDiscoveryUrl"],
        AttemptsToDiscoverUrl =
          int.Parse(ConfigurationManager.AppSettings["AttemptsToDiscoverUrl"]),
        ExchangeListenerRecyclePeriod =
          int.Parse(ConfigurationManager.AppSettings["ExchangeListenerRecyclePeriod"]),
        ApplicationUsers = users,
        DefaultApplicationUser = users[0]
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

    public static ApplicationUser[] GetApplicationUsers()
    {
      using(var model = new EWSQueueEntities())
      {
        model.Configuration.ProxyCreationEnabled = false;

        return model.ApplicationUsers.AsNoTracking().ToArray();
      }
    }
  }
}