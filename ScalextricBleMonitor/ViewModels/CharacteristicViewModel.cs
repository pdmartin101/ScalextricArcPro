using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ScalextricBleMonitor.ViewModels;

/// <summary>
/// View model for a GATT characteristic.
/// </summary>
public partial class CharacteristicViewModel : ObservableObject
{
    [ObservableProperty]
    private Guid _uuid;

    [ObservableProperty]
    private Guid _serviceUuid;

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _properties = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasReadValue))]
    [NotifyPropertyChangedFor(nameof(ReadResultDisplay))]
    private byte[]? _lastReadValue;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasReadValue))]
    [NotifyPropertyChangedFor(nameof(ReadResultDisplay))]
    private string? _lastReadHex;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ReadResultDisplay))]
    private string? _lastReadText;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasReadValue))]
    [NotifyPropertyChangedFor(nameof(ReadResultDisplay))]
    private string? _lastReadError;

    public string DisplayText => $"{Name} [{Properties}]";

    public bool IsReadable => Properties.Contains("R");

    public bool HasReadValue => LastReadHex != null || LastReadError != null;

    public string ReadResultDisplay
    {
        get
        {
            if (LastReadError != null) return $"Error: {LastReadError}";
            if (LastReadHex != null)
            {
                return LastReadText != null ? $"{LastReadHex} \"{LastReadText}\"" : LastReadHex;
            }
            return string.Empty;
        }
    }
}
