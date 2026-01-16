using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using ScalextricBleMonitor.Models;

namespace ScalextricBleMonitor.ViewModels;

/// <summary>
/// View model for a GATT service.
/// Wraps a GattService model with UI-specific properties.
/// </summary>
public partial class ServiceViewModel : ObservableObject
{
    // Underlying domain model
    private readonly GattService _model = new();

    /// <summary>
    /// Gets the underlying GattService model.
    /// </summary>
    public GattService Model => _model;

    [ObservableProperty]
    private Guid _uuid;

    partial void OnUuidChanged(Guid value)
    {
        _model.Uuid = value;
    }

    [ObservableProperty]
    private string _name = string.Empty;

    partial void OnNameChanged(string value)
    {
        _model.Name = value;
    }

    public ObservableCollection<CharacteristicViewModel> Characteristics { get; } = [];
}
