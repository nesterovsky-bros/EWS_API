(function()
{
  require(
  {
    //waitSeconds: 3600,

    paths:
    {
      text: "./text",
      angular: "./angular",
      "angular-resource": "./angular-resource",
      "angular-ui-bootstrap": "./angular-ui/ui-bootstrap-tpls",
      "ui-select": "./ui-select/select",
      "rangy-core": "./rangy/rangy-core",
      "rangy-selectionsaverestore": "./rangy/rangy-selectionsaverestore",
      "sanitize": "./textAngular/textAngular-sanitize",
      "textAngular-setup": "./textAngular/textAngularSetup",
      "textAngular": "./textAngular/textAngular",
    },

    shim:
    {
      "angular": { exports: "angular" },
      "angular-resource": ["angular"],
      "angular-touch": ["angular"],
      "angular-ui-bootstrap": ["angular"],
      "ui-select": ["angular"],
      "rangy-core": ["angular"],
      "rangy-selectionsaverestore": ["angular"],
      "sanitize": ["angular"],
      "textAngular-setup": ["angular"],
      "textAngular": ["angular", "rangy-core", "rangy-selectionsaverestore", "sanitize", "textAngular-setup"],
    }
  }); 
})();


require(
  [
    "angular",
    "./app/appModule",
    "./app/mailer-controller",
    "./upload/upload-link",
  ],
  function(angular)
  {
    "use strict";
    
    return angular.bootstrap(document, ["app"]);
  });
