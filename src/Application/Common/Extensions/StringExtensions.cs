// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Humanizer;
using System.Text;
using System.Text.RegularExpressions;

namespace CleanArchitecture.Blazor.Application.Common.Extensions;

public static class StringExtensions
{
    public static string ToMD5(this string input)
    {
        using (System.Security.Cryptography.MD5 md5 = System.Security.Cryptography.MD5.Create())
        {
            var encoding = Encoding.ASCII;
            var data = encoding.GetBytes(input);

            Span<byte> hashBytes = stackalloc byte[16];
            md5.TryComputeHash(data, hashBytes, out int written);
            if (written != hashBytes.Length)
                throw new OverflowException();


            Span<char> stringBuffer = stackalloc char[32];
            for (int i = 0; i < hashBytes.Length; i++)
            {
                hashBytes[i].TryFormat(stringBuffer.Slice(2 * i), out _, "x2");
            }
            return new string(stringBuffer);
        }
    }

    public static string ToHumanReadableString(this int timeMilliSecs)
    {
        return new TimeSpan(0, 0, 0, 0, timeMilliSecs).Humanize();
    }
    /// <summary>
    ///     Convert a hash to a string
    /// </summary>
    /// <param name="bytes"></param>
    /// <param name="upperCase"></param>
    /// <returns></returns>
    public static string ToHex(this byte[] bytes, bool upperCase)
    {
        var result = new StringBuilder(bytes.Length * 2);

        for (var i = 0; i < bytes.Length; i++)
            result.Append(bytes[i].ToString(upperCase ? "X2" : "x2"));

        return result.ToString();
    }
    /// <summary>
    ///     Trim method with safety for null or empty strings.
    /// </summary>
    /// <param name="source"></param>
    /// <returns></returns>
    public static string SafeTrim(this string source)
    {
        if (source == null)
            return null;

        return source.Trim();
    }
    /// <summary>
    ///     Strip smart-quotes and replace with single or double-quotes
    /// </summary>
    /// <param name="s"></param>
    /// <returns></returns>
    public static string RemoveSmartQuotes(this string s)
    {
        const char singleQuote = '\'';
        const char doubleQuote = '\"';

        var result = s.Replace('\u0091', singleQuote)
            .Replace('\u0092', singleQuote)
            .Replace('\u2018', singleQuote)
            .Replace('\u2019', singleQuote)
            .Replace('\u201d', doubleQuote)
            .Replace('\u201c', doubleQuote);

        return result;
    }
    /// <summary>
    ///     General string sanitisation - used for IPTC keywords and
    ///     other places that don't handle unicode characters so well.
    ///     Based on:
    ///     https://docs.microsoft.com/en-us/dotnet/standard/base-types/how-to-strip-invalid-characters-from-a-string
    /// </summary>
    /// <param name="strIn"></param>
    /// <returns></returns>
    public static string Sanitise(this string strIn)
    {
        var input = RemoveSmartQuotes(strIn);

        input = input.Replace("\u0096", "-");

        // Replace invalid characters with empty strings.
        try
        {
            return Regex.Replace(input, @"[^\x00-\x7F]+", "",
                RegexOptions.None, TimeSpan.FromSeconds(1.5));
        }
        // If we timeout when replacing invalid characters,
        // we should return Empty.
        catch (RegexMatchTimeoutException)
        {
            return string.Empty;
        }
    }
}
