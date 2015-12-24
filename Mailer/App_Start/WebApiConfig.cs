using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Http;
using Mailer.Security;
using NesterovskyBros.Code;

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Mailer
{
    public static class WebApiConfig
    {
        public static void Register(HttpConfiguration config)
        {
            // Web API configuration and services

            config.MapHttpAttributeRoutes();

            config.Routes.MapHttpRoute(
                name: "DefaultApi",
                routeTemplate: "api/{controller}/{action}",
                defaults: new { action = RouteParameter.Optional }
            );

            var jsonSettings = config.Formatters.JsonFormatter.SerializerSettings;

            jsonSettings.NullValueHandling = NullValueHandling.Ignore;
            jsonSettings.ContractResolver = new JsonContractResolver();
            jsonSettings.Converters.Add(
              new StringEnumConverter { CamelCaseText = true });

           //config.Filters.Add(new CsrfFilterAttribute());
      }
  }
}
