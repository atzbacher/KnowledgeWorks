#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LM.Core.Models.DataExtraction;
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
        private string? _friendlyName;
        private string? _notes;
        private string? _linkedEndpointIds;
        private string? _linkedInterventionIds;
        private string? _tableImagePath;
        private string? _imageProvenanceHash;

        public DataExtractionAssetViewModel(DataExtractionAssetKind kind,
                                            string id,
                                            string title)
        {
            _kind = kind;
            Id = id ?? throw new ArgumentNullException(nameof(id));
            _title = string.IsNullOrWhiteSpace(title) ? (kind == DataExtractionAssetKind.Table ? "Table" : "Figure") : title;
            _pages = string.Empty;
            Regions = new ObservableCollection<DataExtractionRegionViewModel>();
            PagePositions = new ObservableCollection<DataExtractionPagePositionViewModel>();
            _friendlyName = kind == DataExtractionAssetKind.Table ? _title : null;
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

        public string? FriendlyName
        {
            get => _friendlyName;
            set => SetProperty(ref _friendlyName, value);
        }

        public string? Notes
        {
            get => _notes;
            set => SetProperty(ref _notes, value);
        }

        public string? LinkedEndpointIds
        {
            get => _linkedEndpointIds;
            set => SetProperty(ref _linkedEndpointIds, value);
        }

        public string? LinkedInterventionIds
        {
            get => _linkedInterventionIds;
            set => SetProperty(ref _linkedInterventionIds, value);
        }

        public string? TableImagePath
        {
            get => _tableImagePath;
            set => SetProperty(ref _tableImagePath, value);
        }

        public string? ImageProvenanceHash
        {
            get => _imageProvenanceHash;
            set => SetProperty(ref _imageProvenanceHash, value);
        }

        public ObservableCollection<DataExtractionPagePositionViewModel> PagePositions { get; }

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
                DictionaryPath = table.DictionaryPath,
                FriendlyName = table.FriendlyName,
                Notes = table.Notes,
                LinkedEndpointIds = string.Join(", ", table.LinkedEndpointIds),
                LinkedInterventionIds = string.Join(", ", table.LinkedInterventionIds),
                TableImagePath = table.ImagePath,
                ImageProvenanceHash = table.ImageProvenanceHash
            };

            foreach (var region in table.Regions)
            {
                var regionVm = new DataExtractionRegionViewModel();
                regionVm.Load(region);
                vm.Regions.Add(regionVm);
            }

            foreach (var position in table.PagePositions)
            {
                var positionVm = new DataExtractionPagePositionViewModel();
                positionVm.Load(position);
                vm.PagePositions.Add(positionVm);
            }

            return vm;
        }

        internal void ApplyExtraction(PreprocessedTable table)
        {
            if (table is null)
                throw new ArgumentNullException(nameof(table));
            if (_kind != DataExtractionAssetKind.Table)
                throw new InvalidOperationException("Extraction results can only be applied to tables.");

            if (!string.IsNullOrWhiteSpace(table.Title))
            {
                Title = table.Title;
            }

            Caption = table.Classification.ToString();
            Pages = string.Join(", ", table.PageNumbers.Select(static p => p.ToString(CultureInfo.InvariantCulture)));
            SourcePath = table.CsvRelativePath;
            ProvenanceHash = table.ProvenanceHash;
            TableImagePath = table.ImageRelativePath;
            ImageProvenanceHash = table.ImageProvenanceHash;
            Tags = string.Join(", ", table.Tags);

            if (!string.IsNullOrWhiteSpace(table.FriendlyName))
            {
                FriendlyName = table.FriendlyName;
            }

            ColumnHint = table.Columns.Count > 0 ? table.Columns.Count : null;

            if (table.Regions.Count > 0)
            {
                Regions.Clear();
                foreach (var region in table.Regions)
                {
                    var created = CreateRegion(region.PageNumber, region.X, region.Y, region.Width, region.Height);
                    if (!string.IsNullOrWhiteSpace(region.Label))
                    {
                        created.Label = region.Label;
                    }
                }
            }

            if (table.PageLocations.Count > 0)
            {
                PagePositions.Clear();
                foreach (var location in table.PageLocations)
                {
                    var vm = new DataExtractionPagePositionViewModel
                    {
                        PageNumber = location.PageNumber,
                        Left = location.Left,
                        Top = location.Top,
                        Width = location.Width,
                        Height = location.Height,
                        PageWidth = location.PageWidth,
                        PageHeight = location.PageHeight
                    };

                    PagePositions.Add(vm);
                }
            }
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
                ImagePath = figure.ImagePath,
                Notes = figure.Notes,
                LinkedEndpointIds = string.Join(", ", figure.LinkedEndpointIds),
                LinkedInterventionIds = string.Join(", ", figure.LinkedInterventionIds)
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
                Summary = Caption,
                FriendlyName = string.IsNullOrWhiteSpace(FriendlyName) ? null : FriendlyName,
                Notes = Notes,
                LinkedEndpointIds = SplitCsv(LinkedEndpointIds),
                LinkedInterventionIds = SplitCsv(LinkedInterventionIds),
                ImagePath = TableImagePath,
                ImageProvenanceHash = ImageProvenanceHash,
                PagePositions = PagePositions.Select(static p => p.ToHookModel()).ToList()
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
                ImagePath = ImagePath,
                Notes = Notes,
                LinkedEndpointIds = SplitCsv(LinkedEndpointIds),
                LinkedInterventionIds = SplitCsv(LinkedInterventionIds)
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

    internal sealed class DataExtractionPagePositionViewModel : ObservableObject
    {
        private int _pageNumber;
        private double _left;
        private double _top;
        private double _width;
        private double _height;
        private double _pageWidth;
        private double _pageHeight;

        public int PageNumber
        {
            get => _pageNumber;
            set => SetProperty(ref _pageNumber, value);
        }

        public double Left
        {
            get => _left;
            set => SetProperty(ref _left, value);
        }

        public double Top
        {
            get => _top;
            set => SetProperty(ref _top, value);
        }

        public double Width
        {
            get => _width;
            set => SetProperty(ref _width, value);
        }

        public double Height
        {
            get => _height;
            set => SetProperty(ref _height, value);
        }

        public double PageWidth
        {
            get => _pageWidth;
            set => SetProperty(ref _pageWidth, value);
        }

        public double PageHeight
        {
            get => _pageHeight;
            set => SetProperty(ref _pageHeight, value);
        }

        public void Load(HookM.DataExtractionPagePosition position)
        {
            if (position is null)
                throw new ArgumentNullException(nameof(position));

            PageNumber = position.PageNumber;
            Left = position.Left;
            Top = position.Top;
            Width = position.Width;
            Height = position.Height;
            PageWidth = position.PageWidth;
            PageHeight = position.PageHeight;
        }

        public HookM.DataExtractionPagePosition ToHookModel()
        {
            return new HookM.DataExtractionPagePosition
            {
                PageNumber = PageNumber,
                Left = Left,
                Top = Top,
                Width = Width,
                Height = Height,
                PageWidth = PageWidth,
                PageHeight = PageHeight
            };
        }
    }
}
