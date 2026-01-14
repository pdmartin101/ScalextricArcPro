using System;

namespace ScalextricBleMonitor.Services;

/// <summary>
/// Scalextric ARC Pro BLE Protocol constants and command builders.
/// Based on ScalextricArcBleProtocolExplorer: https://github.com/RazManager/ScalextricArcBleProtocolExplorer
/// </summary>
public static class ScalextricProtocol
{
    /// <summary>
    /// GATT Characteristic UUIDs for Scalextric ARC devices.
    /// </summary>
    public static class Characteristics
    {
        // Scalextric ARC Custom Characteristics
        public static readonly Guid Command = Guid.Parse("00003b0a-0000-1000-8000-00805f9b34fb");
        public static readonly Guid Slot = Guid.Parse("00003b0b-0000-1000-8000-00805f9b34fb");
        public static readonly Guid Throttle = Guid.Parse("00003b09-0000-1000-8000-00805f9b34fb");
        public static readonly Guid Track = Guid.Parse("00003b0c-0000-1000-8000-00805f9b34fb");
        public static readonly Guid CarId = Guid.Parse("00003b0d-0000-1000-8000-00805f9b34fb");

        // Throttle Profiles (per car slot 1-6)
        public static readonly Guid ThrottleProfile1 = Guid.Parse("0000ff01-0000-1000-8000-00805f9b34fb");
        public static readonly Guid ThrottleProfile2 = Guid.Parse("0000ff02-0000-1000-8000-00805f9b34fb");
        public static readonly Guid ThrottleProfile3 = Guid.Parse("0000ff03-0000-1000-8000-00805f9b34fb");
        public static readonly Guid ThrottleProfile4 = Guid.Parse("0000ff04-0000-1000-8000-00805f9b34fb");
        public static readonly Guid ThrottleProfile5 = Guid.Parse("0000ff05-0000-1000-8000-00805f9b34fb");
        public static readonly Guid ThrottleProfile6 = Guid.Parse("0000ff06-0000-1000-8000-00805f9b34fb");

        /// <summary>
        /// Gets the throttle profile characteristic UUID for a given slot (1-6).
        /// </summary>
        public static Guid GetThrottleProfileForSlot(int slot)
        {
            return slot switch
            {
                1 => ThrottleProfile1,
                2 => ThrottleProfile2,
                3 => ThrottleProfile3,
                4 => ThrottleProfile4,
                5 => ThrottleProfile5,
                6 => ThrottleProfile6,
                _ => throw new ArgumentOutOfRangeException(nameof(slot), "Slot must be 1-6")
            };
        }
    }

    /// <summary>
    /// Command types for the Command characteristic.
    /// </summary>
    public enum CommandType : byte
    {
        /// <summary>No power, timer stopped.</summary>
        NoPowerTimerStopped = 0,

        /// <summary>No power, timer ticking (countdown).</summary>
        NoPowerTimerTicking = 1,

        /// <summary>Power on, race trigger (waiting for start).</summary>
        PowerOnRaceTrigger = 2,

        /// <summary>Power on, racing (normal operation).</summary>
        PowerOnRacing = 3,

        /// <summary>Power on, timer halted.</summary>
        PowerOnTimerHalt = 4,

        /// <summary>No power, reboot PIC18 microcontroller.</summary>
        NoPowerRebootPic18 = 5
    }

    /// <summary>
    /// Represents power settings for a single car slot.
    /// </summary>
    public class SlotPower
    {
        /// <summary>Power multiplier (0-63). Controls the overall power output.</summary>
        public byte PowerMultiplier { get; set; }

        /// <summary>Bit 6 flag - purpose unclear, possibly related to advanced power control.</summary>
        public bool PowerBitSix { get; set; }

        /// <summary>Ghost mode - in this mode, PowerMultiplier acts as a direct throttle value.</summary>
        public bool GhostMode { get; set; }

        /// <summary>Rumble/vibration intensity (0-255).</summary>
        public byte Rumble { get; set; }

        /// <summary>Brake force (0-255).</summary>
        public byte Brake { get; set; }

        /// <summary>KERS (energy recovery) activation.</summary>
        public bool Kers { get; set; }

        /// <summary>
        /// Encodes the power byte (byte 1-6 in command): bits 0-5 = power, bit 6 = PowerBitSix, bit 7 = Ghost.
        /// </summary>
        public byte EncodePowerByte()
        {
            byte value = (byte)(PowerMultiplier & 0x3F);
            if (PowerBitSix) value |= 0x40;
            if (GhostMode) value |= 0x80;
            return value;
        }
    }

    /// <summary>
    /// Builds a 20-byte command to send to the Command characteristic.
    /// </summary>
    public class CommandBuilder
    {
        private readonly SlotPower[] _slots = new SlotPower[6];

        public CommandType Type { get; set; } = CommandType.NoPowerTimerStopped;

        public CommandBuilder()
        {
            for (int i = 0; i < 6; i++)
            {
                _slots[i] = new SlotPower();
            }
        }

        /// <summary>
        /// Gets the power settings for a slot (1-6).
        /// </summary>
        public SlotPower GetSlot(int slot)
        {
            if (slot < 1 || slot > 6)
                throw new ArgumentOutOfRangeException(nameof(slot), "Slot must be 1-6");
            return _slots[slot - 1];
        }

        /// <summary>
        /// Sets power multiplier for all slots.
        /// </summary>
        public CommandBuilder SetAllPower(byte power)
        {
            byte clampedPower = power > 63 ? (byte)63 : power;
            foreach (var slot in _slots)
            {
                slot.PowerMultiplier = clampedPower;
            }
            return this;
        }

        /// <summary>
        /// Sets power multiplier for a specific slot (1-6).
        /// </summary>
        public CommandBuilder SetSlotPower(int slot, byte power)
        {
            GetSlot(slot).PowerMultiplier = power > 63 ? (byte)63 : power;
            return this;
        }

        /// <summary>
        /// Builds the 20-byte command array.
        /// Format:
        /// Byte 0: Command type
        /// Bytes 1-6: Power multiplier per slot (with bit flags)
        /// Bytes 7-12: Rumble per slot
        /// Bytes 13-18: Brake per slot
        /// Byte 19: KERS bitfield (bit 0 = slot 1, bit 1 = slot 2, etc.)
        /// </summary>
        public byte[] Build()
        {
            var data = new byte[20];

            // Byte 0: Command type
            data[0] = (byte)Type;

            // Bytes 1-6: Power per slot
            for (int i = 0; i < 6; i++)
            {
                data[1 + i] = _slots[i].EncodePowerByte();
            }

            // Bytes 7-12: Rumble per slot
            for (int i = 0; i < 6; i++)
            {
                data[7 + i] = _slots[i].Rumble;
            }

            // Bytes 13-18: Brake per slot
            for (int i = 0; i < 6; i++)
            {
                data[13 + i] = _slots[i].Brake;
            }

            // Byte 19: KERS bitfield
            byte kers = 0;
            for (int i = 0; i < 6; i++)
            {
                if (_slots[i].Kers)
                    kers |= (byte)(1 << i);
            }
            data[19] = kers;

            return data;
        }

        /// <summary>
        /// Creates a simple "power on racing" command with specified power for all slots.
        /// </summary>
        public static byte[] CreatePowerOnCommand(byte power = 63)
        {
            var builder = new CommandBuilder
            {
                Type = CommandType.PowerOnRacing
            };
            builder.SetAllPower(power);
            return builder.Build();
        }

        /// <summary>
        /// Creates a "power off" command.
        /// </summary>
        public static byte[] CreatePowerOffCommand()
        {
            var builder = new CommandBuilder
            {
                Type = CommandType.NoPowerTimerStopped
            };
            return builder.Build();
        }
    }

    /// <summary>
    /// Throttle profile helper - creates the 96-value curve data.
    /// Throttle profiles are written in 6 blocks of 17 bytes each:
    /// Byte 0 = block index (0-5), Bytes 1-16 = 16 throttle values.
    /// </summary>
    public static class ThrottleProfile
    {
        /// <summary>
        /// Number of blocks needed to write a complete throttle profile.
        /// </summary>
        public const int BlockCount = 6;

        /// <summary>
        /// Values per block (excluding the block index byte).
        /// </summary>
        public const int ValuesPerBlock = 16;

        /// <summary>
        /// Total throttle curve values (96).
        /// </summary>
        public const int TotalValues = BlockCount * ValuesPerBlock;

        /// <summary>
        /// Creates a linear throttle profile (default).
        /// Maps input 0-95 to output values for throttle response.
        /// </summary>
        public static byte[] CreateLinearCurve()
        {
            var curve = new byte[TotalValues];
            for (int i = 0; i < TotalValues; i++)
            {
                // Formula from ScalextricArcBleProtocolExplorer:
                // Value = (256 * (i + 1) / 64) - 1, but extended to 96 values
                // This creates values: 3, 7, 11, 15... up to 255
                curve[i] = (byte)(256 * (i + 1) / TotalValues - 1);
            }
            return curve;
        }

        /// <summary>
        /// Gets a single block (17 bytes) to write to the throttle profile characteristic.
        /// </summary>
        /// <param name="curve">The full 96-byte throttle curve.</param>
        /// <param name="blockIndex">Block index (0-5).</param>
        /// <returns>17-byte array: [blockIndex, value0, value1, ..., value15]</returns>
        public static byte[] GetBlock(byte[] curve, int blockIndex)
        {
            if (blockIndex < 0 || blockIndex >= BlockCount)
                throw new ArgumentOutOfRangeException(nameof(blockIndex), "Block index must be 0-5");

            var block = new byte[ValuesPerBlock + 1]; // 17 bytes
            block[0] = (byte)blockIndex;

            int startIndex = blockIndex * ValuesPerBlock;
            for (int i = 0; i < ValuesPerBlock; i++)
            {
                block[i + 1] = curve[startIndex + i];
            }

            return block;
        }

        /// <summary>
        /// Gets all 6 blocks for a throttle profile.
        /// </summary>
        /// <param name="curve">The full 96-byte throttle curve.</param>
        /// <returns>Array of 6 blocks, each 17 bytes.</returns>
        public static byte[][] GetAllBlocks(byte[] curve)
        {
            var blocks = new byte[BlockCount][];
            for (int i = 0; i < BlockCount; i++)
            {
                blocks[i] = GetBlock(curve, i);
            }
            return blocks;
        }

        /// <summary>
        /// Creates blocks for a linear throttle profile ready to write.
        /// </summary>
        public static byte[][] CreateLinearBlocks()
        {
            return GetAllBlocks(CreateLinearCurve());
        }
    }
}
