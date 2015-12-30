define(
  [
    "angular",
    "angular-resource",
    "angular-ui-bootstrap",
    "ui-select",
    "ngWYSIWYG",
  ],
  function (angular)
  {
    "use strict";

    var module = angular.module("app",
      [
        "ngResource",
        "ui.bootstrap",
        "ui.select",
        "ngWYSIWYG",
        "ui-upload",
      ]);

    return module.config([
      "$httpProvider",
      function ($httpProvider)
      {
        $httpProvider.useApplyAsync(true);
      }
    ]);
  });