using CommunityToolkit.Mvvm.ComponentModel;

namespace UniOrganiser.ViewModels;

// One checkbox in a filter row. Used for both the subject and category rows -
// a null Id means the "no subject" / "no category" bucket.
public partial class FilterToggle : ObservableObject
{
    public int? Id { get; }
    public string Name { get; }
    public string ColourHex { get; }

    [ObservableProperty]
    private bool isSelected;

    public FilterToggle(int? id, string name, string? colourHex)
    {
        Id = id;
        Name = name;
        ColourHex = colourHex ?? "#5A5A5A";
    }
}
