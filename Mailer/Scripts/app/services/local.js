define(
  [
    "angular",
    "../appModule"
  ],
  function(angular, module)
  {
    // Service to encapsulate the local storage.
    module.factory(
      "local",
      ["$window", "$rootScope", function($window, $rootScope)
      {
        var local = { $save: save, $init: init };
        var unload;

        $window.addEventListener(
          "beforeunload",
          function ()
          {
            unload = true;
            save();
          });

        bind();

        return local;

        function save()
        {
          var storage = $window.localStorage;

          unbind();

          try
          {
            angular.forEach(
              local,
              function(value, key)
              {
                if (key[0] == "$")
                {
                  return;
                }

                if (value == null)
                {
                  storage.removeItem(key);
                }
                else
                {
                  var json = angular.toJson(value);

                  if (storage.getItem(key) !== json)
                  {
                    storage.setItem(key, json);
                  }
                }
              });
          }
          finally
          {
            !unload && bind();
          }
        }

        function init(values)
        {
          if (!values)
          {
            return local;
          }

          var storage = $window.localStorage;

          angular.forEach(
            values,
            function(value, key)
            {
              if ((key[0] == "$") || local.hasOwnProperty(key))
              {
                return;
              }

              local[key] = angular.fromJson(storage.getItem(key)) || value;
            });

          return local;
        }

        function change(event)
        {
          var key = event.key;

          if (key && 
            (key[0] != "$") && 
            local.hasOwnProperty(key) &&
            ($window.localStorage.getItem(key) === event.newValue))
          {
            local[key] = angular.fromJson(event.newValue);

            $rootScope.$evalAsync();
          }
        }

        function bind()
        {
          $window.addEventListener('storage', change);
        }

        function unbind()
        {
          $window.removeEventListener('storage', change);
        }
      }]);
  });