// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Humanizer;
using System.Text;

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
}
