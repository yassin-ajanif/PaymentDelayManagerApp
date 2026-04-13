using Avalonia.Controls;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PaymentDelayApp.DataAccessLayer;
using PaymentDelayApp.Services;

namespace PaymentDelayApp.ViewModels;

public partial class BackupSettingsViewModel : ViewModelBase
{
    private static readonly FilePickerFileType[] SqliteDbFileTypes =
    [
        new("Base SQLite") { Patterns = ["*.db", "*.sqlite", "*.sqlite3"] },
        new("Tous les fichiers") { Patterns = ["*"] },
    ];

    private readonly IDialogService _dialogs;
    private readonly IBackupService _backupService;
    private readonly Window _window;

    [ObservableProperty]
    private string _databasePath = string.Empty;

    [ObservableProperty]
    private string _backupsDirectory = string.Empty;

    [ObservableProperty]
    private int _retentionDays = BackupSettingsFile.DefaultRetentionDays;

    public BackupSettingsViewModel()
    {
        _dialogs = null!;
        _backupService = null!;
        _window = null!;
    }

    public BackupSettingsViewModel(IDialogService dialogs, IBackupService backupService, Window window)
    {
        _dialogs = dialogs;
        _backupService = backupService;
        _window = window;
        ReloadFromDisk();
    }

    public void ReloadFromDisk()
    {
        var doc = BackupSettingsFile.LoadOrDefault();
        DatabasePath = doc.DatabasePath ?? string.Empty;
        BackupsDirectory = doc.BackupsDirectory ?? string.Empty;
        RetentionDays = doc.RetentionDays;
    }

    [RelayCommand]
    private async Task BrowseDatabasePathAsync()
    {
        if (_dialogs is null || _window is null)
            return;

        var top = TopLevel.GetTopLevel(_window);
        if (top?.StorageProvider is not { CanOpen: true } storage)
        {
            await _dialogs.ShowMessageAsync("Sauvegardes", "La sélection de fichier n'est pas disponible.", _window);
            return;
        }

        var startDir = TryGetParentFolder(DatabasePath);
        var options = new FilePickerOpenOptions
        {
            Title = "Choisir la base SQLite",
            AllowMultiple = false,
            FileTypeFilter = SqliteDbFileTypes,
        };
        if (startDir is not null)
            options.SuggestedStartLocation = await storage.TryGetFolderFromPathAsync(startDir);

        var files = await storage.OpenFilePickerAsync(options);
        var file = files.Count > 0 ? files[0] : null;
        var path = file?.Path.LocalPath;
        if (string.IsNullOrWhiteSpace(path))
            return;

        DatabasePath = path.Trim();
    }

    [RelayCommand]
    private async Task BrowseBackupsDirectoryAsync()
    {
        if (_dialogs is null || _window is null)
            return;

        var top = TopLevel.GetTopLevel(_window);
        if (top?.StorageProvider is not { CanOpen: true } storage)
        {
            await _dialogs.ShowMessageAsync("Sauvegardes", "La sélection de dossier n'est pas disponible.", _window);
            return;
        }

        var startDir = TryGetExistingFolder(BackupsDirectory);
        var options = new FolderPickerOpenOptions
        {
            Title = "Choisir le dossier de sauvegarde",
            AllowMultiple = false,
        };
        if (startDir is not null)
            options.SuggestedStartLocation = await storage.TryGetFolderFromPathAsync(startDir);

        var folders = await storage.OpenFolderPickerAsync(options);
        var folder = folders.Count > 0 ? folders[0] : null;
        var path = folder?.Path.LocalPath;
        if (string.IsNullOrWhiteSpace(path))
            return;

        BackupsDirectory = path.Trim();
    }

    private static string? TryGetParentFolder(string? filePath)
    {
        try
        {
            var t = (filePath ?? string.Empty).Trim();
            if (t.Length == 0)
                return null;
            if (Directory.Exists(t))
                return t;
            if (File.Exists(t))
                return Path.GetDirectoryName(Path.GetFullPath(t));
            var dir = Path.GetDirectoryName(t);
            return string.IsNullOrEmpty(dir) ? null : dir;
        }
        catch
        {
            return null;
        }
    }

    private static string? TryGetExistingFolder(string? path)
    {
        try
        {
            var t = (path ?? string.Empty).Trim();
            if (t.Length == 0)
                return null;
            if (Directory.Exists(t))
                return Path.GetFullPath(t);
            return null;
        }
        catch
        {
            return null;
        }
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (_dialogs is null || _backupService is null || _window is null)
            return;

        var dbPath = (DatabasePath ?? string.Empty).Trim();
        var backupDir = (BackupsDirectory ?? string.Empty).Trim();

        if (dbPath.Length == 0)
        {
            await _dialogs.ShowMessageAsync("Sauvegardes", "Indiquez le chemin de la base.", _window);
            return;
        }

        if (backupDir.Length == 0)
        {
            await _dialogs.ShowMessageAsync("Sauvegardes", "Indiquez le dossier de sauvegarde.", _window);
            return;
        }

        try
        {
            dbPath = Path.GetFullPath(dbPath);
            backupDir = Path.GetFullPath(backupDir);
        }
        catch (Exception ex)
        {
            await _dialogs.ShowMessageAsync("Sauvegardes", $"Chemin invalide : {ex.Message}", _window);
            return;
        }

        if (!File.Exists(dbPath))
        {
            await _dialogs.ShowMessageAsync("Sauvegardes", "Le fichier base SQLite est introuvable.", _window);
            return;
        }

        try
        {
            Directory.CreateDirectory(backupDir);
        }
        catch (Exception ex)
        {
            await _dialogs.ShowMessageAsync("Sauvegardes", $"Impossible de créer le dossier de sauvegarde : {ex.Message}", _window);
            return;
        }

        var retention = BackupSettingsFile.ClampRetentionDays(RetentionDays);
        RetentionDays = retention;

        var previous = BackupSettingsFile.LoadOrDefault();
        var doc = new BackupSettingsDocument
        {
            SchemaVersion = 1,
            DatabasePath = dbPath,
            BackupsDirectory = backupDir,
            RetentionDays = retention,
            LastBackupUtc = previous.LastBackupUtc,
        };

        try
        {
            BackupSettingsFile.Save(doc);
            await _backupService.PruneBackupsAsync(backupDir, retention);
            await _dialogs.ShowMessageAsync("Sauvegardes", "Paramètres de sauvegarde enregistrés.", _window);
            _window.Close();
        }
        catch (Exception ex)
        {
            await _dialogs.ShowMessageAsync("Erreur", $"Impossible d'enregistrer : {ex.Message}", _window);
        }
    }
}
