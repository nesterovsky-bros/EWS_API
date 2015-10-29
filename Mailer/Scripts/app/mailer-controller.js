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
      "$q",
//      "selectRecipients",
      "errorHandler",
      "services",
      "fileUploader",
      function init()
      {
        var scope = this.scope = this.$scope;
        this.$invalidate = scope.$applyAsync.bind(scope);
        this.insertImage = this.insertImage.bind(this);
        this.editorConfig = {
          sanitize: false
        };

        this.$reset = function ()
        {
          this.to = [];
          this.cc = [];
          this.bcc = [];
          this.attachments = [];
          this.addresses = [];
          this.senders = [];
          this.message = null;
          this.subject = null;
        };

        this.$reset();
      });

    MailerController.prototype = Object.create(null,
    {
      from: { enumerable: true, value: null, writable: true },
      to: { enumerable: true, value: null, writable: true },
      cc: { enumerable: true, value: null, writable: true },
      bcc: { enumerable: true, value: null, writable: true },
      attachments: { enumerable: true, value: null, writable: true },
      subject: { enumerable: true, value: null, writable: true },
      message: { enumerable: true, value: null, writable: true },

      addresses: { enumerable: true, value: null, writable: true },
      senders: { enumerable: true, value: null, writable: true },
      working: { enumerable: true, value: false, writable: true },

      editorConfig: { enumerable: true, value: {}, writable: true },

      $size: {
        enumerable: false,
        value: ['n/a', 'bytes', 'KB', 'MB', 'GB', 'TB', 'PB', 'EB', 'ZB', 'YB'],
        writable: true
      },

      tagTransform: {
        value: function (tag)
        {
          return {
            name: tag,
            id: null,
            email: null,
          };
        }
      },
      formatItem: {
        value: function (item)
        {
          return item.name;
        }
      },
      getRole: {
        value: function (address)
        {
          var index = -1;

          if (!address.name)
          {
            return null;
          }
          else if ((index = address.name.indexOf('/')) > 0)
          {
            return address.name.substr(0, index);
          }
          else
          {
            return index == -1 ? address.name : null;
          }
        }
      },
      getDivision:
      {
        value: function (address)
        {
          var index = -1;

          if (address.name && ((index = address.name.indexOf('/')) >= 0))
          {
            return address.name.substr(index + 1);
          }

          return null;
        }
      },
      refreshSenders: {
        value: function (filter)
        {
          var self = this;

          self.senders = [];

          var timer =
            self.$timeout(function () { self.working = true; }, 100);

          self.services.GetSenders(
            {
              filter: filter || ""
            },
            function (addresses)
            {
              self.$timeout.cancel(timer);
              self.working = false;
              self.senders = addresses;
            },
            function (e)
            {
              self.$timeout.cancel(timer);
              self.working = false;
              self.errorHandler(e);
            });
        }
      },
      refreshAddresses: {
        value: function (filter)
        {
          var self = this;

          self.addresses = [];

          var timer =
            self.$timeout(function () { self.working = true; }, 100);

          self.services.GetAddresses(
            {
              filter: filter || ""
            },
            function (addresses)
            {
              self.$timeout.cancel(timer);
              self.working = false;
              self.addresses = addresses;
            },
            function (e)
            {
              self.$timeout.cancel(timer);
              self.working = false;
              self.errorHandler(e);
            });
        }
      },
      convertSize: {
        value: function (bytes)
        {
          var i = (bytes === 0) ? 0 : +Math.floor(Math.log(bytes) / Math.log(1024));

          return (bytes / Math.pow(1024, i)).toFixed(i ? 1 : 0) + ' ' +
            this.$size[isNaN(bytes) ? 0 : i + 1];
        }
      },
      select: {
        value: function (recipients)
        {
        }
      },
      add: {
        value: function (data, recipients)
        {
          var map = {};

          recipients.forEach(
            function (item)
            {
              if (item.id)
              {
                map[item.id] = item;
              }
            });

          data.forEach(
            function (item)
            {
              if (item.id && !map[item.id])
              {
                recipients.push(item);
              }
            });
        }
      },
      clean: {
        value: function ()
        {
          this.$reset();

          var form = this.scope.form;

          form.$setPristine();
          form.$setUntouched();
        }
      },
      send: {
        value: function ()
        {
          var self = this;
          var form = self.scope.form;

          form.$setSubmitted();

          if (!form.$valid)
          {
            return;
          }

          alert(self.message);

          return ;

          var timer =
            self.$timeout(function () { self.working = true; }, 100);

          self.services.SendMessage(
            {
              subject: self.subject,
              content: self.message,
              from: self.from && self.from.length ? self.from[0] : null,
              to: self.to,
              cc: self.cc.length ? self.cc : null,
              bcc: self.bcc.length ? self.bcc : null,
              attachments: self.attachments.length ? self.attachments : null
            },
            function (addresses)
            {
              self.$timeout.cancel(timer);

              self.working = false;

              self.clean();
            },
            function (e)
            {
              self.$timeout.cancel(timer);

              self.working = false;

              self.errorHandler(e);
            });
        }
      },
      upload: {
        value: function (data, file)
        {
          var self = this;
          var size = file.size;

          self.attachments.forEach(function (item) { size += item.size; });

          if (size > 2000000)
          {
            self.errorHandler("Total attachments size is bigger than 2000000 bytes.");

            return;
          }

          var marker = "base64,";
          var start = data.indexOf(marker);

          self.attachments.push(
            {
              name: file.name,
              size: file.size,
              content: data.substr(start + marker.length),
            });

          self.$invalidate();
        }
      },
      remove: {
        value: function (attachment)
        {
          var attachments = this.attachments;
          var index = attachments.indexOf(attachment);

          if (index != -1)
          {
            attachments.splice(index, 1);
          }
        }
      },
      insertImage:
      {
        writable: true,
        value: function ()
        {
          var self = this;

          return self.$q(function (resolve, reject)
          {
            self.fileUploader.selectAndUploadFile(null, ".jpg,.png,.gif").then(
              function (result)
              {
                //resolve("<img src='" + result.data + "' style='max-width: 600px'>");
                resolve(result.data);
              },
              function(e) { self.errorHandler(e).then(reject); });
          });
        }
      },
    });

    MailerController.prototype.constructor = MailerController;

    module.controller("MailerController", MailerController);

    return MailerController;
  });