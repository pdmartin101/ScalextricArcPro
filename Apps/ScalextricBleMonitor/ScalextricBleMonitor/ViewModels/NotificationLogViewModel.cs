using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Scalextric;
using ScalextricBleMonitor.Services;

namespace ScalextricBleMonitor.ViewModels;

/// <summary>
/// Manages notification logging with batching, filtering, and display.
/// </summary>
public partial class NotificationLogViewModel : ObservableObject, IDisposable
{
    private const int MaxNotificationLogEntries = 100;
    private const int NotificationBatchIntervalMs = 50;

    private readonly ConcurrentQueue<BleNotificationEventArgs> _notificationBatch = new();
    private Timer? _notificationBatchTimer;
    private bool _disposed;

    /// <summary>
    /// Live notification data received from the powerbase.
    /// </summary>
    public ObservableCollection<NotificationDataViewModel> NotificationLog { get; } = [];

    /// <summary>
    /// Filtered notification log based on current filter settings.
    /// </summary>
    public ObservableCollection<NotificationDataViewModel> FilteredNotificationLog { get; } = [];

    /// <summary>
    /// Characteristic filter for notifications: 0=All, 1=Throttle, 2=Slot, 3=Track, 4=CarId
    /// </summary>
    [ObservableProperty]
    private int _notificationCharacteristicFilter;

    partial void OnNotificationCharacteristicFilterChanged(int value)
    {
        RefreshFilteredNotificationLog();
    }

    /// <summary>
    /// Whether the notification log is paused (not accepting new entries).
    /// </summary>
    [ObservableProperty]
    private bool _isNotificationLogPaused;

    /// <summary>
    /// Event raised when a throttle notification is received and should be processed.
    /// </summary>
    public event EventHandler<byte[]>? ThrottleNotificationReceived;

    /// <summary>
    /// Event raised when a slot notification is received and should be processed.
    /// </summary>
    public event EventHandler<byte[]>? SlotNotificationReceived;

    /// <summary>
    /// Initializes a new instance of the NotificationLogViewModel.
    /// </summary>
    public NotificationLogViewModel()
    {
        // Start notification batch timer to reduce UI dispatcher load
        _notificationBatchTimer = new Timer(
            FlushNotificationBatch,
            null,
            NotificationBatchIntervalMs,
            NotificationBatchIntervalMs);
    }

    /// <summary>
    /// Queues a notification for batched processing.
    /// </summary>
    public void QueueNotification(BleNotificationEventArgs notification)
    {
        _notificationBatch.Enqueue(notification);
    }

    /// <summary>
    /// Flushes batched notifications to the UI thread.
    /// </summary>
    private void FlushNotificationBatch(object? state)
    {
        // Collect all pending notifications
        var batch = new List<BleNotificationEventArgs>();
        while (_notificationBatch.TryDequeue(out var notification))
        {
            batch.Add(notification);
        }

        if (batch.Count == 0)
            return;

        // Process entire batch in a single UI dispatcher call
        Dispatcher.UIThread.Post(() =>
        {
            foreach (var e in batch)
            {
                // Raise events for notification types that need processing
                if (e.CharacteristicUuid == ScalextricProtocol.Characteristics.Throttle)
                {
                    ThrottleNotificationReceived?.Invoke(this, e.Data);
                }
                else if (e.CharacteristicUuid == ScalextricProtocol.Characteristics.Slot)
                {
                    SlotNotificationReceived?.Invoke(this, e.Data);
                }

                // Skip adding to log if paused
                if (IsNotificationLogPaused)
                    continue;

                // Create the notification entry
                var entry = new NotificationDataViewModel
                {
                    Timestamp = e.Timestamp,
                    CharacteristicName = e.CharacteristicName ?? e.CharacteristicUuid.ToString(),
                    CharacteristicUuid = e.CharacteristicUuid,
                    RawData = e.Data,
                    HexData = BitConverter.ToString(e.Data).Replace("-", " "),
                    DecodedData = ScalextricProtocolDecoder.Decode(e.CharacteristicUuid, e.Data)
                };

                // Add to main log
                NotificationLog.Insert(0, entry);

                // Add to filtered log if it passes the filter
                if (PassesCharacteristicFilter(e.CharacteristicUuid))
                {
                    FilteredNotificationLog.Insert(0, entry);
                }
            }

            // Trim logs after batch processing
            while (NotificationLog.Count > MaxNotificationLogEntries)
            {
                NotificationLog.RemoveAt(NotificationLog.Count - 1);
            }
            while (FilteredNotificationLog.Count > MaxNotificationLogEntries)
            {
                FilteredNotificationLog.RemoveAt(FilteredNotificationLog.Count - 1);
            }
        });
    }

    private bool PassesCharacteristicFilter(Guid characteristicUuid)
    {
        return NotificationCharacteristicFilter switch
        {
            0 => true, // All
            1 => characteristicUuid == ScalextricProtocol.Characteristics.Throttle,
            2 => characteristicUuid == ScalextricProtocol.Characteristics.Slot,
            3 => characteristicUuid == ScalextricProtocol.Characteristics.Track,
            4 => characteristicUuid == ScalextricProtocol.Characteristics.CarId,
            _ => true
        };
    }

    private void RefreshFilteredNotificationLog()
    {
        FilteredNotificationLog.Clear();
        foreach (var entry in NotificationLog)
        {
            if (PassesCharacteristicFilter(entry.CharacteristicUuid))
            {
                FilteredNotificationLog.Add(entry);
            }
        }
    }

    /// <summary>
    /// Clears the notification log.
    /// </summary>
    [RelayCommand]
    private void ClearNotificationLog()
    {
        NotificationLog.Clear();
        FilteredNotificationLog.Clear();
    }

    /// <summary>
    /// Disposes resources.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _notificationBatchTimer?.Dispose();
        _notificationBatchTimer = null;

        GC.SuppressFinalize(this);
    }
}
