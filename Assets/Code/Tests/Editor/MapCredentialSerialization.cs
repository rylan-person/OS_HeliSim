using System;
using System.Text.RegularExpressions;

/// <summary>Checks serialized map authentication without exposing credential values.</summary>
public static class MapCredentialSerialization
{
    // Both current CesiumIonServer and obsolete CesiumRuntimeSettings names matter:
    // obsolete Resources assets are still bundled even when no scene references them.
    private const string CredentialField = @"(?:_?defaultIonAccessToken|_?ionAccessToken|AccessToken)";
    private static readonly Regex YamlCredential = new Regex(
        @"^\s*" + CredentialField + @":([^\r\n]*)", RegexOptions.Multiline);
    private static readonly Regex PrefabOverride = new Regex(
        @"^\s*propertyPath: (?:[^\r\n.]+\.)*" + CredentialField
        + @"\r?\n\s*value:([^\r\n]*)", RegexOptions.Multiline);
    private static readonly Regex JsonCredential = new Regex(
        "\"" + CredentialField + "\"\\s*:\\s*(\"(?:\\\\.|[^\"\\\\])*\"|[^,}\\r\\n]+)");
    private static readonly Regex ProviderToken = new Regex(
        @"(?<![A-Za-z0-9_-])(?:(?:pk|sk|tk)\.[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+|eyJ[A-Za-z0-9_-]*\.[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+)");

    public static bool ContainsCredential(string serialized)
    {
        if (serialized == null)
        {
            throw new ArgumentNullException(nameof(serialized));
        }

        return HasNonemptyValue(YamlCredential, serialized)
            || HasNonemptyValue(PrefabOverride, serialized)
            || HasNonemptyValue(JsonCredential, serialized)
            || ProviderToken.IsMatch(serialized);
    }

    private static bool HasNonemptyValue(Regex pattern, string serialized)
    {
        foreach (Match match in pattern.Matches(serialized))
        {
            string value = match.Groups[1].Value.Trim();
            if (value.Length != 0 && value != "\"\"" && value != "''" && value != "null")
            {
                return true;
            }
        }

        return false;
    }
}
