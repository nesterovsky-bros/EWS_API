namespace Cmdlets
{
  using System;
  using System.Collections.Generic;
  using System.Collections.ObjectModel;
  using System.Linq;
  using System.Management.Automation;
  using System.Management.Automation.Runspaces;
  using System.Security;
  using System.Text;
  using System.Threading;
  using System.Threading.Tasks;

  class Program
  {
    static void Main(string[] args)
    {
      //CreateGroups(
      //  "https://outlook.office365.com/powershell-liveid/",
      //  "http://schemas.microsoft.com/powershell/Microsoft.Exchange",
      //  new PSCredential(
      //    "ewsuser10@poalimdev.onmicrosoft.com", 
      //    SecureString("Poxa5169"))).Wait();
    }

    public static SecureString SecureString(string value)
    {
      var result = new SecureString();

      foreach(var c in value)
      {
        result.AppendChar(c);
      }

      return result;
    }

    public static async Task CreateGroups(
      string liveIDConnectionUri, 
      string schemaUri, 
      PSCredential credentials)
    {
      var connectionInfo = new WSManConnectionInfo(
        new Uri(liveIDConnectionUri),
        schemaUri, 
        credentials);

      connectionInfo.AuthenticationMechanism = AuthenticationMechanism.Basic;

      var parallelism = 100;

      using(var runspace = RunspaceFactory.CreateRunspace(connectionInfo))
      {
        runspace.Open();

        using(var semaphore = new SemaphoreSlim(parallelism))
        {
          Func<string, string, Task> invoke = 
            async (group, email) =>
            {
              try
              {
                await Task.Yield();

                using(var powershell = PowerShell.Create())
                {
                  Console.WriteLine(
                    "Before add email {1} to group: {0}.",
                    group,
                    email);
                  
                  powershell.AddCommand("Add-DistributionGroupMember");
                  powershell.AddParameter("Identity", group);
                  powershell.AddParameter("Member", email);

                  powershell.Runspace = runspace;

                  var result = powershell.Invoke();

                  Console.WriteLine(
                    "Group: {0}, email: {1}, Success: {2}",
                    group,
                    email,
                    !powershell.HadErrors);                  
                }
              }
              finally
              {
                semaphore.Release();
              }
            };

          // Fill Group1
          //for(var i = 1; i <= 6000; ++i)
          //{
          //  var index = i;

          //  await semaphore.WaitAsync();

          //  var task = invoke(
          //    "Group1@poalimdev.onmicrosoft.com",
          //    "sharedmailbox" + index + "@poalimdev.onmicrosoft.com");
          //}

          // Fill Group2
          for(var i = 1; i <= 2000; ++i)
          {
            var index = i * 6000 / 2000;

            await semaphore.WaitAsync();

            var task = invoke(
              "Group2@poalimdev.onmicrosoft.com",
              "sharedmailbox" + index + "@poalimdev.onmicrosoft.com");
          }

          // Fill Group3
          for(var i = 1; i <= 1500; ++i)
          {
            var index = i * 6000 / 1500;

            await semaphore.WaitAsync();

            var task = invoke(
              "Group3@poalimdev.onmicrosoft.com",
              "sharedmailbox" + index + "@poalimdev.onmicrosoft.com");
          }

          // Fill Group4
          for(var i = 1; i <= 200; ++i)
          {
            var index = i * 6000 / 200;

            await semaphore.WaitAsync();

            var task = invoke(
              "Group4@poalimdev.onmicrosoft.com",
              "sharedmailbox" + index + "@poalimdev.onmicrosoft.com");
          }

          // Fill Group5
          for(var i = 1; i <= 1000; ++i)
          {
            var index = i * 6000 / 1000;

            await semaphore.WaitAsync();

            var task = invoke(
              "Group5@poalimdev.onmicrosoft.com",
              "sharedmailbox" + index + "@poalimdev.onmicrosoft.com");
          }

          // Wait to complete pending tasks.
          for (var i = 0; semaphore.CurrentCount + i < parallelism; ++i)
          {
            await semaphore.WaitAsync();
          }
        }
      }
    }
  }
}
