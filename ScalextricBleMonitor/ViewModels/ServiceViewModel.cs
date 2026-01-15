using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ScalextricBleMonitor.ViewModels;

/// <summary>
/// View model for a GATT service.
/// </summary>
public partial class ServiceViewModel : ObservableObject
{
    [ObservableProperty]
    private Guid _uuid;

    [ObservableProperty]
    private string _name = string.Empty;

    public ObservableCollection<CharacteristicViewModel> Characteristics { get; } = [];
}
