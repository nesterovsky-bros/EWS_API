define(
  [
    "angular",
    "angular-resource",
    "angular-ui-bootstrap",
    "ui-select",
    "../textAngular/textAngularSetup",
  ],
  function (angular) {
    "use strict";

    //window.rangy = require("rangy");

    return angular.module(
      "app",
      ["ngResource", "ui.bootstrap", "ui.select", "textAngular"]).
      config(
      [
        "$httpProvider", "$provide",
        function ($httpProvider, $provide) {
          $httpProvider.useApplyAsync(true);

          $provide.decorator('taOptions',
            ['taRegisterTool', '$delegate',
              function (taRegisterTool, taOptions) { 
                taOptions.toolbar = [
                  ['h1', 'h2', 'h3', 'h4', 'h5', 'h6', 'p', 'pre', 'quote'],
                  ['bold', 'italics', 'underline', 'strikeThrough', 'ul', 'ol', 'redo', 'undo', 'clear'],
                  ['justifyLeft', 'justifyCenter', 'justifyRight', 'indent', 'outdent'],
                  ['html', 'insertImage','insertLink']
                ];

                return taOptions;
              }]);
        }
      ]);
  });