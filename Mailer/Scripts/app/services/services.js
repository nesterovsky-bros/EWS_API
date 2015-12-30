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

        var simpleTypeInterceptor = {
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
            Init:
            {
              params:
              {
                action: "Init"
              },
              method: "GET",
              responseType: "json",
              headers: { 'Content-Type': 'application/json' },
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
            GetBankUnits:
            {
              params:
              {
                action: "GetBankUnits"
              },
              method: "GET",
              responseType: "json",
              headers: { 'Content-Type': 'application/json' },
              isArray: true,
              then: resolveAction
            },
            GetRoles:
            {
              params:
              {
                action: "GetRoles"
              },
              method: "GET",
              responseType: "json",
              headers: { 'Content-Type': 'application/json' },
              isArray: true,
              then: resolveAction
            },
            GetTaxonomy:
            {
              params:
              {
                action: "GetTaxonomy"
              },
              method: "GET",
              responseType: "json",
              headers: { 'Content-Type': 'application/json' },
              isArray: true,
              then: resolveAction
            },
            GetRecipients:
            {
              params:
              {
                action: "GetRecipients"
              },
              method: "POST",
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
            CreateDraftMessage:
            {
              params:
              {
                action: "CreateDraftMessage"
              },
              method: "POST",
              responseType: "json",
              headers: { 'Content-Type': 'application/json' },
              interceptor: simpleTypeInterceptor,
              then: resolveAction
            },
            GetMessage:
            {
              params:
              {
                action: "GetMessage"
              },
              method: "GET",
              responseType: "json",
              headers: { 'Content-Type': 'application/json' },
              then: resolveAction
            },
            UpdateMessage:
            {
              params:
              {
                action: "UpdateMessage"
              },
              method: "POST",
              responseType: "json",
              headers: { 'Content-Type': 'application/json' },
              interceptor: simpleTypeInterceptor,
              then: resolveAction
            },
            SendDraftMessage:
            {
              params:
              {
                action: "SendDraftMessage"
              },
              method: "POST",
              responseType: "json",
              headers: { 'Content-Type': 'application/json' },
              interceptor: simpleTypeInterceptor,
              then: resolveAction
            },
            DeleteMessage:
            {
              params:
              {
                action: "DeleteMessage"
              },
              method: "POST",
              responseType: "json",
              headers: { 'Content-Type': 'application/json' },
              interceptor: simpleTypeInterceptor,
              then: resolveAction
            },
            AddAttachment:
            {
              params:
              {
                action: "AddAttachment",
              },
              method: "POST",
              responseType: "json",
              headers: { 'Content-Type': 'application/json' },
              interceptor: simpleTypeInterceptor,
              then: resolveAction
            },
            DeleteAttachment:
            {
              params:
              {
                action: "DeleteAttachment"
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