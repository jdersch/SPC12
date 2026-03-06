using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace SPC12
{
    /// <summary>
    /// Implements the SPC-12's built in, bit-banged TTY.  Sort of.
    /// </summary>
    public class TTY
    {
        public TTY()
        {
            _bitLocker = new ReaderWriterLockSlim();
            _keyboard = new SerializationState(false);
            _printer = new SerializationState(true);
            Reset();
        }

        public void Clock(int cycles)
        {
            
            float timeuS = Processor.CycleTimeMicroseconds * cycles;

            _bitLocker.EnterWriteLock();
            _keyboard.Step(timeuS);
            _printer.Step(timeuS);
            _bitLocker.ExitWriteLock();
        }

        public void LatchKey(byte b)
        {
            _bitLocker.EnterUpgradeableReadLock();
            // we can only accept a key if we're not already
            // inputting or outputting something else
            if (!_keyboard.Idle)
            {
                _bitLocker.ExitUpgradeableReadLock();
                return;
            }

            _bitLocker.EnterWriteLock();
            _keyboard.SetByte(b);
            _bitLocker.ExitWriteLock();
            _bitLocker.ExitUpgradeableReadLock();
        }


        public void Reset()
        {
            _bitLocker.EnterWriteLock();
            _keyboard.Reset();
            _printer.Reset();
            _bitLocker.ExitWriteLock();
        }

        public void StrobePrinter(bool bit)
        {
            _bitLocker.EnterWriteLock();
            _printer.WriteBit(bit);
            _bitLocker.ExitWriteLock();
        }

        public bool StrobeKeyboard()
        {
            _bitLocker.EnterWriteLock();
            bool ret = _keyboard.ReadBit();
            _bitLocker.ExitWriteLock();
            return ret;
        }

        private class SerializationState
        {
            public SerializationState(bool printer)
            {
                // TODO: we should just refactor this into subclasses probably
                _isPrinter = printer;
            }

            private enum State
            {
                Idle,
                Start,
                Data,
                Stop,
            }

            public bool Idle => _state == State.Idle;

            public byte Byte => _byte;

            public void Reset()
            {
                _bit = true;
                _byte = 0;
                _state = State.Idle;
                _bitCount = 0;
                _bitTime = 0;
            }

            public void SetByte(byte b)
            {
                if (_state != State.Idle)
                {
                    // enforce some kind of sanity
                    throw new InvalidOperationException();
                }

                _byte = b;

                // Loading a byte moves us out of Idle and into Start.
                _bitCount = 0;
                _bitTime = 0;
                _state = State.Start;

                
            }

            public bool ReadBit()
            {
                switch (_state)
                {
                    case State.Start:
                        return false;       // space

                    case State.Data:
                        return (_byte & (1 << _bitCount)) != 0;

                    case State.Stop:
                        return true;        // mark

                    default:
                        return true;        // assume mark while idle
                }
            }

            public void WriteBit(bool bit)
            {
                _bit = bit;
                if (_state == State.Idle && _bit == false)
                {
                    // Writing a space bit while in idle moves us out of Idle and into Start.
                    _bitCount = 0;
                    _bitTime = 0;
                    _byte = 0;
                    _state = State.Start;
                }
                else if (_state == State.Data)
                {
                    if (_bit)
                    {
                        _byte |= (byte)(1 << _bitCount);
                    }
                    else
                    {
                        _byte &= (byte)~(1 << _bitCount);
                    }
                }
                else
                {
                    // we can just ignore it, a real teletype would send this somewhere but we don't
                    // need this data.
                }
            }

            public void Step(float elapsedTimeMicroseconds)
            {
                _bitTime += elapsedTimeMicroseconds;

                if (_bitTime < BitTimeMicroseconds)
                {
                    return;
                }

                _bitTime = _bitTime - BitTimeMicroseconds;
                switch (_state)
                {
                    case State.Idle:
                        if (_isPrinter && !_bit)
                        {
                            _state = State.Data;
                        }
                        else  
                        {
                            _state = State.Idle;
                        }
                        break;

                    case State.Start:
                        _state = State.Data;
                        break;

                    case State.Data:
                        _bitCount++;

                        if (_bitCount >= 8)
                        {
                            _state = State.Stop;
                        }
                        break;

                    case State.Stop:
                        _bitCount++;

                        if (_bitCount >= 9)    // two stop bits
                        {
                            if (_isPrinter)
                            {
                                // TODO: move the actual console output somewhere else more appropriate
                                Console.Write((char)(_byte & 0x7f));
                            }

                            _state = State.Idle;
                            _bitCount = 0;
                        }
                        break;

                }
            }

            private bool _isPrinter;
            private byte _byte;
            private int _bitCount;
            private bool _bit;
            private float _bitTime;
            private State _state;

            private static readonly float BitTimeMicroseconds = 9.1f * 1000;        // 9.1ms per bit

        }

        ReaderWriterLockSlim _bitLocker;
        SerializationState _keyboard;
        SerializationState _printer;
    }
}
