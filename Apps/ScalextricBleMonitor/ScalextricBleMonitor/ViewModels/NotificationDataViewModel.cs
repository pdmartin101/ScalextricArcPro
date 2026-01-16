using System;
using CommunityToolkit.Mvvm.ComponentModel;
using ScalextricBleMonitor.Models;
using ScalextricBleMonitor.Services;

namespace ScalextricBleMonitor.ViewModels;

/// <summary>
/// View model for notification data received from a characteristic.
/// Wraps a NotificationEntry model with UI-specific properties.
/// </summary>
public partial class NotificationDataViewModel : ObservableObject
{
    // Underlying domain model
    private readonly NotificationEntry _model = new();

    /// <summary>
    /// Gets the underlying NotificationEntry model.
    /// </summary>
    public NotificationEntry Model => _model;

    [ObservableProperty]
    private DateTime _timestamp;

    partial void OnTimestampChanged(DateTime value)
    {
        _model.Timestamp = value;
    }

    [ObservableProperty]
    private string _characteristicName = string.Empty;

    partial void OnCharacteristicNameChanged(string value)
    {
        _model.CharacteristicName = value;
    }

    [ObservableProperty]
    private Guid _characteristicUuid;

    partial void OnCharacteristicUuidChanged(Guid value)
    {
        _model.CharacteristicUuid = value;
    }

    [ObservableProperty]
    private byte[] _rawData = [];

    partial void OnRawDataChanged(byte[] value)
    {
        _model.Data = value;
    }

    [ObservableProperty]
    private string _hexData = string.Empty;

    [ObservableProperty]
    private string _decodedData = string.Empty;

    partial void OnDecodedDataChanged(string value)
    {
        _model.DecodedValue = value;
    }

    public string TimestampText => Timestamp.ToString("HH:mm:ss.fff");

    public string DisplayText => $"[{TimestampText}] {CharacteristicName}: {HexData}";

    /// <summary>
    /// Short name for the characteristic based on known Scalextric UUIDs.
    /// </summary>
    public string CharacteristicShortName
    {
        get
        {
            if (CharacteristicUuid == ScalextricProtocol.Characteristics.Throttle)
                return "Throttle";
            if (CharacteristicUuid == ScalextricProtocol.Characteristics.Slot)
                return "Slot";
            if (CharacteristicUuid == ScalextricProtocol.Characteristics.Track)
                return "Track";
            if (CharacteristicUuid == ScalextricProtocol.Characteristics.Command)
                return "Command";
            if (CharacteristicUuid == ScalextricProtocol.Characteristics.CarId)
                return "CarId";

            // Extract short UUID for unknown characteristics
            var uuidStr = CharacteristicUuid.ToString();
            if (uuidStr.StartsWith("0000") && uuidStr.Contains("-0000-1000-8000"))
                return uuidStr.Substring(4, 4);

            return CharacteristicName;
        }
    }
}
