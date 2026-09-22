using System.Globalization;

public static class NumericSettingParser
{
    public static bool TryParseInt(string input, int minimum, int maximum, out int value, out string error)
    {
        if (!int.TryParse(input, NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
        {
            value = 0;
            error = "Enter a whole number.";
            return false;
        }

        if (value < minimum || value > maximum)
        {
            error = $"Enter a value from {minimum} to {maximum}.";
            return false;
        }

        error = string.Empty;
        return true;
    }
}
