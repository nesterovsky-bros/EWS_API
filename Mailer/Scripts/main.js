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
      //"text-angular-rangy": "./textAngular/textAngular-rangy.min",
      //"text-angular-sanitize": "./textAngular/textAngular-sanitize",
      //"text-angular": "./textAngular/textAngular",
    },

    shim:
    {
      "angular": { exports: "angular" },
      "angular-resource": ["angular"],
      "angular-touch": ["angular"],
      "angular-ui-bootstrap": ["angular"],
      "ui-select": ["angular"],
      //"text-angular-rangy": ["angular"],
      //"text-angular-sanitize": ["angular"],
      //"text-angular": ["angular", "text-angular-rangy", "text-angular-sanitize"],
    }
  }); 
})();


require(
  [
    "angular",
    "./app/appModule",
    //"text-angular",
    "./app/mailer-controller",
    "./upload/upload-link",
  ],
  function(angular)
  {
    "use strict";
    
    return angular.bootstrap(document, ["app"]);
  });
