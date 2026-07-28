using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using UniOrganiser.Models;
using UniOrganiser.Services;

namespace UniOrganiser.ViewModels;

public partial class SubjectsViewModel : ObservableObject
{
    private readonly DatabaseService _db;

    public ObservableCollection<Subject> Subjects { get; } = [];

    public string[] Palette => SubjectPalette.Colours;

    [ObservableProperty]
    private bool isEditorOpen;

    [ObservableProperty]
    private string editorTitle = "New Subject";

    private int _editingId;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private string editingName = string.Empty;

    [ObservableProperty]
    private string editingColourHex = SubjectPalette.Colours[0];

    [ObservableProperty]
    private string? validationError;

    public SubjectsViewModel(DatabaseService db)
    {
        _db = db;
        Load();
    }

    public void Load()
    {
        Subjects.Clear();
        foreach (var subject in _db.GetSubjects())
            Subjects.Add(subject);
    }

    [RelayCommand]
    private void AddNew()
    {
        _editingId = 0;
        EditorTitle = "New Subject";
        EditingName = string.Empty;
        EditingColourHex = Palette[0];
        ValidationError = null;
        IsEditorOpen = true;
    }

    [RelayCommand]
    private void Edit(Subject subject)
    {
        _editingId = subject.Id;
        EditorTitle = "Edit Subject";
        EditingName = subject.Name;
        EditingColourHex = subject.ColourHex;
        ValidationError = null;
        IsEditorOpen = true;
    }

    [RelayCommand]
    private void SelectColour(string hex)
    {
        EditingColourHex = hex;
    }

    private bool CanSave() => !string.IsNullOrWhiteSpace(EditingName);

    [RelayCommand(CanExecute = nameof(CanSave))]
    private void Save()
    {
        var subject = new Subject
        {
            Id = _editingId,
            Name = EditingName.Trim(),
            ColourHex = EditingColourHex,
        };
        _db.SaveSubject(subject);
        Load();
        IsEditorOpen = false;
    }

    [RelayCommand]
    private void Cancel()
    {
        IsEditorOpen = false;
    }

    [RelayCommand]
    private void Delete(Subject subject)
    {
        var result = MessageBox.Show(
            $"Delete subject \"{subject.Name}\"? Tasks using it will become unassigned.",
            "Delete Subject",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes) return;

        _db.DeleteSubject(subject.Id);
        Load();
    }
}
