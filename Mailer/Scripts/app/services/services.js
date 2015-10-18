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
        function resolveAction(resolve)
        {
          var params = this.params;

          if (params)
          {
            this.timeout = params.timeout;

            params.timeout = null;
          }

          this.then = null;

          resolve(this);
        }

        var simpleTypeInterceptor =
        {
          response: function(response)
          {
            response.resource.data = response.data;

            return response.resource;
          }
        };

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
/*
            UploadIdentities:
            {
              params:
              {
                action: "UploadIdentities"
              },
              method: "POST",
              responseType: "json",
              headers: { 'Content-Type': 'application/json' },
              isArray: true,
              then: resolveAction
            },
            CopyLessons:
            {
              params:
              {
                action: "CopyLessons"
              },
              method: "POST",
              responseType: "json",
              headers: { 'Content-Type': 'application/json' },
              isArray: true,
              then: resolveAction
            },
            SuggestImages:
            {
              params:
              {
                action: "SuggestImages"
              },
              method: "GET",
              responseType: "json",
              headers: { 'Content-Type': 'application/json' },
              isArray: true,
              then: resolveAction
            },
 */
          });
      }]);
  });