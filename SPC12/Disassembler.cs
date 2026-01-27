
namespace SPC12
{

    internal class Disassembler
    {
        public Disassembler(IMemoryBus mem)
        {
            _mem = mem;
        }

        public int Disassemble(int address, out string disassembly)
        {
            disassembly = String.Empty;
            byte byte1 = _mem.ReadByte(address);

            int op = (byte1 >> 4);
            switch (op)
            {
                case 0x0:
                    {
                        byte byte2 = GetImmediateCommandByte(address);

                        // consume the instruction bytes thus far
                        address += 2;

                        switch (byte1)
                        {
                            // ARITHMETIC/LOGICAL:  00000100
                            // REGISTER TRANSFER:   00001000
                            // PULSE/SHIFT:         00001100

                            case 0x04:  // Arithmetic / Logical
                                disassembly = ArithmeticLogical(byte2, ref address);
                                break;

                            case 0x08:  // Register/Transfer
                                disassembly = RegisterTransfer(byte2);
                                break;

                            case 0x0c:  // Pulse/Shift
                                disassembly = PulseShift(byte2);
                                break;

                            default:    // Must be augment mem.
                                disassembly = AugmentMemoryAddressing(byte1, byte2);
                                break;
                        }
                    }
                    break;

                // SHARED ARITHMETIC:   0001xxxx
                case 0x1:
                    {
                        address++;
                        byte byte2 = GetSharedCommandByte(byte1, ArithmeticLogicalSharedOffset);
                        disassembly = ArithmeticLogical(byte2, ref address) + "  !shared";
                    }
                    break;

                // SHARED REGISTER TRAN:0010xxxx
                case 0x2:
                    {
                        address++;
                        byte byte2 = GetSharedCommandByte(byte1, RegisterTransferSharedOffset);
                        disassembly = RegisterTransfer(byte2) + "  !shared";
                    }
                    break;

                // SHARED PULSE:        0011xxxx
                case 0x3:
                    {
                        address++;
                        byte byte2 = GetSharedCommandByte(byte1, PulseShiftSharedOffset);
                        disassembly =PulseShift(byte2) + "  !shared";
                    }
                    break;

                // SKIP CONDITIONALLY:  010xxxxx
                case 0x4:
                case 0x5:
                    address++;
                    disassembly = ConditionalSkip(byte1, address);
                    break;

                // JUMP UNCONDITIONALLY:0110xxxx
                case 0x6:
                    {
                        ushort dest = GetImmediateAddress(byte1, address);
                        disassembly = $"JMP {Helpers.ToOctal(dest)}";
                        address += 2;
                    }
                    break;

                // LOAD B EXTENDED:     0111xxxx
                case 0x7:
                    {
                        byte byte2 = GetImmediateAddressByte(address);
                        int eoa = ((byte1 & 0xf) << 8) | byte2;

                        disassembly = $"ELB {Helpers.ToOctal((ushort)eoa)}";

                        address += 2;
                    }
                    break;

                // SHARED AUGMENT:      10xxxxxx
                case 0x8:
                case 0x9:
                case 0xa:
                case 0xb:
                    {
                        address++;
                        byte byte2 = GetSharedCommandByte(byte1, AugmentedMemorySharedOffset);
                        disassembly = AugmentMemoryAddressing(byte1, byte2) + "  !shared";
                    }
                    break;

                // LOAD/STORE B:        110xxxxx or 111xxxxx
                case 0xc:
                    {
                        disassembly = $"STB {Helpers.ToOctal(GetImmediateAddress(byte1, address))}";
                        address += 2;
                    }
                    break;

                case 0xd:
                    {
                        disassembly = $"STB {Helpers.ToOctal(GetImmediateAddress(byte1, address))}, X";
                        address += 2;
                    }
                    break;

                case 0xe:
                    {
                        disassembly = $"LDB {Helpers.ToOctal(GetImmediateAddress(byte1, address))}";
                        address += 2;
                    }
                    break;

                case 0xf:
                    {
                        disassembly = $"LDB {Helpers.ToOctal(GetImmediateAddress(byte1, address))}, X";
                        address += 2;
                    }
                    break;
            }

            return address;
        }

        private byte GetImmediateCommandByte(int p)
        {
            return _mem.ReadByte(p + 1);

        }

        private byte GetImmediateAddressByte(int p)
        {
            return _mem.ReadByte(p + 1);

        }

        private ushort GetImmediateAddress(byte byte1, int p)
        {
            byte byte2 = _mem.ReadByte(p + 1);
            return (ushort)(((byte1 & 0xf) << 8) | byte2);
        }

        private byte GetSharedCommandByte(byte byte1, ushort sharedOffset)
        {
            // shared
            int ccba;

            // This is kind of ugly, move to enum for this?
            // Or just move the shift outside of this function?
            if (sharedOffset == AugmentedMemorySharedOffset)
            {
                // has different encoding for byte 1 because REASONS
                ccba = (byte1 >> 2) & 0x0f;
            }
            else
            {
                ccba = byte1 & 0xf;
            }
            return _mem.ReadByte((ushort)(ccba + sharedOffset));
        }

        private string ArithmeticLogical(byte byte2, ref int p)
        {
            int opcode = (byte2 >> 4);

            // Operand: 0 - B register, 1 - immediate value
            bool immediate = (byte2 & 0x08) != 0;

            string operand = (byte2 & 0x08) == 0 ? "B" : Helpers.ToOctal(_mem.ReadByte(p++));
            Register reg = (Register)(byte2 & 0x7);
            switch (opcode)
            {
                case 0: // Zero
                    return $"AZE {reg}";

                case 1: // And
                    return $"AND {reg}, {operand}";

                case 2: // xor:
                    return $"AXR {reg}, {operand}";

                case 3: // or:
                    return $"AOR {reg}, {operand}";

                case 4: // load:
                    return $"ALD {reg}, {operand}";

                case 8:   // subtract
                    return $"ASU {reg}, {operand}";

                case 0xc: // add
                    return $"AAD {reg}, {operand}";

                default:
                    return "INVALID ARITHMETIC LOGICAL INSTRUCTION";

            }
        }

        private string RegisterTransfer(byte byte2)
        {
            int command = (byte2 >> 6);
            Register dest = (Register)((byte2 & 0x38) >> 3);
            Register src = (Register)(byte2 & 0x7);

            // TODO: LINK logic sucks assssssss
            switch (command)
            {
                case 0: // Transfer and add link bit
                    return $"RLK {src}, {dest}";

                case 1: // Transfer and increment
                    return $"RIC {src}, {dest}";

                case 2: // Transfer and decrement
                    // Special case; if src is the B register then the destination just gets 7777.
                    // Why?  I have no idea, it's explicitly called out as a special case in the docs.
                    if (src == Register.B)
                    {
                        return $"RDC {src}, {dest}  !B <- 7777";
                    }
                    else
                    {
                        return $"RDC {src}, {dest}";
                    }

                case 3: // Transfer
                    return $"RTR {src}, {dest}";

                default:
                    return "INVALID REGISTER TRANSFER INSTRUCTION";

            }
        }

        private string PulseShift(byte byte2)
        {
            int mode = (byte2 >> 7);

            if (mode == 0 )
            {
                return Shift(byte2);
            }
            else
            {
                return Pulse(byte2);
            }
        }

        private string Shift(byte byte2)
        {
            int command = (byte2 >> 3);
            Register reg = (Register)(byte2 & 0x7);

            switch (command) 
            {
                case 0: // Shift register right
                    return $"SHR {reg}";

                case 1: // Circulate register with link bit
                    return $"SHL {reg}";

                case 2: // Circulate register
                    return $"SHC {reg}";

                case 4: // Shift serial data in
                    return $"SHI {reg}";

                case 9: // Circulate register with link bit and data out
                    return $"SHLO {reg}";

                case 0xa: // Circulate serial data out
                    return $"SHCO {reg}";

                case 0xc: // Shift Serial Data In and Echo Out
                    return $"SHIO {reg}";

                default:
                    return "INVALID SHIFT INSTRUCTION";
            }
        }

        private string Pulse(byte byte2)
        {
            bool halt = (byte2 & 0x40) != 0;
            int command = (byte2 & 0x7);

            switch (command)
            {
                case 0: // Reset link
                    if (halt)
                    {
                        return "PLR/HALT";
                    }
                    else
                    {
                        return "PLR";
                    }

                case 1: // Set link
                    return "PLS";

                case 2: // Function Address Pulse
                    return "FOB";

                case 3: // Transfer Out Pulse
                    return "DOB";

                case 4: // Transfer In Pulse
                    return "DIB"; // still hates ZIM

                case 5: // Transfer (B) upper to B
                    return "TBB";

                case 6: // Load E register
                    return "TBE";

                case 7: // Interrogate interrupt
                    return "INE";

                default:
                    return "INVALID CONTROL INSTRUCTION";
            }
        }

        private string AugmentMemoryAddressing(byte byte1, byte byte2)
        {
            int field = byte1 & 0x3;
            Register preIndexReg = (Register)(byte2 >> 6);
            bool indirectAddressing = (byte2 & 0x20) != 0;
            int command = (byte2 & 0x18) >> 3;
            Register reg = (Register)(byte2 & 0x7);

            int B = (ushort)(field * 2);

            // Finally... do the thing with the EOA:
            switch (command)
            {
                case 0: // Load:
                    if (indirectAddressing)
                    {
                        return $"G{B}L {reg}, {preIndexReg}, I";
                    }
                    else
                    {
                        return $"G{B}L {reg}, {preIndexReg}";
                    }
                    
                case 1: // Store and zero:
                    if (indirectAddressing)
                    {
                        return $"G{B}Z {reg}, {preIndexReg}, I";
                    }
                    else
                    {
                        return $"G{B}Z {reg}, {preIndexReg}";
                    }

                case 2: // Add:
                    if (indirectAddressing)
                    {
                        return $"G{B}A {reg}, {preIndexReg}, I";
                    }
                    else
                    {
                        return $"G{B}A {reg}, {preIndexReg}";
                    }

                case 3: // Store:
                    if (indirectAddressing)
                    {
                        return $"G{B}S {reg}, {preIndexReg}, I";
                    }
                    else
                    {
                        return $"G{B}S {reg}, {preIndexReg}";
                    }

                default:
                    return "INVALID AUGMENT INSTRUCTION";
            }
        }

        private string ConditionalSkip(byte byte1, int p)
        {
            int offset = (byte1 & 0x3) * 2;
            int condition = (byte1 & 0x1c) >> 2;

            string mnemonic = "INV";

            switch (condition)
            {
                case 0: // Skip if I/O test false
                    mnemonic = "SKF";
                    break;

                case 1: // Skip if link reset
                    mnemonic = "SKR";
                    break;

                case 2: // Skip if not zero
                    mnemonic = "SKN";
                    break;

                case 3: // Skip if minus
                    mnemonic = "SKM";
                    break;

                case 4: // Skip if I/O test true
                    mnemonic = "SKT";
                    break;

                case 5: // Skip if link set
                    mnemonic = "SKS";
                    break;

                case 6: // Skip if zero
                    mnemonic = "SKZ";
                    break;

                case 7: // Skip if plus
                    mnemonic = "SKP";
                    break;
            }

            return string.Format("{0} {1}   !({2})", mnemonic, offset, Helpers.ToOctal((ushort)((p + offset) & 0xfff)));
        }

        private enum Register
        {
            A = 0,
            X,
            Y,
            Z,
            P,
            B,
            YY,
            ZZ
        }

        // addresses for shared command code bytes:
        static readonly ushort ArithmeticLogicalSharedOffset = 0x10;
        static readonly ushort RegisterTransferSharedOffset  = 0x20;
        static readonly ushort PulseShiftSharedOffset        = 0x30;
        static readonly ushort AugmentedMemorySharedOffset   = 0x40;

        IMemoryBus _mem;
    }
}
