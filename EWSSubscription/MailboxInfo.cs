using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Exchange.WebServices.Autodiscover;
using Microsoft.Exchange.WebServices.Data;
using System.Diagnostics;

namespace ConsoleApplication1
{
    public class MailboxInfo
    {
        private string _groupInfo;
        private string _ewsUrl;
        private string _calendarSyncStatus;
        private string _inboxSyncStatus;
        private string _mailAddress;
        private FolderId[] _folderId;
        private bool _notifyOnNewMails;
        private bool _notifyOnNewAppointment;

        public string mailAddress
        {
            get { return _mailAddress; }
            set { _mailAddress = value; }
        }
        public string groupInfo
        {
            get{return _groupInfo;}
            set{_groupInfo = value;}
        }

        public string ewsUrl
        {
            get
            {return _ewsUrl;}
            set
            {
                _ewsUrl = value;
            }
        }

        public string calendarSyncStatus
        {
            get { return _calendarSyncStatus; }
            set 
            {_calendarSyncStatus = value;}
        }

        public string inboxSyncStatus
        {
            get { return _inboxSyncStatus; }
            set {_inboxSyncStatus = value;}
        }

        public bool notifyOnNewMails{
            get{return _notifyOnNewMails;}
            set{_notifyOnNewMails = value;}
        }

        public bool notifyOnNewAppointment {
            get { return _notifyOnNewAppointment; }
            set{_notifyOnNewAppointment=value;}
        }

        public FolderId[] folderId { get { return _folderId; } }
        
        public MailboxInfo(string mailAddress,string groupInfo, string ewsUrl,string calendarSyncStatus,string inboxSyncStatus,bool notifyOnNewMails,bool notifyOnNewAppointment)
        {
            this.mailAddress = mailAddress;
            this.groupInfo = groupInfo;
            this.ewsUrl = ewsUrl;
            this.calendarSyncStatus = calendarSyncStatus;
            this.inboxSyncStatus = inboxSyncStatus;
            this.notifyOnNewAppointment = notifyOnNewAppointment;
            this.notifyOnNewMails = notifyOnNewMails;
            //NEEED TO HANDLE
            if (notifyOnNewAppointment)
            {
                _folderId = new FolderId[1];
                _folderId[0] = new FolderId(WellKnownFolderName.Calendar, new Mailbox(mailAddress));
            }
            
            if (notifyOnNewMails) { }
            //NEEED TO HANDLE
        }

        public MailboxInfo(AutodiscoverService service, string mailAddress,bool notifyOnNewMails,bool notifyOnNewAppointment)
        {
            GetUserSettingsResponse userresponse = GetUserSettings(service, mailAddress, 10, UserSettingName.GroupingInformation, UserSettingName.ExternalEwsUrl);
            if (userresponse.ErrorCode == Microsoft.Exchange.WebServices.Autodiscover.AutodiscoverErrorCode.InvalidUser)
            {
                Util.writeLog(String.Format("User {0} was not found in Office 365", mailAddress), 1);
                this.groupInfo = null;
            }
            else
            {
                this.mailAddress = mailAddress;
                this.groupInfo = (string)userresponse.Settings[UserSettingName.GroupingInformation];
                this.ewsUrl = (string)userresponse.Settings[UserSettingName.ExternalEwsUrl];
                this.calendarSyncStatus = null;
                this.inboxSyncStatus = null;
                this.notifyOnNewAppointment = notifyOnNewAppointment;
                this.notifyOnNewMails = notifyOnNewMails;

                //NEEED TO HANDLE
                if (notifyOnNewAppointment)
                {
                    _folderId = new FolderId[1];
                    _folderId[0] = new FolderId(WellKnownFolderName.Calendar, new Mailbox(mailAddress));
                }
                if (notifyOnNewMails) { }
                //NEEED TO HANDLE
            }
           
           
        }

         private GetUserSettingsResponse GetUserSettings(AutodiscoverService service, string emailAddress, int maxHops, params UserSettingName[] settings)
        {
            Uri url = null;
            GetUserSettingsResponse response = null;
            for (int attempt = 0; attempt < maxHops; attempt++)
            {
                service.Url = url;
                service.EnableScpLookup = (attempt < 2);
                try
                {
                    response = service.GetUserSettings(emailAddress, settings);
                    if (response.ErrorCode == AutodiscoverErrorCode.RedirectAddress)
                    {
                        url = new Uri(response.RedirectTarget);
                    }
                    else if (response.ErrorCode == AutodiscoverErrorCode.RedirectUrl)
                    {
                        url = new Uri(response.RedirectTarget);
                    }
                    else
                    {
                        return response;
                    }
                }
                catch (Exception ex)
                {
                    if (ex.Message == "The server is too busy to process the request.")
                    {
                        Util.writeLog("The server is too busy to process the request waiting 30sec.", 1,EventLogEntryType.Warning);
                        System.Threading.Thread.Sleep(30000);
                        //try again until we get an answer!!!
                        return GetUserSettings(service, emailAddress, maxHops, settings);
                    }
                    else
                    {
                        Util.writeLog(string.Format("MailboxInfo.GetUserSettingsResponse\n{1}\n{0}", ex.Message,emailAddress), 2);
                    }   
                }
                
            }
            throw new Exception("No suitable Autodiscover endpoint was found.");
        }
    }
}
