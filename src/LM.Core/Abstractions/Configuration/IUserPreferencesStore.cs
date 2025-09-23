using System.Threading;
using System.Threading.Tasks;
using LM.Core.Models;

namespace LM.Core.Abstractions.Configuration
{
    /// <summary>Persists user-level application preferences.</summary>
    public interface IUserPreferencesStore
    {
        Task<UserPreferences> LoadAsync(CancellationToken ct = default);
        Task SaveAsync(UserPreferences preferences, CancellationToken ct = default);
    }
}
