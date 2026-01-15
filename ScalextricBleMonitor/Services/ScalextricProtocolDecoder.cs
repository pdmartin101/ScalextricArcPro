using System;
using System.Collections.Generic;

namespace ScalextricBleMonitor.Services;

/// <summary>
/// Decodes Scalextric ARC Pro BLE notification data into human-readable strings.
/// Extracted from MainViewModel to follow Single Responsibility Principle.
/// </summary>
public static class ScalextricProtocolDecoder
{
    /// <summary>
    /// Decodes notification data from a Scalextric characteristic into a human-readable string.
    /// </summary>
    /// <param name="characteristicUuid">The UUID of the characteristic the data came from.</param>
    /// <param name="data">The raw byte data from the notification.</param>
    /// <returns>A decoded string representation of the data.</returns>
    public static string Decode(Guid characteristicUuid, byte[] data)
    {
        if (data.Length == 0) return "(empty)";

        // Decode based on characteristic type
        if (characteristicUuid == ScalextricProtocol.Characteristics.Slot)
        {
            return DecodeSlotData(data);
        }
        else if (characteristicUuid == ScalextricProtocol.Characteristics.Throttle)
        {
            return DecodeThrottleData(data);
        }
        else if (characteristicUuid == ScalextricProtocol.Characteristics.Track)
        {
            return DecodeTrackData(data);
        }

        // Generic decode for unknown characteristics
        return DecodeGenericData(data);
    }

    /// <summary>
    /// Decodes slot sensor data (finish line timestamps).
    /// </summary>
    private static string DecodeSlotData(byte[] data)
    {
        if (data.Length < 18) return $"(incomplete: {data.Length} bytes)";

        var parts = new List<string>();

        // Status byte and Slot ID
        parts.Add($"St:{data[0]}");
        int slotId = data[1];
        parts.Add($"Slot:{slotId}");

        // t1: Lane 1 entry timestamp (bytes 2-5, centiseconds)
        uint t1 = (uint)(data[2] | (data[3] << 8) | (data[4] << 16) | (data[5] << 24));
        double t1Seconds = t1 / 100.0;
        parts.Add($"t1:{t1}({t1Seconds:F2}s)");

        // t2: Lane 2 entry timestamp (bytes 6-9, centiseconds)
        uint t2 = (uint)(data[6] | (data[7] << 8) | (data[8] << 16) | (data[9] << 24));
        double t2Seconds = t2 / 100.0;
        parts.Add($"t2:{t2}({t2Seconds:F2}s)");

        // t3: Lane 1 exit timestamp (bytes 10-13, centiseconds) - t3 > t1 by a few tenths
        uint t3 = (uint)(data[10] | (data[11] << 8) | (data[12] << 16) | (data[13] << 24));
        double t3Seconds = t3 / 100.0;
        parts.Add($"t3:{t3}({t3Seconds:F2}s)");

        // t4: Lane 2 exit timestamp (bytes 14-17, centiseconds) - t4 > t2 by a few tenths
        uint t4 = (uint)(data[14] | (data[15] << 8) | (data[16] << 16) | (data[17] << 24));
        double t4Seconds = t4 / 100.0;
        parts.Add($"t4:{t4}({t4Seconds:F2}s)");

        return string.Join(" | ", parts);
    }

    /// <summary>
    /// Decodes throttle/controller data.
    /// </summary>
    private static string DecodeThrottleData(byte[] data)
    {
        var parts = new List<string>();

        // First byte is header
        if (data.Length >= 1)
            parts.Add($"H:{data[0]:X2}");

        // Remaining bytes are controller data
        for (int i = 1; i < data.Length && i <= 6; i++)
        {
            var b = data[i];
            int throttle = b & 0x3F;
            bool brake = (b & 0x40) != 0;
            bool laneChange = (b & 0x80) != 0;

            var decoded = $"C{i}:T{throttle}";
            if (brake) decoded += "+B";
            if (laneChange) decoded += "+L";
            parts.Add(decoded);
        }

        return string.Join(" | ", parts);
    }

    /// <summary>
    /// Decodes track sensor data.
    /// </summary>
    private static string DecodeTrackData(byte[] data)
    {
        // Track sensor data - show raw byte values for debugging
        var parts = new List<string>();
        for (int i = 0; i < Math.Min(data.Length, 8); i++)
        {
            parts.Add($"b{i}:{data[i]}");
        }
        if (data.Length > 8)
            parts.Add($"+{data.Length - 8}more");
        return string.Join(" | ", parts);
    }

    /// <summary>
    /// Generic decoding for unknown characteristic data.
    /// </summary>
    private static string DecodeGenericData(byte[] data)
    {
        if (data.Length >= 2)
        {
            var parts = new List<string>();
            parts.Add($"H:{data[0]:X2}");
            for (int i = 1; i < Math.Min(data.Length, 7); i++)
            {
                parts.Add($"b{i}:{data[i]}");
            }
            if (data.Length > 7)
                parts.Add($"+{data.Length - 7}more");
            return string.Join(" | ", parts);
        }
        else if (data.Length == 1)
        {
            return $"H:{data[0]:X2}";
        }
        return "(raw)";
    }
}
