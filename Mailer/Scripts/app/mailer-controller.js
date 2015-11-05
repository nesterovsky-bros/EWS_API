define(
  [
    "./appModule",
    "../injectFn",
    "./services/errorHandler",
    "./services/services",
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
      function init()
      {
        var self = this;
        var scope = self.scope = self.$scope;

        self.$invalidate = scope.$applyAsync.bind(scope);
        self.insertImage = self.insertImage.bind(self);
        self.editorConfig = { sanitize: false };
        self.taxonomy = {};

        self.$reset = function ()
        {
          self.to = [];
          self.attachments = [];
          self.senders = [];
          self.message = null;
          self.subject = null;
          self.group = [];
          self.department = [];
          self.administration = [];
          self.branch = [];
          self.role = [];
        };

        self.$reset();

        self.$timeout(
          function ()
          {
            self.services.GetTaxonomy(
              function (data)
              {
                for (var i = 0, c = data.length; i < c; i++)
                {
                  var item = data[i];

                  self.taxonomy[item.hierarchyID] = item;
                }
              },
              self.errorHandler);
          }, 0);
      });

    MailerController.prototype = Object.create(null,
    {
      from: { enumerable: true, value: null, writable: true },
      to: { enumerable: true, value: null, writable: true },

      group: { enumerable: true, value: null, writable: true },
      department: { enumerable: true, value: null, writable: true },
      administration: { enumerable: true, value: null, writable: true },
      branch: { enumerable: true, value: null, writable: true },
      role: { enumerable: true, value: null, writable: true },

      groups: { enumerable: true, value: null, writable: true },
      departments: { enumerable: true, value: null, writable: true },
      administrations: { enumerable: true, value: null, writable: true },
      branches: { enumerable: true, value: null, writable: true },
      roles: { enumerable: true, value: null, writable: true },

      taxonomy: { enumerable: true, value: null, writable: true },

      attachments: { enumerable: true, value: null, writable: true },
      subject: { enumerable: true, value: null, writable: true },
      message: { enumerable: true, value: null, writable: true },

      senders: { enumerable: true, value: null, writable: true },
      working: { enumerable: true, value: false, writable: true },

      editorConfig: { enumerable: true, value: {}, writable: true },

      $updateTimer: { enumerable: false, value: null, writable: true },
      $size: {
        enumerable: false,
        value: ['n/a', 'bytes', 'KB', 'MB', 'GB', 'TB', 'PB', 'EB', 'ZB', 'YB'],
        writable: true
      },

      refreshData: {
        value: function (name, filter)
        {
          var self = this;

          self[name] = [];

          function handleData(data)
          {
            self.$timeout.cancel(timer);
            self.working = false;

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
            }

            self[name] = data;

            return data;
          }

          function handleError(e)
          {
            self.$timeout.cancel(timer);
            self.working = false;
            self.errorHandler(e);
          }

          var request =
          {
            filter: filter || ""
          };

          var timer =
            self.$timeout(function () { self.working = true; }, 100);

          if (name == "senders")
          {
            self.services.GetSenders(
              request,
              handleData,
              handleError);
          }
          else if (name == "roles")
          {
            self.services.GetRoles(
              request,
              handleData,
              handleError);
          }
          else
          {
            request.units = name;

            self.services.GetBankUnits(
              request,
              handleData,
              handleError);
          }
        }
      },
      updateRecipients: {
        value: function ($item, $model)
        {
          var self = this;

          if (!self.$updateTimer)
          {
            self.$timeout.cancel(self.$updateTimer);
          }

          self.$updateTimer = self.$timeout(
            function()
            {
              var request =
              {
                hierarchyIDs: [],
                roles: []
              };

              function appendHierarchyID(item)
              {
                request.hierarchyIDs.push(item.hierarchyID);
              }

              self.group.forEach(appendHierarchyID);
              self.department.forEach(appendHierarchyID);
              self.administration.forEach(appendHierarchyID);
              self.branch.forEach(appendHierarchyID);

              self.role.forEach(function (item) { roles.push(item.itemName); });

              self.services.GetRecipients(
                request,
                function (data)
                {
                  data.forEach(function (item)
                  {
                    item.selected = true;
                  });

                  self.to = data;
                },
                self.errorHandler);
            }, 200);
        }
      },
      toggleSelection: {
        value: function(collection)
        {
          var self = this;

          for (var i = 0, c = collection.length; i < c; i++)
          {
            collection[i].selected = self.selectAll;
          }
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
              from: self.from && self.from.length ? self.from[0] : null,
              to: recipients,
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
    });

    MailerController.prototype.constructor = MailerController;

    module.controller("MailerController", MailerController);

    return MailerController;
  });