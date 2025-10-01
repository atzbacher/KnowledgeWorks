using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using LM.App.Wpf.ViewModels.Pdf;

namespace LM.App.Wpf.Views
{
    public partial class PdfViewer
    {
        [ComVisible(true)]
        [Guid("7DE2B7E3-CC91-4629-A24E-7618C1F9EAC9")]
        [InterfaceType(ComInterfaceType.InterfaceIsIDispatch)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        public interface IPdfViewerHostObject
        {
            [DispId(1)]
            Task<string?> LoadPdfAsync();

            [DispId(2)]
            Task<string?> CreateHighlightAsync(string payloadJson);

            [DispId(3)]
            Task<string?> GetCurrentSelectionAsync();

            [DispId(4)]
            Task SetOverlayAsync(string payloadJson);
        }

        [ComVisible(true)]
        [ClassInterface(ClassInterfaceType.None)]
        [Guid("3F7E9A15-902A-4DF2-9919-45B29A339DAF")]
        [EditorBrowsable(EditorBrowsableState.Never)]
        public sealed class PdfViewerHostObject : IPdfViewerHostObject
        {
            private readonly PdfViewer _owner;

            public PdfViewerHostObject(PdfViewer owner)
            {
                _owner = owner ?? throw new ArgumentNullException(nameof(owner));
            }

            public Task<string?> LoadPdfAsync()
            {
                Trace.TraceInformation("PdfViewerHostObject.LoadPdfAsync invoked");
                return InvokeAsync(async viewModel =>
                {
                    Trace.TraceInformation("PdfViewerHostObject delegating LoadPdfAsync to view model");
                    var result = await viewModel.LoadPdfAsync().ConfigureAwait(true);
                    Trace.TraceInformation(
                        "PdfViewerHostObject.LoadPdfAsync returning {0}",
                        string.IsNullOrEmpty(result) ? "(null/empty)" : DescribeForLog(result));
                    return result;
                });
            }

            public Task<string?> CreateHighlightAsync(string payloadJson)
            {
                Trace.TraceInformation(
                    "PdfViewerHostObject.CreateHighlightAsync invoked (payload length={0})",
                    payloadJson?.Length ?? 0);
                return InvokeAsync(async viewModel =>
                {
                    var payload = payloadJson ?? string.Empty;
                    Trace.TraceInformation(
                        "PdfViewerHostObject forwarding highlight payload: {0}",
                        DescribeForLog(payload));
                    var result = await viewModel.CreateHighlightAsync(payload).ConfigureAwait(true);
                    Trace.TraceInformation(
                        "PdfViewerHostObject.CreateHighlightAsync result: {0}",
                        string.IsNullOrEmpty(result) ? "(null/empty)" : DescribeForLog(result));
                    return result;
                });
            }

            public Task<string?> GetCurrentSelectionAsync()
            {
                Trace.TraceInformation("PdfViewerHostObject.GetCurrentSelectionAsync invoked");
                return InvokeAsync(async viewModel =>
                {
                    var snapshot = await viewModel.GetCurrentSelectionAsync().ConfigureAwait(true);
                    Trace.TraceInformation(
                        "PdfViewerHostObject returning selection snapshot: {0}",
                        string.IsNullOrEmpty(snapshot) ? "(null/empty)" : DescribeForLog(snapshot));
                    return snapshot;
                });
            }

            public Task SetOverlayAsync(string payloadJson)
            {
                Trace.TraceInformation(
                    "PdfViewerHostObject.SetOverlayAsync invoked (payload length={0})",
                    payloadJson?.Length ?? 0);
                return InvokeAsync(async viewModel =>
                {
                    var payload = payloadJson ?? string.Empty;
                    Trace.TraceInformation(
                        "PdfViewerHostObject forwarding overlay payload: {0}",
                        DescribeForLog(payload));
                    await viewModel.SetOverlayAsync(payload).ConfigureAwait(true);
                    Trace.TraceInformation("PdfViewerHostObject.SetOverlayAsync completed");
                    return (string?)null;
                });
            }

            private Task<TResult?> InvokeAsync<TResult>(Func<PdfViewerViewModel, Task<TResult?>> callback)
            {
                var dispatcher = _owner.PdfWebView.Dispatcher;
                if (dispatcher.CheckAccess())
                {
                    return ExecuteAsync(callback);
                }

                return dispatcher.InvokeAsync(() => ExecuteAsync(callback)).Task.Unwrap();
            }

            private async Task<TResult?> ExecuteAsync<TResult>(Func<PdfViewerViewModel, Task<TResult?>> callback)
            {
                var viewModel = _owner._viewModel;
                if (viewModel is null)
                {
                    return default;
                }

                try
                {
                    return await callback(viewModel).ConfigureAwait(true);
                }
                catch (Exception ex)
                {
                    Trace.TraceError("Host bridge invocation failed: {0}", ex);
                    return default;
                }
            }
        }
    }
}

