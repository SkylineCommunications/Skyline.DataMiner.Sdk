namespace Skyline.DataMiner.Sdk.Helpers
{
    using System;
    using System.Linq;
    using System.Security.Cryptography;
    using System.Text;

    internal static class StringExtensions
    {
        public static Guid ToGuid(this string input)
        {
            if (input == null)
            {
                throw new ArgumentNullException(nameof(input));
            }

            using (var sha = SHA256.Create())
            {
                var byte32hash = sha.ComputeHash(Encoding.UTF8.GetBytes(input));
                return new Guid(byte32hash.Take(16).ToArray());
            }
        }
    }
}
