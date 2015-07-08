namespace Bnhp.Office365
{
  using System;
  using System.Collections;
  using System.Collections.Generic;
  using System.Collections.ObjectModel;
  using System.Data.Entity.Infrastructure;
  using System.Diagnostics;
  using System.Linq;
  using System.Threading;
  using System.Threading.Tasks;

  /// <summary>
  /// Extensions API.
  /// </summary>
  public static class Extensions
  {
    /// <summary>
    /// Asynchronously enumerates the query results and performs the 
    /// specified action on each element.
    /// </summary>
    /// <remarks>
    /// Multiple active operations on the same context instance are not supported.  
    /// Use 'await' to ensure that any asynchronous operations have completed before 
    /// calling another method on this context.
    /// </remarks>
    /// <param name="source">
    /// An <see cref="T:System.Linq.IQueryable" /> to enumerate.
    /// </param>
    /// <param name="action">The action to perform on each element.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public static async Task ForEachAsync<T>(
      this IQueryable<T> enumerable, 
      Func<T, Task> action, 
      CancellationToken cancellationToken = default(CancellationToken))
    {
      var asyncEnumerable = (IDbAsyncEnumerable<T>)enumerable;
      using (var enumerator = asyncEnumerable.GetAsyncEnumerator())
      {

        if (await enumerator.MoveNextAsync(cancellationToken).ConfigureAwait(continueOnCapturedContext: false))
        {
          Task<bool> moveNextTask;
          do
          {
            var current = enumerator.Current;
            moveNextTask = enumerator.MoveNextAsync(cancellationToken);
            await action(current); //now with await
          }
          while (await moveNextTask.ConfigureAwait(continueOnCapturedContext: false));
        }
      }
    }
    
    /// <summary>
    /// Adds range to the collection.
    /// </summary>
    /// <typeparam name="T">An item type.</typeparam>
    /// <param name="collection">A collection to add items to.</param>
    /// <param name="items">Items to add.</param>
    public static void AddRange<T>(
      this ICollection<T> collection, 
      IEnumerable<T> items)
    {
      foreach(var item in items)
      {
        collection.Add(item);
      }
    }

    /// <summary>
    /// Joins queryables in a enumeration into a single queriable.
    /// </summary>
    /// <typeparam name="T">Element type.</typeparam>
    /// <param name="source">Input sources</param>
    /// <returns>Output result.</returns>
    public static IQueryable<T> Join<T>(this IEnumerable<IQueryable<T>> source)
    {
      IQueryable<T> result = null;

      foreach(var item in source)
      {
        result = result == null ? item : result.Concat(item);
      }

      return result;
    }

    /// <summary>
    /// Gets a dictionary key by value.
    /// If value does not exists for the key, then null is returned.
    /// </summary>
    /// <typeparam name="K">A key type.</typeparam>
    /// <typeparam name="V">A value class type.</typeparam>
    /// <param name="dictionary">A dictionary to get value from.</param>
    /// <param name="key">A key to get value for.</param>
    /// <returns>A value.</returns>
    public static V Get<K, V>(this IDictionary<K, V> dictionary, K key)
      where V : class
    {
      V value;

      if (key == null)
      {
        value = null;
      }
      else
      {
        dictionary.TryGetValue(key, out value);
      }

      return value;
    }

    /// <summary>
    /// Gets a dictionary key by value.
    /// If value does not exists for the key, then null is returned.
    /// </summary>
    /// <typeparam name="K">A key type.</typeparam>
    /// <typeparam name="V">A value class type.</typeparam>
    /// <param name="dictionary">A dictionary to get value from.</param>
    /// <param name="key">A key to get value for.</param>
    /// <returns>A value.</returns>
    public static V Get<K, V>(this IDictionary<K, V> dictionary, K? key)
      where V : class
      where K : struct
    {
      V value;

      if (key == null)
      {
        value = null;
      }
      else
      {
        dictionary.TryGetValue(key.GetValueOrDefault(), out value);
      }

      return value;
    }

    /// <summary>
    /// Adds or gets existing value from a dictionary.
    /// </summary>
    /// <typeparam name="K">A key type.</typeparam>
    /// <typeparam name="V">A value type.</typeparam>
    /// <param name="dictionary">A dictionary instance.</param>
    /// <param name="key">A key.</param>
    /// <param name="value">A value.</param>
    /// <returns>A value from dictionary.</returns>
    public static V AddOrGetExisting<K, V>(
      this IDictionary<K, V> dictionary,
      K key,
      V value)
    {
      V result;

      if (key != null)
      {
        if (dictionary.TryGetValue(key, out result))
        {
          return result;
        }

        dictionary.Add(key, value);
      }

      return value;
    }

    /// <summary>
    /// Returns an empty enumeration if items is null.
    /// </summary>
    /// <typeparam name="T">An item type.</typeparam>
    /// <param name="items">Items to check.</param>
    /// <returns>A non null enumeration.</returns>
    public static IEnumerable<T> EmptyIfNull<T>(this IEnumerable<T> items)
    {
      return items == null ? Enumerable.Empty<T>() : items;
    }

    /// <summary>
    /// Returns empty string if value is null.
    /// </summary>
    /// <param name="value">A value.</param>
    /// <returns>Adjusted value.</returns>
    public static string EmptyIfNull(this string value)
    {
      return value == null ? "" : value;
    }

    /// <summary>
    /// Returns null if array is empty.
    /// </summary>
    /// <typeparam name="T">An item type.</typeparam>
    /// <param name="items">Items to check.</param>
    /// <returns>Null or not empty array.</returns>
    public static T[] NullIfEmpty<T>(this T[] items)
    {
      return (items != null) && (items.Length == 0) ? null : items;
    }

    /// <summary>
    /// Returns null or non empty string.
    /// </summary>
    /// <param name="value">A value.</param>
    /// <returns>Adjusted value.</returns>
    public static string NullIfEmpty(this string value)
    {
      return string.IsNullOrEmpty(value) ? null : value;
    }

    /// <summary>
    /// Converts a enumerable of tasks into a task.
    /// </summary>
    /// <typeparam name="T">An element type.</typeparam>
    /// <param name="tasks">Tasks to run.</param>
    /// <returns>Result task.</returns>
    public static async Task ToTask(this IEnumerable<Task> tasks)
    {
      foreach(var task in tasks)
      {
        await task;
      }
    }

    /// <summary>
    /// Converts a enumerable of tasks into a task that brings final result.
    /// </summary>
    /// <typeparam name="T">An element type.</typeparam>
    /// <param name="tasks">Tasks to run.</param>
    /// <returns>Result task.</returns>
    public static Task<T[]> ToTask<T>(this IEnumerable<Task<T>> tasks)
    {
      return Task.WhenAll(tasks);
    }

    /// <summary>
    /// Performs a binary search on the specified collection.
    /// </summary>
    /// <typeparam name="T">The type of the item.</typeparam>
    /// <typeparam name="S">The type of the searched item.</typeparam>
    /// <param name="list">The list to be searched.</param>
    /// <param name="value">The value to search for.</param>
    /// <param name="comparer">
    /// The comparer that is used to compare the value with the list items.
    /// </param>
    /// <returns>
    /// The index of the specified value in the specified list, 
    /// if value is found. If value is not found and value is less 
    /// than one or more elements in array, a negative number which is 
    /// the bitwise complement of the index of the first element that is 
    /// larger than value. If value is not found and value is greater than
    /// any of the elements in array, a negative number which is the bitwise
    /// complement of (the index of the last element plus 1).
    /// </returns>
    public static int BinarySearch<T, S>(
      this IList<T> list,
      S value,
      Func<S, T, int> comparer)
    {
      int lower = 0;
      int upper = list.Count - 1;

      while (lower <= upper)
      {
        int middle = lower + (upper - lower) / 2;
        int comparisonResult = comparer(value, list[middle]);

        if (comparisonResult < 0)
        {
          upper = middle - 1;
        }
        else if (comparisonResult > 0)
        {
          lower = middle + 1;
        }
        else
        {
          return middle;
        }
      }

      return ~lower;
    }

    /// <summary>
    /// Performs a binary search on the specified collection.
    /// </summary>
    /// <typeparam name="T">The type of the item.</typeparam>
    /// <param name="list">The list to be searched.</param>
    /// <param name="value">The value to search for.</param>
    /// <returns>
    /// The index of the specified value in the specified list, 
    /// if value is found. If value is not found and value is less 
    /// than one or more elements in array, a negative number which is 
    /// the bitwise complement of the index of the first element that is 
    /// larger than value. If value is not found and value is greater than
    /// any of the elements in array, a negative number which is the bitwise
    /// complement of (the index of the last element plus 1).
    /// </returns>
    public static int BinarySearch<T>(this IList<T> list, T value)
    {
      return BinarySearch(list, value, Comparer<T>.Default);
    }

    /// <summary>
    /// Performs a binary search on the specified collection.
    /// </summary>
    /// <typeparam name="T">The type of the item.</typeparam>
    /// <param name="list">The list to be searched.</param>
    /// <param name="value">The value to search for.</param>
    /// <param name="comparer">
    /// The comparer that is used to compare the value with the list items.
    /// </param>
    /// <returns>
    /// The index of the specified value in the specified list, 
    /// if value is found. If value is not found and value is less 
    /// than one or more elements in array, a negative number which is 
    /// the bitwise complement of the index of the first element that is 
    /// larger than value. If value is not found and value is greater than
    /// any of the elements in array, a negative number which is the bitwise
    /// complement of (the index of the last element plus 1).
    /// </returns>
    public static int BinarySearch<T>(
      this IList<T> list,
      T value,
      IComparer<T> comparer)
    {
      return list.BinarySearch(value, comparer.Compare);
    }

    /// <summary>
    /// Builds sorted list from a enumerable of items.
    /// </summary>
    /// <typeparam name="K">A key tye.</typeparam>
    /// <typeparam name="V">A value type.</typeparam>
    /// <param name="items">An items to build SortedList from.</param>
    /// <param name="keySelector">A key selector.</param>
    /// <param name="comparer">Optional comparer.</param>
    /// <returns>A SortedList instance.</returns>
    public static SortedList<K, V> ToSortedList<K, V>(
      this IEnumerable<V> items,
      Func<V, K> keySelector,
      IComparer<K> comparer = null)
    {
      var list = comparer == null ?
        new SortedList<K, V>() : new SortedList<K, V>(comparer);

      foreach(var item in items)
      {
        list.Add(keySelector(item), item);
      }

      return list;
    }

    /// <summary>
    /// Projects each element of a sequence into a new form.
    /// </summary>
    /// <typeparam name="T">A type of elements of source sequence.</typeparam>
    /// <typeparam name="R">A type of elements of target sequence.</typeparam>
    /// <param name="source">A source sequence.</param>
    /// <param name="where">A predicate to filter elements.</param>
    /// <param name="selector">A result element selector.</param>
    /// <returns>A target sequence.</returns>
    public static IEnumerable<R> Select<T, R>(
      this IEnumerable<T> source, 
      Func<T, bool> where, 
      Func<T, R> selector)
    {
      return source.Where(where).Select(selector);
    }

    /// <summary>
    /// Projects each element of a sequence into a new form.
    /// </summary>
    /// <typeparam name="T">A type of elements of source sequence.</typeparam>
    /// <typeparam name="R">A type of elements of target sequence.</typeparam>
    /// <param name="source">A source sequence.</param>
    /// <param name="where">A predicate to filter elements.</param>
    /// <param name="selector">A result element selector.</param>
    /// <returns>A target sequence.</returns>
    public static IEnumerable<R> Select<T, R>(
      this IEnumerable<T> source,
      Func<T, int, bool> where,
      Func<T, int, R> selector)
    {
      var index = 0;

      foreach(var value in source)
      {
        if (where(value, index))
        {
          yield return selector(value, index);
        }

        ++index;
      }
    }

    /// <summary>
    /// Returns a sequence of source elements preceeded 
    /// with a call to a cardinality handler.
    /// </summary>
    /// <typeparam name="T">An element type.</typeparam>
    /// <param name="source">A source sequence.</param>
    /// <param name="cardinality">A cardinality handler.</param>
    /// <returns>A sequence of source elements.</returns>
    public static IEnumerable<T> Cardinality<T>(
      this IEnumerable<T> source,
      Action<int> cardinality)
    {
      var count = 0;
      T prev = default(T);

      foreach (var value in source)
      {
        switch (count)
        {
          case 0:
          {
            count = 1;
            prev = value;

            break;
          }
          case 1:
          {
            count = int.MaxValue;
            cardinality(count);

            goto default;
          }
          default:
          {
            yield return prev;

            prev = value;

            break;
          }
        }
      }

      if (count == 0)
      {
        cardinality(0);
      }
      else
      {
        if (count == 1)
        {
          cardinality(1);
        }

        yield return prev;
      }
    }

    /// <summary>
    /// Projects source sequence into target sequence.
    /// 
    /// Target elements are derived from a window of source elements:
    ///   tagtet[i] = 
    ///     selector(source[i], source[i - 1], ... source[i - window + 1])
    /// </summary>
    /// <typeparam name="T">A type of elements of source sequence.</typeparam>
    /// <typeparam name="R">A type of elements of target sequence.</typeparam>
    /// <param name="source">A source sequence.</param>
    /// <param name="window">A size of window.</param>
    /// <param name="lookbehind">
    /// Indicate whether to produce target if the number of source elements 
    /// preceeding the current is less than the window size.
    /// </param>
    /// <param name="lookahead">
    /// Indicate whether to produce target if the number of source elements 
    /// following current is less than the window size.
    /// </param>
    /// <param name="selector">
    /// A selector that derives target element.
    /// On input it recieves:
    ///   an array of source elements stored in round-robing fashon;
    ///   an index of the first element;
    ///   a number of elements in the array to count.
    /// </param>
    /// <returns>Returns a sequence of target elements.</returns>
    public static IEnumerable<R> Window<T, R>(
      this IEnumerable<T> source,
      int window,
      bool lookbehind,
      bool lookahead,
      Func<T[], int, int, R> selector)
    {
      var buffer = new T[window];
      var index = 0;
      var count = 0;

      foreach(var value in source)
      {
        if (count < window)
        {
          buffer[count++] = value;

          if (lookbehind)
          {
            yield return selector(buffer, index, count);
          }
        }
        else
        {
          buffer[index] = value;
          index = index + 1 == buffer.Length ? 0 : index + 1;

          yield return selector(buffer, index, count);
        }
      }

      if (lookahead)
      {
        while(--count > 0)
        {
          index = index + 1 == buffer.Length ? 0 : index + 1;

          yield return selector(buffer, index, count);
        }
      }
    }

    /// <summary>
    /// Projects source sequence into target sequence.
    /// 
    /// Target elements are derived from a window of source elements:
    ///   tagtet[i] = 
    ///     selector(source[i], source[i - 1], ... source[i - window + 1])
    /// </summary>
    /// <typeparam name="T">A type of elements of source sequence.</typeparam>
    /// <typeparam name="R">A type of elements of target sequence.</typeparam>
    /// <param name="source">A source sequence.</param>
    /// <param name="window">A size of window.</param>
    /// <param name="lookbehind">
    /// Indicate whether to produce target if the number of source elements 
    /// preceeding the current is less than the window size.
    /// </param>
    /// <param name="lookahead">
    /// Indicate whether to produce target if the number of source elements 
    /// following current is less than the window size.
    /// </param>
    /// <returns>Returns a sequence of windows.</returns>
    public static IEnumerable<T[]> Window<T, R>(
      this IEnumerable<T> source,
      int window,
      bool lookbehind,
      bool lookahead)
    {
      return source.Window(
        window,
        lookbehind,
        lookahead,
        (buffer, index, count) =>
          {
            var result = new T[count];

            for(var i = 0; i < count; ++i)
            {
              result[i] = buffer[index];
              index = index + 1 == buffer.Length ? 0 : index + 1;
            }

            return result;
          });
    }

    /// <summary>          
    /// Groups the adjacent elements of a sequence according to a           
    /// specified key selector function.          
    /// </summary>          
    /// <typeparam name="TSource">The type of the elements of           
    /// <paramref name="source"/>.</typeparam>          
    /// <typeparam name="TKey">The type of the key returned by           
    /// <paramref name="keySelector"/>.</typeparam>          
    /// <param name="source">A sequence whose elements to group.</param>          
    /// <param name="keySelector">A function to extract the key for each           
    /// element.</param>          
    /// <returns>A sequence of groupings where each grouping          
    /// (<see cref="IGrouping{TKey,TElement}"/>) contains the key          
    /// and the adjacent elements in the same order as found in the           
    /// source sequence.</returns>          
    /// <remarks>          
    /// This method is implemented by using deferred execution and           
    /// streams the groupings. The grouping elements, however, are           
    /// buffered. Each grouping is therefore yielded as soon as it           
    /// is complete and before the next grouping occurs.          
    /// </remarks>
    public static IEnumerable<IGrouping<TKey, TSource>>
      GroupAdjacent<TSource, TKey>(
        this IEnumerable<TSource> source,
        Func<TSource, TKey> keySelector)
    {
      return GroupAdjacent(source, keySelector, null);
    }

    /// <summary>          
    /// Groups the adjacent elements of a sequence according to a           
    /// specified key selector function and compares the keys by using a
    /// specified comparer.          
    /// </summary>          
    /// <typeparam name="TSource">The type of the elements of           
    /// <paramref name="source"/>.</typeparam>          
    /// <typeparam name="TKey">The type of the key returned by           
    /// <paramref name="keySelector"/>.</typeparam>          
    /// <param name="source">A sequence whose elements to group.</param>          
    /// <param name="keySelector">A function to extract the key for each           
    /// element.</param>          
    /// <param name="comparer">An <see cref="IEqualityComparer{T}"/> to           
    /// compare keys.</param>          
    /// <returns>A sequence of groupings where each grouping          
    /// (<see cref="IGrouping{TKey,TElement}"/>) contains the key          
    /// and the adjacent elements in the same order as found in the           
    /// source sequence.</returns>          
    /// <remarks>          
    /// This method is implemented by using deferred execution and           
    /// streams the groupings. The grouping elements, however, are           
    /// buffered. Each grouping is therefore yielded as soon as it           
    /// is complete and before the next grouping occurs.          
    /// </remarks>
    public static IEnumerable<IGrouping<TKey, TSource>>
      GroupAdjacent<TSource, TKey>(
        this IEnumerable<TSource> source,
        Func<TSource, TKey> keySelector,
        IEqualityComparer<TKey> comparer)
    {
      if (source == null)
      {
        throw new ArgumentNullException("source");
      }

      if (keySelector == null)
      {
        throw new ArgumentNullException("keySelector");
      }

      return GroupAdjacent(source, keySelector, e => e, comparer);
    }

    /// <summary>          
    /// Groups the adjacent elements of a sequence according to a           
    /// specified key selector function and projects the elements for           
    /// each group by using a specified function.          
    /// </summary>          
    /// <typeparam name="TSource">The type of the elements of           
    /// <paramref name="source"/>.</typeparam>          
    /// <typeparam name="TKey">The type of the key returned by           
    /// <paramref name="keySelector"/>.</typeparam>          
    /// <typeparam name="TElement">The type of the elements in the          
    /// resulting groupings.</typeparam>          
    /// <param name="source">A sequence whose elements to group.</param>          
    /// <param name="keySelector">A function to extract the key for each           
    /// element.</param>          
    /// <param name="elementSelector">A function to map each source           
    /// element to an element in the resulting grouping.</param>          
    /// <returns>A sequence of groupings where each grouping          
    /// (<see cref="IGrouping{TKey,TElement}"/>) contains the key          
    /// and the adjacent elements (of type <typeparamref name="TElement"/>)
    /// in the same order as found in the source sequence.</returns>          
    /// <remarks>          
    /// This method is implemented by using deferred execution and           
    /// streams the groupings. The grouping elements, however, are           
    /// buffered. Each grouping is therefore yielded as soon as it           
    /// is complete and before the next grouping occurs.          
    /// </remarks>
    public static IEnumerable<IGrouping<TKey, TElement>>
      GroupAdjacent<TSource, TKey, TElement>(
        this IEnumerable<TSource> source,
        Func<TSource, TKey> keySelector,
        Func<TSource, TElement> elementSelector)
    {
      return GroupAdjacent(source, keySelector, elementSelector, null);
    }

    /// <summary>          
    /// Groups the adjacent elements of a sequence according to a           
    /// specified key selector function. The keys are compared by using           
    /// a comparer and each group's elements are projected by using a           
    /// specified function.          
    /// </summary>          
    /// <typeparam name="TSource">The type of the elements of           
    /// <paramref name="source"/>.</typeparam>          
    /// <typeparam name="TKey">The type of the key returned by           
    /// <paramref name="keySelector"/>.</typeparam>          
    /// <typeparam name="TElement">The type of the elements in the          
    /// resulting groupings.</typeparam>          
    /// <param name="source">A sequence whose elements to group.</param>          
    /// <param name="keySelector">A function to extract the key for each           
    /// element.</param>          
    /// <param name="elementSelector">A function to map each source           
    /// element to an element in the resulting grouping.</param>          
    /// <param name="comparer">An <see cref="IEqualityComparer{T}"/> to           
    /// compare keys.</param>          
    /// <returns>A sequence of groupings where each grouping          
    /// (<see cref="IGrouping{TKey,TElement}"/>) contains the key          
    /// and the adjacent elements (of type <typeparamref name="TElement"/>)        
    /// in the same order as found in the source sequence.</returns>          
    /// <remarks>          
    /// This method is implemented by using deferred execution and           
    /// streams the groupings. The grouping elements, however, are           
    /// buffered. Each grouping is therefore yielded as soon as it           
    /// is complete and before the next grouping occurs.          
    /// </remarks>
    public static IEnumerable<IGrouping<TKey, TElement>>
      GroupAdjacent<TSource, TKey, TElement>(
        this IEnumerable<TSource> source,
        Func<TSource, TKey> keySelector,
        Func<TSource, TElement> elementSelector,
        IEqualityComparer<TKey> comparer)
    {
      if (source == null)
      {
        throw new ArgumentException("source");
      }

      if (keySelector == null)
      {
        throw new ArgumentException("keySelector");
      }

      if (elementSelector == null)
      {
        throw new ArgumentException("elementSelector");
      }

      return GroupAdjacentImpl(
        source,
        keySelector,
        elementSelector,
        comparer ?? EqualityComparer<TKey>.Default);
    }

    private static IEnumerable<IGrouping<TKey, TElement>>
      GroupAdjacentImpl<TSource, TKey, TElement>(
        this IEnumerable<TSource> source,
        Func<TSource, TKey> keySelector,
        Func<TSource, TElement> elementSelector,
        IEqualityComparer<TKey> comparer)
    {
      Debug.Assert(source != null);
      Debug.Assert(keySelector != null);
      Debug.Assert(elementSelector != null);
      Debug.Assert(comparer != null);

      var group = default(TKey);
      var members = null as List<TElement>;

      foreach(var item in
        source.Select(
          item =>
            Tuple.Create(keySelector(item), elementSelector(item))))
      {
        if ((members != null) && comparer.Equals(group, item.Item1))
        {
          members.Add(item.Item2);
        }
        else
        {
          if (members != null)
          {
            yield return CreateGroupAdjacentGrouping(group, members);
          }

          group = item.Item1;
          members = new List<TElement> { item.Item2 };
        }
      }

      if (members != null)
      {
        yield return CreateGroupAdjacentGrouping(group, members);
      }
    }

    private static Grouping<TKey, TElement>
      CreateGroupAdjacentGrouping<TKey, TElement>(
        TKey key,
        IList<TElement> members)
    {
      Debug.Assert(members != null);

      return Grouping.Create(
        key,
        members.IsReadOnly ?
          members : new ReadOnlyCollection<TElement>(members));
    }

    static class Grouping
    {
      public static Grouping<TKey, TElement> Create<TKey, TElement>(
        TKey key,
        IEnumerable<TElement> members)
      {
        return new Grouping<TKey, TElement>(key, members);
      }
    }

    [Serializable]
    private sealed class Grouping<TKey, TElement> : IGrouping<TKey, TElement>
    {
      private readonly IEnumerable<TElement> _members;
      public Grouping(TKey key, IEnumerable<TElement> members)
      {
        Debug.Assert(members != null);
        Key = key;
        _members = members;
      }

      public TKey Key { get; private set; }

      public IEnumerator<TElement> GetEnumerator()
      {
        return _members.GetEnumerator();
      }

      IEnumerator IEnumerable.GetEnumerator() { return GetEnumerator(); }
    }
  }
}
