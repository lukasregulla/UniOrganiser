namespace UniOrganiser.ViewModels;

// One entry in a subject or category ComboBox. A null Id is the "none" entry.
public sealed record PickerOption(int? Id, string Name, string? ColourHex);
