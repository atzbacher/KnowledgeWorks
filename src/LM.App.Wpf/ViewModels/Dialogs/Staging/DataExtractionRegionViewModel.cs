#nullable enable

using System;
using CommunityToolkit.Mvvm.ComponentModel;
using HookM = LM.HubSpoke.Models;

namespace LM.App.Wpf.ViewModels.Dialogs.Staging
{
    internal sealed class DataExtractionRegionViewModel : ObservableObject
    {
        private int _pageNumber;
        private double _x;
        private double _y;
        private double _width = 0.25;
        private double _height = 0.25;
        private string? _label;

        public int PageNumber
        {
            get => _pageNumber;
            set => SetProperty(ref _pageNumber, Math.Max(1, value));
        }

        public double X
        {
            get => _x;
            set => SetProperty(ref _x, Clamp01(value));
        }

        public double Y
        {
            get => _y;
            set => SetProperty(ref _y, Clamp01(value));
        }

        public double Width
        {
            get => _width;
            set => SetProperty(ref _width, Clamp01(value));
        }

        public double Height
        {
            get => _height;
            set => SetProperty(ref _height, Clamp01(value));
        }

        public string? Label
        {
            get => _label;
            set => SetProperty(ref _label, value);
        }

        public HookM.DataExtractionRegion ToHookModel()
        {
            return new HookM.DataExtractionRegion
            {
                PageNumber = PageNumber,
                X = X,
                Y = Y,
                Width = Width,
                Height = Height,
                Label = Label
            };
        }

        public void Load(HookM.DataExtractionRegion region)
        {
            if (region is null)
                throw new ArgumentNullException(nameof(region));

            PageNumber = region.PageNumber;
            X = region.X;
            Y = region.Y;
            Width = region.Width;
            Height = region.Height;
            Label = region.Label;
        }

        public void Apply(int pageNumber, double x, double y, double width, double height)
        {
            PageNumber = pageNumber;
            X = x;
            Y = y;
            Width = width;
            Height = height;
        }

        private static double Clamp01(double value)
        {
            if (double.IsNaN(value))
                return 0d;
            if (value < 0d)
                return 0d;
            if (value > 1d)
                return 1d;
            return value;
        }
    }
}
