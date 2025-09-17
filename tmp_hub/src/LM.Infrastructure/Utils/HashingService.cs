using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using LM.Core.Abstractions;

namespace LM.Infrastructure.Utils
{
    public sealed class HashingService : IHasher
    {
        public async Task<string> ComputeSha256Async(string filePath, CancellationToken ct = default)
        {
            await using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var sha = SHA256.Create();
            var hash = await sha.ComputeHashAsync(fs, ct);
            return string.Concat(System.Array.ConvertAll(hash, b => b.ToString("x2")));
        }
    }
}
