using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using PhotoEditorApp.Services;

namespace PhotoEditorApp.ViewModels;

public class MainViewModel : INotifyPropertyChanged
{
    private WriteableBitmap? _displayImage;
    private byte[]? _basePixels;
    private byte[]? _currentPixels;
    private int _pixelWidth;
    private int _pixelHeight;
    private int _stride;

    private readonly Stack<byte[]> _undoStack = new();
    private readonly Stack<byte[]> _redoStack = new();

    private int _brightness = 0;
    private double _contrast = 0;
    private bool _isProcessing = false;

    public WriteableBitmap? DisplayImage
    {
        get => _displayImage;
        set { _displayImage = value; OnPropertyChanged(); }
    }

    public bool IsProcessing
    {
        get => _isProcessing;
        set { _isProcessing = value; OnPropertyChanged(); }
    }

    public int Brightness
    {
        get => _brightness;
        set { _brightness = value; OnPropertyChanged(); _ = UpdateAdjustmentsAsync(); }
    }

    public double Contrast
    {
        get => _contrast;
        set { _contrast = value; OnPropertyChanged(); _ = UpdateAdjustmentsAsync(); }
    }

    public ICommand LoadCommand { get; }
    public ICommand SaveCommand { get; }
    public ICommand GrayscaleCommand { get; }
    public ICommand InvertCommand { get; }
    public ICommand BlurCommand { get; }
    public ICommand UndoCommand { get; }
    public ICommand RedoCommand { get; }
    public ICommand CommitAdjustmentsCommand { get; }

    public MainViewModel()
    {
        LoadCommand = new RelayCommand(_ => LoadImage());
        SaveCommand = new RelayCommand(_ => SaveImage(), _ => _currentPixels != null);
        GrayscaleCommand = new RelayCommand(async _ => await ApplyFilterAsync(ImageProcessor.ApplyGrayscaleAsync), _ => _currentPixels != null);
        InvertCommand = new RelayCommand(async _ => await ApplyFilterAsync(ImageProcessor.ApplyInvertAsync), _ => _currentPixels != null);
        BlurCommand = new RelayCommand(async _ => await ApplyFilterAsync(p => ImageProcessor.ApplyBlurAsync(p, _pixelWidth, _pixelHeight)), _ => _currentPixels != null);
        
        UndoCommand = new RelayCommand(_ => PerformUndo(), _ => _undoStack.Count > 0);
        RedoCommand = new RelayCommand(_ => PerformRedo(), _ => _redoStack.Count > 0);
        
        CommitAdjustmentsCommand = new RelayCommand(_ => CommitChanges()); 
    }

    private void LoadImage()
    {
        var dlg = new OpenFileDialog { Filter = "Image Files|*.jpg;*.jpeg;*.png;*.bmp" };
        if (dlg.ShowDialog() == true)
        {
            using var stream = new FileStream(dlg.FileName, FileMode.Open, FileAccess.Read);
            var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
            var bitmap = decoder.Frames[0];

            var formattedBitmap = new FormatConvertedBitmap(bitmap, PixelFormats.Bgra32, null, 0);
            
            _pixelWidth = formattedBitmap.PixelWidth;
            _pixelHeight = formattedBitmap.PixelHeight;
            _stride = _pixelWidth * 4;

            _basePixels = new byte[_pixelHeight * _stride];
            formattedBitmap.CopyPixels(_basePixels, _stride, 0);
            
            _currentPixels = (byte[])_basePixels.Clone();
            _undoStack.Clear();
            _redoStack.Clear();
            
            UpdateDisplayImage(_currentPixels);
            ResetSliders();
        }
    }

    private void SaveImage()
    {
        if (DisplayImage == null) return;
        var dlg = new SaveFileDialog { Filter = "PNG Image|*.png|JPEG Image|*.jpg", DefaultExt = ".png" };
        if (dlg.ShowDialog() == true)
        {
            BitmapEncoder encoder = dlg.FileName.EndsWith(".jpg") ? new JpegBitmapEncoder() : new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(DisplayImage));
            using var stream = new FileStream(dlg.FileName, FileMode.Create);
            encoder.Save(stream);
        }
    }

    private async Task UpdateAdjustmentsAsync()
    {
        if (_basePixels == null || IsProcessing) return;
        IsProcessing = true;
        try
        {
            _currentPixels = await ImageProcessor.ApplyAdjustmentsAsync(_basePixels, Brightness, Contrast);
            UpdateDisplayImage(_currentPixels);
        }
        finally { IsProcessing = false; }
    }

    private void CommitChanges()
    {
        if (_basePixels == null || _currentPixels == null) return;
        _undoStack.Push((byte[])_basePixels.Clone());
        _redoStack.Clear();
        _basePixels = (byte[])_currentPixels.Clone();
        ResetSliders();
    }

    private async Task ApplyFilterAsync(Func<byte[], Task<byte[]>> filterFunc)
    {
        if (_basePixels == null) return;
        IsProcessing = true;
        try
        {
            _undoStack.Push((byte[])_basePixels.Clone());
            _redoStack.Clear();
            _basePixels = await filterFunc(_basePixels);
            _currentPixels = (byte[])_basePixels.Clone();
            UpdateDisplayImage(_currentPixels);
            ResetSliders();
        }
        finally { IsProcessing = false; }
    }

    private void PerformUndo()
    {
        if (_undoStack.Count > 0 && _basePixels != null)
        {
            _redoStack.Push((byte[])_basePixels.Clone());
            _basePixels = _undoStack.Pop();
            _currentPixels = (byte[])_basePixels.Clone();
            UpdateDisplayImage(_currentPixels);
            ResetSliders();
        }
    }

    private void PerformRedo()
    {
        if (_redoStack.Count > 0 && _basePixels != null)
        {
            _undoStack.Push((byte[])_basePixels.Clone());
            _basePixels = _redoStack.Pop();
            _currentPixels = (byte[])_basePixels.Clone();
            UpdateDisplayImage(_currentPixels);
            ResetSliders();
        }
    }

    private void UpdateDisplayImage(byte[] pixels)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            var bitmap = new WriteableBitmap(_pixelWidth, _pixelHeight, 96, 96, PixelFormats.Bgra32, null);
            bitmap.WritePixels(new Int32Rect(0, 0, _pixelWidth, _pixelHeight), pixels, _stride, 0);
            DisplayImage = bitmap;
        });
    }

    private void ResetSliders()
    {
        _brightness = 0;
        _contrast = 0;
        OnPropertyChanged(nameof(Brightness));
        OnPropertyChanged(nameof(Contrast));
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
