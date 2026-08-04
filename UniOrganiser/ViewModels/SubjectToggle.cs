using CommunityToolkit.Mvvm.ComponentModel;

namespace UniOrganiser.ViewModels;

public partial class SubjectToggle : ObservableObject
{
    public int? SubjectId { get; }
    public string Name { get; }
    public string ColourHex { get; }

    [ObservableProperty]
    private bool isSelected;

    public SubjectToggle(int? subjectId, string name, string? colourHex)
    {
        SubjectId = subjectId;
        Name = name;
        ColourHex = colourHex ?? "#5A5A5A";
    }
}
