using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Exchange.WebServices.Data;
using Microsoft.Exchange.WebServices.Autodiscover;
using System.Diagnostics;

namespace ConsoleApplication1
{
    class Util
    {
        private static EventLog myLog;

        static Util()
        {
            myLog = new EventLog();
            myLog.Source = "NotificationsSubscriptionService";
        }

        static public void writeLog(string message, int id, EventLogEntryType eventType = EventLogEntryType.Error) 
        {
            //RUN THIS @ SETUP LEVEL TO CREATE EVENT SOURCE AND LOG
            //EventLog.CreateEventSource("NotificationsSubscriptionService", "NotificationsSubscriptionService");

            myLog.WriteEntry(message,eventType,id);
            Console.WriteLine(message);
        }

        static public string getChanges(ExchangeService exchange, string mailbox, string syncState, FolderId folderId, int numOfMessages, bool firstSync)
        {
            bool moreChangesAvailable = false;
            do
            {
                exchange.ImpersonatedUserId = new ImpersonatedUserId(ConnectingIdType.SmtpAddress, mailbox);

                try
                {
                    ChangeCollection<ItemChange> icc = exchange.SyncFolderItems(folderId, PropertySet.IdOnly, null, numOfMessages, SyncFolderItemsScope.NormalItems, syncState);
                    if (icc.Count == 0)
                    {
                        return null;
                    }
                    else
                    {
                        if (!firstSync)
                        {
                            foreach (ItemChange ic in icc)
                            {
                                Console.WriteLine("===========");
                                Console.WriteLine("Mailbox: " + mailbox);
                                Console.WriteLine("Folder: " + folderId.FolderName);
                                Console.WriteLine("ChangeType: " + ic.ChangeType.ToString());
                                Console.WriteLine("ItemId: " + ic.ItemId);
                                Console.WriteLine("===========");
                            }
                        }
                    }
                    syncState = icc.SyncState;
                    moreChangesAvailable = icc.MoreChangesAvailable;
                }
                catch (Exception ex)
                {
                    if (ex.Message == "The specified folder could not be found in the store.")
                    {
                        Console.WriteLine("Cannot access this user folder user: {0} message: {1}", mailbox, ex.Message);
                    }
                    else
                    { 
                        throw; 
                    }
                }
            }
            while (moreChangesAvailable);

            return syncState;
        }
    }


}
