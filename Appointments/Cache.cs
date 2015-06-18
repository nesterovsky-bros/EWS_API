namespace Bnhp.Office365
{
  using System;
  using System.Runtime.Caching;
  using System.Threading;
  using System.Threading.Tasks;

  /// <summary>
  /// A cache API.
  /// </summary>
  public class Cache
  {
    /// <summary>
    /// Short delay.
    /// </summary>
    public static readonly TimeSpan ShortDelay = new TimeSpan(0, 3, 0);

    /// <summary>
    /// Regular delay.
    /// </summary>
    public static readonly TimeSpan Delay = new TimeSpan(0, 20, 0);

    /// <summary>
    /// Long delay.
    /// </summary>
    public static readonly TimeSpan LongDelay = new TimeSpan(2, 0, 0);
  }

  /// <summary>
  /// A type specific cache API.
  /// </summary>
  /// <typeparam name="K">An element key type.</typeparam>
  /// <typeparam name="V">An element value type.</typeparam>
  public class Cache<K, V>
    where V: class
  {
    /// <summary>
    /// A cache builder.
    /// </summary>
    public struct Builder
    {
      /// <summary>
      /// A memory cache. If not specified then MemoryCache.Default is used.
      /// </summary>
      public MemoryCache MemoryCache;

      /// <summary>
      /// An expiration value.
      /// Alternatively CachePolicyFunc can be used.
      /// </summary>
      public TimeSpan Expiration;

      /// <summary>
      /// Indicates whether to use sliding (true), or absolute (false) 
      /// expiration.
      /// Alternatively CachePolicyFunc can be used.
      /// </summary>
      public bool Sliding;
      
      /// <summary>
      /// Optional function to get caching policy.
      /// Alternatively Expiration and Sliding property can be used.
      /// </summary>
      public Func<V, CacheItemPolicy> CachePolicyFunc;

      /// <summary>
      /// Optional value validator.
      /// </summary>
      public Func<V, bool> Validator;

      /// <summary>
      /// A value factory.
      /// Alternatively FactoryAsync can be used.
      /// </summary>
      public Func<K, V> Factory;

      /// <summary>
      /// Async value factory.
      /// Alternatively Factory can be used.
      /// </summary>
      public Func<K, CancellationToken, Task<V>> FactoryAsync;

      /// <summary>
      /// A key to string converter.
      /// </summary>
      public Func<K, string> Key;

      /// <summary>
      /// Converts builder to a Cache&lt;K, V> instance.
      /// </summary>
      /// <param name="builder">A builder to convert.</param>
      /// <returns>A Cache&lt;K, V> instance.</returns>
      public static implicit operator Cache<K, V>(Builder builder)
      {
        return new Cache<K, V>(builder);
      }

      /// <summary>
      /// Sets a value for a key.
      /// </summary>
      /// <param name="key">A key to set.</param>
      /// <param name="value">A value to set.</param>
      public void Set(K key, V value)
      {
        SetImpl(GetKey(key), IsValid(value) ? value : null);
      }

      /// <summary>
      /// Gets a value for a key.
      /// </summary>
      /// <param name="key">A key to get value for.</param>
      /// <returns>A value instance.</returns>
      public V Get(K key)
      {
        var keyValue = GetKey(key);
        var cache = MemoryCache ?? MemoryCache.Default;
        var value = cache.Get(keyValue) as V;

        if (!IsValid(value))
        {
          value = CreateValue(key);
          SetImpl(keyValue, value);
        }

        return value;
      }

      /// <summary>
      /// Gets a task to return an async value.
      /// </summary>
      /// <param name="key">A key.</param>
      /// <param name="cancellationToken">Optional cancellation token.</param>
      /// <returns>A cached value.</returns>
      public async Task<V> GetAsync(
        K key, 
        CancellationToken cancellationToken = default(CancellationToken))
      {
        var keyValue = GetKey(key);
        var cache = MemoryCache ?? MemoryCache.Default;
        var value = cache.Get(keyValue) as V;

        if (!IsValid(value))
        {
          value = await CreateValueAsync(key, cancellationToken);
          SetImpl(keyValue, value);
        }

        return value;
      }

      /// <summary>
      /// Gets string key value for a key.
      /// </summary>
      /// <param name="key">A key.</param>
      /// <returns>A string key value.</returns>
      public string GetKey(K key)
      {
        return Key != null ? Key(key) :
          key == null ? null : key.ToString();
      }

      /// <summary>
      /// Creates a value for a key.
      /// </summary>
      /// <param name="key">A key to create value for.</param>
      /// <returns>A value instance.</returns>
      public V CreateValue(K key)
      {
        return Factory != null ? Factory(key) : 
          FactoryAsync(key, default(CancellationToken)).Result;
      }

      /// <summary>
      /// Creates a task for value for a key.
      /// </summary>
      /// <param name="key">A key to create value for.</param>
      /// <param name="cancellationToken">Optional cancellation token.</param>
      /// <returns>A task for a value instance.</returns>
      public Task<V> CreateValueAsync(
        K key,
        CancellationToken cancellationToken = default(CancellationToken))
      {
        return FactoryAsync != null ? FactoryAsync(key, cancellationToken) :
          Task.FromResult(Factory(key));
      }

      /// <summary>
      /// Validates the value.
      /// </summary>
      /// <param name="value">A value to validate.</param>
      /// <returns>
      /// true if value is valid for a cache, and false otherise.
      /// </returns>
      public bool IsValid(V value)
      {
        return (value != null) && ((Validator == null) || Validator(value));
      }

      /// <summary>
      /// Set implementation.
      /// </summary>
      /// <param name="key">A key to set value for.</param>
      /// <param name="value">A value to set.</param>
      /// <returns>A set value.</returns>
      private void SetImpl(string key, V value)
      {
        var cache = MemoryCache ?? MemoryCache.Default;

        if (value == null)
        {
          cache.Remove(key);
        }
        else
        {
          cache.Set(
            key,
            value,
            CachePolicyFunc != null ? CachePolicyFunc(value) :
            Sliding ?
              new CacheItemPolicy { SlidingExpiration = Expiration } :
              new CacheItemPolicy
              {
                AbsoluteExpiration = DateTime.Now + Expiration
              });
        }
      }
    }

    /// <summary>
    /// Creates a cache from a cache builder.
    /// </summary>
    /// <param name="builder">A cache builder instance.</param>
    public Cache(Builder builder)
    {
      if ((builder.Factory == null) && (builder.FactoryAsync == null))
      {
        throw new ArgumentException("builder.Factory");
      }

      this.builder = builder;
    }

    /// <summary>
    /// Sets a value for a key.
    /// </summary>
    /// <param name="key">A key to set.</param>
    /// <param name="value">A value to set.</param>
    public void Set(K key, V value)
    {
      builder.Set(key, value);
    }

    /// <summary>
    /// Gets a value for a key.
    /// </summary>
    /// <param name="key">A key to get value for.</param>
    /// <returns>A value instance.</returns>
    public V Get(K key)
    {
      return builder.Get(key);
    }
    
    /// <summary>
    /// Gets a task to return an async value.
    /// </summary>
    /// <param name="key">A key.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>A cached value.</returns>
    public Task<V> GetAsync(
      K key,
      CancellationToken cancellationToken = default(CancellationToken))
    {
      return builder.GetAsync(key, cancellationToken);
    }

    /// <summary>
    /// Cache builder.
    /// </summary>
    private Builder builder;
  }

  /// <summary>
  /// A type specific cache API for a signle value.
  /// </summary>
  /// <typeparam name="V">An element value type.</typeparam>
  public class Cache<V>
    where V: class
  {
    /// <summary>
    /// A cache builder.
    /// </summary>
    public struct Builder
    {
      /// <summary>
      /// A memory cache. If not specified then MemoryCache.Default is used.
      /// </summary>
      public MemoryCache MemoryCache;

      /// <summary>
      /// An expiration value.
      /// Alternatively CachePolicyFunc can be used.
      /// </summary>
      public TimeSpan Expiration;

      /// <summary>
      /// Indicates whether to use sliding (true), or absolute (false) 
      /// expiration.
      /// Alternatively CachePolicyFunc can be used.
      /// </summary>
      public bool Sliding;

      /// <summary>
      /// Optional function to get caching policy.
      /// Alternatively Expiration and Sliding property can be used.
      /// </summary>
      public Func<V, CacheItemPolicy> CachePolicyFunc;

      /// <summary>
      /// Optional value validator.
      /// </summary>
      public Func<V, bool> Validator;

      /// <summary>
      /// A value factory.
      /// Alternatively FactoryAsync can be used.
      /// </summary>
      public Func<V> Factory;

      /// <summary>
      /// Async value factory.
      /// Alternatively Factory can be used.
      /// </summary>
      public Func<CancellationToken, Task<V>> FactoryAsync;

      /// <summary>
      /// A key value.
      /// </summary>
      public string Key;

      /// <summary>
      /// Converts builder to a Cache&lt;V> instance.
      /// </summary>
      /// <param name="builder">A builder to convert.</param>
      /// <returns>A Cache&lt;V> instance.</returns>
      public static implicit operator Cache<V>(Builder builder)
      {
        return new Cache<V>(builder);
      }

      /// <summary>
      /// Sets a value.
      /// </summary>
      /// <param name="value">A value to set.</param>
      public void Set(V value)
      {
        SetImpl(IsValid(value) ? value : null);
      }

      /// <summary>
      /// Gets a value.
      /// </summary>
      /// <returns>A value instance.</returns>
      public V Get()
      {
        var cache = MemoryCache ?? MemoryCache.Default;
        var value = cache.Get(Key) as V;

        if (!IsValid(value))
        {
          value = CreateValue();
          SetImpl(value);
        }

        return value;
      }

      /// <summary>
      /// Gets a task to return an async value.
      /// </summary>
      /// <param name="cancellationToken">Optional cancellation token.</param>
      /// <returns>A cached value.</returns>
      public async Task<V> GetAsync(
        CancellationToken cancellationToken = default(CancellationToken))
      {
        var cache = MemoryCache ?? MemoryCache.Default;
        var value = cache.Get(Key) as V;

        if (!IsValid(value))
        {
          value = await CreateValueAsync(cancellationToken);
          SetImpl(value);
        }

        return value;
      }

      /// <summary>
      /// Creates a value.
      /// </summary>
      /// <returns>A value instance.</returns>
      public V CreateValue()
      {
        return Factory != null ? Factory() : 
          FactoryAsync(default(CancellationToken)).Result;
      }

      /// <summary>
      /// Creates a task for value.
      /// </summary>
      /// <param name="cancellationToken">Optional cancellation token.</param>
      /// <returns>A task for a value instance.</returns>
      public Task<V> CreateValueAsync(
        CancellationToken cancellationToken = default(CancellationToken))
      {
        return FactoryAsync != null ? FactoryAsync(cancellationToken) :
          Task.FromResult(Factory());
      }

      /// <summary>
      /// Validates the value.
      /// </summary>
      /// <param name="value">A value to validate.</param>
      /// <returns>
      /// true if value is valid for a cache, and false otherise.
      /// </returns>
      public bool IsValid(V value)
      {
        return (value != null) && ((Validator == null) || Validator(value));
      }

      /// <summary>
      /// Set implementation.
      /// </summary>
      /// <param name="value">A value to set.</param>
      /// <returns>A set value.</returns>
      private void SetImpl(V value)
      {
        var cache = MemoryCache ?? MemoryCache.Default;

        if (value == null)
        {
          cache.Remove(Key);
        }
        else
        {
          cache.Set(
            Key,
            value,
            CachePolicyFunc != null ? CachePolicyFunc(value) :
            Sliding ?
              new CacheItemPolicy { SlidingExpiration = Expiration } :
              new CacheItemPolicy
              {
                AbsoluteExpiration = DateTime.Now + Expiration
              });
        }
      }
    }

    /// <summary>
    /// Creates a cache from a cache builder.
    /// </summary>
    /// <param name="builder">A cache builder instance.</param>
    public Cache(Builder builder)
    {
      if ((builder.Factory == null) && (builder.FactoryAsync == null))
      {
        throw new ArgumentException("builder.Factory");
      }

      this.builder = builder;
    }

    /// <summary>
    /// Sets a value.
    /// </summary>
    /// <param name="value">A value to set.</param>
    public void Set(V value)
    {
      builder.Set(value);
    }

    /// <summary>
    /// Gets a value.
    /// </summary>
    /// <returns>A value instance.</returns>
    public V Get()
    {
      return builder.Get();
    }

    /// <summary>
    /// Gets a task to return an async value.
    /// </summary>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>A cached value.</returns>
    public Task<V> GetAsync(
      CancellationToken cancellationToken = default(CancellationToken))
    {
      return builder.GetAsync(cancellationToken);
    }

    /// <summary>
    /// Cache builder.
    /// </summary>
    private Builder builder;
  }
}