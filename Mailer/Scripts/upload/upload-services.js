define(
  [
    "../appModule",
    "angular-resource",
  ],
  function(module)
  {
    "use strict";

    // Application services.
    module.factory("services",
      ["$resource", 
      function ($resource)
      {


        return $resource(
          "../api/Mailer/:action",
          {},
          {
            GetAddresses:
            {
              params:
              {
                action: "GetAddresses"
              },
              method: "GET",
              responseType: "json",
              headers: { 'Content-Type': 'application/json' },
              isArray: true,
              then: resolveAction
            },
            GetSenders:
            {
              params:
              {
                action: "GetSenders"
              },
              method: "GET",
              responseType: "json",
              headers: { 'Content-Type': 'application/json' },
              isArray: true,
              then: resolveAction
            },
            SendMessage:
            {
              params:
              {
                action: "SendMessage"
              },
              method: "POST",
              responseType: "json",
              headers: { 'Content-Type': 'application/json' },
              interceptor: simpleTypeInterceptor,
              then: resolveAction
            },
          });
      }]);
  });