using UniOrganiser.ViewModels;
using Wpf.Ui.Controls;

namespace UniOrganiser
{
    public partial class MainWindow : FluentWindow
    {
        public MainWindow()
        {
            InitializeComponent();
            DataContext = new MainViewModel(App.Db, App.Recurrence);
        }
    }
}
