#nullable enable
using System.Threading;
using System.Threading.Tasks;

namespace LM.App.Wpf.Services.Review
{
    internal interface ILitSearchRunPicker
    {
        Task<LitSearchRunSelection?> PickAsync(CancellationToken cancellationToken);
    }
}
