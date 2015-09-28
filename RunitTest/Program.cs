namespace RunitTest
{
  using Bnhp.Office365;
  using System;
  using System.IO;
  using System.Reflection;
  using System.Collections.Generic;

  class Program
  {
    static void Main(string[] args)
    {
      var dataDirectory = Path.GetFullPath(Path.Combine(
        Path.GetDirectoryName(typeof(Program).Assembly.Location),
        "..\\..\\..\\Appointments\\App_Data"));

      AppDomain.CurrentDomain.SetData("DataDirectory",  dataDirectory);

      var service = new RunitService();
      var operation = @"Get";
      var request =
@"<Get xmlns='https://www.bankhapoalim.co.il/'>
  <email>ewsuser2@Kaplana.onmicrosoft.com</email>
  <start>2015-07-07T07:15:00</start>
  <end>2015-07-08T11:15:00</end>
  <maxResults>1</maxResults>
</Get>";

      Console.WriteLine(request);

      var response = service.Request(operation, request);

      Console.WriteLine(response);
    }
  }
}
