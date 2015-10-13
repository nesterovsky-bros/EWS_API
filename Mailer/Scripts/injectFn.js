define(function()
{
  "use strict";

  /**
   * Returns a constructor function annotated with $inject array.
   * Arguments of injectFn are parameter names, with optional init
   * function as a last argument.
   *
   * Returned function assigns input parameters to object's fields, and
   * calls init function, if any.
   *
   * Example
   * -------
   *
   * A call:
   *    injectFn("a", "b", "c", function init() { ... })
   *
   * returns a function equivalent to:
   *    function Fn(a, b, c)
   *    {
   *       this.a = a;
   *       this.b = b;
   *       this.c = c;
   *       init.call(this);
   *    }
   *
   *    Fn.$inject = ["a", "b", "c"];
   *    Fn.init = init;
   */
  return function injectFn()
  {
    var names = Array.prototype.slice.call(arguments);
    var init = names[names.length - 1];

    typeof init === "function" ? names.pop() : (init = null);

    create.$inject = names;
    create.init = init;

    return create;

    function create()
    {
      for(var i = 0; i < names.length; ++i)
      {
        this[names[i]] = arguments[i];
      }

      init && init.call(this);
    }
  }
});