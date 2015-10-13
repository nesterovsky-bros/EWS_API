define(
  [
    "./appModule",
    "../injectFn",
    "./services/errorHandler",
    "./services/services",
   // "./services/selectRecipients",
  ],
  function (module, injectFn)
  {
    "use strict";

    var MailerController = injectFn(
      "$scope",
      "$timeout",
//      "selectRecipients",
      "errorHandler",
      "services",
      function init() {
        this.to = [];
        this.cc = [];
        this.bcc = [];
        this.attachments = [];
        this.addresses = [];
      });


    MailerController.prototype = Object.create(null,
    {
      to: { enumerable: true, value: null, writable: true },
      cc: { enumerable: true, value: null, writable: true },
      bcc: { enumerable: true, value: null, writable: true },
      attachments: { enumerable: true, value: null, writable: true },
      subject: { enumerable: true, value: null, writable: true },
      message: { enumerable: true, value: null, writable: true },

      addresses: { enumerable: true, value: null, writable: true },
      working: { enumerable: true, value: false, writable: true },

      tagTransform: {
        value: function (tag)
        {
          return {
            Name: tag,
            Id: null,
            Email: null,
          };
        }
      },
      formatItem: {
        value: function(item)
        {
          return item.Name;
        }
      },
      getRole: {
        value: function (address)
        {
          var index = -1;

          if (address.Name && ((index = address.Name.indexOf('/')) != -1))
          {
            return address.Name.substr(0, index);
          }

          return null;
        }
      },
      getDivision: {
        value: function (address) {
          var index = -1;

          if (address.Name && ((index = address.Name.indexOf('/')) != -1))
          {
            return address.Name.substr(index + 1);
          }

          return null;
        }
      },
      refreshAddresses: {
        value: function (filter)
        {
          var self = this;
          var timer =
            self.$timeout(function () { self.working = true; }, 100);
            
          self.services.GetAddresses(
            {
              filter: filter || ""
            },
            function (addresses) {
              self.$timeout.cancel(timer);

              self.working = false;

              self.addresses = addresses;
            },
            function (e) {
              self.$timeout.cancel(timer);

              self.working = false;

              self.errorHandler(e);
            });
        }
      },
      select: {
        value: function (recipients)
        {
        }
      },
      add: {
        value: function (data, recipients) {
          var map = {};

          recipients.forEach(
            function (item) {
              if (item.Id) {
                map[item.Id] = item;
              }
            });

          data.forEach(
            function (item) {
              if (item.Id && !map[item.Id]) {
                recipients.push(item);
              }
            });
        }
      },
      clean: {
        value: function () {
          this.to = [];
          this.cc = [];
          this.bcc = [];
          this.attachments = [];
          this.addresses = [];
          this.message = null;
          this.subject = null;
        }
      },
      send: {
        value: function () {
          return;
        }
      },
      upload: {
        value: function () {
        }
      },
      remove: {
        value: function (attachment) {
        }
      }
    });

    MailerController.prototype.constructor = MailerController;
        
    module.controller("MailerController", MailerController);

    return MailerController;
  });