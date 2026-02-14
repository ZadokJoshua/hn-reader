using System.ComponentModel;
using System.Reflection;

namespace HNReader.Core.Helpers;

public static class EnumExtensions
{
    public static string GetDescription(this Enum value)
    {
        var enumFieldName = value.ToString();
        var field = value.GetType().GetField(enumFieldName) 
            ?? throw new InvalidOperationException($"Field '{enumFieldName}' not found in enum '{value.GetType().Name}'.");

        var attribute = field.GetCustomAttribute<DescriptionAttribute>() 
            ?? throw new InvalidOperationException($"Enum field '{enumFieldName}' in enum '{value.GetType().Name}' does not have a Description attribute.");

        return attribute.Description;
    }
}
