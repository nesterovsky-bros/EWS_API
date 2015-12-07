define(
  [
    "./appModule",
    "../injectFn",
    "./services/errorHandler",
    "./services/services",
    "./services/selectRecipients",
    "./services/local",
  ],
  function (module, injectFn)
  {
    "use strict";

    var MailerController = injectFn(
      "$scope",
      "$timeout",
      "$q",
      "errorHandler",
      "services",
      "fileUploader",
      "selectRecipients",
      "local",
      function init()
      {
        var self = this;
        var scope = self.scope = self.$scope;

        self.$invalidate = scope.$applyAsync.bind(scope);
        self.insertImage = self.insertImage.bind(self);
        self.editorConfig = { sanitize: false };
        self.$from = null;
        self.$dirtyFlag = 0;
        self.$updateTimer = null;

        self.$reset = function ()
        {
          self.$to = [];
          self.senders = [];
          self.$message = null;
          self.$subject = null;
          self.attachments = [];
        };

        self.$reset();

        var messageID = self.local.$init({ messageID: null }).messageID;

        if (!messageID)
        {
          self.$timeout(
            function()
            {
              self.getRecipients().then(
                function (data)
                {
                  self.services.CreateDraftMessage(
                    {
                      from: self.from,
                      toRecipients: self.to
                    },
                    function (messageID)
                    {
                      self.local["messageID"] = messageID.data;

                      self.local.$save();
                    },
                    self.errorHandler);
                });
            },
            500);
        }
        else
        {
          self.services.GetMessage(
            {
              messageId: messageID,
            },
            function (message)
            {
              self.$subject = message.subject || null;
              self.$message = message.content || null;
              self.$from = message.from || null;
              self.$to = message.to || [];
              self.attachments = message.attachments || [];
            },
            self.errorHandler);
        }
      });

    MailerController.prototype = Object.create(null,
    {
      senders: { enumerable: true, value: null, writable: true },
      from: {
        enumerable: true,
        get: function ()
        {
          return this.$from;
        },
        set: function (value)
        {
          this.$from = value;

          this.propertyChanged(8);
        }
      },
      to: {
        enumerable: true,
        get: function ()
        {
          return this.$to;
        },
        set: function (value)
        {
          this.$to = value;

          this.propertyChanged(1);
        }
      },
      subject: {
        enumerable: true,
        get: function()
        {
          return this.$subject;
        },
        set: function (value)
        {
          this.$subject = value;

          this.propertyChanged(2);
        }
      },
      message: {
        enumerable: true,
        get: function ()
        {
          return this.$message;
        },
        set: function (value)
        {
          this.$message = value;

          this.propertyChanged(4);
        }
      },
      attachments: { enumerable: true, value: false, writable: true },
      working: { enumerable: true, value: false, writable: true },
      editorConfig: { enumerable: true, value: {}, writable: true },

      $size: {
        enumerable: false,
        value: ['n/a', 'bytes', 'KB', 'MB', 'GB', 'TB', 'PB', 'EB', 'ZB', 'YB'],
        writable: false
      },

      propertyChanged: {
        value: function (propertyID)
        {
          var self = this;

          self.$dirtyFlag |= propertyID;

          self.$timeout.cancel(self.$updateTimer);

          self.$updateTimer =
            self.$timeout(self.updateMessage.bind(self), 500);
        }
      },
      updateMessage: {
        value: function ()
        {
          var self = this;

          self.$updateTimer = null;

          if (!self.$dirtyFlag)
          {
            return;
          }

          var flag = self.$dirtyFlag;
          var message = {
            id: self.local.messageID
          };

          self.$dirtyFlag = 0;

          if (flag & 1)
          {
            message.toRecipients = self.$to;
          }

          if (flag & 2)
          {
            message.subject = self.$subject;
          }

          if (flag & 4)
          {
            message.textBody = self.$message;
          }

          if (flag & 8)
          {
            message.from = self.$from;
          }

          self.services.UpdateMessage(
            message,
            function () {},
            self.errorHandler)
        }
      },
      refreshData: {
        value: function (filter)
        {
          var self = this;
          
          var request =
          {
            filter: filter || "",
            take: 20
          };

          var timer =
            self.$timeout(function () { self.working = true; }, 100);

          self.services.GetSenders(
            request,
            function(data)
            {
              self.$timeout.cancel(timer);
              self.working = false;
              self.senders = [];

              for (var i = 0, c = data.length; i < c; i++)
              {
                var item = data[i];

                item.name = "";

                if (item.branchID)
                {
                  item.name = item.branchID + " - ";
                }
                else if (item.employeeCode)
                {
                  item.name = item.firstName + " " + item.secondName;
                }
                else if (item.title)
                {
                  item.name = item.title;
                }

                if (item.branchName)
                {
                  item.name = item.name.length ?
                    item.name + item.branchName : item.branchName;
                }
                else if (item.administrationName)
                {
                  item.name = item.name.length ?
                    item.name + item.administrationName : item.administrationName;
                }
                else if (item.departmentName)
                {
                  item.name = item.name.length ?
                    item.name + item.departmentName : item.departmentName;
                }
                else if (item.groupName)
                {
                  item.name = item.name.length ?
                    item.name + item.groupName : item.groupName;
                }

                self.senders.push(item);
              }
            },
            function(e)
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
      clean: {
        value: function ()
        {
          var self = this;

          self.$timeout.cancel(self.$updateTimer);
          self.$reset();

          self.$dirtyFlag = 0;
          self.$updateTimer = null;

          var form = self.scope.form;

          form.$setPristine();
          form.$setUntouched();

          self.services.DeleteMessage(
            {
              messageId: self.local.messageID,
            },
            function ()
            {
              self.services.CreateDraftMessage(
                {
                  from: self.from
                },
                function (messageID)
                {
                  self.local.messageID = messageID.data;

                  self.local.$save();
                },
                self.errorHandler);
            },
            self.errorHandler);
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

          setTimeout(
            function ()
            {
              alert(self.message);
            },
            200
          );

          return ;

          var timer =
            self.$timeout(function () { self.working = true; }, 100);

          var recipients = [];

          for (var i = 0, c = self.to.length; i < c; i++)
          {
            var item = self.to[i];

            if (!item.selected)
            {
              continue;
            }

            recipients.push(
              {
                email: item.email,
                name: item.name
              });
          }

          self.services.SendMessage(
            {
              subject: self.subject,
              content: self.message,
              from: self.from || null,
              to: recipients,
              attachments: self.attachments.length ? self.attachments : null
            },
            function()
            {
              self.$timeout.cancel(timer);
              self.working = false;
              self.clean();
            },
            function(e)
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
          var attachment = {
            name: file.name,
            size: file.size,
            content: data.substr(start + marker.length),
          };

          self.attachments.push(attachment);

          self.services.AddAttachment(
            {
              messageID: self.local.messageID,
              attachment: attachment
            },
            angular.noop,
            self.errorHandler);
      
          //self.$invalidate();
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
      insertImage: {
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
      getRecipients: {
        value: function()
        {
          var self = this;
          var defered = self.$q.defer();

          self.selectRecipients(self.to).then(
            function (data)
            {
              self.to = data;

              defered.resolve(data);
            },
            defered.reject);

          return defered.promise;
        }
      },
    });

    MailerController.prototype.constructor = MailerController;

    module.controller("MailerController", MailerController);

    return MailerController;
  });