using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TotpManager.Maui.Models;
using TotpManager.Maui.Services;

namespace TotpManager.Maui.ViewModels;

public enum AddMode { Url, Camera, File }

public partial class AddAccountViewModel : ObservableObject
{
    private readonly QrImportService _importer;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsUrlMode))]
    [NotifyPropertyChangedFor(nameof(IsCameraMode))]
    [NotifyPropertyChangedFor(nameof(IsFileMode))]
    private AddMode _selectedMode = AddMode.Url;

    [ObservableProperty]
    private string _urlInput = string.Empty;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    [ObservableProperty]
    private bool _isBusy;

    public bool IsUrlMode => SelectedMode == AddMode.Url;
    public bool IsCameraMode => SelectedMode == AddMode.Camera;
    public bool IsFileMode => SelectedMode == AddMode.File;

    /// <summary>Set by the page to wire imported accounts back to AccountsViewModel.</summary>
    public Action<IReadOnlyList<AccountRecord>>? OnAccountsImported { get; set; }

    public AddAccountViewModel(QrImportService importer)
    {
        _importer = importer;
    }

    [RelayCommand]
    private void SelectMode(string mode) =>
        SelectedMode = mode switch
        {
            "camera" => AddMode.Camera,
            "file"   => AddMode.File,
            _        => AddMode.Url,
        };

    [RelayCommand]
    private async Task SubmitUrl()
    {
        ErrorMessage = string.Empty;
        if (string.IsNullOrWhiteSpace(UrlInput))
        {
            ErrorMessage = "Please enter a migration URL or otpauth:// URI.";
            return;
        }

        IsBusy = true;
        try
        {
            var accounts = _importer.ParseInput(UrlInput);
            if (accounts.Count == 0)
            {
                ErrorMessage = "No valid accounts found in the input.";
                return;
            }
            OnAccountsImported?.Invoke(accounts);
            await Shell.Current.GoToAsync("..");
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>Called by the page when ZXing detects a barcode.</summary>
    public void HandleScannedBarcode(string value)
    {
        if (string.IsNullOrEmpty(value)) return;
        var accounts = _importer.ParseInput(value);
        if (accounts.Count == 0) return;

        OnAccountsImported?.Invoke(accounts);
        MainThread.BeginInvokeOnMainThread(async () =>
            await Shell.Current.GoToAsync(".."));
    }

    [RelayCommand]
    private async Task PickFile()
    {
        IsBusy = true;
        ErrorMessage = string.Empty;
        try
        {
            var result = await FilePicker.PickAsync(new PickOptions
            {
                PickerTitle = "Select accounts.txt or otpauth URI list",
                FileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
                {
                    { DevicePlatform.iOS, ["public.plain-text", "public.text"] },
                    { DevicePlatform.Android, ["text/plain"] },
                }),
            });
            if (result == null) return;

            using var stream = await result.OpenReadAsync();
            using var reader = new StreamReader(stream);
            var content = await reader.ReadToEndAsync();
            var accounts = _importer.ParseInput(content);

            if (accounts.Count == 0)
            {
                ErrorMessage = "No valid otpauth:// URIs found in the file.";
                return;
            }

            OnAccountsImported?.Invoke(accounts);
            await Shell.Current.GoToAsync("..");
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
