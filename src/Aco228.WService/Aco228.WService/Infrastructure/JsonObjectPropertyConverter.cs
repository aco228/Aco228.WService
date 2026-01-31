using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Aco228.WService.Attributes;

/// <summary>
/// A generic JSON converter that supports nested property paths via [JsonObjectProperty] attribute.
/// Example: [JsonObjectProperty("owner.url")] will extract the value from json.owner.url
/// </summary>
public class JsonObjectPropertyConverter<T> : JsonConverter<T> where T : new()
{
    public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using (JsonDocument doc = JsonDocument.ParseValue(ref reader))
        {
            var root = doc.RootElement;

            // Handle non-object root elements
            if (root.ValueKind != JsonValueKind.Object)
            {
                if (root.ValueKind == JsonValueKind.Null)
                {
                    return new T();
                }
                
                if (root.ValueKind == JsonValueKind.Array)
                {
                    throw new JsonException(
                        $"Cannot deserialize a JSON array to type '{typeToConvert.Name}'. " +
                        "If you're expecting an array of objects, use JsonSerializer.Deserialize<List<" + 
                        typeToConvert.Name + ">>(json, options) instead.");
                }

                throw new JsonException(
                    $"Expected a JSON object but got {root.ValueKind}");
            }

            var instance = new T();
            var properties = typeToConvert.GetProperties(BindingFlags.Public | BindingFlags.Instance);

            foreach (var prop in properties)
            {
                if (!prop.CanWrite)
                    continue;

                // Check for JsonObjectProperty attribute
                var objectAttr = prop.GetCustomAttribute<JsonObjectPropertyAttribute>();
                if (objectAttr != null)
                {
                    var value = GetNestedJsonValue(root, objectAttr.Name);
                    if (value != null)
                    {
                        if (TryConvertValue(value, prop.PropertyType, out var convertedValue))
                        {
                            prop.SetValue(instance, convertedValue);
                        }
                        // If conversion fails, property remains at default value
                    }
                    continue;
                }

                // Check for JsonPropertyName attribute (standard System.Text.Json support)
                var jsonAttr = prop.GetCustomAttribute<JsonPropertyNameAttribute>();
                string propertyName = jsonAttr?.Name ?? prop.Name;

                if (root.TryGetProperty(propertyName, out var element))
                {
                    try
                    {
                        var value = JsonSerializer.Deserialize(element.GetRawText(), prop.PropertyType, options);
                        prop.SetValue(instance, value);
                    }
                    catch (JsonException)
                    {
                        // Ignore JSON deserialization errors - property remains at default value
                    }
                    catch (NotSupportedException)
                    {
                        // Ignore unsupported type errors - property remains at default value
                    }
                }
            }

            return instance;
        }
    }

    public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
    {
        // Default serialization - you can customize this if needed
        JsonSerializer.Serialize(writer, value, value.GetType(), options);
    }

    /// <summary>
    /// Extracts a value from a nested JSON path supporting:
    /// - Simple paths: "owner.url"
    /// - Deep nesting: "owner.obj.name"
    /// - Array indexing: "owners[0].url" or "items[2].data"
    /// Returns null if path not found
    /// </summary>
    private static object? GetNestedJsonValue(JsonElement element, string path)
    {
        JsonElement? current = element;

        // Split by dots, but preserve array indices like "owners[0]"
        var parts = SplitPath(path);

        foreach (var part in parts)
        {
            if (current == null)
                return null;

            current = NavigatePath(current.Value, part);
        }

        if (current == null)
            return null;

        // Convert JsonElement to appropriate type
        return current.Value.ValueKind switch
        {
            JsonValueKind.String => current.Value.GetString(),
            JsonValueKind.Number => current.Value.TryGetInt32(out int intVal) ? (object)intVal : current.Value.GetDecimal(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            _ => current.Value.GetRawText()
        };
    }

    /// <summary>
    /// Splits a path by dots, but keeps array indices together
    /// "owner.items[0].url" => ["owner", "items[0]", "url"]
    /// </summary>
    private static string[] SplitPath(string path)
    {
        var result = new List<string>();
        var current = "";

        foreach (var c in path)
        {
            if (c == '.')
            {
                if (!string.IsNullOrEmpty(current))
                {
                    result.Add(current);
                    current = "";
                }
            }
            else
            {
                current += c;
            }
        }

        if (!string.IsNullOrEmpty(current))
            result.Add(current);

        return result.ToArray();
    }

    /// <summary>
    /// Navigates a single path segment, handling both properties and array indices
    /// "owner" => accesses property "owner"
    /// "items[0]" => accesses property "items" then array element [0]
    /// </summary>
    private static JsonElement? NavigatePath(JsonElement element, string part)
    {
        // Check if this part has array indexing like "items[0]"
        int bracketIndex = part.IndexOf('[');
        
        if (bracketIndex > 0)
        {
            // Extract property name and index
            string propertyName = part.Substring(0, bracketIndex);
            string indexPart = part.Substring(bracketIndex);

            // Get the array property
            if (!element.TryGetProperty(propertyName, out var arrayElement))
                return null;

            // Parse the index from "[0]" format
            if (!ExtractArrayIndex(indexPart, out int index))
                return null;

            // Access the array element
            if (arrayElement.ValueKind != JsonValueKind.Array)
                return null;

            try
            {
                var enumerator = arrayElement.EnumerateArray();
                int currentIndex = 0;
                foreach (var item in enumerator)
                {
                    if (currentIndex == index)
                        return item;
                    currentIndex++;
                }
                return null; // Index out of range
            }
            catch
            {
                return null;
            }
        }
        else
        {
            // Simple property access
            if (element.ValueKind != JsonValueKind.Object)
                return null;

            if (element.TryGetProperty(part, out var next))
                return next;

            return null;
        }
    }

    /// <summary>
    /// Extracts the array index from a string like "[0]" or "[123]"
    /// </summary>
    private static bool ExtractArrayIndex(string indexPart, out int index)
    {
        index = -1;
        
        // Remove brackets: "[0]" => "0"
        if (indexPart.StartsWith("[") && indexPart.EndsWith("]"))
        {
            string numberPart = indexPart.Substring(1, indexPart.Length - 2);
            return int.TryParse(numberPart, out index);
        }

        return false;
    }

    /// <summary>
    /// Attempts to convert a value to the target type, handling nullable types, enums, and common types.
    /// Returns true if conversion succeeds, false otherwise.
    /// </summary>
    private static bool TryConvertValue(object? value, Type targetType, out object? result)
    {
        result = null;

        if (value == null)
        {
            // Null can only be assigned to reference types or nullable value types
            if (!targetType.IsValueType || Nullable.GetUnderlyingType(targetType) != null)
            {
                result = null;
                return true;
            }
            return false;
        }

        // Get the underlying type if nullable (e.g., int? -> int)
        var underlyingType = Nullable.GetUnderlyingType(targetType) ?? targetType;

        try
        {
            // If types match exactly, no conversion needed
            if (value.GetType() == underlyingType)
            {
                result = value;
                return true;
            }

            // Handle enums
            if (underlyingType.IsEnum)
            {
                if (value is string stringValue)
                {
                    result = Enum.Parse(underlyingType, stringValue, ignoreCase: true);
                    return true;
                }
                else if (value is int || value is long || value is byte || value is short)
                {
                    result = Enum.ToObject(underlyingType, value);
                    return true;
                }
                return false;
            }

            // Handle Guid
            if (underlyingType == typeof(Guid))
            {
                if (value is string guidString)
                {
                    if (Guid.TryParse(guidString, out var guid))
                    {
                        result = guid;
                        return true;
                    }
                }
                return false;
            }

            // Handle DateTime
            if (underlyingType == typeof(DateTime))
            {
                if (value is string dateString)
                {
                    if (DateTime.TryParse(dateString, System.Globalization.CultureInfo.InvariantCulture,
                        System.Globalization.DateTimeStyles.None, out var date))
                    {
                        result = date;
                        return true;
                    }
                }
                return false;
            }

            // Handle DateTimeOffset
            if (underlyingType == typeof(DateTimeOffset))
            {
                if (value is string dateOffsetString)
                {
                    if (DateTimeOffset.TryParse(dateOffsetString, System.Globalization.CultureInfo.InvariantCulture,
                        System.Globalization.DateTimeStyles.None, out var dateOffset))
                    {
                        result = dateOffset;
                        return true;
                    }
                }
                return false;
            }

            // Handle TimeSpan
            if (underlyingType == typeof(TimeSpan))
            {
                if (value is string timeSpanString)
                {
                    if (TimeSpan.TryParse(timeSpanString, System.Globalization.CultureInfo.InvariantCulture, out var timeSpan))
                    {
                        result = timeSpan;
                        return true;
                    }
                }
                return false;
            }

            // Handle boolean with flexible parsing
            if (underlyingType == typeof(bool))
            {
                if (value is string boolString)
                {
                    if (bool.TryParse(boolString, out var boolResult))
                    {
                        result = boolResult;
                        return true;
                    }

                    // Support common boolean representations
                    var normalized = boolString.Trim().ToLowerInvariant();
                    if (normalized == "1" || normalized == "yes" || normalized == "y")
                    {
                        result = true;
                        return true;
                    }
                    if (normalized == "0" || normalized == "no" || normalized == "n")
                    {
                        result = false;
                        return true;
                    }
                }
                else if (value is int intValue)
                {
                    result = intValue != 0;
                    return true;
                }
                return false;
            }

            // For other types, try Convert.ChangeType with culture-safe conversion
            result = Convert.ChangeType(value, underlyingType, System.Globalization.CultureInfo.InvariantCulture);
            return true;
        }
        catch (InvalidCastException)
        {
            return false;
        }
        catch (FormatException)
        {
            return false;
        }
        catch (OverflowException)
        {
            return false;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }
}