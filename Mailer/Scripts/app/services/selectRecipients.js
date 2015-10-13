define(
  [
    "../appModule",
    "text!../templates/selectRecipientsDialog.html!strip",
  ],
  function (module, template)
  {
    "use strict";

    /**
     * Named controller for "select recipients" dialog.
     */
    module.controller(
      "selectRecipientsController",
      [
        "$scope", "$modalInstance", "promiseFn",
        function ($scope, $modalInstance, promiseFn)
        {
          $scope.recipients = [];
          $scope.waiting = true;

          promiseFn().then(
            function (recipients)
            {
              if (recipients)
              {
                $scope.recipients = recipients;
                $scope.waiting = false;
              }
            },
            function ()
            {
              $modalInstance.dismiss('error');
            });

          $scope.getRecipients = function (filter)
          {
            // TODO: call server in order to obtain recipients

            //$scope.recipients = [];
          };

          $scope.ok = function ()
          {
            $modalInstance.close($scope.recipients);
          };

          $scope.cancel = function ()
          {
            $modalInstance.dismiss('cancel');
          };

          $scope.selectAll = function ()
          {
            $scope.recipients.forEach(
              function (recipient)
              {
                recipient.selected = true;
              });
          };

          $scope.unselectAll = function ()
          {
            $scope.recipients.forEach(
              function (recipient)
              {
                recipient.selected = false;
              });
          };
        }
      ]);

    /**
     * selectRecipients service opens "select recipients" dialog and allows to 
     * select/unselect recipients.
     *
     * Usage: selectRecipients(promise).then(function(recipients) { ... });
     * Where:
     *    recipients - an array of Recipient instance to handle;
     *
     * Returns a promise that is fulfilled when dialog is closed.
     */
    module.factory(
      "selectRecipients",
      ["$modal",
      function ($modal)
      {
        return function (promise)
        {
          return $modal.open(
          {
            template: template,
            controller: "selectRecipientsController",
            size: "lg",
            resolve:
            {
              promiseFn: function () { return function () { return promise; }; }
            }
          }).result;
        };
      }]);
  });
