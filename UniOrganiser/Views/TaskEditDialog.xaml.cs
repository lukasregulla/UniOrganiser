using System.Windows;
using UniOrganiser.ViewModels;

namespace UniOrganiser.Views;

public partial class TaskEditDialog : Window
{
    public TaskEditDialog(TaskEditViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        viewModel.RequestClose += (_, success) =>
        {
            DialogResult = success;
            Close();
        };
    }
}
