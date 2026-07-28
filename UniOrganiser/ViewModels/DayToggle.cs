using CommunityToolkit.Mvvm.ComponentModel;

namespace UniOrganiser.ViewModels;

public partial class DayToggle : ObservableObject
{
    public string Label { get; }
    public DayOfWeek Day { get; }

    [ObservableProperty]
    private bool isSelected;

    public DayToggle(string label, DayOfWeek day)
    {
        Label = label;
        Day = day;
    }
}
