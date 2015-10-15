namespace NesterovskyBros.Code
{
  using System;
  using System.Linq;
  using System.Reflection;
  using System.Collections.Generic;
  using Newtonsoft.Json;
  using Newtonsoft.Json.Serialization;
  
  /// <summary>
  /// A contract resolver for JSON serialization.
  /// This contract uses camelCase naming strategy, and 
  /// does not serialize empty collections.
  /// </summary>
  public class JsonContractResolver : CamelCasePropertyNamesContractResolver
  {
    /// <summary>
    /// Creates a JsonProperty for the given MemberInfo of ICollection type.
    /// </summary>
    /// <param name="member">
    /// A member to serialize.
    /// </param>
    /// <param name="memberSerialization">
    /// Member serialization options.
    /// </param>
    /// <returns>A JsonProperty instance.</returns>
    protected override JsonProperty CreateProperty(
      MemberInfo member,
      MemberSerialization memberSerialization)
    {
      JsonProperty property = base.CreateProperty(member, memberSerialization);

      if (property.ShouldSerialize == null) 
      {
        var type = property.PropertyType;

        if (type.IsGenericType && 
          (type.GetGenericTypeDefinition() == typeof(IEnumerable<>)))
        {
          var tester = (IShouldSerializeMember)Activator.CreateInstance(
            typeof(ShouldSerializeMember<>).
              MakeGenericType(type.GetGenericArguments()[0]));

          property.ShouldSerialize = 
            instance => tester.Test(property.ValueProvider.GetValue(instance));
        }
      }

      return property;
    }

    private interface IShouldSerializeMember
    {
      bool Test(object value);
    }

    private class ShouldSerializeMember<T>: IShouldSerializeMember
    {
      public bool Test(object value)
      {
        var collection = value as ICollection<T>;

        if (collection != null)
        {
          return collection.Count > 0;
        }

        var enumerable = value as IEnumerable<T>;

        if (enumerable != null)
        {
          return enumerable.Any();
        }

        return false;
      }
    }
  }
}