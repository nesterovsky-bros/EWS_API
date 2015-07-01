using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Exchange.WebServices.Data;

namespace ConsoleApplication1
{
    class Group
    {
        private string _name = "";
        private string _primaryMailbox = "";
        private List<String> _mailboxes;
        private ExchangeService _exchangeService = null;
        private string _ewsUrl = "";

        public Group(string Name, string EWSUrl)
        {
            // initialise the group information
            _name = Name;
            _ewsUrl = EWSUrl;
            _mailboxes = new List<String>();
        }

        public string Name
        {
            get { return _name; }
        }

        public string ewsUrl { get { return _ewsUrl; } }

        public string PrimaryMailbox
        {
            get { return _primaryMailbox; }
            set
            {
                _primaryMailbox = value;
                if (!_mailboxes.Contains(_primaryMailbox))
                    _mailboxes.Add(_primaryMailbox);
            }
        }

        public ExchangeService ExchangeService
        {
            get
            {
                if (_exchangeService != null)
                    return _exchangeService;

                // Create exchange service for this group
                ExchangeService exchange = new ExchangeService(ExchangeVersion.Exchange2013_SP1);
                exchange.ImpersonatedUserId = new ImpersonatedUserId(ConnectingIdType.SmtpAddress, _primaryMailbox);
                exchange.HttpHeaders.Add("X-AnchorMailbox", _primaryMailbox);
                exchange.HttpHeaders.Add("X-PreferServerAffinity", "true");
                exchange.Url = new Uri(_ewsUrl);
                return exchange;
            }
        }

        public List<String> Mailboxes
        {
            get { return _mailboxes; }
        }

    }
}
