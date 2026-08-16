namespace UniOrganiser.Models;

// Deliberately muted and darker than SubjectPalette: category marks are a
// secondary signal, and the two axes must stay tellable apart even where a
// hue lands near a subject's.
public static class CategoryPalette
{
    public static readonly string[] Colours =
    [
        "#4FA39C", // teal
        "#7C93B8", // slate blue
        "#9AA65C", // olive
        "#B08968", // tan
        "#C08497", // dusty rose
        "#8E7CC3", // muted violet
        "#7D8B99", // slate grey
    ];
}
