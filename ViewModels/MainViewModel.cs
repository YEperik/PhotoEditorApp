// ... (Using statements sama seperti sebelumnya) ...

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
                
                // Force UI update command bindings
                ((RelayCommand)SaveCommand).CanExecuteChanged -= null; 
                ((RelayCommand)GrayscaleCommand).CanExecuteChanged -= null;
                ((RelayCommand)SepiaCommand).CanExecuteChanged -= null;
                ((RelayCommand)InvertCommand).CanExecuteChanged -= null;
            }
        }

        private bool _isBusy;
        public bool IsBusy
        {
            get => _isBusy;
            set { _isBusy = value; OnPropertyChanged(); }
        }

        // Deklarasi Commands
        public ICommand OpenCommand { get; }
        public ICommand SaveCommand { get; }
        public ICommand UndoCommand { get; }
        public ICommand GrayscaleCommand { get; }
        public ICommand SepiaCommand { get; }
        public ICommand InvertCommand { get; }

        public MainViewModel()
        {
            _imageProcessor = new ImageProcessor();
            _history = new EditHistory(10);

            OpenCommand = new RelayCommand(async _ => await OpenImageAsync());
            SaveCommand = new RelayCommand(async _ => await SaveImageAsync(), _ => CurrentImage != null);
            UndoCommand = new RelayCommand(_ => PerformUndo(), _ => _history.CanUndo);
            
            // Inisialisasi Filter Commands
            GrayscaleCommand = new RelayCommand(async _ => await ApplyFilterAsync(_imageProcessor.ApplyGrayscaleAsync), _ => CurrentImage != null);
            SepiaCommand = new RelayCommand(async _ => await ApplyFilterAsync(_imageProcessor.ApplySepiaAsync), _ => CurrentImage != null);
            InvertCommand = new RelayCommand(async _ => await ApplyFilterAsync(_imageProcessor.ApplyInvertAsync), _ => CurrentImage != null);
        }

        // Metode Orkestrator Filter Umum agar kode DRY (Don't Repeat Yourself)
        private async Task ApplyFilterAsync(Func<BitmapSource, Task<BitmapSource>> filterMethod)
        {
            if (CurrentImage == null) return;

            IsBusy = true;
            try
            {
                // Push ke history untuk fitur Undo
                _history.Push(CurrentImage);
                
                // Panggil delegate fungsi filter
                CurrentImage = await filterMethod(CurrentImage);
            }
            finally
            {
                IsBusy = false;
            }
        }

        // ... (Fungsi OpenImageAsync, SaveImageAsync, PerformUndo sama seperti implementasi sebelumnya) ...
    }
}
