using System.Windows;
using UniOrganiser.ViewModels;

namespace UniOrganiser
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            DataContext = new MainViewModel(App.Db, App.Recurrence);
        }
    }
}
