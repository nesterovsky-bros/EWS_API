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
  using System.Threading.Tasks;
  using System.Threading;
  using System.Globalization;
  using System.Collections.Generic;
  using Microsoft.Exchange.WebServices.Data;
  using Configuration;

  public class WcfServiceFactory: UnityServiceHostFactory
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
      var listener = new EwsListener();

      container.
        RegisterInstance(GetSettings()).
        RegisterInstance<IResponseNotifier>(new ResponseNotifier()).
        RegisterInstance(listener).
        RegisterType<IEwsService, EwsService>().
        RegisterType<IRulesService, RulesService>();

      container.BuildUp(listener);

      var startTask = Start(listener);
    }

    public static ApplicationUser[] GetApplicationUsers()
    {
      using(var model = new EWSQueueEntities())
      {
        model.Configuration.ProxyCreationEnabled = false;

        return model.ApplicationUsers.AsNoTracking().
          OrderBy(item => item.Email).
          ToArray();
      }
    }

    private static Settings GetSettings()
    {
      if (globalSettings != null)
      {
        return globalSettings;
      }

      var users = GetApplicationUsers();

      if (users.Length == 0)
      {
        Trace.TraceError("No application users are defined.");

        throw new ApplicationException("No application users are defined.");
      }

      var boolValue = false;

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
        UsersPerUsersSettins =
          int.Parse(ConfigurationManager.AppSettings["UsersPerUsersSettins"]),
        ExchangeListenerRecyclePeriod =
          int.Parse(ConfigurationManager.AppSettings["ExchangeListenerRecyclePeriod"]),
        RetryCount =
          int.Parse(ConfigurationManager.AppSettings["RetryCount"] ?? "3"),
        EWSTrace =
          bool.TryParse(
            ConfigurationManager.AppSettings["EWSTrace"], 
            out boolValue) && boolValue,
        ApplicationUsers = users,
        ExtendedPropertyDefinitions = 
          new Dictionary<string, ExtendedPropertyDefinition>()
      };

      var config =
        ConfigurationManager.GetSection("extendedProperties") as
        ExtendedPropertiesConfigurationSection;

      if ((config != null) && (config.PropertiesCollection != null))
      {
        for (int i = 0, c = config.PropertiesCollection.Count; i < c; i++)
        {
          var item = config.PropertiesCollection[i];
          var propertyDefinition = null as ExtendedPropertyDefinition;

          if (item.Tag.HasValue)
          {
            propertyDefinition = 
              new ExtendedPropertyDefinition(item.Tag.Value, item.MapiType);
          }
          else
          {
            propertyDefinition = new ExtendedPropertyDefinition(
              Settings.ExtendedPropertySetId,
              item.Name, 
              item.MapiType);
          }

          settings.ExtendedPropertyDefinitions.Add(item.Name, propertyDefinition);
        }
      }

      globalSettings = settings;

      if (bool.TryParse(
        ConfigurationManager.AppSettings["ValidateSSLCertificates"],
        out boolValue) && boolValue)
      {
        // Trust all certificates 
        ServicePointManager.ServerCertificateValidationCallback =
          (sender, certificate, chain, sslPolicyErrors) => true;
      }

      return settings;
    }

    private static async System.Threading.Tasks.Task Start(EwsListener listener)
    {
      if (globalListener != null)
      {
        return;
      }

      globalListener = listener;

      try
      {
        await listener.Start();
      }
      catch (Exception e)
      {
        Trace.TraceError("Listener failed. {0}", e);

        throw;
      }
      finally
      {
        globalListener = null;
      }
    }

    private static EwsListener globalListener;
    private static Settings globalSettings;
  }
}