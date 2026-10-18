using Microsoft.Win32;
using PhotoEditorApp.Models;
using PhotoEditorApp.Services;
using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace PhotoEditorApp.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private readonly IImageProcessor _imageProcessor;
        private readonly EditHistory _history;

        private BitmapSource _currentImage;
        public BitmapSource CurrentImage
        {
            get => _currentImage;
            set
            {
                _currentImage = value;
                OnPropertyChanged();
                ((RelayCommand)SaveCommand).CanExecuteChanged -= null; // Force UI update
                ((RelayCommand)GrayscaleCommand).CanExecuteChanged -= null;
            }
        }

        private bool _isBusy;
        public bool IsBusy
        {
            get => _isBusy;
            set { _isBusy = value; OnPropertyChanged(); }
        }

        public ICommand OpenCommand { get; }
        public ICommand SaveCommand { get; }
        public ICommand UndoCommand { get; }
        public ICommand GrayscaleCommand { get; }

        public MainViewModel()
        {
            // Dalam arsitektur asli, gunakan Dependency Injection (misal: IServiceProvider)
            _imageProcessor = new ImageProcessor();
            _history = new EditHistory(10);

            OpenCommand = new RelayCommand(async _ => await OpenImageAsync());
            SaveCommand = new RelayCommand(async _ => await SaveImageAsync(), _ => CurrentImage != null);
            UndoCommand = new RelayCommand(_ => PerformUndo(), _ => _history.CanUndo);
            GrayscaleCommand = new RelayCommand(async _ => await ApplyGrayscaleAsync(), _ => CurrentImage != null);
        }

        private async Task OpenImageAsync()
        {
            var openFileDialog = new OpenFileDialog
            {
                Filter = "Image Files (*.jpg;*.png)|*.jpg;*.png|All Files (*.*)|*.*"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                IsBusy = true;
                try
                {
                    // Load gambar secara asinkron untuk mencegah UI terblokir pada file besar
                    var path = openFileDialog.FileName;
                    CurrentImage = await Task.Run(() =>
                    {
                        var bitmap = new BitmapImage();
                        bitmap.BeginInit();
                        bitmap.CacheOption = BitmapCacheOption.OnLoad; // Penting agar file tidak dilock oleh sistem
                        bitmap.UriSource = new Uri(path);
                        bitmap.EndInit();
                        bitmap.Freeze(); // Wajib di-freeze
                        return bitmap;
                    });
                    _history.Clear();
                }
                finally
                {
                    IsBusy = false;
                }
            }
        }

        private async Task ApplyGrayscaleAsync()
        {
            if (CurrentImage == null) return;

            IsBusy = true;
            try
            {
                // Simpan state saat ini ke memori sebelum dimodifikasi
                _history.Push(CurrentImage);
                
                CurrentImage = await _imageProcessor.ApplyGrayscaleAsync(CurrentImage);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void PerformUndo()
        {
            if (_history.CanUndo)
            {
                CurrentImage = _history.Pop();
            }
        }

        private async Task SaveImageAsync()
        {
            if (CurrentImage == null) return;

            var saveFileDialog = new SaveFileDialog
            {
                Filter = "JPEG Image (*.jpg)|*.jpg|PNG Image (*.png)|*.png",
                DefaultExt = ".jpg"
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                IsBusy = true;
                var path = saveFileDialog.FileName;
                var format = Path.GetExtension(path).ToLower() == ".png" 
                             ? (BitmapEncoder)new PngBitmapEncoder() 
                             : new JpegBitmapEncoder { QualityLevel = 90 };

                await Task.Run(() =>
                {
                    format.Frames.Add(BitmapFrame.Create(CurrentImage));
                    using (var fileStream = new FileStream(path, FileMode.Create))
                    {
                        format.Save(fileStream);
                    }
                });
                IsBusy = false;
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
