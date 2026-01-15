using System;
using CommunityToolkit.Mvvm.ComponentModel;
using ScalextricBleMonitor.Services;

namespace ScalextricBleMonitor.ViewModels;

/// <summary>
/// View model for notification data received from a characteristic.
/// </summary>
public partial class NotificationDataViewModel : ObservableObject
{
    [ObservableProperty]
    private DateTime _timestamp;

    [ObservableProperty]
    private string _characteristicName = string.Empty;

    [ObservableProperty]
    private Guid _characteristicUuid;

    [ObservableProperty]
    private byte[] _rawData = [];

    [ObservableProperty]
    private string _hexData = string.Empty;

    [ObservableProperty]
    private string _decodedData = string.Empty;

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
