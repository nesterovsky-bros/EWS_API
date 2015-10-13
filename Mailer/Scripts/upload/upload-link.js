/**
 *   (c) 2014-2015 by Arthur and Vladimir Nesterovsky (http://www.nesterovsky-bros.com)
 *                        All rights reserved.
 */
define(
  [
    "angular",
    "../app/appModule",
    "text!./upload-link.html!strip",
    "./upload-link-controller",
  ],
  function (angular, module, template, controller)
  {
    "use strict";

    module.directive(
      "uploadLink",
      function()
      {
        return {
          restrict: "AE",
          scope: {
            serverUrl: "@",
            accept: "@",
            onSuccess: "&",
          },
          controller: controller,
          controllerAs: "controller",
          template: template,
          transclude: true,
          bindToController: true,
          link: function (scope, element, attrs, controller)
          {
            var link = element.find("a");
            var value = attrs["class"];

            if (value)
            {
              link.addClass(value);
            }
            
            element[0].removeAttribute("class");

            value = attrs["title"];

            if (value)
            {
              link.attr("title", value);
            }

            controller.input = element.find("input");

            if (controller.input.length)
            {
              controller.input.on(
                "change",
                controller.fileChanged.bind(controller));
            }
          },
        };
      });
  });
