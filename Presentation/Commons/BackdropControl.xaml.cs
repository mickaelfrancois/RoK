using Microsoft.UI.Xaml.Controls;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Rok.Commons
{
    public sealed partial class BackdropControl : UserControl
    {
        private int _currentImageIndex = 1;

        public static readonly DependencyProperty SourceProperty = DependencyProperty.Register(nameof(Source), typeof(ImageSource), typeof(BackdropControl), new PropertyMetadata(null));


        public ImageSource Source
        {
            get => (ImageSource)base.GetValue(SourceProperty);
            set
            {
                base.SetValue(SourceProperty, value);
                Execute();
            }
        }


        public BackdropControl()
        {
            InitializeComponent();
        }


        private void Execute()
        {
            if (Source != null)
            {
                if (_currentImageIndex == 1)
                {
                    img1.Source = Source;
                    _currentImageIndex = 2;
                    VisualStateManager.GoToState(this, "img2Loaded", true);
                }
                else
                {
                    img2.Source = Source;
                    _currentImageIndex = 1;
                    VisualStateManager.GoToState(this, "img1Loaded", true);
                }
            }
        }
    }
}
