using UniOrganiser.ViewModels;
using Wpf.Ui.Controls;

namespace UniOrganiser.Views;

public partial class TaskEditDialog : FluentWindow
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
