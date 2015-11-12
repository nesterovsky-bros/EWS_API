define(
  [
    "../appModule",
    "text!../templates/errorHandlerDialog.html!strip",
  ],
  function(module, template)
  {
    "use strict";

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
      ["$q", "$log", "$uibModal", "$rootScope",
      function ($q, $log, $modal, $rootScope)
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
              error.message = e.status + " " + e.statusText + "\n";
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

          var scope = $rootScope.$new();

          scope.error = error;

          return $modal.open(
          {
            template: template,
            size: "sm",
            scope: scope
          }).result;
        };
      }]);
  });
