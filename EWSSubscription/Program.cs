using System;
using System.Management;
using System.Collections.Generic;
using System.Linq;
using System.Data.Linq;
using System.Data.Linq.Mapping;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Exchange.WebServices.Data;
using Microsoft.Exchange.WebServices.Autodiscover;
using System.Net;
using System.Threading;
using System.Diagnostics;

namespace ConsoleApplication1
{
    class Program
    {
        static string user = "ewsuser2@Kaplana.onmicrosoft.com";
        static string pass = "Poxa5169";
        
        static int connLifeTime = 30;
        static Dictionary<string, Group> _groups = null;
        static Dictionary<string, StreamingSubscriptionConnection> _connections = null;
        static Dictionary<string, StreamingSubscription> _subscriptions = null;
        static SortedDictionary<string, MailboxInfo> mailboxes;
        static private bool _reconnect = false;
        static private Object _reconnectLock = new Object();
        static ExchangeService getChangesExchange;

        static ManualResetEvent signal;
        static int numberOfTasks;

        static void Main(string[] args)
        {
            ServicePointManager.DefaultConnectionLimit = 255;

            getChangesExchange = new ExchangeService(ExchangeVersion.Exchange2013_SP1);
            getChangesExchange.Credentials = new WebCredentials(user, pass);
            _connections = new Dictionary<string, StreamingSubscriptionConnection>();

            #region Collect mailboxes from db and groupingInformation
            mailboxes = new SortedDictionary<string, MailboxInfo>();

            DatabaseDataContext db = new DatabaseDataContext();
            var mailboxesQResult = from mbxInfo in db.BankMailboxes select mbxInfo;
            numberOfTasks = mailboxesQResult.Count();
            if (numberOfTasks > 0) { signal = new ManualResetEvent(false); }
            
            int threadCounter = 0;
            foreach (var mbx in mailboxesQResult)
            {
                if(mbx.notifyOnNewMails == false && mbx.notifyOnNewAppointments == false)
                {
                    if (Interlocked.Decrement(ref numberOfTasks) == 0)
                    {
                        signal.Set();
                    }
                    continue;
                }
                    
                
                if (mbx.groupingInformation == null || mbx.ewsUrl == null)
                {
                    ThreadPool.QueueUserWorkItem(new WaitCallback(NewMailboxInfo), new object[] { mbx.mailAddress, mbx.notifyOnNewAppointments, mbx.notifyOnNewMails,false });
                    //NEED TO CHECK RESTRICTIONS
                    threadCounter = restrictionHandle(threadCounter);
                }
                else
                {
                    MailboxInfo mailboxInfo = new MailboxInfo(mbx.mailAddress, mbx.groupingInformation, mbx.ewsUrl, mbx.calendarSyncStatus, mbx.inboxSyncStatus, mbx.notifyOnNewMails, mbx.notifyOnNewAppointments);
                    mailboxes.Add(mbx.mailAddress, mailboxInfo);
                    if (Interlocked.Decrement(ref numberOfTasks) == 0)
                    {
                        signal.Set();
                    }
                }

            }
           
            signal.WaitOne();
            
            
            //save to db
            var mailboxesWithoutGroupInDB = from mbxInfo in db.BankMailboxes where mbxInfo.groupingInformation == null select mbxInfo;
            foreach (var mbx in mailboxesWithoutGroupInDB)
            {
                MailboxInfo mailboxInfo;
                if (mailboxes.TryGetValue(mbx.mailAddress, out mailboxInfo))
                {
                    mbx.groupingInformation = mailboxInfo.groupInfo;
                    mbx.ewsUrl = mailboxInfo.ewsUrl;
                }
            }
            db.SubmitChanges();
            #endregion Collect mailboxes from db and groupingInformation


            #region FIRST SYNC
            var mailboxesWithoutSyncState = from mbxInfo in db.BankMailboxes
                                            where mbxInfo.notifyOnNewAppointments == true && mbxInfo.calendarSyncStatus == null && mbxInfo.ewsUrl != null ||
                                            mbxInfo.notifyOnNewMails == true && mbxInfo.inboxSyncStatus == null && mbxInfo.ewsUrl != null
                                            select mbxInfo;

            numberOfTasks = mailboxesWithoutSyncState.Count();
            if (numberOfTasks > 0) { signal = new ManualResetEvent(false); }
            threadCounter = 0;
            foreach (var mbx in mailboxesWithoutSyncState)
            {
                ThreadPool.QueueUserWorkItem(new WaitCallback(getSyncData), mbx);
                //NEED TO CHECK RESTRICTIONS
                threadCounter = restrictionHandle(threadCounter);
            }
            if(numberOfTasks>0)
            {
                signal.WaitOne();
            }
            
            //save results to db
            foreach (var mbx in mailboxesWithoutSyncState)
            {
                MailboxInfo m;
                if(mailboxes.TryGetValue(mbx.mailAddress,out m))
                {
                    mbx.inboxSyncStatus = m.inboxSyncStatus;
                    mbx.calendarSyncStatus = m.calendarSyncStatus;
                }
            }
            db.SubmitChanges();
            #endregion FIRST SYNC


            #region Connections not multi threaded yet
            createGroups();
           
            ConnectToSubscriptions();

            //clean db memory
            db.Dispose();

            while (true)
            {
                foreach(string grpName in _groups.Keys)
                {
                    if(!_connections.ContainsKey(grpName))
                    {
                        AddGroupSubscriptions(grpName);
                    }
                }

                foreach (StreamingSubscriptionConnection conn in _connections.Values)
                {
                    Console.WriteLine("Connection is open: " + conn.IsOpen);
                }
                Console.WriteLine("We have {0} connections.", _connections.Count);
                if (_reconnect)
                    ReconnectToSubscriptions();
                Thread.Sleep(5000);
            } 
            #endregion

        }

        private static int restrictionHandle(int threadCounter)
        {
            if (threadCounter == 3999)
            {
                Thread.Sleep(30000);
                return 0;
            }
            else { return threadCounter++; }
        }

        private static void getSyncData(object data)
        {
            BankMailbox mbx = (BankMailbox)data;
            ExchangeService getChangesExchangeFirstRun = new ExchangeService(ExchangeVersion.Exchange2013_SP1);
            getChangesExchangeFirstRun.Credentials = new WebCredentials(user, pass);
            MailboxInfo mailboxInfo = mailboxes[mbx.mailAddress];
            getChangesExchangeFirstRun.Url = new Uri(mbx.ewsUrl);
            foreach (FolderId folderId in mailboxInfo.folderId)
            {
                string syncstate = Util.getChanges(getChangesExchangeFirstRun, mbx.mailAddress, null, folderId, 512, true);
                switch (folderId.FolderName)
                {
                    case WellKnownFolderName.Calendar:
                        mailboxInfo.calendarSyncStatus = syncstate;
                        break;
                    case WellKnownFolderName.Inbox:
                        mailboxInfo.inboxSyncStatus = syncstate;
                        break;
                }
            }
            if (Interlocked.Decrement(ref numberOfTasks) == 0)
            {
                signal.Set();
            }
        }

        private static void NewMailboxInfo(object data)
        {
            object[] array = data as object[];
            string emailAddress = (string)array[0];
            bool notifyOnNewAppointments = (bool)array[1];
            bool notifyOnNewMails = (bool)array[2];
            bool fixing = (bool)array[3];
            AutodiscoverService autodiscoverService = new AutodiscoverService();
            autodiscoverService.Credentials = new NetworkCredential(user, pass);
            autodiscoverService.Url = new Uri("https://autodiscover-s.outlook.com/autodiscover/autodiscover.xml");

            MailboxInfo mailboxInfo = new MailboxInfo(autodiscoverService, (string)emailAddress,notifyOnNewMails,notifyOnNewAppointments);

            if (mailboxInfo.groupInfo != null)
            {
                lock (mailboxes)
                {
                    if (!mailboxes.ContainsKey((string)emailAddress))
                    {
                        mailboxes.Add((string)emailAddress, mailboxInfo);
                    }
                    else
                    {
                        Util.writeLog(string.Format("Found a duplicate mailbox in the Mailboxes table named {0}", emailAddress), 3);
                    }
                } 
            }

            if (!fixing)
            {
                if (Interlocked.Decrement(ref numberOfTasks) == 0)
                {
                    signal.Set();
                }
            }
        }

        static void createGroups() 
        {
            _groups = new Dictionary<string,Group>();

            #region "build groups according to groupInformation"
                foreach (string mailbox in mailboxes.Keys) 
                {
                    
                    MailboxInfo mailboxInfo = mailboxes[mailbox];
                     addMailbox2Group(mailbox, mailboxInfo);    
                }
            #endregion "build groups according to groupInformation"
                foreach (Group grp in _groups.Values)
                {
                    grp.Mailboxes.Sort();
                    grp.PrimaryMailbox = grp.Mailboxes.First();
                }
        }

        private static Group addMailbox2Group(string mailbox, MailboxInfo mailboxInfo)
        {
            Group group = null;
            if (_groups.ContainsKey(mailboxInfo.groupInfo))
            {
                group = _groups[mailboxInfo.groupInfo];
                if (group.Mailboxes.Count > 199)
                {
                    int i = 1;
                    while (_groups.ContainsKey(string.Format("{0}{1}", group.Name, i)))
                        i++;

                    _groups.Remove(group.Name);
                    _groups.Add(String.Format("{0}{1}", group.Name, i), group);

                    //If a group is created when groupingInformation is changed to a mailbox
                    if(_connections.ContainsKey(group.Name))
                    {
                        StreamingSubscriptionConnection conn = _connections[group.Name];
                        _connections.Remove(group.Name);
                        _connections.Add(string.Format("{0}{1}", group.Name, i), conn);
                    }
                    //If a group is created when groupingInformation is changed to a mailbox

                    group = new Group(mailboxInfo.groupInfo, mailboxInfo.ewsUrl);
                    _groups.Add(mailboxInfo.groupInfo, group);
                }
            }
            else
            {
                group = new Group(mailboxInfo.groupInfo, mailboxInfo.ewsUrl);
                _groups.Add(mailboxInfo.groupInfo, group);
            }
            group.Mailboxes.Add(mailbox);
            if (group.PrimaryMailbox == null)
            {
                group.PrimaryMailbox = mailbox;
            }
            return group;
        }

        static private void AddAllSubscriptions()
        {
            foreach (string sGroup in _groups.Keys)
            {
                AddGroupSubscriptions(sGroup);
            }
        }

        static private void AddGroupSubscriptions(string sGroup)
        {
            if (!_groups.ContainsKey(sGroup))
                return;

            if (_connections.ContainsKey(sGroup))
            {
                foreach (StreamingSubscription subscription in _connections[sGroup].CurrentSubscriptions)
                {
                    try
                    {
                        subscription.Unsubscribe();
                    }
                    catch { }
                }
                try
                {
                    _connections[sGroup].Close();
                }
                catch { }
            }

            try
            {
                // Create the connection for this group, and the primary mailbox subscription
                Group group = _groups[sGroup];
                StreamingSubscription subscription = AddSubscription(group.PrimaryMailbox, group);
                if (_connections.ContainsKey(sGroup))
                    _connections[sGroup] = new StreamingSubscriptionConnection(subscription.Service, connLifeTime);
                else
                    _connections.Add(sGroup, new StreamingSubscriptionConnection(subscription.Service, connLifeTime));

                //SubscribeConnectionEvents
                _connections[sGroup].OnNotificationEvent += OnNotificationEvent;
                _connections[sGroup].OnDisconnect += OnDisconnect;
                _connections[sGroup].OnSubscriptionError += OnSubscriptionError;

                _connections[sGroup].AddSubscription(subscription);

                numberOfTasks = group.Mailboxes.Count();
                signal = new ManualResetEvent(false);
                int threadCounter = 0;

                foreach (string sMailbox in group.Mailboxes)
                {
                    if (!sMailbox.Equals(group.PrimaryMailbox))
                    {
                        ThreadPool.QueueUserWorkItem(new WaitCallback(ThreadAddSubscription), new object[] { group, sMailbox});
                        //NEED TO CHECK RESTRICTIONS
                        threadCounter = restrictionHandle(threadCounter);
                    }
                    else
                    {
                        if (Interlocked.Decrement(ref numberOfTasks) == 0)
                        {
                            signal.Set();
                        }
                    }
                }
                signal.WaitOne();
            }
               
            catch (Exception ex)
            {
                Util.writeLog(String.Format("ERROR when creating subscription connection group {0}\n {1}", sGroup, ex.Message), 4);
            }
        }

        private static void ThreadAddSubscription( object data)
        {
            object[] array = data as object[];
            Group group = (Group)array[0];
            string sMailbox = (string)array[1];

            try
            {
                StreamingSubscription subscription = AddSubscription(sMailbox, group);
                _connections[group.Name].AddSubscription(subscription);
            }
            catch (Exception ex)
            {
                Util.writeLog(String.Format("ERROR when subscribing {0} in group {1}\n {2}.", sMailbox, group.Name, ex.Message), 5, EventLogEntryType.Error);
                if (ex.Message == "One or more subscriptions in the request reside on another Client Access server. GetStreamingEvents won't proxy in the event of a batch request.")
                {
                    MailboxInfo mailboxInfo = mailboxes[sMailbox];
                    mailboxes.Remove(sMailbox);
                    group.Mailboxes.Remove(sMailbox);
                    NewMailboxInfo(new object[] { mailboxInfo.mailAddress, mailboxInfo.notifyOnNewAppointment, mailboxInfo.notifyOnNewMails,true });
                    mailboxInfo = mailboxes[sMailbox];
                   using(DatabaseDataContext myDb = new DatabaseDataContext())
	                {
		                BankMailbox mbx = myDb.BankMailboxes.Single(m => m.mailAddress==sMailbox);
                        mbx.groupingInformation = mailboxInfo.groupInfo;
                        mbx.ewsUrl = mailboxInfo.ewsUrl;
                        myDb.SubmitChanges();
	                }
                   Util.writeLog(String.Format("Updated groupingInformation for user  {0}.", sMailbox), 5, EventLogEntryType.Information);
                   Group grp;
                    lock (_groups){
                        
                        grp = addMailbox2Group(sMailbox, mailboxInfo);
                    }
                    //if the group is already connected
                    if(_connections.ContainsKey(grp.Name))
                    {
                       ThreadAddSubscription(new object[] { grp,sMailbox });
                    } 
                }
            }
            if (Interlocked.Decrement(ref numberOfTasks) == 0)
            {
                signal.Set();
            }
        }

        static private StreamingSubscription AddSubscription(string emailaddress, Group group)
        {
            // Return the subscription, or create a new one if we don't already have one

            if (_subscriptions == null)
                _subscriptions = new Dictionary<string, StreamingSubscription>();

            if (_subscriptions.ContainsKey(emailaddress))
            {
                lock (_subscriptions)
                {
                    _subscriptions.Remove(emailaddress);
                }
            }
            ExchangeService exchange = group.ExchangeService;
            MailboxInfo mailboxInfo = mailboxes[emailaddress];
            exchange.Credentials = new WebCredentials(user, pass);
            exchange.ImpersonatedUserId = new ImpersonatedUserId(ConnectingIdType.SmtpAddress, emailaddress);
            StreamingSubscription subscription;
            if (mailboxInfo.folderId[0].FolderName.Equals("AllFolders"))
            {
                subscription = exchange.SubscribeToStreamingNotificationsOnAllFolders(SelectedEvents());
            }
            else
                subscription = exchange.SubscribeToStreamingNotifications(mailboxInfo.folderId, SelectedEvents());
            lock (_subscriptions)
            {
                _subscriptions.Add(emailaddress, subscription);
            }
            return subscription;
        }

         static private void ConnectToSubscriptions()
        {
            AddAllSubscriptions();
            foreach (StreamingSubscriptionConnection connection in _connections.Values)
            {
                connection.Open();
                Console.WriteLine("Opened connection ");
            }
        }

        static void OnNotificationEvent(object sender, NotificationEventArgs args)
        {
            foreach (NotificationEvent e in args.Events)
            {
                ProcessNotification(e, args.Subscription);
            }
        }

        static void OnDisconnect(object sender, SubscriptionErrorEventArgs args)
        {
            Console.WriteLine("NEED TO RECONNECT!!!");
            //CHECK FOR NEW MAILBOXES IN SQL
            _reconnect = true;
        }

        static void OnSubscriptionError(object sender, SubscriptionErrorEventArgs args)
        {
            Util.writeLog(String.Format("OnSubscriptionError received for {0}\n {1}", args.Subscription.Service.ImpersonatedUserId.Id, args.Exception.Message),6);
        }

        static void ProcessNotification(object e, StreamingSubscription Subscription)
        {
// NOTE: Here we should call notifier.
            string emailaddress = Subscription.Service.ImpersonatedUserId.Id;
            Folder fld = Folder.Bind(Subscription.Service, (e as ItemEvent).ParentFolderId.UniqueId);
            
            MailboxInfo mailboxInfo = mailboxes[emailaddress];
            getChangesExchange.Url = new Uri(mailboxInfo.ewsUrl);

            string syncstate;
            switch (fld.DisplayName)
            {
                case "Calendar":
                    syncstate = Util.getChanges(getChangesExchange, emailaddress, mailboxInfo.calendarSyncStatus, new FolderId(WellKnownFolderName.Calendar,new Mailbox(emailaddress)), 512, false);
                    mailboxInfo.calendarSyncStatus = syncstate;
                    break;
                case "Deleted Items":
                    Item item = Item.Bind(Subscription.Service, (e as ItemEvent).ItemId.UniqueId);
                    if (item.ItemClass == "IPM.Appointment")
                    {
                        syncstate = Util.getChanges(getChangesExchange, emailaddress, mailboxInfo.calendarSyncStatus, new FolderId(WellKnownFolderName.Calendar, new Mailbox(emailaddress)), 512, false);
                        mailboxInfo.calendarSyncStatus = syncstate;
                    }
                    break;
                case "Inbox":
                    syncstate = Util.getChanges(getChangesExchange, emailaddress, mailboxInfo.inboxSyncStatus, new FolderId(WellKnownFolderName.Inbox, new Mailbox(emailaddress)), 512, false);
                    mailboxInfo.inboxSyncStatus = syncstate;
                    break;
            }
         }

        static private void ReconnectToSubscriptions()
        {
            _reconnect = false;
            lock (_reconnectLock)
            {
                foreach (string sConnectionGroup in _connections.Keys)
                {
                    StreamingSubscriptionConnection connection = _connections[sConnectionGroup];
                    if (!connection.IsOpen)
                    {
                        try
                        {
                            try
                            {
                                connection.Open();
                            }
                            catch (Exception ex)
                            {
                                if (ex.Message.StartsWith("You must add at least one subscription to this connection before it can be opened"))
                                {
                                    // Try recreating this group
                                    AddGroupSubscriptions(sConnectionGroup);
                                }
                                else
                                    Util.writeLog(String.Format("Failed to reopen connection\n {0}", ex.Message),7);
                            }
                        }
                        catch (Exception ex)
                        {
                            Util.writeLog(String.Format("Failed to reopen connection\n {0}", ex.Message),8);
                        }
                    }
                }
            }
        }

        static private EventType[] SelectedEvents()
        {
            //NEED TO GET ANSWERS FROM DB
            List<string> dbAnwer = new List<string>();
            dbAnwer.Add("Deleted");
            dbAnwer.Add("FreeBusyChanged");

            EventType[] events = new EventType[dbAnwer.Count]; //depends on answer from db

            for (int i = 0; i < dbAnwer.Count; i++)
            {
                switch (dbAnwer[i])
                {
                    case "NewMail": { events[i] = EventType.NewMail; break; }
                    case "Deleted": { events[i] = EventType.Deleted; break; }
                    case "Modified": { events[i] = EventType.Modified; break; }
                    case "Moved": { events[i] = EventType.Moved; break; }
                    case "Copied": { events[i] = EventType.Copied; break; }
                    case "Created": { events[i] = EventType.Created; break; }
                    case "FreeBusyChanged": { events[i] = EventType.FreeBusyChanged; break; }
                }
            }
            return events;
        }

    }
}
