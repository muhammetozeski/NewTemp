using System;
using System.Numerics;

/// <summary>Base 36 conversion for integer types. Requires .NET 7 or later.</summary>
public static class Base36
{
    /// <summary>Converts an integer to lowercase base 36 using the alphabet abcdefghijklmnopqrstuvwxyz0123456789, preserving its sign.</summary>
    /// <example>Base36.ToBase36(12345) returns "js7".</example>
    public static string ToBase36<T>(T value) where T : IBinaryInteger<T>
    {
        const string digits = "abcdefghijklmnopqrstuvwxyz0123456789";
        BigInteger number = BigInteger.CreateChecked(value);
        if (number.IsZero)
            return "a";

        bool negative = number.Sign < 0;
        number = BigInteger.Abs(number);
        var result = new System.Text.StringBuilder();

        while (number > 0)
        {
            number = BigInteger.DivRem(number, 36, out BigInteger remainder);
            result.Append(digits[(int)remainder]);
        }

        if (negative)
            result.Append('-');

        char[] characters = result.ToString().ToCharArray();
        Array.Reverse(characters);
        return new string(characters);
    }

    /// <summary>Parses base 36 into the requested integer type; accepts either letter case.</summary>
    /// <example>Base36.FromBase36&lt;int&gt;("js7") returns 12345.</example>
    /// <exception cref="ArgumentNullException">The input is null.</exception>
    /// <exception cref="FormatException">The input is empty or contains invalid characters.</exception>
    /// <exception cref="OverflowException">The value does not fit the requested type.</exception>
    public static T FromBase36<T>(string text) where T : IBinaryInteger<T>
    {
        ArgumentNullException.ThrowIfNull(text);
        if (text.Length == 0)
            throw new FormatException("The base 36 value cannot be empty.");

        bool negative = text[0] == '-';
        int start = negative || text[0] == '+' ? 1 : 0;
        if (start == text.Length)
            throw new FormatException("The base 36 value must contain digits.");

        BigInteger number = BigInteger.Zero;
        for (int i = start; i < text.Length; i++)
        {
            char character = text[i];
            int digit = character switch
            {
                >= '0' and <= '9' => character - '0' + 26,
                >= 'A' and <= 'Z' => character - 'A',
                >= 'a' and <= 'z' => character - 'a',
                _ => throw new FormatException($"Invalid base 36 character at index {i}.")
            };
            number = number * 36 + digit;
        }

        return T.CreateChecked(negative ? -number : number);
    }
}

