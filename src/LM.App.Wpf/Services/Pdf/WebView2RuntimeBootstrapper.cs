using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Web.WebView2.Core;

namespace LM.App.Wpf.Services.Pdf
{
    /// <summary>
    /// Ensures the WebView2 native loader is available before instantiating the PDF viewer.
    /// </summary>
    internal static class WebView2RuntimeBootstrapper
    {
        private static readonly object SyncRoot = new();
        private static bool _initialized;
        private static IntPtr _loaderHandle = IntPtr.Zero;

        public static bool TryEnsureRuntime(out string? errorMessage)
        {
            errorMessage = null;

            if (!OperatingSystem.IsWindows())
            {
                return true;
            }

            lock (SyncRoot)
            {
                if (!_initialized)
                {
                    if (!TryLoadNativeLoader(out errorMessage))
                    {
                        return false;
                    }

                    _initialized = true;
                }
            }

            try
            {
                _ = CoreWebView2Environment.GetAvailableBrowserVersionString();
                return true;
            }
            catch (WebView2RuntimeNotFoundException)
            {
                errorMessage = "Microsoft Edge WebView2 Runtime is not installed on this machine. " +
                               "Install the runtime from https://go.microsoft.com/fwlink/p/?LinkId=2124703 and try again.";
                return false;
            }
            catch (DllNotFoundException ex)
            {
                errorMessage = $"WebView2 failed to load its native components: {ex.Message}";
                return false;
            }
            catch (BadImageFormatException ex)
            {
                errorMessage = $"WebView2 runtime architecture mismatch detected: {ex.Message}";
                return false;
            }
        }

        private static bool TryLoadNativeLoader(out string? errorMessage)
        {
            errorMessage = null;

            if (_loaderHandle != IntPtr.Zero)
            {
                return true;
            }

            if (TryLoadHandle("WebView2Loader.dll", out _loaderHandle, out errorMessage))
            {
                return true;
            }

            if (!string.IsNullOrEmpty(errorMessage))
            {
                errorMessage = $"Unable to load WebView2Loader.dll: {errorMessage}";
                return false;
            }

            var baseDirectory = AppContext.BaseDirectory;
            var architecture = RuntimeInformation.ProcessArchitecture;

            foreach (var candidate in EnumerateLoaderProbePaths(baseDirectory, architecture))
            {
                if (!File.Exists(candidate))
                {
                    continue;
                }

                if (TryLoadHandle(candidate, out _loaderHandle, out errorMessage))
                {
                    return true;
                }

                if (!string.IsNullOrEmpty(errorMessage))
                {
                    errorMessage = $"Unable to load WebView2Loader.dll from '{candidate}': {errorMessage}";
                    return false;
                }
            }

            errorMessage = "Unable to locate WebView2Loader.dll in the application directory.";
            return false;
        }

        private static bool TryLoadHandle(string path, out IntPtr handle, out string? errorMessage)
        {
            errorMessage = null;
            handle = IntPtr.Zero;

            try
            {
                if (NativeLibrary.TryLoad(path, out handle))
                {
                    return true;
                }
            }
            catch (BadImageFormatException ex)
            {
                errorMessage = ex.Message;
                return false;
            }
            catch (DllNotFoundException)
            {
                // Fall through to return false so that other locations can be probed.
            }

            return false;
        }

        internal static IEnumerable<string> EnumerateLoaderProbePaths(string? baseDirectory, Architecture architecture)
        {
            if (string.IsNullOrWhiteSpace(baseDirectory))
            {
                yield break;
            }

            var trimmed = baseDirectory.Trim();
            var archFolder = architecture switch
            {
                Architecture.X64 => "win-x64",
                Architecture.X86 => "win-x86",
                Architecture.Arm64 => "win-arm64",
                _ => null
            };

            if (archFolder is not null)
            {
                yield return Path.Combine(trimmed, "runtimes", archFolder, "native", "WebView2Loader.dll");
            }

            yield return Path.Combine(trimmed, "WebView2Loader.dll");
        }
    }
}
