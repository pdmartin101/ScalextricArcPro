using ScalextricBleMonitor.Services;
using static ScalextricBleMonitor.Services.ScalextricProtocol;

namespace ScalextricBleMonitor.Tests;

public class ScalextricProtocolTests
{
    public class CommandBuilderTests
    {
        [Fact]
        public void Build_ReturnsCorrectLength()
        {
            var builder = new ScalextricProtocol.CommandBuilder();
            var result = builder.Build();

            Assert.Equal(20, result.Length);
        }

        [Fact]
        public void Build_DefaultCommand_HasNoPowerTimerStopped()
        {
            var builder = new ScalextricProtocol.CommandBuilder();
            var result = builder.Build();

            Assert.Equal((byte)ScalextricProtocol.CommandType.NoPowerTimerStopped, result[0]);
        }

        [Fact]
        public void Build_PowerOnRacing_SetsCorrectCommandType()
        {
            var builder = new ScalextricProtocol.CommandBuilder
            {
                Type = ScalextricProtocol.CommandType.PowerOnRacing
            };
            var result = builder.Build();

            Assert.Equal((byte)ScalextricProtocol.CommandType.PowerOnRacing, result[0]);
        }

        [Fact]
        public void SetAllPower_SetsAllSlots()
        {
            var builder = new ScalextricProtocol.CommandBuilder();
            builder.SetAllPower(32);
            var result = builder.Build();

            // Bytes 1-6 should all be 32
            for (int i = 1; i <= 6; i++)
            {
                Assert.Equal(32, result[i]);
            }
        }

        [Fact]
        public void SetAllPower_ClampsToPower63()
        {
            var builder = new ScalextricProtocol.CommandBuilder();
            builder.SetAllPower(100);
            var result = builder.Build();

            // Should be clamped to 63
            for (int i = 1; i <= 6; i++)
            {
                Assert.Equal(63, result[i]);
            }
        }

        [Fact]
        public void SetSlotPower_SetsSpecificSlot()
        {
            var builder = new ScalextricProtocol.CommandBuilder();
            builder.SetSlotPower(3, 42);
            var result = builder.Build();

            Assert.Equal(42, result[3]); // Slot 3 is at byte index 3
            Assert.Equal(0, result[1]);  // Other slots should be 0
            Assert.Equal(0, result[2]);
        }

        [Fact]
        public void GetSlot_ThrowsForInvalidSlot()
        {
            var builder = new ScalextricProtocol.CommandBuilder();

            Assert.Throws<ArgumentOutOfRangeException>(() => builder.GetSlot(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => builder.GetSlot(7));
        }

        [Fact]
        public void GhostMode_SetsBit7()
        {
            var builder = new ScalextricProtocol.CommandBuilder();
            builder.GetSlot(1).PowerMultiplier = 30;
            builder.GetSlot(1).GhostMode = true;
            var result = builder.Build();

            // Byte 1 should have power (30) + ghost bit (0x80)
            Assert.Equal(30 | 0x80, result[1]);
        }

        [Fact]
        public void PowerBitSix_SetsBit6()
        {
            var builder = new ScalextricProtocol.CommandBuilder();
            builder.GetSlot(2).PowerMultiplier = 20;
            builder.GetSlot(2).PowerBitSix = true;
            var result = builder.Build();

            // Byte 2 should have power (20) + bit 6 (0x40)
            Assert.Equal(20 | 0x40, result[2]);
        }

        [Fact]
        public void Rumble_SetsCorrectBytes()
        {
            var builder = new ScalextricProtocol.CommandBuilder();
            builder.GetSlot(1).Rumble = 100;
            builder.GetSlot(3).Rumble = 200;
            var result = builder.Build();

            // Rumble bytes are at indices 7-12
            Assert.Equal(100, result[7]);  // Slot 1
            Assert.Equal(0, result[8]);    // Slot 2
            Assert.Equal(200, result[9]);  // Slot 3
        }

        [Fact]
        public void Brake_SetsCorrectBytes()
        {
            var builder = new ScalextricProtocol.CommandBuilder();
            builder.GetSlot(1).Brake = 50;
            builder.GetSlot(6).Brake = 150;
            var result = builder.Build();

            // Brake bytes are at indices 13-18
            Assert.Equal(50, result[13]);   // Slot 1
            Assert.Equal(150, result[18]);  // Slot 6
        }

        [Fact]
        public void Kers_SetsBitfield()
        {
            var builder = new ScalextricProtocol.CommandBuilder();
            builder.GetSlot(1).Kers = true; // Bit 0
            builder.GetSlot(3).Kers = true; // Bit 2
            builder.GetSlot(6).Kers = true; // Bit 5
            var result = builder.Build();

            // KERS bitfield is at byte 19
            // Bits 0, 2, 5 set = 0b00100101 = 37
            Assert.Equal(0b00100101, result[19]);
        }

        [Fact]
        public void CreatePowerOnCommand_ReturnsValidCommand()
        {
            var result = ScalextricProtocol.CommandBuilder.CreatePowerOnCommand(50);

            Assert.Equal(20, result.Length);
            Assert.Equal((byte)ScalextricProtocol.CommandType.PowerOnRacing, result[0]);
            for (int i = 1; i <= 6; i++)
            {
                Assert.Equal(50, result[i]);
            }
        }

        [Fact]
        public void CreatePowerOffCommand_ReturnsValidCommand()
        {
            var result = ScalextricProtocol.CommandBuilder.CreatePowerOffCommand();

            Assert.Equal(20, result.Length);
            Assert.Equal((byte)ScalextricProtocol.CommandType.NoPowerTimerStopped, result[0]);
            for (int i = 1; i <= 6; i++)
            {
                Assert.Equal(0, result[i]);
            }
        }
    }

    public class ThrottleProfileTests
    {
        [Fact]
        public void CreateLinearCurve_ReturnsCorrectLength()
        {
            var curve = ScalextricProtocol.ThrottleProfile.CreateLinearCurve();

            Assert.Equal(96, curve.Length);
        }

        [Fact]
        public void CreateLinearCurve_StartsNearZero()
        {
            var curve = ScalextricProtocol.ThrottleProfile.CreateLinearCurve();

            // First value should be close to 0
            Assert.True(curve[0] < 10);
        }

        [Fact]
        public void CreateLinearCurve_EndsNear255()
        {
            var curve = ScalextricProtocol.ThrottleProfile.CreateLinearCurve();

            // Last value should be close to 255
            Assert.True(curve[95] > 245);
        }

        [Fact]
        public void CreateLinearCurve_IsMonotonicallyIncreasing()
        {
            var curve = ScalextricProtocol.ThrottleProfile.CreateLinearCurve();

            for (int i = 1; i < curve.Length; i++)
            {
                Assert.True(curve[i] >= curve[i - 1], $"Value at {i} should be >= value at {i - 1}");
            }
        }

        [Fact]
        public void GetBlock_ReturnsCorrectLength()
        {
            var curve = ScalextricProtocol.ThrottleProfile.CreateLinearCurve();
            var block = ScalextricProtocol.ThrottleProfile.GetBlock(curve, 0);

            Assert.Equal(17, block.Length);
        }

        [Fact]
        public void GetBlock_FirstByteIsBlockIndex()
        {
            var curve = ScalextricProtocol.ThrottleProfile.CreateLinearCurve();

            for (int i = 0; i < 6; i++)
            {
                var block = ScalextricProtocol.ThrottleProfile.GetBlock(curve, i);
                Assert.Equal(i, block[0]);
            }
        }

        [Fact]
        public void GetBlock_ContainsCorrectValues()
        {
            var curve = ScalextricProtocol.ThrottleProfile.CreateLinearCurve();
            var block = ScalextricProtocol.ThrottleProfile.GetBlock(curve, 2);

            // Block 2 should contain values from curve[32] to curve[47]
            for (int i = 0; i < 16; i++)
            {
                Assert.Equal(curve[32 + i], block[i + 1]);
            }
        }

        [Fact]
        public void GetBlock_ThrowsForInvalidIndex()
        {
            var curve = ScalextricProtocol.ThrottleProfile.CreateLinearCurve();

            Assert.Throws<ArgumentOutOfRangeException>(() => ScalextricProtocol.ThrottleProfile.GetBlock(curve, -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => ScalextricProtocol.ThrottleProfile.GetBlock(curve, 6));
        }

        [Fact]
        public void GetAllBlocks_Returns6Blocks()
        {
            var curve = ScalextricProtocol.ThrottleProfile.CreateLinearCurve();
            var blocks = ScalextricProtocol.ThrottleProfile.GetAllBlocks(curve);

            Assert.Equal(6, blocks.Length);
        }

        [Fact]
        public void CreateLinearBlocks_ReturnsReadyToUseBlocks()
        {
            var blocks = ScalextricProtocol.ThrottleProfile.CreateLinearBlocks();

            Assert.Equal(6, blocks.Length);
            for (int i = 0; i < 6; i++)
            {
                Assert.Equal(17, blocks[i].Length);
                Assert.Equal(i, blocks[i][0]); // Block index
            }
        }

        [Fact]
        public void CreateExponentialCurve_ReturnsCorrectLength()
        {
            var curve = ScalextricProtocol.ThrottleProfile.CreateExponentialCurve();

            Assert.Equal(96, curve.Length);
        }

        [Fact]
        public void CreateExponentialCurve_StartsNearZero()
        {
            var curve = ScalextricProtocol.ThrottleProfile.CreateExponentialCurve();

            // First value should be very small due to x^2 curve
            Assert.True(curve[0] < 5);
        }

        [Fact]
        public void CreateExponentialCurve_EndsAt255()
        {
            var curve = ScalextricProtocol.ThrottleProfile.CreateExponentialCurve();

            // Last value should be 255
            Assert.Equal(255, curve[95]);
        }

        [Fact]
        public void CreateExponentialCurve_IsMonotonicallyIncreasing()
        {
            var curve = ScalextricProtocol.ThrottleProfile.CreateExponentialCurve();

            for (int i = 1; i < curve.Length; i++)
            {
                Assert.True(curve[i] >= curve[i - 1], $"Value at {i} should be >= value at {i - 1}");
            }
        }

        [Fact]
        public void CreateExponentialCurve_LowerValuesInFirstHalf()
        {
            var curve = ScalextricProtocol.ThrottleProfile.CreateExponentialCurve();
            var linear = ScalextricProtocol.ThrottleProfile.CreateLinearCurve();

            // Exponential should have lower values than linear in the first half
            // (due to x^2 being below x for x < 1)
            for (int i = 0; i < 48; i++)
            {
                Assert.True(curve[i] <= linear[i], $"Exponential value at {i} should be <= linear");
            }
        }

        [Fact]
        public void CreateSteppedCurve_ReturnsCorrectLength()
        {
            var curve = ScalextricProtocol.ThrottleProfile.CreateSteppedCurve();

            Assert.Equal(96, curve.Length);
        }

        [Fact]
        public void CreateSteppedCurve_HasFourDistinctBands()
        {
            var curve = ScalextricProtocol.ThrottleProfile.CreateSteppedCurve();

            // Band 1 (indices 0-23): 63
            // Band 2 (indices 24-47): 126
            // Band 3 (indices 48-71): 189
            // Band 4 (indices 72-95): 252
            Assert.Equal(63, curve[0]);
            Assert.Equal(63, curve[23]);
            Assert.Equal(126, curve[24]);
            Assert.Equal(126, curve[47]);
            Assert.Equal(189, curve[48]);
            Assert.Equal(189, curve[71]);
            Assert.Equal(252, curve[72]);
            Assert.Equal(252, curve[95]);
        }

        [Fact]
        public void CreateSteppedCurve_IsMonotonicallyNonDecreasing()
        {
            var curve = ScalextricProtocol.ThrottleProfile.CreateSteppedCurve();

            for (int i = 1; i < curve.Length; i++)
            {
                Assert.True(curve[i] >= curve[i - 1], $"Value at {i} should be >= value at {i - 1}");
            }
        }

        [Fact]
        public void CreateCurve_ReturnsLinearForLinearType()
        {
            var curve = ScalextricProtocol.ThrottleProfile.CreateCurve(ThrottleProfileType.Linear);
            var linear = ScalextricProtocol.ThrottleProfile.CreateLinearCurve();

            Assert.Equal(linear, curve);
        }

        [Fact]
        public void CreateCurve_ReturnsExponentialForExponentialType()
        {
            var curve = ScalextricProtocol.ThrottleProfile.CreateCurve(ThrottleProfileType.Exponential);
            var exponential = ScalextricProtocol.ThrottleProfile.CreateExponentialCurve();

            Assert.Equal(exponential, curve);
        }

        [Fact]
        public void CreateCurve_ReturnsSteppedForSteppedType()
        {
            var curve = ScalextricProtocol.ThrottleProfile.CreateCurve(ThrottleProfileType.Stepped);
            var stepped = ScalextricProtocol.ThrottleProfile.CreateSteppedCurve();

            Assert.Equal(stepped, curve);
        }

        [Fact]
        public void CreateBlocks_ReturnsCorrectBlocksForProfileType()
        {
            var blocks = ScalextricProtocol.ThrottleProfile.CreateBlocks(ThrottleProfileType.Exponential);
            var expectedCurve = ScalextricProtocol.ThrottleProfile.CreateExponentialCurve();

            Assert.Equal(6, blocks.Length);
            for (int i = 0; i < 6; i++)
            {
                Assert.Equal(17, blocks[i].Length);
                Assert.Equal(i, blocks[i][0]); // Block index
                // Verify curve values
                for (int j = 0; j < 16; j++)
                {
                    Assert.Equal(expectedCurve[i * 16 + j], blocks[i][j + 1]);
                }
            }
        }

        [Theory]
        [InlineData(ThrottleProfileType.Linear)]
        [InlineData(ThrottleProfileType.Exponential)]
        [InlineData(ThrottleProfileType.Stepped)]
        public void AllProfileTypes_Return96Values(ThrottleProfileType profileType)
        {
            var curve = ScalextricProtocol.ThrottleProfile.CreateCurve(profileType);

            Assert.Equal(96, curve.Length);
        }
    }

    public class CharacteristicsTests
    {
        [Fact]
        public void GetThrottleProfileForSlot_ReturnsCorrectGuids()
        {
            Assert.Equal(ScalextricProtocol.Characteristics.ThrottleProfile1,
                ScalextricProtocol.Characteristics.GetThrottleProfileForSlot(1));
            Assert.Equal(ScalextricProtocol.Characteristics.ThrottleProfile2,
                ScalextricProtocol.Characteristics.GetThrottleProfileForSlot(2));
            Assert.Equal(ScalextricProtocol.Characteristics.ThrottleProfile3,
                ScalextricProtocol.Characteristics.GetThrottleProfileForSlot(3));
            Assert.Equal(ScalextricProtocol.Characteristics.ThrottleProfile4,
                ScalextricProtocol.Characteristics.GetThrottleProfileForSlot(4));
            Assert.Equal(ScalextricProtocol.Characteristics.ThrottleProfile5,
                ScalextricProtocol.Characteristics.GetThrottleProfileForSlot(5));
            Assert.Equal(ScalextricProtocol.Characteristics.ThrottleProfile6,
                ScalextricProtocol.Characteristics.GetThrottleProfileForSlot(6));
        }

        [Fact]
        public void GetThrottleProfileForSlot_ThrowsForInvalidSlot()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                ScalextricProtocol.Characteristics.GetThrottleProfileForSlot(0));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                ScalextricProtocol.Characteristics.GetThrottleProfileForSlot(7));
        }
    }

    public class SlotPowerTests
    {
        [Fact]
        public void EncodePowerByte_PowerOnly()
        {
            var slot = new ScalextricProtocol.SlotPower { PowerMultiplier = 30 };

            Assert.Equal(30, slot.EncodePowerByte());
        }

        [Fact]
        public void EncodePowerByte_PowerMaskedTo6Bits()
        {
            var slot = new ScalextricProtocol.SlotPower { PowerMultiplier = 100 }; // > 63

            // Should mask to 6 bits (100 & 0x3F = 36)
            Assert.Equal(36, slot.EncodePowerByte());
        }

        [Fact]
        public void EncodePowerByte_WithGhostMode()
        {
            var slot = new ScalextricProtocol.SlotPower
            {
                PowerMultiplier = 30,
                GhostMode = true
            };

            Assert.Equal(30 | 0x80, slot.EncodePowerByte());
        }

        [Fact]
        public void EncodePowerByte_WithPowerBitSix()
        {
            var slot = new ScalextricProtocol.SlotPower
            {
                PowerMultiplier = 20,
                PowerBitSix = true
            };

            Assert.Equal(20 | 0x40, slot.EncodePowerByte());
        }

        [Fact]
        public void EncodePowerByte_AllFlags()
        {
            var slot = new ScalextricProtocol.SlotPower
            {
                PowerMultiplier = 15,
                PowerBitSix = true,
                GhostMode = true
            };

            // 15 + bit6 (0x40) + bit7 (0x80) = 15 + 64 + 128 = 207
            Assert.Equal(15 | 0x40 | 0x80, slot.EncodePowerByte());
        }
    }
}
