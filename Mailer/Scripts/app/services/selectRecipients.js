define(
  [
    "../appModule",
    "text!../templates/selectRecipientsDialog.html!strip",
  ],
  function (module, template)
  {
    "use strict";

    var SelectRecipientsController = function(
      $scope,
      $timeout,
      errorHandler, 
      services)
    {
      var self = this;

      self.$scope = $scope;
      self.$timeout = $timeout;
      self.errorHandler = errorHandler;
      self.services = services;
      self.taxonomy = {};
      self.to = $scope.to || [];
      self.group = [];
      self.department = [];
      self.administration = [];
      self.branch = [];
      self.role = [];

      $timeout(
        function ()
        {
          services.GetTaxonomy(
            function (data)
            {
              for (var i = 0, c = data.length; i < c; i++)
              {
                var item = data[i];

                self.taxonomy[item.hierarchyID] = item;
              }

              self.taxonomy.$resolved = true;
            },
            errorHandler);
        }, 0);
    };

    SelectRecipientsController.prototype = Object.create(null,
    {
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
      working: { enumerable: true, value: false, writable: true },

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

              self[name].push(item);
            }
          }

          function handleError(e)
          {
            self.$timeout.cancel(timer);
            self.working = false;
            self.errorHandler(e);
          }

          function handleBankUnit()
          {
            var bankUnitName = "";
            var index = -1;

            switch(name)
            {
              case "branches": 
                {
                  bankUnitName = "branchName";
                  index = 5;
                  break;
                }
              case "administrations": 
                {
                  bankUnitName = "administrationName";
                  index = 4;
                  break;
                }
              case "departments": 
                {
                  bankUnitName = "departmentName";
                  index = 3;
                  break;
                }
              case "groups": 
                {
                  bankUnitName = "groupName";
                  index = 2;
                  break;
                }
            }

            var data = [];

            for (var hierarchyID in self.taxonomy)
            {
              var units = hierarchyID.split('/');

              if (units.length != index + 2)
              {
                continue;
              }

              var bankUnit = self.taxonomy[hierarchyID];

              if (((units[index] + "").indexOf(filter) != -1) ||
                bankUnit[bankUnitName] && (bankUnit[bankUnitName].indexOf(filter) != -1))
              {
                data.push(bankUnit);
              }
            }

            handleData(data);
          }

          var request =
          {
            filter: filter || "",
            take: 20
          };

          var timer =
            self.$timeout(function () { self.working = true; }, 100);

          if (name == "roles")
          {
            self.services.GetRoles(
              request,
              handleData,
              handleError);
          }
          else if (self.taxonomy.$resolved)
          {
            handleBankUnit();
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
      handleRecipients: {
        enumerable: false,
        value: function (data)
        {
          var self = this;
          var result = [];

          for (var i = 0, c = data.length; i < c; i++)
          {
            var item = data[i];
            
            item.selected = true;
            item.name = item.firstName + " " + item.secondName;

            var branch = self.taxonomy[item.hierarchyID];
            var bankUnits = item.hierarchyID.split('/');

            item.branchName = branch.branchID + " - " + branch.branchName;
            item.administrationName = branch.administrationName ?
              bankUnits[4] + " - " + branch.administrationName : null;
            item.departmentName = branch.departmentName ? 
              bankUnits[3] + " - " + branch.departmentName : null;
            item.groupName = branch.groupName ? 
              bankUnits[2] + " - " + branch.groupName : null;

            result.push(item);
          }

          return result;
        }
      },
      updateRecipients: {
        value: function ()
        {
          var self = this;

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

          self.role.forEach(function (item) { request.roles.push(item.itemName); });

          var timer =
            self.$timeout(function () { self.working = true; }, 100);

          self.services.GetRecipients(
            request,
            function (data)
            {
              self.$timeout.cancel(timer);
              self.working = false;
              self.to = self.handleRecipients(data);
            },
            function(e)
            {
              self.$timeout.cancel(timer);
              self.working = false;
              self.errorHandler(e);
            });
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
      add: {
        value: function (data)
        {
          var self = this;
          var map = {};

          self.to.forEach(
            function (item)
            {
              if (item.employeeCode)
              {
                map[item.employeeCode] = item;
              }
            });

          self.handleRecipients(data.data).forEach(
            function(item)
            {
              if (item.employeeCode && !map[item.employeeCode])
              {
                self.to.push(item);
              }
            });
        }
      },
      ok: {
        value: function ()
        {
          var self = this;

          self.$scope.$close(
            self.to.filter(function (item) { return item.selected; }));
        }
      },
      clear: {
        value: function ()
        {
          this.group = [];
          this.department = [];
          this.administration = [];
          this.branch = [];
          this.role = [];
          this.to = [];
        }
      },
    });

    SelectRecipientsController.prototype.constructor = SelectRecipientsController;

    /**
     * selectRecipients service open a dialog that allows to select recipients by
     * group, department, administration, branch and role.
     *
     * Usage: selectRecipients(oldRecipients)
     *
     * Returns a promise that is fulfilled when dialog is closed.
     */
    module.factory(
      "selectRecipients",
      ["$uibModal", "$rootScope",
      function ($modal, $rootScope)
      {
        return function(recipients)
        {
          var scope = $rootScope.$new();

          scope.to = recipients || [];

          return $modal.open(
          {
            template: template,
            size: "lg",
            scope: scope,
            controller: [
              "$scope",
              "$timeout",
              "errorHandler",
              "services",
              SelectRecipientsController
            ],
            controllerAs: "ctrl",
          }).result;
        };
      }]);
  });
