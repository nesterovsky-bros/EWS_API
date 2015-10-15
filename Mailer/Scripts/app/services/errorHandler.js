define(
  [
    "../appModule",
    "text!../templates/errorHandlerDialog.html!strip",
  ],
  function(module, template)
  {
    "use strict";

    module.controller(
      "errorHandlerController",
      [
        "$scope", "$modalInstance", "error",
        function($scope, $modalInstance, error)
        {
          $scope.error = error;

          $scope.close = function()
          {
            $modalInstance.dismiss('cancel');
          };
        }
      ]);

    /**
     * errorHandler service writes to a console log a messages and provides a common way
     *  to handle (display and post process) errors.
     *
     * Usage: errorHandler(exception);
     * Where:
     *    exception - an javascript exception to handle;
     *
     * Returns a promise that is fulfilled when error handling is complete.
     */
    module.factory(
      "errorHandler",
      ["$q", "$log", "$modal", 
      function($q, $log, $modal)
      {
        return function(e)
        {
          if (!e)
          {
            return $q(function (resolve) { resolve(); });
          }

          var error =
          {
            message: "",
            id: 0,
            type: "",
            details: "",
            stackTrace: ""
          };

          if (typeof e == "string")
          {
            error.message = "Unknown error";
            error.details = e;
          }
          else
          {
            if (e.status == 0)
            {
              return $q(function (resolve) { resolve(); });
            }

            if (e.status && e.statusText)
            {
              error.message = e.status + " " + e.statusText + " ";
            }

            if (e.data && (typeof e.data != "string"))
            {
              e = e.data;
            }

            if (e.message)
            {
              error.message += e.message;
            }

            if (e.errorID)
            {
              error.id = e.errorID;
            }

            if (e.exceptionType)
            {
              error.type = e.exceptionType;
            }

            if (e.exceptionMessage)
            {
              error.details = e.exceptionMessage;
            }

            if (e.stackTrace)
            {
              error.stackTrace = e.stackTrace;
            }
          }

          $log.error(JSON.stringify(error));

          return $modal.open(
          {
            template: template,
            controller: 'errorHandlerController',
            size: "sm",
            resolve:
            {
              error: function() { return error;  }
            }
          }).result;
        };
      }]);
  });
