using System;
using System.Collections.Generic;

namespace Scalextric;

/// <summary>
/// Decodes Scalextric ARC Pro BLE notification data into human-readable strings.
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
        if (data.Length < ScalextricProtocol.SlotData.FullLength)
            return $"(incomplete: {data.Length} bytes)";

        var parts = new List<string>();

        // Status byte and Slot ID
        parts.Add($"St:{data[ScalextricProtocol.SlotData.StatusOffset]}");
        int slotId = data[ScalextricProtocol.SlotData.SlotIdOffset];
        parts.Add($"Slot:{slotId}");

        // t1: Lane 1 entry timestamp (centiseconds)
        uint t1 = ReadUInt32LittleEndian(data, ScalextricProtocol.SlotData.Lane1EntryOffset);
        double t1Seconds = t1 / ScalextricProtocol.SlotData.TimestampUnitsPerSecond;
        parts.Add($"t1:{t1}({t1Seconds:F2}s)");

        // t2: Lane 2 entry timestamp (centiseconds)
        uint t2 = ReadUInt32LittleEndian(data, ScalextricProtocol.SlotData.Lane2EntryOffset);
        double t2Seconds = t2 / ScalextricProtocol.SlotData.TimestampUnitsPerSecond;
        parts.Add($"t2:{t2}({t2Seconds:F2}s)");

        // t3: Lane 1 exit timestamp (centiseconds) - t3 > t1 by a few tenths
        uint t3 = ReadUInt32LittleEndian(data, ScalextricProtocol.SlotData.Lane1ExitOffset);
        double t3Seconds = t3 / ScalextricProtocol.SlotData.TimestampUnitsPerSecond;
        parts.Add($"t3:{t3}({t3Seconds:F2}s)");

        // t4: Lane 2 exit timestamp (centiseconds) - t4 > t2 by a few tenths
        uint t4 = ReadUInt32LittleEndian(data, ScalextricProtocol.SlotData.Lane2ExitOffset);
        double t4Seconds = t4 / ScalextricProtocol.SlotData.TimestampUnitsPerSecond;
        parts.Add($"t4:{t4}({t4Seconds:F2}s)");

        return string.Join(" | ", parts);
    }

    /// <summary>
    /// Reads a 32-bit unsigned integer from a byte array in little-endian format.
    /// </summary>
    public static uint ReadUInt32LittleEndian(byte[] data, int offset)
    {
        return (uint)(data[offset] | (data[offset + 1] << 8) | (data[offset + 2] << 16) | (data[offset + 3] << 24));
    }

    /// <summary>
    /// Decodes throttle/controller data.
    /// </summary>
    private static string DecodeThrottleData(byte[] data)
    {
        var parts = new List<string>();

        // First byte is header
        if (data.Length >= 1)
            parts.Add($"H:{data[ScalextricProtocol.ThrottleData.HeaderOffset]:X2}");

        // Remaining bytes are controller data
        int maxController = ScalextricProtocol.ThrottleData.FirstControllerOffset + ScalextricProtocol.ThrottleData.MaxControllers;
        for (int i = ScalextricProtocol.ThrottleData.FirstControllerOffset; i < data.Length && i < maxController; i++)
        {
            var b = data[i];
            int throttle = b & ScalextricProtocol.ThrottleData.ThrottleMask;
            bool brake = (b & ScalextricProtocol.ThrottleData.BrakeMask) != 0;
            bool laneChange = (b & ScalextricProtocol.ThrottleData.LaneChangeMask) != 0;

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
