using System.Security.Cryptography;
using System.Text;

namespace LM.Core.Utils
{
    public static class IdGen
    {
        // Time-ordered GUID using .NET 8/9 built-in API
        public static string NewId() => Guid.CreateVersion7().ToString("N");
    }

    public static class JsonEx
    {
        public static readonly System.Text.Json.JsonSerializerOptions Options =
            new(System.Text.Json.JsonSerializerDefaults.Web) { WriteIndented = true };

        public static string Serialize<T>(T v) => System.Text.Json.JsonSerializer.Serialize(v, Options);
        public static T? Deserialize<T>(string s) => System.Text.Json.JsonSerializer.Deserialize<T>(s, Options);
    }

    public static class Hashes
    {
        public static string Sha256File(string path)
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            var sha = SHA256.Create().ComputeHash(fs);
            return Convert.ToHexString(sha).ToLowerInvariant();
        }
        public static string Sha1(string input)
        {
            var bytes = Encoding.UTF8.GetBytes(input);
            var sha1 = SHA1.HashData(bytes);
            return Convert.ToHexString(sha1).ToLowerInvariant();
        }
    }
}
