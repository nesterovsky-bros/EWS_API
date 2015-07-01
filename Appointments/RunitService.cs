namespace Bnhp.Office365
{
  public class RunitService: Bnhp.RunitChanel.RunitService<Appointments>
  {
    protected override System.ServiceModel.ServiceHost CreateServiceHost()
    {
      var factory = new WcfServiceFactory();

      return factory.Create<Appointments>();
    }
  }
}