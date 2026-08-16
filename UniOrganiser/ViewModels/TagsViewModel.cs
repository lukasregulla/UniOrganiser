using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using UniOrganiser.Models;
using UniOrganiser.Services;

namespace UniOrganiser.ViewModels;

// Both tag axes on one page. Subjects and Categories keep separate lists and
// separate commands, but share a single editor overlay - only the palette and
// which table Save writes to differ.
public partial class TagsViewModel : ObservableObject
{
    private enum TagKind { Subject, Category }

    private readonly DatabaseService _db;

    public ObservableCollection<Subject> Subjects { get; } = [];
    public ObservableCollection<Category> Categories { get; } = [];

    [ObservableProperty]
    private bool isEditorOpen;

    [ObservableProperty]
    private string editorTitle = "New Subject";

    [ObservableProperty]
    private string[] editingPalette = SubjectPalette.Colours;

    private TagKind _editingKind = TagKind.Subject;
    private int _editingId;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private string editingName = string.Empty;

    [ObservableProperty]
    private string editingColourHex = SubjectPalette.Colours[0];

    [ObservableProperty]
    private string? validationError;

    public TagsViewModel(DatabaseService db)
    {
        _db = db;
        Load();
    }

    public void Load()
    {
        Subjects.Clear();
        foreach (var subject in _db.GetSubjects())
            Subjects.Add(subject);

        Categories.Clear();
        foreach (var category in _db.GetCategories())
            Categories.Add(category);
    }

    private void OpenEditor(TagKind kind, string title, int id, string name, string colourHex)
    {
        _editingKind = kind;
        _editingId = id;
        EditorTitle = title;
        EditingPalette = kind == TagKind.Subject ? SubjectPalette.Colours : CategoryPalette.Colours;
        EditingName = name;
        EditingColourHex = colourHex;
        ValidationError = null;
        IsEditorOpen = true;
    }

    [RelayCommand]
    private void AddSubject() =>
        OpenEditor(TagKind.Subject, "New Subject", 0, string.Empty, SubjectPalette.Colours[0]);

    [RelayCommand]
    private void EditSubject(Subject subject) =>
        OpenEditor(TagKind.Subject, "Edit Subject", subject.Id, subject.Name, subject.ColourHex);

    [RelayCommand]
    private void AddCategory() =>
        OpenEditor(TagKind.Category, "New Category", 0, string.Empty, CategoryPalette.Colours[0]);

    [RelayCommand]
    private void EditCategory(Category category) =>
        OpenEditor(TagKind.Category, "Edit Category", category.Id, category.Name, category.ColourHex);

    [RelayCommand]
    private void SelectColour(string hex)
    {
        EditingColourHex = hex;
    }

    private bool CanSave() => !string.IsNullOrWhiteSpace(EditingName);

    [RelayCommand(CanExecute = nameof(CanSave))]
    private void Save()
    {
        var name = EditingName.Trim();

        if (_editingKind == TagKind.Subject)
            _db.SaveSubject(new Subject { Id = _editingId, Name = name, ColourHex = EditingColourHex });
        else
            _db.SaveCategory(new Category { Id = _editingId, Name = name, ColourHex = EditingColourHex });

        Load();
        IsEditorOpen = false;
    }

    [RelayCommand]
    private void Cancel()
    {
        IsEditorOpen = false;
    }

    [RelayCommand]
    private void DeleteSubject(Subject subject)
    {
        if (!ConfirmDelete("subject", subject.Name)) return;
        _db.DeleteSubject(subject.Id);
        Load();
    }

    [RelayCommand]
    private void DeleteCategory(Category category)
    {
        if (!ConfirmDelete("category", category.Name)) return;
        _db.DeleteCategory(category.Id);
        Load();
    }

    private static bool ConfirmDelete(string noun, string name)
    {
        var result = MessageBox.Show(
            $"Delete {noun} \"{name}\"? Tasks using it will become unassigned.",
            $"Delete {char.ToUpper(noun[0])}{noun[1..]}",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        return result == MessageBoxResult.Yes;
    }
}
