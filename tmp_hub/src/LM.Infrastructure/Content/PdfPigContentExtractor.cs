using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LM.Core.Abstractions;
using UglyToad.PdfPig;

namespace LM.Infrastructure.Content
{
    public sealed class PdfPigContentExtractor : IContentExtractor
    {
        public Task<string> ExtractTextAsync(string absolutePath, CancellationToken ct = default)
        {
            var sb = new StringBuilder();
            using var doc = PdfDocument.Open(absolutePath);
            foreach (var page in doc.GetPages())
                sb.AppendLine(page.Text);
            return Task.FromResult(sb.ToString());
        }
    }
}
