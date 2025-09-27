#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HookM = LM.HubSpoke.Models;

namespace LM.App.Wpf.ViewModels.Dialogs.Staging
{
    internal enum DataExtractionAssetKind
    {
        Table,
        Figure
    }

    internal sealed partial class DataExtractionAssetViewModel : ObservableObject
    {
        private readonly DataExtractionAssetKind _kind;
        private string _title;
        private string? _caption;
        private string _pages;
        private string? _sourcePath;
        private string? _provenanceHash;
        private string? _tags;
        private int? _columnHint;
        private string? _dictionaryPath;
        private string? _figureLabel;
        private string? _thumbnailPath;
        private string? _imagePath;

        public DataExtractionAssetViewModel(DataExtractionAssetKind kind,
                                            string id,
                                            string title)
        {
            _kind = kind;
            Id = id ?? throw new ArgumentNullException(nameof(id));
            _title = string.IsNullOrWhiteSpace(title) ? (kind == DataExtractionAssetKind.Table ? "Table" : "Figure") : title;
            _pages = string.Empty;
            Regions = new ObservableCollection<DataExtractionRegionViewModel>();
        }

        public string Id { get; }

        public DataExtractionAssetKind Kind => _kind;

        public ObservableCollection<DataExtractionRegionViewModel> Regions { get; }

        public string Title
        {
            get => _title;
            set => SetProperty(ref _title, value);
        }

        public string? Caption
        {
            get => _caption;
            set => SetProperty(ref _caption, value);
        }

        public string Pages
        {
            get => _pages;
            set => SetProperty(ref _pages, value);
        }

        public string? SourcePath
        {
            get => _sourcePath;
            set => SetProperty(ref _sourcePath, value);
        }

        public string? ProvenanceHash
        {
            get => _provenanceHash;
            set => SetProperty(ref _provenanceHash, value);
        }

        public string? Tags
        {
            get => _tags;
            set => SetProperty(ref _tags, value);
        }

        public int? ColumnHint
        {
            get => _columnHint;
            set => SetProperty(ref _columnHint, value);
        }

        public string? DictionaryPath
        {
            get => _dictionaryPath;
            set => SetProperty(ref _dictionaryPath, value);
        }

        public string? FigureLabel
        {
            get => _figureLabel;
            set => SetProperty(ref _figureLabel, value);
        }

        public string? ThumbnailPath
        {
            get => _thumbnailPath;
            set => SetProperty(ref _thumbnailPath, value);
        }

        public string? ImagePath
        {
            get => _imagePath;
            set => SetProperty(ref _imagePath, value);
        }

        public string DisplayName => _kind == DataExtractionAssetKind.Table
            ? FormattableString.Invariant($"Table · {Title}")
            : FormattableString.Invariant($"Figure · {Title}");

        public static DataExtractionAssetViewModel FromTable(HookM.DataExtractionTable table)
        {
            if (table is null)
                throw new ArgumentNullException(nameof(table));

            var vm = new DataExtractionAssetViewModel(DataExtractionAssetKind.Table, table.Id, table.Title)
            {
                Caption = table.Caption,
                Pages = string.Join(", ", table.Pages),
                SourcePath = table.SourcePath,
                ProvenanceHash = table.ProvenanceHash,
                Tags = string.Join(", ", table.Tags),
                ColumnHint = table.ColumnCountHint,
                DictionaryPath = table.DictionaryPath
            };

            foreach (var region in table.Regions)
            {
                var regionVm = new DataExtractionRegionViewModel();
                regionVm.Load(region);
                vm.Regions.Add(regionVm);
            }

            return vm;
        }

        public static DataExtractionAssetViewModel FromFigure(HookM.DataExtractionFigure figure)
        {
            if (figure is null)
                throw new ArgumentNullException(nameof(figure));

            var vm = new DataExtractionAssetViewModel(DataExtractionAssetKind.Figure, figure.Id, figure.Title)
            {
                Caption = figure.Caption,
                Pages = string.Join(", ", figure.Pages),
                SourcePath = figure.SourcePath,
                ProvenanceHash = figure.ProvenanceHash,
                Tags = string.Join(", ", figure.Tags),
                FigureLabel = figure.FigureLabel,
                ThumbnailPath = figure.ThumbnailPath,
                ImagePath = figure.ImagePath
            };

            foreach (var region in figure.Regions)
            {
                var regionVm = new DataExtractionRegionViewModel();
                regionVm.Load(region);
                vm.Regions.Add(regionVm);
            }

            return vm;
        }

        public HookM.DataExtractionTable ToTableHook()
        {
            if (_kind != DataExtractionAssetKind.Table)
                throw new InvalidOperationException("Asset is not a table.");

            return new HookM.DataExtractionTable
            {
                Id = Id,
                Title = Title,
                Caption = Caption,
                Pages = SplitCsv(Pages),
                SourcePath = SourcePath,
                ProvenanceHash = ProvenanceHash ?? string.Empty,
                Regions = Regions.Select(static r => r.ToHookModel()).ToList(),
                Tags = SplitCsv(Tags),
                ColumnCountHint = ColumnHint,
                DictionaryPath = DictionaryPath,
                TableLabel = Caption,
                Summary = Caption
            };
        }

        public HookM.DataExtractionFigure ToFigureHook()
        {
            if (_kind != DataExtractionAssetKind.Figure)
                throw new InvalidOperationException("Asset is not a figure.");

            return new HookM.DataExtractionFigure
            {
                Id = Id,
                Title = Title,
                Caption = Caption,
                Pages = SplitCsv(Pages),
                SourcePath = SourcePath,
                ProvenanceHash = ProvenanceHash ?? string.Empty,
                Regions = Regions.Select(static r => r.ToHookModel()).ToList(),
                Tags = SplitCsv(Tags),
                FigureLabel = FigureLabel,
                ThumbnailPath = ThumbnailPath,
                ImagePath = ImagePath
            };
        }

        [RelayCommand]
        private void AddRegion()
        {
            var region = new DataExtractionRegionViewModel
            {
                PageNumber = 1,
                X = 0.1,
                Y = 0.1,
                Width = 0.5,
                Height = 0.4
            };

            region.Label = GetRegionLabel(Regions.Count + 1);

            Regions.Add(region);
        }

        [RelayCommand(CanExecute = nameof(CanRemoveRegion))]
        private void RemoveRegion(DataExtractionRegionViewModel? region)
        {
            if (region is null)
                return;

            Regions.Remove(region);
        }

        private bool CanRemoveRegion(DataExtractionRegionViewModel? region)
            => region is not null && Regions.Contains(region);

        internal DataExtractionRegionViewModel CreateRegion(int pageNumber, double x, double y, double width, double height)
        {
            var region = new DataExtractionRegionViewModel();
            region.Apply(pageNumber, x, y, width, height);
            region.Label = GetRegionLabel(Regions.Count + 1);
            Regions.Add(region);
            return region;
        }

        private string GetRegionLabel(int index)
        {
            return _kind == DataExtractionAssetKind.Table
                ? FormattableString.Invariant($"Table {index}")
                : FormattableString.Invariant($"Figure {index}");
        }

        private static List<string> SplitCsv(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return new List<string>();

            return value
                .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(static part => part.Trim())
                .Where(static part => !string.IsNullOrEmpty(part))
                .ToList();
        }
    }
}
