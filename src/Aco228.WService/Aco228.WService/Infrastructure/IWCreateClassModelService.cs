using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

public interface IWCreateClassModelService
{
    string ConvertJsonToClass(string jsonString, string className);
}

public class WCreateClassModelService : IWCreateClassModelService
{
    private static readonly HashSet<string> ReservedWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "abstract", "as", "base", "bool", "break", "byte", "case", "catch", "char", "checked",
        "class", "const", "continue", "decimal", "default", "delegate", "do", "double", "else",
        "enum", "event", "explicit", "extern", "false", "finally", "fixed", "float", "for",
        "foreach", "goto", "if", "implicit", "in", "int", "interface", "internal", "is",
        "lock", "long", "namespace", "new", "null", "object", "operator", "out", "override",
        "params", "private", "protected", "public", "readonly", "ref", "return", "sbyte",
        "sealed", "short", "sizeof", "stackalloc", "static", "string", "struct", "switch",
        "this", "throw", "true", "try", "typeof", "uint", "ulong", "unchecked", "unsafe",
        "ushort", "using", "virtual", "void", "volatile", "while"
    };

    public string ConvertJsonToClass(string jsonString, string className)
    {
        try
        {
            var jsonNode = JsonNode.Parse(jsonString);
            var generatedClasses = new HashSet<string>();
            var allClasses = new StringBuilder();

            if (jsonNode is JsonObject jsonObject)
            {
                // Root is an object
                GenerateClass(jsonObject, className, allClasses, generatedClasses);
            }
            else if (jsonNode is JsonArray jsonArray)
            {
                // Root is an array - scan all elements to build complete schema
                if (jsonArray.Count > 0)
                {
                    var mergedObject = MergeArrayObjects(jsonArray);
                    GenerateClass(mergedObject, className, allClasses, generatedClasses);
                }
                else
                {
                    throw new ArgumentException("Array is empty");
                }
            }
            else
            {
                throw new ArgumentException("JSON must be an object or array of objects");
            }

            // Reorder so main class comes first
            return ReorderClasses(allClasses.ToString(), className);
        }
        catch (Exception ex)
        {
            throw new ArgumentException($"Invalid JSON provided: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Reorders generated classes so the main class appears first.
    /// </summary>
    private string ReorderClasses(string allClassesContent, string mainClassName)
    {
        var lines = allClassesContent.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
        
        var mainClassLines = new List<string>();
        var otherClassLines = new List<string>();
        var currentClass = new List<string>();
        var inMainClass = false;

        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i];

            if (line.StartsWith("public class "))
            {
                // Save previous class if exists
                if (currentClass.Count > 0)
                {
                    if (inMainClass)
                    {
                        mainClassLines.AddRange(currentClass);
                    }
                    else
                    {
                        otherClassLines.AddRange(currentClass);
                    }
                }

                currentClass = new List<string> { line };
                inMainClass = line.Contains(mainClassName);
            }
            else
            {
                currentClass.Add(line);
            }
        }

        // Add last class
        if (currentClass.Count > 0)
        {
            if (inMainClass)
            {
                mainClassLines.AddRange(currentClass);
            }
            else
            {
                otherClassLines.AddRange(currentClass);
            }
        }

        var result = new StringBuilder();
        result.AppendLine(string.Join(Environment.NewLine, mainClassLines));
        result.Append(string.Join(Environment.NewLine, otherClassLines));

        return result.ToString();
    }

    /// <summary>
    /// Merges all objects in an array to get a complete schema.
    /// Handles missing properties and null values across array elements.
    /// </summary>
    private JsonObject MergeArrayObjects(JsonArray jsonArray)
    {
        var mergedObject = new JsonObject();
        var propertyTypes = new Dictionary<string, (string type, bool isNullable)>();

        foreach (var item in jsonArray)
        {
            if (item is not JsonObject obj)
                continue;

            // Track all properties and their types
            foreach (var property in obj)
            {
                var propName = property.Key;
                var propValue = property.Value;

                if (!propertyTypes.ContainsKey(propName))
                {
                    propertyTypes[propName] = (GetBaseType(propValue), propValue == null);
                }
                else
                {
                    // Check if types are consistent
                    var currentType = GetBaseType(propValue);
                    var (existingType, wasNullable) = propertyTypes[propName];
                    
                    // If we encounter null, mark as nullable
                    var isNullable = wasNullable || propValue == null;
                    
                    // Update with merged type if different
                    if (currentType != existingType && currentType != "object")
                    {
                        propertyTypes[propName] = (existingType == "object" ? currentType : existingType, isNullable);
                    }
                    else
                    {
                        propertyTypes[propName] = (existingType, isNullable);
                    }
                }
            }
        }

        // Mark properties that don't exist in all objects as nullable
        var allProperties = propertyTypes.Keys.ToList();
        var objectCount = jsonArray.Count;
        var propertyOccurrences = new Dictionary<string, int>();

        foreach (var item in jsonArray)
        {
            if (item is not JsonObject obj)
                continue;
            foreach (var property in obj)
            {
                if (!propertyOccurrences.ContainsKey(property.Key))
                    propertyOccurrences[property.Key] = 0;
                propertyOccurrences[property.Key]++;
            }
        }

        // Build the merged object with representative values
        foreach (var property in allProperties)
        {
            var (baseType, isNullableFromNull) = propertyTypes[property];
            var isMissingInSomeObjects = propertyOccurrences[property] < objectCount;
            
            // Find a non-null value for this property to use as representative
            JsonNode representativeValue = null;
            foreach (var item in jsonArray)
            {
                if (item is JsonObject obj && obj.ContainsKey(property) && obj[property] != null)
                {
                    // Clone the value to avoid "parent node" issues
                    var valueJson = obj[property].ToJsonString();
                    representativeValue = JsonNode.Parse(valueJson);
                    break;
                }
            }

            mergedObject[property] = representativeValue;
        }

        return mergedObject;
    }

    /// <summary>
    /// Gets the base type of a JSON value without considering nullability.
    /// </summary>
    private string GetBaseType(JsonNode token)
    {
        if (token == null)
            return "object";

        if (token is JsonValue jsonValue)
        {
            if (jsonValue.TryGetValue<string>(out _))
                return "string";
            if (jsonValue.TryGetValue<int>(out _))
                return "int";
            if (jsonValue.TryGetValue<long>(out _))
                return "long";
            if (jsonValue.TryGetValue<double>(out _))
                return "decimal";
            if (jsonValue.TryGetValue<bool>(out _))
                return "bool";
            if (jsonValue.TryGetValue<DateTime>(out _))
                return "DateTime";
            return "object";
        }

        if (token is JsonObject)
            return "object";

        if (token is JsonArray)
            return "array";

        return "object";
    }

    private void GenerateClass(JsonObject jsonObject, string className, StringBuilder classDefinitions, HashSet<string> generatedClasses)
    {
        // Avoid duplicate class generation
        if (generatedClasses.Contains(className))
            return;

        generatedClasses.Add(className);
        var properties = new StringBuilder();

        // Track which properties are nullable
        var nullableProperties = new HashSet<string>();
        
        // Collect all properties from the merged object
        var propertyList = new List<(string name, JsonNode value)>();
        foreach (var property in jsonObject)
        {
            if (property.Value == null)
            {
                nullableProperties.Add(property.Key);
            }
            propertyList.Add((property.Key, property.Value));
        }

        foreach (var (propertyName, value) in propertyList)
        {
            var propertyType = GetPropertyType(value, propertyName, classDefinitions, generatedClasses);
            var isNullable = nullableProperties.Contains(propertyName);
            
            // Add nullability to types that support it
            if (isNullable && !propertyType.EndsWith("?") && propertyType != "string" && propertyType != "object")
            {
                propertyType += "?";
            }

            var safeName = GetSafePropertyName(propertyName);
            var attribute = IsReservedWord(propertyName) ? $"[JsonPropertyName(\"{propertyName}\")]\n    " : "";

            properties.AppendLine($"    {attribute}public {propertyType} {safeName} {{ get; set; }}");
        }

        classDefinitions.AppendLine($"public class {className}");
        classDefinitions.AppendLine("{");
        classDefinitions.Append(properties.ToString());
        classDefinitions.AppendLine("}");
        classDefinitions.AppendLine();
    }

    private string GetPropertyType(JsonNode token, string propertyName, StringBuilder classDefinitions, HashSet<string> generatedClasses)
    {
        if (token == null)
            return "object";

        if (token is JsonValue jsonValue)
        {
            if (jsonValue.TryGetValue<string>(out _))
                return "string";
            if (jsonValue.TryGetValue<int>(out _))
                return "int";
            if (jsonValue.TryGetValue<long>(out _))
                return "long";
            if (jsonValue.TryGetValue<double>(out _))
                return "decimal";
            if (jsonValue.TryGetValue<bool>(out _))
                return "bool";
            if (jsonValue.TryGetValue<DateTime>(out _))
                return "DateTime";
            return "object";
        }

        if (token is JsonObject jsonObject)
        {
            var nestedClassName = ToPascalCase(propertyName) + "DTO";
            GenerateClass(jsonObject, nestedClassName, classDefinitions, generatedClasses);
            return nestedClassName;
        }

        if (token is JsonArray arrayToken)
        {
            if (arrayToken.Count > 0)
            {
                // Merge all array elements to get complete schema
                var mergedArraySchema = MergeArrayObjects(arrayToken);
                var elementType = GetPropertyType(mergedArraySchema, propertyName.TrimEnd('s'), classDefinitions, generatedClasses);
                return $"List<{elementType}>";
            }
            return "List<object>";
        }

        return "object";
    }

    private bool IsReservedWord(string word)
    {
        return ReservedWords.Contains(word);
    }

    private string GetSafePropertyName(string name)
    {
        if (IsReservedWord(name))
        {
            return ToPascalCase(name);
        }
        return name;
    }

    private string ToPascalCase(string str)
    {
        if (string.IsNullOrEmpty(str))
            return str;

        var words = str.Split(new[] { '_', '-' }, StringSplitOptions.RemoveEmptyEntries);
        var result = new StringBuilder();

        foreach (var word in words)
        {
            if (word.Length > 0)
            {
                result.Append(char.ToUpperInvariant(word[0]));
                if (word.Length > 1)
                {
                    result.Append(word.Substring(1).ToLowerInvariant());
                }
            }
        }

        return result.ToString();
    }
}