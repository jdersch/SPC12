
namespace SPC12
{
    public interface IMemoryBus
    {
        byte ReadByte(int address, int mode);
        void WriteByte(int address, int mode, byte value);
    }

    public interface IIOBus
    {

    }

    public class Processor
    {
        public static readonly float CycleTimeMicroseconds = 2.16f;
        public Processor(IMemoryBus mem, IIOBus io)
        {
            _mem = mem;
            _io = io;
            _tty = new TTY();
        }

        public void Reset()
        {
            _a = 0;
            _x = 0;
            _y = 0;
            _z = 0;
            _p = 0;
            _b = 0;
            _e = 0;

            _tty.Reset();
        }


        /// <summary>
        /// Executes a single instruction, returns the number of memory cycles consumed.
        /// </summary>
        /// <returns></returns>
        public int Step()
        {
            int cycles = 0;
            byte byte1 = ReadByte(P);

            // let's just write out some instruction encodings here:
            // LOAD/STORE B:        110xxxxx or 111xxxxx
            // LOAD B EXTENDED:     0111xxxx
            // ARITHMETIC/LOGICAL:  00000100
            // REGISTER TRANSFER:   00001000
            // PULSE/SHIFT:         00001100
            // AUGMENT MEMORY ADDR: 000000xx
            // SKIP CONDITIONALLY:  010xxxxx
            // JUMP UNCONDITIONALLY:0110xxxx
            // SHARED ARITHMETIC:   0001xxxx
            // SHARED REGISTER TRAN:0010xxxx
            // SHARED PULSE:        0011xxxx
            // SHARED AUGMENT:      10xxxxxx

            // Cool.  Cool cool cool.  Let's do our decoding then I suppose
            int op = (byte1 >> 4);
            switch (op)
            {
                case 0x0:
                    {
                        byte byte2 = GetImmediateCommandByte();

                        // consume the instruction bytes thus far
                        P++;

                        switch (byte1)
                        {
                            // ARITHMETIC/LOGICAL:  00000100
                            // REGISTER TRANSFER:   00001000
                            // PULSE/SHIFT:         00001100

                            case 0x04:  // Arithmetic / Logical
                                cycles = ArithmeticLogical(byte2);
                                break;

                            case 0x08:  // Register/Transfer
                                 RegisterTransfer(byte2);
                                cycles = 2;
                                break;

                            case 0x0c:  // Pulse/Shift
                                PulseShift(byte2);
                                cycles = 2;
                                break;

                            default:    // Must be augment mem.
                                cycles = AugmentMemoryAddressing(byte1, byte2);
                                break;
                        }

                        // consume last byte
                        P++;
                    }
                    break;

                // SHARED ARITHMETIC:   0001xxxx
                case 0x1:
                    {

                        byte byte2 = GetSharedCommandByte(byte1, ArithmeticLogicalSharedOffset);
                        cycles = ArithmeticLogical(byte2);
                        P++;
                    }
                    break;

                // SHARED REGISTER TRAN:0010xxxx
                case 0x2:
                    {
                        byte byte2 = GetSharedCommandByte(byte1, RegisterTransferSharedOffset);
                        RegisterTransfer(byte2);
                        P++;
                        cycles = 2;
                    }
                    break;

                // SHARED PULSE:        0011xxxx
                case 0x3:
                    {
                        byte byte2 = GetSharedCommandByte(byte1, PulseShiftSharedOffset);
                        PulseShift(byte2);
                        P++;
                        cycles = 2;
                    }
                    break;

                // SKIP CONDITIONALLY:  010xxxxx
                case 0x4:
                case 0x5:
                    P++;
                    ConditionalSkip(byte1);
                    cycles = 1;
                    break;

                // JUMP UNCONDITIONALLY:0110xxxx
                case 0x6:
                    {
                        P = GetImmediateAddress(byte1);

                        // Why?  Because this system is weiiiird.
                        _zero = true;
                        _plus = true;
                        cycles = 2;
                    }
                    break;

                // LOAD B EXTENDED:     0111xxxx
                case 0x7:
                    {
                        byte byte2 = GetImmediateAddressByte();
                        int eoa = ((byte1 & 0xf) << 8) | byte2;
                        B = (ushort)(ReadByte((ushort)(eoa)) | (ReadByte((ushort)(eoa+1)) << 8));
                        _zero = true;
                        _plus = true;
                        P += 2;
                        cycles = 4;
                    }
                    break;

                // SHARED AUGMENT:      10xxxxxx
                case 0x8:
                case 0x9:
                case 0xa:
                case 0xb:
                    {
                        byte byte2 = GetSharedCommandByte(byte1, AugmentedMemorySharedOffset);
                        cycles = AugmentMemoryAddressing(byte1, byte2);
                        P++;
                    }
                    break;

                // LOAD/STORE B:        110xxxxx or 111xxxxx
                case 0xc:
                    {
                        WriteByte(GetImmediateAddress(byte1), (byte)B);
                        _zero = (B & 0xff) == 0;
                        _plus = (B & 0x80) == 0;
                        P += 2;
                        cycles = 3;
                    }
                    break;

                case 0xd:
                    {
                        WriteByte(GetImmediateAddress(byte1) + X, (byte)B);
                        _zero = (B & 0xff) == 0;
                        _plus = (B & 0x80) == 0;
                        P += 2;
                        cycles = 3;
                    }
                    break;

                case 0xe:
                    {
                        B = ReadByte(GetImmediateAddress(byte1));
                        _zero = (B & 0xff) == 0;
                        _plus = (B & 0x80) == 0;
                        P += 2;
                        cycles = 3;
                    }
                    break;

                case 0xf:
                    {
                        B = ReadByte(GetImmediateAddress(byte1) + X);
                        _zero = (B & 0xff) == 0;
                        _plus = (B & 0x80) == 0;
                        P += 2;
                        cycles = 3;
                    }
                    break;
            }

            // Do this here for now, but the TTY should be moved outside the Processor methinks.
            _tty.Clock(cycles);

            return cycles;
        }

        public ushort A
        {
            get { return _a; }
            set { _a = (ushort)(value & 0xfff); }
        }

        public ushort X
        {
            get { return _x; }
            set { _x = (ushort)(value & 0xfff); }
        }

        public ushort Y
        {
            get { return _y; }
            set { _y = (ushort)(value & 0xfff); }
        }

        public ushort Z
        {
            get { return _z; }
            set { _z = (ushort)(value & 0xfff); }
        }

        public ushort P
        {
            get { return _p; }
            set { _p = (ushort)(value & 0xfff); }
        }

        public ushort B
        {
            get { return _b; }
            set { _b = (ushort)(value & 0xfff); }
        }

        public int E
        {
            get { return _e; }
            set { _e = value & 0x3; }
        }

        public ushort YY
        {
            get { return (ushort)(_y >> 8); }
        }

        public ushort ZZ
        {
            get { return (ushort)(_z >> 8); }
        }

        public bool Link
        {
            get { return _link; }
        }

        public bool Plus
        {
            get { return _plus; }
        }

        public bool Zero
        {
            get { return _zero; }
        }

        public bool IOStatus
        {
            get { return _ioStatus; }
        }

        public TTY TTY
        { 
            get { return _tty; }
        }

        private byte GetImmediateCommandByte()
        {
            return ReadByte(P + 1);
        }

        private byte GetImmediateAddressByte()
        {
            return ReadByte(P + 1);
        }

        private ushort GetImmediateAddress(byte byte1)
        {
            byte byte2 = ReadByte(P + 1);
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
            return ReadByte((ushort)(ccba + sharedOffset));
        }

        /// <summary>
        /// Returns cycles consumed
        /// </summary>
        /// <param name="byte2"></param>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        private int ArithmeticLogical(byte byte2)
        {
            int opcode = (byte2 >> 4);

            // Operand: 0 - B register, 1 - immediate value
            bool immediate = (byte2 & 0x08) != 0;
            ushort operand = immediate ? ReadByte((P++) + 1) : B;
            Register reg = (Register)(byte2 & 0x7);
            switch (opcode)
            {
                case 0: // Zero
                    SetRegister(reg, 0);
                    break;

                case 1: // And
                    {
                        int r = GetRegister(reg);
                        SetRegister(reg, r & operand);
                    }
                    break;

                case 2: // xor:
                    {
                        int r = GetRegister(reg);
                        SetRegister(reg, r ^ operand);
                    }
                    break;

                case 3: // or:
                    {
                        int r = GetRegister(reg);
                        SetRegister(reg, r | operand);
                    }
                    break;

                case 4: // load:
                    {
                        B = operand;
                        SetRegister(reg, B);
                        _zero = true;
                        _plus = true;
                    }
                    break;

                case 8:   // subtract
                    {
                        int r = GetRegister(reg);
                        // TODO: is this link behavior correct (see also the decrement RT instructions)
                        _link = (r & 0xff) - (operand & 0xff) > 0 ? true : false;
                        r = r - operand;
                        SetRegister(reg, r);
                    }
                    break;

                case 0xc: // add
                    {
                        int r = GetRegister(reg);
                        _link = (r & 0xff) + (operand & 0xff) > 0xff ? true : false;
                        r = r + operand;
                        SetRegister(reg, r);
                    }
                    break;

                default:
                    throw new NotImplementedException("Unimplemented arithmetic/logic op");

            }

            if (opcode != 4)        // sigh.  See p. 3-8, ALD docs.
            {
                _zero = (GetRegister(reg) & 0xff) == 0;
                _plus = (GetRegister(reg) & 0x80) == 0;
            }

            return immediate ? 3 : 2;
        }

        private void RegisterTransfer(byte byte2)
        {
            int command = (byte2 >> 6);
            Register dest = (Register)((byte2 & 0x38) >> 3);
            Register src = (Register)(byte2 & 0x7);
            switch (command)
            {
                case 0: // Transfer and add link bit
                    bool newLink = _link ? (GetRegister(src) & 0xff) == 0xff : false;
                    SetRegister(dest, GetRegister(src) + (_link ? 1 : 0));
                    _link = newLink;
                    break;

                case 1: // Transfer and increment
                    _link = (GetRegister(src) & 0xff) == 0xff;
                    SetRegister(dest, GetRegister(src) + 1);
                    break;

                case 2: // Transfer and decrement
                    // Special case; if src is the B register then the destination just gets 7777.
                    // Why?  I have no idea, it's explicitly called out as a special case in the docs.
                    if (src == Register.B)
                    {
                        SetRegister(dest, 0xfff);
                    }
                    else
                    {
                        _link = (GetRegister(src) & 0xff) > 0;     // this should be zero, right?
                        SetRegister(dest, GetRegister(src) - 1);

                        // problem: when modifying PC, code seems to assume that PC will be incremented after the transfer instruction
                        // what is the expected order of operations in general?
                    }
                    break;

                case 3: // Transfer
                    // link unaffected
                    SetRegister(dest, GetRegister(src));
                    break;

            }

            // TODO: maybe factor this out
            _zero = (GetRegister(dest) & 0xff) == 0;
            _plus = (GetRegister(dest) & 0x80) == 0;
        }

        private void PulseShift(byte byte2)
        {
            int mode = (byte2 >> 7);

            if (mode == 0 )
            {
                Shift(byte2);
            }
            else
            {
                Pulse(byte2);
            }
        }

        private void Shift(byte byte2)
        {
            int command = (byte2 >> 3);
            Register reg = (Register)(byte2 & 0x7);

            switch (command) 
            {
                case 0: // Shift register right
                case 8: // 0's to TTY
                    {
                        int r = GetRegister(reg) & 0xff;
                        SetRegister(reg, r >> 1);
                        _link = (r & 0x1) != 0;
                        _plus = true; // P. 3-11

                        if (command == 8)
                        {
                            // shifts a 0 to the TTY
                            TTY.StrobePrinter(false);
                        }
                    }
                    break;

                case 1: // Circulate register with link bit
                case 9: // with data out
                    {
                        int r = GetRegister(reg) & 0xff;
                        bool oldLink = _link;
                        SetRegister(reg, (r >> 1) | (oldLink ? 0x80 : 0x0));
                        _link = (r & 0x1) != 0; // link gets the previous 0-bit

                        if (command == 9)
                        {
                            TTY.StrobePrinter(oldLink);   // shift the previous link out the TTY
                        }
                    }
                    break;

                case 2: // Circulate register
                case 0xa:   // with data out
                    {
                        int r = GetRegister(reg) & 0xff;
                        _link = (r & 0x1) != 0;     // the textual description of SHCO on p. 3-13 does not mention the Link register.  But it later says (r)0 -> (Link)...
                                                    // motorola docs also suggest that link is updated here
                        SetRegister(reg, (r >> 1) | (_link ? 0x80 : 0x0));

                        if (command == 0xa)
                        {
                            // current link to tty
                            TTY.StrobePrinter(_link);
                        }
                    }
                    break;

                case 4: // Shift serial data in
                case 0xc:   // also out
                    {
                        int r = GetRegister(reg) & 0xff;
                        bool bit = TTY.StrobeKeyboard();
                        int bitValue = bit ? 0x80 : 0x00;
                        SetRegister(reg, ((r >> 1) | bitValue));
                        _link = (r & 0x1) != 0;
                        _plus = (r & 0x80) == 0;

                        if (command == 0xc)
                        {
                            // Also strobe the bit out
                            TTY.StrobePrinter(bit);
                        }
                    }
                    break;

                default:
                    throw new InvalidOperationException("Invalid shift command.");
            }

            _zero = (GetRegister(reg) & 0xff) == 0;

            if (command != 0)  // right shift is a little special boy, see p. 3-11
            {
                _plus = (GetRegister(reg) & 0x80) == 0;
            }
        }

        private void Pulse(byte byte2)
        {
            bool halt = (byte2 & 0x40) != 0;
            int command = (byte2 & 0x7);

            switch (command)
            {
                case 0: // Reset link
                    _link = false;

                    if (halt)
                    {
                        throw new NotImplementedException("Halt not implemented, but this will do.");
                    }
                    break;

                case 1: // Set link
                    _link = true;
                    break;

                case 2: // Function Address Pulse
                case 3: // Transfer Out Pulse
                case 4: // Transfer In Pulse
                    throw new NotImplementedException("Pulse command not implemented.");

                case 5: // Transfer (B) upper to B
                    B = (ushort)(B >> 8);
                    _plus = true;
                    _zero = (B == 0);
                    break;

                case 6: // Load E register
                    E = B;
                    _zero = true;   // p. 3-15
                    _plus = true;
                    break;

                case 7: // Interrogate interrupt
                    // WHERE WERE YOU ON OCTOBER THIRD NINETEEN SIXTY-EIGHT?
                    throw new NotImplementedException("Interrogate Interrupt command not implemented.");

            }
        }

        /// <summary>
        /// Returns cycles consume
        /// </summary>
        /// <param name="byte1"></param>
        /// <param name="byte2"></param>
        /// <returns></returns>
        private int AugmentMemoryAddressing(byte byte1, byte byte2)
        {
            int field = byte1 & 0x3;
            Register preIndexReg = (Register)(byte2 >> 6);
            bool indirectAddressing = (byte2 & 0x20) != 0;
            int command = (byte2 & 0x18) >> 3;
            Register reg = (Register)(byte2 & 0x7);

            B = (ushort)(field * 2);
            if (preIndexReg != 0)
            {
                // Preindexing
                B = (ushort)(B + GetRegister(preIndexReg));
            }

            if (indirectAddressing)
            {
                int indirectAddress = ReadByte(B);
                indirectAddress |= (ReadByte((ushort)(B + 1)) << 8);

                // This "address" is actually 12 bits of address and 4 bits specifying indexing options
                // to be used by the augmented indirect instruction that reads it.
                // This is a really, really weird architecture.
                Register postIndexReg = (Register)(indirectAddress >> 14);
                bool autoIncrement = (indirectAddress & 0x2000) != 0;
                B = (ushort)indirectAddress;

                if (postIndexReg != 0)
                {
                    if (autoIncrement)
                    {
                        SetRegister(postIndexReg, GetRegister(postIndexReg) + 1);
                    }

                    B = (ushort)(GetRegister(postIndexReg) + B);
                }
            }

            // Finally... do the thing with the EOA:
            switch (command)
            {
                case 0: // Load:
                    {
                        ushort operand = ReadByte(B);
                        SetRegister(reg, operand);
                    }
                    break;
                case 1: // Store and zero:
                    {
                        WriteByte(B, (byte)GetRegister(reg));
                        SetRegister(reg, 0);
                    }
                    break;
                case 2: // Add:
                    {
                        byte operand = ReadByte(B);
                        int result = operand + GetRegister(reg);
                        _link = (operand + (GetRegister(reg) & 0xff)) > 0xff;
                        SetRegister(reg, result);
                    }
                    break;
                case 3: // Store:
                    {
                        WriteByte(B, (byte)GetRegister(reg));

                        // See p. 3-21 of the PDF
                        _zero = true;
                        _plus = true;
                    }
                    break;
            }

            // See case 3 above for a link to why.  I don't know why it works this way, I just follow the docs where they lead me, man
            if (command != 3)
            {
                _zero = (GetRegister(reg) & 0xff) == 0;
                _plus = (GetRegister(reg) & 0x80) == 0;
            }

            return indirectAddressing ? 5 : 3;
        }

        private void ConditionalSkip(byte byte1)
        {
            int offset = (byte1 & 0x3) * 2;
            int condition = (byte1 & 0x1c) >> 2;

            bool conditionMet = false;
            switch (condition)
            {
                case 0: // Skip if I/O test false
                    conditionMet = !_ioStatus;
                    break;

                case 1: // Skip if link reset
                    conditionMet = !_link;
                    break;

                case 2: // Skip if not zero
                    conditionMet = !_zero;
                    break;

                case 3: // Skip if minus
                    conditionMet = !_plus;
                    break;

                case 4: // Skip if I/O test true
                    conditionMet = _ioStatus;
                    break;

                case 5: // Skip if link set
                    conditionMet = _link;
                    break;

                case 6: // Skip if zero
                    conditionMet = _zero;
                    break;

                case 7: // Skip if plus
                    conditionMet = _plus;
                    break;
            }

            if (conditionMet)
            {
                P = (ushort)(P + offset);
            }
        }


        // TODO: Replace switch statement with array of registers and just index into that;
        // this is silly.
        private ushort GetRegister(Register reg)
        {
            switch (reg)
            {
                case Register.A:
                    return A;

                case Register.X:
                    return X;

                case Register.Y:
                    return Y;

                case Register.Z:
                    return Z;

                case Register.P:
                    return P;

                case Register.B:
                    return B;

                case Register.YY:
                    return YY;

                case Register.ZZ:
                    return ZZ;

                default:
                    throw new ArgumentOutOfRangeException(nameof(reg));
            }
        }

        private void SetRegister(Register reg, int val)
        {
            switch (reg)
            {
                case Register.A:
                    A = (ushort)val;
                    break;

                case Register.X:
                    X = (ushort)val;
                    break;

                case Register.Y:
                    Y = (ushort)val;
                    break;

                case Register.Z:
                    Z = (ushort)val;
                    break;

                case Register.P:
                    P = (ushort)val;
                    break;

                case Register.B:
                    B = (ushort)val;
                    break;

                case Register.YY:
                    Y = (ushort)((Y & 0xff) | ((val & 0xf) << 8));
                    break;

                case Register.ZZ:
                    Z = (ushort)((Z & 0xff) | ((val & 0xf) << 8));
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(reg));
            }
        }

        private byte ReadByte(int addr)
        {
            return _mem.ReadByte(addr, E);
        }

        private void WriteByte(int addr, byte val)
        {
            _mem.WriteByte(addr, E, val);
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

        // Registers backing store (12 bit)
        ushort _a;
        ushort _x;
        ushort _y;
        ushort _z;
        ushort _p;
        ushort _b;

        // memory block address
        int _e;

        // Flags
        bool _link;
        bool _plus;
        bool _zero;
        bool _ioStatus;

        // addresses for shared command code bytes:
        static readonly ushort ArithmeticLogicalSharedOffset = 0x10;
        static readonly ushort RegisterTransferSharedOffset  = 0x20;
        static readonly ushort PulseShiftSharedOffset        = 0x30;
        static readonly ushort AugmentedMemorySharedOffset   = 0x40;

        // Built-in weird-ass bit-banged TTY interface
        TTY _tty;

        IMemoryBus _mem;
        IIOBus _io;
    }
}
