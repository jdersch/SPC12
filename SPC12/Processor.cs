using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SPC12
{
    public interface IMemoryBus
    {
        byte ReadByte(ushort address);
        void WriteByte(ushort address, byte value);
    }

    public interface IIOBus
    {

    }

    internal class Processor
    {
        public Processor(IMemoryBus mem, IIOBus io)
        {
            _mem = mem;
            _io = io;
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
        }

        public void Step()
        {
            byte byte1 = _mem.ReadByte(_p);
            _p++;

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
                    switch (byte1)
                    {
                        // ARITHMETIC/LOGICAL:  00000100
                        // REGISTER TRANSFER:   00001000
                        // PULSE/SHIFT:         00001100

                        case 0x04:  // Arithmetic / Logical
                            {
                                byte byte2 = GetImmediateCommandByte();
                                ArithmeticLogical(byte2);
                            }
                            break;

                        case 0x08:  // Register/Transfer
                            {
                                byte byte2 = GetImmediateCommandByte();
                                RegisterTransfer(byte2);
                            }
                            break;

                        case 0x0c:  // Pulse/Shift
                            {
                                byte byte2 = GetImmediateCommandByte();
                                PulseShift(byte2);
                            }
                            break;

                        default:    // Must be augment mem.
                            {
                                byte byte2 = GetImmediateCommandByte();
                                AugmentMemoryAddressing(byte1, byte2);
                            }
                            break;
                    }

                    // consume the command bytes
                    _p++;
                    break;

                // SHARED ARITHMETIC:   0001xxxx
                case 0x1:
                    {
                        _p--;
                        byte byte2 = GetSharedCommandByte(byte1, ArithmeticLogicalSharedOffset);
                        ArithmeticLogical(byte2);
                        _p++;
                    }
                    break;

                // SHARED REGISTER TRAN:0010xxxx
                case 0x2:
                    {
                        _p--;
                        byte byte2 = GetSharedCommandByte(byte1, RegisterTransferSharedOffset);
                        RegisterTransfer(byte2);
                        _p++;
                    }
                    break;

                // SHARED PULSE:        0011xxxx
                case 0x3:
                    {
                        byte byte2 = GetSharedCommandByte(byte1, PulseShiftSharedOffset);
                        PulseShift(byte2);
                    }
                    break;

                // SKIP CONDITIONALLY:  010xxxxx
                case 0x4:
                case 0x5:
                    ConditionalSkip(byte1);
                    break;

                // JUMP UNCONDITIONALLY:0110xxxx
                case 0x6:
                    {
                        byte byte2 = _mem.ReadByte(_p);
                        _p = (ushort)(((byte1 & 0xf) << 8) | byte2);

                        // Why?  Because this system is weiiiird.
                        _zero = true;
                        _plus = true;
                    }
                    break;

                // LOAD B EXTENDED:     0111xxxx
                case 0x7:
                    {
                        byte byte2 = _mem.ReadByte(_p);
                        int eoa = ((byte1 & 0xf) << 8) | byte2;
                        B = (ushort)(_mem.ReadByte((ushort)(eoa)) | (_mem.ReadByte((ushort)(eoa+1)) << 8));
                        _zero = true;
                        _plus = true;
                        _p++;
                    }
                    break;

                // SHARED AUGMENT:      10xxxxxx
                case 0x8:
                case 0x9:
                case 0xa:
                case 0xb:
                    {
                        byte byte2 = GetSharedCommandByte(byte1, AugmentedMemorySharedOffset);
                        AugmentMemoryAddressing(byte1, byte2);
                    }
                    break;

                // LOAD/STORE B:        110xxxxx or 111xxxxx
                case 0xc:
                    {
                        // TODO: factor this out (see JUMP UNCONDITIONALLY)
                        byte byte2 = _mem.ReadByte(_p);
                        _mem.WriteByte((ushort)(((byte1 & 0xf) << 8) | byte2), (byte)B);
                        _zero = (B & 0xff) == 0;
                        _plus = (B & 0x80) == 0;
                        _p++;
                    }
                    break;

                case 0xd:
                    {
                        // TODO: factor this out (see JUMP UNCONDITIONALLY)
                        byte byte2 = _mem.ReadByte(_p);
                        _mem.WriteByte((ushort)((((byte1 & 0xf) << 8) | byte2) + X), (byte)B);
                        _zero = (B & 0xff) == 0;
                        _plus = (B & 0x80) == 0;
                        _p++;
                    }
                    break;

                case 0xe:
                    {
                        // TODO: factor this out (see JUMP UNCONDITIONALLY)
                        byte byte2 = _mem.ReadByte(_p);
                        B = _mem.ReadByte((ushort)(((byte1 & 0xf) << 8) | byte2));
                        _zero = (B & 0xff) == 0;
                        _plus = (B & 0x80) == 0;
                        _p++;
                    }
                    break;

                case 0xf:
                    {
                        // TODO: factor this out (see JUMP UNCONDITIONALLY)
                        byte byte2 = _mem.ReadByte(_p);
                        B = _mem.ReadByte((ushort)((((byte1 & 0xf) << 8) | byte2) + X));
                        _zero = (B & 0xff) == 0;
                        _plus = (B & 0x80) == 0;
                        _p++;
                    }
                    break;
            }
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

        private byte GetImmediateCommandByte()
        {
            return _mem.ReadByte(_p);

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

        private void ArithmeticLogical(byte byte2)
        {
            int opcode = (byte2 >> 4);

            // Operand: 0 - B register, 1 - immediate value
            ushort operand = (byte2 & 0x08) == 0 ? B : _mem.ReadByte(++_p);
            int reg = byte2 & 0x7;
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
                    }
                    break;

                case 8:   // subtract
                    {
                        int r = GetRegister(reg);
                        _link = (r & 0xff) - (operand & 0xff) < 0 ? true : false;
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

            _zero = (GetRegister(reg) & 0xff) == 0;
            _plus = (GetRegister(reg) & 0x80) == 0;
        }

        private void RegisterTransfer(byte byte2)
        {
            int command = (byte2 >> 6);
            int dest = ((byte2 & 0x38) >> 3);
            int src = (byte2 & 0x7);

            // TODO: LINK logic sucks assssssss
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
                    if (src == 5)
                    {
                        SetRegister(dest, 0xfff);
                    }
                    else
                    {
                        _link = (GetRegister(src) & 0xff) == 0;
                        SetRegister(dest, GetRegister(src) - 1);
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
            int reg = byte2 & 0x7;

            switch (command) 
            {
                case 0: // Shift register right
                    {
                        int r = GetRegister(reg) & 0xff;
                        SetRegister(reg, r >> 1);
                        _link = (r & 0x1) != 0;
                    }
                    break;

                case 1: // Circulate register with link bit
                    {
                        int r = GetRegister(reg) & 0xff;
                        SetRegister(reg, (r >> 1) | (_link ? 0x80 : 0x0));
                        _link = (r & 0x1) != 0;
                    }
                    break;

                case 2: // Circulate register
                    {
                        int r = GetRegister(reg) & 0xff;
                        SetRegister(reg, (r >> 1) | (_link ? 0x80 : 0x0));
                        _link = (r & 0x1) != 0;
                    }
                    break;

                case 4: // Shift serial data in
                case 9: // Circulate register with link bit and data out
                case 0xa: // Circulate serial data out
                case 0xc: // Shift Serial Data In and Echo Out
                    throw new NotImplementedException("Shift with data stuff not implemented.");

                default:
                    throw new InvalidOperationException("Invalid shift command.");
            }

            _zero = (GetRegister(reg) & 0xff) == 0;
            _plus = (GetRegister(reg) & 0x80) == 0;
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
                    _b = (ushort)(_b >> 8);
                    break;

                case 6: // Load E register
                    _e = _b & 0x3;
                    break;

                case 7: // Interrogate interrupt
                    // WHERE WERE YOU ON OCTOBER THIRD NINETEEN SIXTY-EIGHT?
                    throw new NotImplementedException("Interrogate Interrupt command not implemented.");

            }
        }

        private void AugmentMemoryAddressing(byte byte1, byte byte2)
        {
            int field = byte1 & 0x3;
            int preIndexReg = byte2 >> 6;
            bool indirectAddressing = (byte2 & 0x20) != 0;
            int command = (byte2 & 0x18) >> 3;
            int reg = byte2 & 0x7;

            B = (ushort)(field * 2);
            if (preIndexReg != 0)
            {
                // Preindexing
                B = (ushort)(B + GetRegister(preIndexReg));
            }

            if (indirectAddressing)
            {
                int indirectAddress = _mem.ReadByte(B);
                indirectAddress |= (_mem.ReadByte((ushort)(B + 1)) << 8);

                // This "address" is actually 12 bits of address and 4 bits specifying indexing options
                // to be used by the augmented indirect instruction that reads it.
                // This is a really, really weird architecture.
                int postIndexReg = indirectAddress >> 14;
                bool autoIncrement = (indirectAddress & 0x2000) != 0;
                B = (ushort)(indirectAddress & 0xfff);

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
                        ushort operand = _mem.ReadByte(B);
                        SetRegister(reg, operand);
                    }
                    break;
                case 1: // Store and zero:
                    {
                        _mem.WriteByte(B, (byte)GetRegister(reg));
                        SetRegister(reg, 0);
                    }
                    break;
                case 2: // Add:
                    {
                        byte operand = _mem.ReadByte(B);
                        int result = operand + GetRegister(reg);
                        _link = (operand + (GetRegister(reg) & 0xff)) > 0xff;
                        SetRegister(reg, result);
                    }
                    break;
                case 3: // Store:
                    {
                        _mem.WriteByte(B, (byte)GetRegister(reg));

                        // See p. 38 of the PDF
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
                _p = (ushort)(_p + offset);
            }
        }


        private ushort GetRegister(int reg)
        {
            switch (reg)
            {
                case 0:
                    return A;

                case 1:
                    return X;

                case 2:
                    return Y;

                case 3:
                    return Z;

                case 4:
                    return P;

                case 5:
                    return B;

                case 6:
                    return YY;

                case 7:
                    return ZZ;

                default:
                    throw new ArgumentOutOfRangeException(nameof(reg));
            }
        }

        private void SetRegister(int reg, int val)
        {
            switch (reg)
            {
                case 0:
                    A = (ushort)val;
                    break;

                case 1:
                    X = (ushort)val;
                    break;

                case 2:
                    Y = (ushort)val;
                    break;

                case 3:
                    Z = (ushort)val;
                    break;

                case 4:
                    P = (ushort)val;
                    break;

                case 5:
                    B = (ushort)val;
                    break;

                case 6:
                    Y = (ushort)((Y & 0xff) | ((val & 0xf) << 8));
                    break;

                case 7:
                    Z = (ushort)((Z & 0xff) | ((val & 0xf) << 8));
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(reg));
            }
        }

        // Registers (12 bit)
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

        IMemoryBus _mem;
        IIOBus _io;
    }
}
