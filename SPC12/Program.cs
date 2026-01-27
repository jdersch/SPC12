namespace SPC12
{
    internal class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("SPC-12 Thingus");

            Memory m = new Memory(1);
            Processor p = new Processor(m, new IO());
            Disassembler d = new Disassembler(m);
            p.P = 0xa00; // 5000 octal

            int i = 0xc77;
            while (i <= 0xcba)
            {
                string disassembly;
                int nextI = d.Disassemble(i, out disassembly);
                Console.WriteLine("{0}  {1}", Helpers.ToOctal((ushort)i), disassembly);

                i = nextI;
            }

            Console.ReadLine();

            while (true)
            {
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

                string disassembly;
                d.Disassemble(p.P, out disassembly);
                Console.WriteLine("{0} - {1}", Helpers.ToOctal(p.P), disassembly);

                //Console.ReadLine();

                ushort oldP = p.P;
                p.Step();

                if (oldP == p.P)
                {
                    Console.WriteLine("Stopping, infinite loop.");
                    break;
                }
            }
        }
    }
}
