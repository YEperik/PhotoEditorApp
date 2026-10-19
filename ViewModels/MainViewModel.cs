using Microsoft.Win32;
using PhotoEditorApp.Models;
using PhotoEditorApp.Services;
using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace PhotoEditorApp.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private readonly IImageProcessor _processor;
        private readonly EditHistory _history;
        private CancellationTokenSource _sliderCancellationTokenSource;
        private BitmapSource _baseImage; 

        private BitmapSource _currentImage;
        public BitmapSource CurrentImage
        {
            get => _currentImage;
            set { _currentImage = value; OnPropertyChanged(); ForceCommandUpdate(); }
        }

        private bool _isBusy;
        public bool IsBusy
        {
            get => _isBusy;
            set { _isBusy = value; OnPropertyChanged(); }
        }

        private double _brightness;
        public double Brightness
        {
            get => _brightness;
            set { if (_brightness != value) { _brightness = value; OnPropertyChanged(); TriggerAdjustment(); } }
        }

        private double _contrast;
        public double Contrast
        {
            get => _contrast;
            set { if (_contrast != value) { _contrast = value; OnPropertyChanged(); TriggerAdjustment(); } }
        }

        public ICommand OpenCommand { get; }
        public ICommand SaveCommand { get; }
        public ICommand UndoCommand { get; }
        public ICommand ApplyCommand { get; }
        public ICommand GrayscaleCommand { get; }
        public ICommand SepiaCommand { get; }
        public ICommand InvertCommand { get; }

        public MainViewModel()
        {
            _processor = new ImageProcessor();
            _history = new EditHistory(10);

            OpenCommand = new RelayCommand(async _ => await OpenImageAsync());
            SaveCommand = new RelayCommand(async _ => await SaveImageAsync(), _ => CurrentImage != null);
            UndoCommand = new RelayCommand(_ => PerformUndo(), _ => _history.CanUndo);
            ApplyCommand = new RelayCommand(_ => ApplyAdjustments(), _ => CurrentImage != null && (_brightness != 0 || _contrast != 0));

            GrayscaleCommand = new RelayCommand(async _ => await ApplyFilterAsync(_processor.ApplyGrayscaleAsync), _ => CurrentImage != null);
            SepiaCommand = new RelayCommand(async _ => await ApplyFilterAsync(_processor.ApplySepiaAsync), _ => CurrentImage != null);
            InvertCommand = new RelayCommand(async _ => await ApplyFilterAsync(_processor.ApplyInvertAsync), _ => CurrentImage != null);
        }

        private async void TriggerAdjustment()
        {
            if (_baseImage == null) return;
            _sliderCancellationTokenSource?.Cancel();
            _sliderCancellationTokenSource = new CancellationTokenSource();
            var token = _sliderCancellationTokenSource.Token;

            try
            {
                IsBusy = true;
                await Task.Delay(150, token);
                if (!token.IsCancellationRequested)
                {
                    var result = await _processor.AdjustBrightnessContrastAsync(_baseImage, Brightness, Contrast);
                    if (!token.IsCancellationRequested) CurrentImage = result;
                }
            }
            catch (TaskCanceledException) { }
            finally { IsBusy = false; }
        }

        private void ApplyAdjustments()
        {
            if (_baseImage == null) return;
            _history.Push(_baseImage);
            _baseImage = CurrentImage;
            Brightness = 0;
            Contrast = 0;
            ForceCommandUpdate();
        }

        private async Task ApplyFilterAsync(Func<BitmapSource, Task<BitmapSource>> filterMethod)
        {
            if (CurrentImage == null) return;
            IsBusy = true;
            try
            {
                var imageToProcess = _baseImage ?? CurrentImage;
                _history.Push(imageToProcess);
                var result = await filterMethod(imageToProcess);
                _baseImage = result;
                CurrentImage = result;
            }
            finally { IsBusy = false; }
        }

        private async Task OpenImageAsync()
        {
            var dialog = new OpenFileDialog { Filter = "Images|*.jpg;*.png;*.jpeg|All Files|*.*" };
            if (dialog.ShowDialog() == true)
            {
                IsBusy = true;
                try
                {
                    var path = dialog.FileName;
                    var bitmap = await Task.Run(() =>
                    {
                        var bmp = new BitmapImage();
                        bmp.BeginInit();
                        bmp.CacheOption = BitmapCacheOption.OnLoad;
                        bmp.UriSource = new Uri(path);
                        bmp.EndInit();
                        bmp.Freeze();
                        return bmp;
                    });
                    _baseImage = bitmap;
                    CurrentImage = bitmap;
                    Brightness = 0;
                    Contrast = 0;
                    _history.Clear();
                }
                finally { IsBusy = false; }
            }
        }

        private async Task SaveImageAsync()
        {
            if (CurrentImage == null) return;
            var dialog = new SaveFileDialog { Filter = "JPEG|*.jpg|PNG|*.png", DefaultExt = ".jpg" };
            if (dialog.ShowDialog() == true)
            {
                IsBusy = true;
                var path = dialog.FileName;
                var format = Path.GetExtension(path).ToLower() == ".png" ? (BitmapEncoder)new PngBitmapEncoder() : new JpegBitmapEncoder { QualityLevel = 90 };
                
                await Task.Run(() =>
                {
                    format.Frames.Add(BitmapFrame.Create(CurrentImage));
                    using (var fs = new FileStream(path, FileMode.Create)) format.Save(fs);
                });
                IsBusy = false;
            }
        }

        private void PerformUndo()
        {
            if (_history.CanUndo)
            {
                var prev = _history.Pop();
                _baseImage = prev;
                CurrentImage = prev;
                Brightness = 0;
                Contrast = 0;
            }
        }

        private void ForceCommandUpdate()
        {
            ((RelayCommand)SaveCommand).CanExecuteChanged -= null;
            ((RelayCommand)ApplyCommand).CanExecuteChanged -= null;
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
