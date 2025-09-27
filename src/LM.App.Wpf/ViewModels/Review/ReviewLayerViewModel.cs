namespace LM.App.Wpf.ViewModels.Review;

using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using LM.Review.Core.Models;

internal sealed class ReviewLayerViewModel : INotifyPropertyChanged
{
    private string _name;
    private ReviewLayerKind _kind;
    private ReviewLayerDisplayMode _displayMode;
    private string _fieldsCsv;
    private string? _instructions;

    public event PropertyChangedEventHandler? PropertyChanged;

    public ReviewLayerViewModel(string name, ReviewLayerKind kind, ReviewLayerDisplayMode displayMode, string fieldsCsv, string? instructions)
    {
        _name = string.IsNullOrWhiteSpace(name) ? "Untitled layer" : name;
        _kind = kind;
        _displayMode = displayMode;
        _fieldsCsv = fieldsCsv ?? string.Empty;
        _instructions = instructions;
    }

    public string Name
    {
        get => _name;
        set
        {
            if (!string.Equals(_name, value, StringComparison.Ordinal))
            {
                _name = value ?? string.Empty;
                OnPropertyChanged();
            }
        }
    }

    public ReviewLayerKind Kind
    {
        get => _kind;
        set
        {
            if (_kind != value)
            {
                _kind = value;
                OnPropertyChanged();
            }
        }
    }

    public ReviewLayerDisplayMode DisplayMode
    {
        get => _displayMode;
        set
        {
            if (_displayMode != value)
            {
                _displayMode = value;
                OnPropertyChanged();
            }
        }
    }

    public string FieldsCsv
    {
        get => _fieldsCsv;
        set
        {
            if (!string.Equals(_fieldsCsv, value, StringComparison.Ordinal))
            {
                _fieldsCsv = value ?? string.Empty;
                OnPropertyChanged();
                OnPropertyChanged(nameof(DisplayFieldsPreview));
            }
        }
    }

    public string? Instructions
    {
        get => _instructions;
        set
        {
            if (!string.Equals(_instructions, value, StringComparison.Ordinal))
            {
                _instructions = value;
                OnPropertyChanged();
            }
        }
    }

    public string DisplayFieldsPreview => string.IsNullOrWhiteSpace(_fieldsCsv) ? "No custom fields" : _fieldsCsv;

    public ReviewLayerDefinition ToDefinition()
    {
        var fields = _fieldsCsv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return new ReviewLayerDefinition(_name, _kind, _displayMode, fields, _instructions);
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
