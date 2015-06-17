//------------------------------------------------------------------------------
// <copyright file="HexEncoder.cs" company="Multiconn">
//   Copyright (c) 2004 Multiconn Technologies. All rights reserved.
// </copyright>
// <description>
//   Utility class that converts hex string to and from byte array.
// </description>
// <history>
//  $Modtime: 11/08/11 17:57 $
//  $Revision: 1 $
// </history>
//------------------------------------------------------------------------------

namespace Multiconn.Experanto.Serializer
{
  using System;
  using System.Text;

  /// <summary>
	/// Utility class that converts hex string to and from byte array.
	/// </summary>
	public class HexEncoder
	{
    /// <summary>
    /// Converts hex character to a digit.
    /// </summary>
    /// <param name="c">Character to convert.</param>
    /// <returns>Digit value.</returns>
    public static int HexCharToDigit(char c)
    {
      if ((c >= '0') && c <= '9')
        return (int)(c - '0');

      if ((c >= 'A') && (c <= 'F'))
        return (int)(c - 'A') + 10;

      if ((c >= 'a') && (c <= 'f'))
        return (int)(c - 'a') + 10;

      throw new ArgumentException();
    }

    /// <summary>
    /// Converts digit to a hex character.
    /// </summary>
    /// <param name="digit">Digit to convert.</param>
    /// <returns>Hex character.</returns>
    public static char DigitToHexChar(int digit)
    {
      if ((digit >= 0) && (digit <= 9))
        return (char)((int)'0' + digit);

      if ((digit >= 0xa) && (digit <= 0xf))
        return (char)((int)'a' + digit - 0xa);

      throw new ArgumentException();
    }

    /// <summary>
    /// Converts hex string to a byte array.
    /// </summary>
    /// <param name="data">Hex string to convert.</param>
    /// <returns>Byte array.</returns>
    public static byte[] HexStringToArray(string data)
    {
      if ((data == null) || (data.Length == 0))
        return null;

      byte[] result = new byte[(data.Length + 1)/2];
      int i = 0;
      int pos = 0;

      if ((data.Length & 1) != 0)
      {
        result[0] = (byte)HexCharToDigit(data[i++]);
        pos = 1;
      }

      for(; i < data.Length; i += 2)
      {
        result[pos++] = 
          (byte)((HexCharToDigit(data[i]) << 4) + HexCharToDigit(data[i + 1]));
      }

      return result;
    }

    /// <summary>
    /// Converts byte array to a hex string.
    /// </summary>
    /// <param name="data">Array to convert.</param>
    /// <returns>Hex string value.</returns>
    public static string ArrayToHexString(byte[] data)
    {
      if ((data == null) || (data.Length == 0))
        return null;

      int value = data[0];
      int count = data.Length * 2;
      int pos = 0;

      StringBuilder result;

      if (value < 0x10)
      {
        count--;
        result = new StringBuilder(count);
        result.Append(DigitToHexChar(value));
        pos = 1;
      }
      else
      {
        result = new StringBuilder(count);
      }

      for(; pos < data.Length; pos++)
      {
        value = data[pos];
        result.Append(DigitToHexChar(value >> 4));
        result.Append(DigitToHexChar(value & 0xf));
      }

      return result.ToString();
    }
	}
}