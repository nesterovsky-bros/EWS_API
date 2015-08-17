namespace Bnhp.Office365
{
  public class RunitService: Bnhp.RunitChanel.RunitService<EwsService>
  {
    protected override System.ServiceModel.ServiceHost CreateServiceHost()
    {
      var factory = new WcfServiceFactory();

      return factory.Create<EwsService>();
    }
  }
}