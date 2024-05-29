using System;
using System.Linq;
using System.Security.Cryptography;

namespace Base58
{
    public class ArrayHelpers
    {
        public static T[] ConcatArrays<T>(params T[][] arrays)
        {
            var result = new T[arrays.Sum(arr => arr.Length)];
            int offset = 0;
            for (int i = 0; i < arrays.Length; i++)
            {
                var arr = arrays[i];
                Buffer.BlockCopy(arr, 0, result, offset, arr.Length);
                offset += arr.Length;
            };
            return result;
        }

        public static T[] ConcatArrays<T>(T[] arr1, T[] arr2)
        {
            var result = new T[arr1.Length + arr2.Length];
            Buffer.BlockCopy(arr1, 0, result, 0, arr1.Length);
            Buffer.BlockCopy(arr2, 0, result, arr1.Length, arr2.Length);
            return result;
        }

        public static T[] SubArray<T>(T[] arr, int start, int length)
        {
            var result = new T[length];
            Buffer.BlockCopy(arr, start, result, 0, length);
            return result;
        }

        public static T[] SubArray<T>(T[] arr, int start) => SubArray(arr, start, arr.Length - start);
    }

    public static class Base58Encoding
    {
        public const byte CheckSumSizeInBytes = 4;
        private const string Digits = "123456789ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz";

        public static byte[] AddCheckSum(byte[] data) => ArrayHelpers.ConcatArrays(data, GetCheckSum(data));

        public static byte[] VerifyAndRemoveCheckSum(byte[] data)
        {
            byte[] result = ArrayHelpers.SubArray(data, 0, data.Length - CheckSumSizeInBytes);
            byte[] givenCheckSum = ArrayHelpers.SubArray(data, data.Length - CheckSumSizeInBytes);
            byte[] correctCheckSum = GetCheckSum(result);
            if (givenCheckSum.SequenceEqual(correctCheckSum)) return result;
            else return null;
        }        

        public static string Encode(byte[] data)
        {
            // Decode byte[] to long
            long intData = 0;
            for (int i = 0; i < data.Length; i++)
                intData = intData * 256 + data[i];

            // Encode long to Base58 string
            string result = "";
            while (intData > 0)
            {
                int remainder = (int)(intData % 58);
                intData /= 58;
                result = Digits[remainder] + result;
            };

            // Append `1` for each leading 0 byte
            for (int i = 0; i < data.Length && data[i] == 0; i++)
                result = '1' + result;
            return result;
        }

        public static string EncodeWithCheckSum(byte[] data) => Encode(AddCheckSum(data));

        public static byte[] Decode(string s)
        {
            // Decode Base58 string to long 
            long intData = 0;
            for (int i = 0; i < s.Length; i++)
            {
                int digit = Digits.IndexOf(s[i]); //Slow
                if (digit < 0)
                    throw new FormatException(string.Format("Invalid Base58 character `{0}` at position {1}", s[i], i));
                intData = intData * 58 + digit;
            };

            // Encode long to byte[]
            // Leading zero bytes get encoded as leading `1` characters
            int leadingZeroCount = s.TakeWhile(c => c == '1').Count();
            var leadingZeros = Enumerable.Repeat((byte)0, leadingZeroCount);
            var bytesWithoutLeadingZeros = BitConverter.GetBytes(intData).Reverse().SkipWhile(b => b == 0);// to big endian + strip sign byte
            byte[] result = leadingZeros.Concat(bytesWithoutLeadingZeros).ToArray();
            return result;
        }

        public static byte[] DecodeWithCheckSum(string s)
        {
            byte[] dataWithCheckSum = Decode(s);
            byte[] dataWithoutCheckSum = VerifyAndRemoveCheckSum(dataWithCheckSum);
            if (dataWithoutCheckSum == null) throw new FormatException("Base58 checksum is invalid");
            return dataWithoutCheckSum;
        }

        private static byte[] GetCheckSum(byte[] data)
        {
            SHA256 sha256 = new SHA256Managed();
            byte[] hash1 = sha256.ComputeHash(data);
            byte[] hash2 = sha256.ComputeHash(hash1);

            byte[] result = new byte[CheckSumSizeInBytes];
            Buffer.BlockCopy(hash2, 0, result, 0, result.Length);

            return result;
        }
    }
}