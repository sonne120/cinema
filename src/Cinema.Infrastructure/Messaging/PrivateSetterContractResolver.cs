using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace Cinema.Infrastructure.Messaging;

/// <summary>
/// Lets Json.NET rehydrate domain events whose properties have private setters,
/// which is how outbox payloads are deserialized before republishing.
/// </summary>
public class PrivateSetterContractResolver : DefaultContractResolver
{
    protected override JsonProperty CreateProperty(MemberInfo member, MemberSerialization memberSerialization)
    {
        var prop = base.CreateProperty(member, memberSerialization);

        if (!prop.Writable && member is PropertyInfo property)
        {
            prop.Writable = property.GetSetMethod(nonPublic: true) != null;
        }

        return prop;
    }
}
