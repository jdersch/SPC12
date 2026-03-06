using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SPC12
{
    public class System
    {
        public System()
        {
            _io = null;
            _memory = new Memory(8);
            _processor = new Processor(_memory, _io);

            _processor.P = 0xd40; // 16500 octal
            _processor.E = 3;
            //_processor.P = 0xa00;
            //_processor.E = 1;
        }

        public Processor Processor => _processor;
        public Memory Memory => _memory;

        public int Step()
        {
            return _processor.Step();
        }


        private Processor _processor;
        private Memory _memory;
        private IIOBus _io;
    }
}
