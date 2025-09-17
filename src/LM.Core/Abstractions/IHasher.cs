using System.Threading;
using System.Threading.Tasks;

namespace LM.Core.Abstractions
{
    public interface IHasher
    {
        Task<string> ComputeSha256Async(string filePath, CancellationToken ct = default);
    }
}
