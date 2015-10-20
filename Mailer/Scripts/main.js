(function()
{
  require(
  {
    waitSeconds: 3600,

    paths:
    {
      text: "./text",
      angular: "./angular",
      "angular-resource": "./angular-resource",
      "angular-ui-bootstrap": "./angular-ui/ui-bootstrap-tpls",
      "ui-select": "./ui-select/select",
      "rangy": "./rangy/lib/rangy-core",
      //"rangy-selectionsaverestore": "./rangy/lib/rangy-selectionsaverestore",
      //"sanitize": "./textAngular/textAngular-sanitize",
      //"textAngular-setup": "./textAngular/textAngularSetup",
      //"textAngular": "./textAngular/textAngular",
    },

    shim:
    {
      "angular": { exports: "angular" },
      "angular-resource": ["angular"],
      "angular-touch": ["angular"],
      "angular-ui-bootstrap": ["angular"],
      "ui-select": ["angular"],
      //"rangy": ["angular"],
      //"rangy-selectionsaverestore": ["angular"],
      //"sanitize": ["angular"],

      "./textAngular/textAngular-sanitize": ["angular"],
      "./textAngular/textAngular": ["./textAngular/textAngular-sanitize", "rangy", "./rangy/lib/rangy-selectionsaverestore"],
      "./textAngular/textAngularSetup": ["./textAngular/textAngular"],
      //"textAngular": ["angular", "rangy", "rangy-selectionsaverestore", "sanitize", "textAngular-setup"],
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
