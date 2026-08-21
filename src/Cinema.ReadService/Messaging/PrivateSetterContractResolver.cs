using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace Cinema.ReadService.Messaging;

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
