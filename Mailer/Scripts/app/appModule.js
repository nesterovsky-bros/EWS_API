define(
  [
    "angular",
    "angular-resource",
    "angular-ui-bootstrap",
    "ui-select",
  ],
  function(angular)
  {
    "use strict";
    
    return angular.module(
      "app",
      ["ngResource", "ui.bootstrap", "ui.select"]).
      config(
      [
        "$httpProvider",
        function($httpProvider)
        {
          $httpProvider.useApplyAsync(true);
        }
      ]);
  });