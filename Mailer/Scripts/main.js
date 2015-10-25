(function()
{
  require(
  {
    waitSeconds: 3600,

    paths:
    {
      "angular-ui-bootstrap": "angular-ui/ui-bootstrap-tpls",
      "angular-sanitize": "angular-sanitize",
      "ui-select": "ui-select/select",
      "errorHandler": "app/services/errorHandler",
      "ngWYSIWYG": "ngWYSIWYG/wysiwyg",
    },

    shim:
    {
      "angular": { exports: "angular" },
      "angular-resource": ["angular"],
      "angular-touch": ["angular"],
      "angular-ui-bootstrap": ["angular"],
      "angular-sanitize": ["angular"],
      "ui-select": ["angular"],
      "ngWYSIWYG": ["angular", "angular-sanitize"],
    }
  }); 
})();

require(
  [
    "angular",
    "./app/appModule",
    "./app/mailer-controller",
    "./upload/upload-link",
    "ngWYSIWYG",
  ],
  function(angular)
  {
    "use strict";
    
    return angular.bootstrap(document, ["app"]);
  });
