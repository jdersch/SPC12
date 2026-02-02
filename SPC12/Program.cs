namespace SPC12
{
    internal class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("SPC-12 Emulator v0.1");

            Memory m = new Memory(8);
            Processor p = new Processor(m, new IO());
            Disassembler d = new Disassembler(m);
            p.P = 0xd40; // 16500 octal
            p.E = 3;
            //p.P = 0xa00;
            //p.E = 1;

            /*
            for (int addr = Helpers.GetUshortForOctalString("120"); addr < Helpers.GetUshortForOctalString("255"); )
            {
                string disassembly;
                int nextAddr = d.Disassemble(addr, out disassembly);
                Console.WriteLine("{0}     {1}", Helpers.ToOctal((ushort)addr), disassembly);
                addr = nextAddr;
            } */

            Console.ReadLine();

            while (true)
            {

                /*
                Console.WriteLine("P={0} A={1} X={2} Y={3} Z={4} B={5} Link={6} Zero={7} Plus={8} IO={9} E={10}", 
                    Helpers.ToOctal(p.P),
                    Helpers.ToOctal(p.A),
                    Helpers.ToOctal(p.X),
                    Helpers.ToOctal(p.Y),
                    Helpers.ToOctal(p.Z),
                    Helpers.ToOctal(p.B),
                    p.Link,
                    p.Zero, 
                    p.Plus, 
                    p.IOStatus,
                    p.E);
                */

              
                //Console.ReadLine();
                

                ushort oldP = p.P;
                int cycles = p.Step();

                if (oldP == p.P)
                {
                    Console.WriteLine("Stopping, infinite loop.");
                    break;
                }

                if (Console.KeyAvailable)
                {
                    // Extremely crude TTY input for now
                    p.TTY.LatchKey((byte)Console.ReadKey(true).KeyChar);
                }
            }
        }
    }
}
