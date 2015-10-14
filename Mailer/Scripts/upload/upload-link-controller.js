/**
 *   (c) 2014-2015 by Arthur and Vladimir Nesterovsky (http://www.nesterovsky-bros.com)
 *                        All rights reserved.
 */
define(
  [
    "angular",
    "../injectFn",
    "../app/services/errorHandler",
  ],
  function (angular, injectFn)
  {
    "use strict";

    var UploadController = injectFn(
      "$scope",
      "$http",
      "$window",
      "errorHandler",
      function ()
      {
        // TODO: initialize controller, if need.
      });

    UploadController.prototype = Object.create(null,
    {
      input: { enumerable: true, writable: true, value: null },

      // open select file dialog
      openDialog:
      {
        enumerable: true,
        value: function()
        {
          var self = this;

          if (!self.input.length)
          {
            return;
          }

          var input = self.input[0];

          this.$scope.$applyAsync(function ()
          {
            var document = self.$window.document;

            if (typeof MouseEvent == "function")
            {
              var e = new MouseEvent("click", { bubbles: true, cancelable: true });

              input.dispatchEvent(e);
            }
            else if (document.createEvent)
            {
              var e = document.createEvent("MouseEvent");

              e.initEvent("click", true, false);

              input.dispatchEvent(e);
            }
            else
            {
              input.click();
            }
          });
        }
      },

      fileChanged:
      {
        enumerable: true,
        value: function()
        {
          var self = this;
          var input = self.input[0];

          if (!input.value)
          {
            return;
          }

          var file = input.files[0];
          var description = input.value;

          input.value = null;

          if (self.serverUrl)
          {
            var form = new FormData();
            form.append("fname", file);
            form.append("description", description);
            form.append("rnd", new Date().getMilliseconds());

            self.$http.post(
              self.serverUrl,
              form,
              {
                headers: { "Content-Type": undefined },
                transformRequest: angular.identity
              }).
              success(function (data) { self.onSuccess({ data: data }); }).
              error(self.errorHandler);
          }
          else
          {
            var reader = new FileReader();

            reader.onload = function ()
            {
              var url = this.result;

              self.onSuccess({ data: url, file: file });
            };

            reader.readAsDataURL(file);
          }
        }
      }
    });

    UploadController.prototype.constructor = UploadController;

    return UploadController;
  });