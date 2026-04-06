using SPC12;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SPC12
{
    public enum ExecutionState
    {
        Debug,
        Step,
        Go,
        Breakpoint,
    }

    public class DebugPrompt
    {
        public DebugPrompt(System system)
        {
            _system = system;
        }

        public ExecutionState Prompt()
        {
            ExecutionState next = ExecutionState.Step;
            PrintStatus();

            bool done = false;
            while (!done)
            {
                Console.Write(":]> ");
                string input = Console.ReadLine();

                if (string.IsNullOrWhiteSpace(input))
                {
                    // Default is to step the processor.
                    next = ExecutionState.Step;
                    break;
                }

                string[] tokens = input.Split(new char[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);

                switch (tokens[0].ToLowerInvariant())
                {
                    case "s":
                        next = ExecutionState.Step;
                        done = true;
                        break;

                    case "g":
                        if (tokens.Length > 1)
                        {
                            _system.Processor.P = Helpers.GetUshortForOctalString(tokens[1]);
                        }
                        next = ExecutionState.Go;
                        done = true;
                        break;

                    case "d":
                        if (tokens.Length < 2)
                        {
                            Console.WriteLine("d <addr> [length]");
                        }
                        else
                        {
                            try
                            {
                                ushort start = Helpers.GetUshortForOctalString(tokens[1]);
                                int mode = (start & 0x7fff) >> 12;
                                ushort length = tokens.Length > 2 ? Helpers.GetUshortForOctalString(tokens[2]) : (ushort)1;
                                Disassembler dis = new Disassembler(_system.Memory);
                                for (ushort i = start; i < start + length;)
                                {
                                    string disassembly;
                                    i = (ushort)dis.Disassemble(i, mode, out disassembly);
                                    Console.WriteLine("{0}  {1}", Helpers.ToOctal(i), disassembly);
                                }
                            }
                            catch (Exception e)
                            {
                                Console.WriteLine("invalid args.");
                            }
                        }

                        next = ExecutionState.Debug;
                        break;

                    case "m":
                        if (tokens.Length < 2)
                        {
                            Console.WriteLine("m <addr> [length]");
                        }
                        else
                        {
                            try
                            {
                                ushort start = Helpers.GetUshortForOctalString(tokens[1]);
                                int mode = (start & 0x7fff) >> 12;
                                ushort length = tokens.Length > 2 ? Helpers.GetUshortForOctalString(tokens[2]) : (ushort)1;

                                for (ushort i = start; i < start + length; i++)
                                {
                                    Console.WriteLine("{0}: {1}", Helpers.ToOctal(i), Helpers.ToOctal(_system.Memory.ReadByte(i, mode)));
                                }
                            }
                            catch (Exception e)
                            {
                                Console.WriteLine("invalid args.");
                            }
                        }

                        next = ExecutionState.Debug;
                        break;
                }


            }

            return next;
        }

        private void PrintStatus()
        {
            Processor p = _system.Processor;
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

            Disassembler dis = new Disassembler(_system.Memory);
            
            string disassembly;
            dis.Disassemble(p.P, p.E, out disassembly);
            Console.WriteLine("{0}{1}  {2}", Helpers.ToOctal((ushort)p.E, 1), Helpers.ToOctal(p.P, 4), disassembly);
        }

        private System _system;
    }
}