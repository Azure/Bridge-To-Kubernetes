using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System.Reflection;
using System.Text.Json.Serialization;

namespace Microsoft.BridgeToKubernetes.Common.Json
{
    public class STJContractResolver : DefaultContractResolver
    {
        protected override JsonProperty CreateProperty(MemberInfo member, MemberSerialization memberSerialization)
        {
            JsonProperty property = base.CreateProperty(member, memberSerialization);

            if (member.GetCustomAttribute<JsonPropertyNameAttribute>() is { } stj)
            {
                property.PropertyName = stj.Name;
                return property;
            }

            return property;
        }
    }

    public class STJCamelCaseContractResolver : CamelCasePropertyNamesContractResolver
    {
        protected override JsonProperty CreateProperty(MemberInfo member, MemberSerialization memberSerialization)
        {
            JsonProperty property = base.CreateProperty(member, memberSerialization);

            if (member.GetCustomAttribute<JsonPropertyNameAttribute>() is { } stj)
            {
                property.PropertyName = stj.Name;
                return property;
            }

            return property;
        }
    }

}
