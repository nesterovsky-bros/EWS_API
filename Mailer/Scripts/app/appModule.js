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

    return angular.
      module("app",
      [
        "ngResource",
        "ui.bootstrap",
        "ui.select",
        "ngWYSIWYG",
        "ui-upload",
        "ngCookies",
      ]).
      config([
        "$httpProvider",
        function ($httpProvider)
        {
          $httpProvider.useApplyAsync(true);
        }
      ]);
  });