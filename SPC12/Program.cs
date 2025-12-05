namespace SPC12
{
    internal class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("SPC-12 Thingus");

            Memory m = new Memory(1);
            Processor p = new Processor(m, new IO());

            p.P = 0xa00; // 5000 octal

            while (true)
            {
                Console.WriteLine("P={0} A={1} X={2} Y={3} Z={4} B={5} Link={6} Zero={7} Plus={8} IO={9}", 
                    Helpers.ToOctal(p.P),
                    Helpers.ToOctal(p.A),
                    Helpers.ToOctal(p.X),
                    Helpers.ToOctal(p.Y),
                    Helpers.ToOctal(p.Z),
                    Helpers.ToOctal(p.B),
                    p.Link,
                    p.Zero, 
                    p.Plus, 
                    p.IOStatus);

                Console.WriteLine("{0} - {1}", Helpers.ToOctal(p.P), Helpers.ToOctal(m.ReadByte(p.P)));

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
