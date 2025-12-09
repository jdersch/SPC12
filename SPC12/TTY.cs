using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SPC12
{
    /// <summary>
    /// Implements the SPC-12's built in, bit-banged TTY.  Sort of.
    /// </summary>
    internal class TTY
    {
        public TTY()
        {
            Reset();
        }

        public void Reset()
        {
            _bitCount = 0;
            _byte = 0;
        }

        public void StrobeOut(bool bit)
        {
            _byte |= ((bit ? 1 : 0) << _bitCount);

            _bitCount++;

            if (_bitCount > 11)
            {
                // TODO: don't do this, do something else
                Console.Write((char)(_byte & 0xff));
                _bitCount = 0;
                _byte = 0;
            }
        }


        int _bitCount;
        int _byte;
    }
}
