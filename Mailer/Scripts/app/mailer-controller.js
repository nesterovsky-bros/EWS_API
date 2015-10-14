define(
  [
    "./appModule",
    "../injectFn",
    "./services/errorHandler",
    "./services/services",
    "../b64"
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
        this.$reset = function () {
          this.to = [];
          this.cc = [];
          this.bcc = [];
          this.attachments = [];
          this.addresses = [];
          this.message = null;
          this.subject = null;
          this.$invalidate = this.$scope.$applyAsync.bind(this.$scope);
        };

        this.$reset();
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

          if (!address.Name)
          {
            return null;
          }
          else if ((index = address.Name.lastIndexOf('/')) != -1) {
            return address.Name.substr(0, index);
          }
          else {
            return address.Name;
          }
        }
      },
      getDivision: {
        value: function (address) {
          var index = -1;

          if (address.Name && ((index = address.Name.lastIndexOf('/')) != -1))
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
          this.$reset();

          var form = this.$scope.form;

          form.$setPristine();
          form.$setUntouched();
        }
      },
      send: {
        value: function () {
          var self = this;
          var form = self.$scope.form;
          
          form.$setSubmitted();

          if (!form.$valid)
          {
            return;
          }

          var timer =
            self.$timeout(function () { self.working = true; }, 100);

          // TODO: send mail to server

        }
      },
      upload: {
        value: function (data, file) {
          var self = this;
          var size = file.size;

          self.attachments.forEach(function (item) { size += item.size; });

          if (size > 2000000)
          {
            self.errorHandler("Total attachments size is bigger than 2000000 bytes.");

            return;
          }

          var start = data.lastIndexOf("base64,");
          var content = base64js.toByteArray(data.substr(start + 7));

          self.attachments.push(
            {
              name: file.name,
              size: file.size,
              content: content,
            });

          self.$invalidate();
        }
      },
      remove: {
        value: function (attachment) {
          var attachments = this.attachments;
          var index = attachments.indexOf(attachment);

          if (index != -1)
          {
            attachments.splice(index, 1);
          }
        }
      }
    });

    MailerController.prototype.constructor = MailerController;
        
    module.controller("MailerController", MailerController);

    return MailerController;
  });