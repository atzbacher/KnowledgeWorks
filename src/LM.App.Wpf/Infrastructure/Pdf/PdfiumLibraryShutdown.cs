#nullable enable
using System;
using System.Reflection;
using PdfiumViewer.Core;

namespace LM.App.Wpf.Infrastructure.Pdf;

internal static class PdfiumLibraryShutdown
{
    private static readonly Lazy<Action> ReleaseLibrary = new(CreateReleaseAction);
    private static readonly object ReleaseGate = new();
    private static bool _isReleased;

    public static void Release()
    {
        lock (ReleaseGate)
        {
            if (_isReleased)
            {
                return;
            }

            ReleaseLibrary.Value();
            _isReleased = true;
        }
    }

    private static Action CreateReleaseAction()
    {
        var libraryType = typeof(PdfDocument).Assembly.GetType("PdfiumViewer.Core.PdfLibrary");
        if (libraryType is null)
        {
            return static () => { };
        }

        var instanceField = libraryType.GetField("_library", BindingFlags.Static | BindingFlags.NonPublic);
        var disposeMethod = libraryType.GetMethod("Dispose", BindingFlags.Instance | BindingFlags.Public);

        if (instanceField is null || disposeMethod is null)
        {
            return static () => { };
        }

        return () =>
        {
            var instance = instanceField.GetValue(null);
            if (instance is null)
            {
                return;
            }

            try
            {
                disposeMethod.Invoke(instance, Array.Empty<object>());
                GC.SuppressFinalize(instance);
            }
            catch
            {
                // Swallow exceptions to avoid crash on shutdown.
            }
            finally
            {
                instanceField.SetValue(null, null);
            }
        };
    }
}
