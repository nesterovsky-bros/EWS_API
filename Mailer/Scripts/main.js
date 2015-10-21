(function()
{
  require(
  {
    waitSeconds: 3600,

    paths:
    {
      "angular-ui-bootstrap": "angular-ui/ui-bootstrap-tpls",
      "ui-select": "ui-select/select"
    },

    shim:
    {
      "angular": { exports: "angular" },
      "angular-resource": ["angular"],
      "angular-touch": ["angular"],
      "angular-ui-bootstrap": ["angular"],
      "ui-select": ["angular"],
      "textAngular/textAngular-sanitize": ["angular"],
      "textAngular/textAngularSetup": ["angular"],
      "textAngular/textAngular":
      {
        deps:
        [
          "rangy/lib/rangy-core",
          "rangy/lib/rangy-selectionsaverestore",
          "./textAngular-sanitize",
          "./textAngularSetup"
        ],
        init: function(rangy) { window.rangy = rangy; }
      }
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
    
    // forbid define during angular bootstrap.
    var prevDefine;

    define = null;

    try
    {
      return angular.bootstrap(document, ["app"]);
    }
    finally
    {
      define = prevDefine;
    }
  });
