namespace SPC12
{
    internal class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("SPC-12 Emulator v0.1");

            System system = new System();
            int stepCount = 0;

            DebugPrompt prompt = new DebugPrompt(system);

            while (true)
            {

                ExecutionState state = prompt.Prompt();

                switch (state)
                {
                    case ExecutionState.Debug:
                        break;

                    case ExecutionState.Step:
                        system.Step();
                        break;

                    case ExecutionState.Go:
                        Run(system);
                        state = ExecutionState.Debug;
                        break;
                }
            }
        }

        private static void Run(System system) 
        {
            int stepCount = 0;

            while (true)
            { 
                ushort oldP = system.Processor.P;
                int cycles = system.Step();
                stepCount++;

                if (system.Processor.P == oldP) //Helpers.GetUshortForOctalString("7445"))
                {
                    Console.WriteLine("Stopping, infinite loop.");
                    break;
                }

                // only poll so often because KeyAvailable is apparently expensive!
                if ((stepCount % 10000) == 0 && Console.KeyAvailable)
                {
                    // Extremely crude TTY input for now
                    ConsoleKeyInfo info = Console.ReadKey(true);
                    char key = info.KeyChar;
                    system.Processor.TTY.LatchKey((byte)key);

                    if (info.Key == ConsoleKey.F1)
                    {
                        break;
                    }
                }
            }
        }

        
    }
}
