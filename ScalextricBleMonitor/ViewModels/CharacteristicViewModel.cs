using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ScalextricBleMonitor.Models;

namespace ScalextricBleMonitor.ViewModels;

/// <summary>
/// View model for a GATT characteristic.
/// Wraps a GattCharacteristic model with UI-specific properties.
/// </summary>
public partial class CharacteristicViewModel : ObservableObject
{
    // Underlying domain model
    private readonly GattCharacteristic _model = new();

    /// <summary>
    /// Action to invoke when reading this characteristic.
    /// Set by the parent ViewModel to handle the read operation.
    /// </summary>
    public Action<Guid, Guid>? ReadAction { get; set; }

    /// <summary>
    /// Command to read this characteristic's value.
    /// </summary>
    [RelayCommand]
    private void Read()
    {
        ReadAction?.Invoke(ServiceUuid, Uuid);
    }

    /// <summary>
    /// Gets the underlying GattCharacteristic model.
    /// </summary>
    public GattCharacteristic Model => _model;

    [ObservableProperty]
    private Guid _uuid;

    partial void OnUuidChanged(Guid value)
    {
        _model.Uuid = value;
    }

    [ObservableProperty]
    private Guid _serviceUuid;

    partial void OnServiceUuidChanged(Guid value)
    {
        _model.ServiceUuid = value;
    }

    [ObservableProperty]
    private string _name = string.Empty;

    partial void OnNameChanged(string value)
    {
        _model.Name = value;
    }

    [ObservableProperty]
    private string _properties = string.Empty;

    partial void OnPropertiesChanged(string value)
    {
        _model.Properties = value;
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasReadValue))]
    [NotifyPropertyChangedFor(nameof(ReadResultDisplay))]
    private byte[]? _lastReadValue;

    partial void OnLastReadValueChanged(byte[]? value)
    {
        _model.LastValue = value;
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasReadValue))]
    [NotifyPropertyChangedFor(nameof(ReadResultDisplay))]
    private string? _lastReadHex;

    partial void OnLastReadHexChanged(string? value)
    {
        _model.LastValueDisplay = value;
    }

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
