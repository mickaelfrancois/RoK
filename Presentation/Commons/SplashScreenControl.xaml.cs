using Microsoft.UI.Xaml.Controls;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Rok.Commons;

public sealed partial class SplashScreenControl : UserControl
{
    public event EventHandler? Completed;


    public SplashScreenControl()
    {
        this.InitializeComponent();
    }

    public void Start()
    {
        myStoryboard.Begin();
    }


    private void myStoryboard_Completed(object sender, object e)
    {
        Completed?.Invoke(this, EventArgs.Empty);
    }
}
