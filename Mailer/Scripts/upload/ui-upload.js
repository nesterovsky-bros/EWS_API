/** 
   @copyright 2014-2015 Nesterovsky bros (mailto:contact@nesterovsky-bros.com). 
   @module ui-upload
    
   @description This module simplifies client-side file upload. 
  
   Module defines a fileUploader service and a uploadLink directive. 
 */

define(["angular"], function(angular)
{
  "use strict";

  var module = angular.module('ui-upload', []);

  /**
    @ngdoc service
    @name fileUploader
    @description Allows to load a file as a dataUri or upload it to a server.
    */
  module.service(
    "fileUploader",
    ["$window", "$q", "$http", "$timeout", function ($window, $q, $http, $timeout)
    {
      var document = $window.document;
      var inputFile = angular.element(
        "<input type='file' style='position: absolute; width: 0px; height: 0px; opacity: 0; filter: alpha(opacity=0);'/>");

      angular.element(document.body).append(inputFile);

      function clickInput()
      {
        return $q(function(resolve)
        {
          var input = inputFile[0];

          inputFile.one(
            "change",
            function()
            {
              if (input.value)
              {
                resolve({ file: input.files[0], description: input.value });

                input.value = null;
              }
            });

          $timeout(function()
          {
            if (typeof MouseEvent === "function")
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
          }, 0, false);
        });
      }

      /**
        @description The fileUploader service's implementation.  
        @class
        */
      var service =
      {
        /**
          @description selects a file and then uploads it to a server or loads it as a dataUri.
          @memberof fileUploader
          @param {string} serverUrl - determines a server URL where to upload file. 
            A selected file will be loaded as a dataUri when this parameter is empty or null.
          @param {string} accept - determines file extensions that will appear in select dialog.
          @returns a promise instance that's resolved when file is successfully uploaded/loaded.
          @example

              fileUploader.selectAndUploadFile("api/Uploader", ".txt").then(
                function(data, file) 
                { 
                  // TODO success handler
                },
                function(e) 
                { 
                  // TODO error handling
                });
          */
        selectAndUploadFile: function(serverUrl, accept)
        {
          inputFile.attr("accept", accept || null);

          return clickInput().then(
            function(result)
            {
              return service.uploadFile(
                serverUrl,
                result.file,
                result.description);
            });
        },

        /**
          @description uploads a File or Blob instance to a server, or loads it into a dataUri.
          @memberof fileUploader
          @param {string} serverUrl - determines a server URL where to upload file. 
            A selected file will be loaded into the memory when this parameter is empty or null.
          @param {File} fileOrBlob - a File or Blob instance that will be uploaded to a server.
          @param {string} [description] - determines a file description that will be uploaded 
            to a server along with file itself.
          @returns a promise instance that's resolved when file is successfully uploaded/loaded.
          @example

              fileUploader.uploadFile("api/Uploader", file, "text").then(
                function(data, file) 
                { 
                  // TODO success handler
                },
                function(e) 
                { 
                  // TODO error handling
                });
          */
        uploadFile: function(serverUrl, fileOrBlob, description)
        {
          return $q(function(resolve, reject)
          {
            if (serverUrl)
            {
              var form = new FormData();

              form.append("fname", fileOrBlob);
              form.append("description", description || fileOrBlob.name);

              $http.post(
                serverUrl,
                form,
                {
                  headers: { "Content-Type": undefined },
                  transformRequest: angular.identity
                }).
                then(
                  function(url) { resolve({ data: url, file: fileOrBlob }); },
                  reject);
            }
            else
            {
              var reader = new FileReader();

              reader.onload = function()
              {
                resolve({ data: reader.result, file: fileOrBlob });
              };

              reader.onerror = function() { reject(reader.error); };

              if ((fileOrBlob instanceof Blob) && (fileOrBlob.type === "text/plain"))
              {
                reader.readAsText(fileOrBlob);
              }
              else
              {
                reader.readAsDataURL(fileOrBlob);
              }
            }
          });
        },
      };

      return service;
    }]);

  /**
    @ngdoc directive
    @name uploadLink
    @restrict AE
    @description A "upload-link" directive.
    @scope
    @param {expression} [serverUrl] determines an URL of server-side 
      controller that accepts form multipart file attachment.
      When this attribute is empty then on-success handler will receives a 
      file content in data URI format as first parameter.
    @param {expression} [accept] A file extension for files that will appear in select dialog.
    @param {expression} onSuccess a handler that is executed when upload operation completes.
    @param {expression} [onError] a handler that is executed when upload operation fails. 
    @example

            <a upload-link
              class="btn btn-primary"
              accept=".*"
              server-url="api/Uploader/{{id}}"
              on-success="successHandler(dataUri, file)"
              on-error="errorHandler(e)">Click me to upload file</a>
    */
  module.directive(
    "uploadLink",
    [
      "fileUploader", "$log", 
      function (fileUploader, $log)
      {
        var directive =
        {
          restrict: "AE",
          scope:
          {
            serverUrl: "@",
            accept: "@?",
            onSuccess: "&",
            onError: "&?",
          },
          link: function (scope, element, attrs, controller)
          {
            element.on(
              "click",
              function()
              {
                fileUploader.
                  selectAndUploadFile(scope.serverUrl, scope.accept).
                  then(scope.onSuccess, scope.onError || $log.error);
              });
          },
        };

        return directive;
      }]);
});