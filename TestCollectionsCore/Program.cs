using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using InContex.Collections.Persisted.Core;
using CommandLine;
using System.IO;
using System.IO.MemoryMappedFiles;
using Microsoft.Win32.SafeHandles;

namespace IPPArrayTest
{
    class Options
    {
        [Option('o', "Open", Default=false,
          HelpText = "Test open and close action across multiple processes to ensure proper synchronization.")]
        public bool ExecuteOpenTest { get; set; }

        [Option('d', "Dispose", Default = false,
          HelpText = "Test the correct disposal of IPPArray objects.")]
        public bool ExecuteDisploseTest { get; set; }

        [Option('r', "ReadWrite", Default = false,
            HelpText = "Test the read / write actions to array.")]
        public bool ExecuteReadWriteTest { get; set; }

        [Option('t', "Thread", Default = false,
            HelpText = "Test thread id.")]
        public bool ExecuteThreadTest { get; set; }

        [Option('l', "LargeInstance", Default = false,
            HelpText = "Large instance count test.")]
        public bool LargeInstanceCountTest { get; set; }

        [Option('g', "GrowMemoryMap", Default = false,
            HelpText = "Grow Memory Map Test.")]
        public bool GrowMemmoryMap { get; set; }

    }

    class Program
    {
        static void Main(string[] args)
        {
            var results = CommandLine.Parser.Default.ParseArguments<Options>(args);

            results.WithParsed<Options>(
                (Options options) =>
                {
                    if (options.ExecuteDisploseTest) IPPArrayTestCases.DisposeTest();
                    if (options.ExecuteOpenTest) IPPArrayTestCases.OpenCoseTest();
                    if (options.ExecuteReadWriteTest) IPPArrayTestCases.ReadWriteTest();
                    if (options.ExecuteThreadTest) IPPArrayTestCases.ThreadTest();
                    if (options.LargeInstanceCountTest) IPPArrayTestCases.LargeInstanceCountTest();
                    if (options.GrowMemmoryMap) GrowMemmoryMapTest();
                }
            );
        }

        private static void GrowMemmoryMapTest()
        {
            string name = Guid.NewGuid().ToString();
            string path = @"C:\ProgramData\InContex\Data\mm_test.mmf";
            int pageSize = Environment.SystemPageSize;
            long mapSize = 0;
            bool server = true;

            using (FileStream stream = new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite))
            {
                if(stream.Length == 0)
                {
                    mapSize = pageSize;
                }
                else
                {
                    mapSize = stream.Length * 2;
                }

                try
                {
                    using (MemoryMappedFile mmf = MemoryMappedFile.CreateFromFile(stream, name, mapSize, MemoryMappedFileAccess.ReadWrite, HandleInheritability.None, true))
                    {
                        using (MemoryMappedViewAccessor view = mmf.CreateViewAccessor())
                        {
                            unsafe
                            {
                                byte* viewPtr = null;

                                view.SafeMemoryMappedViewHandle.AcquirePointer(ref viewPtr);

                                for(int i =0; i < mapSize; i++)
                                {
                                    if(viewPtr[i] > 0)
                                    {
                                        Console.WriteLine("Index: {0} Value: {1}", i.ToString(), viewPtr[i].ToString());
                                    }
                                }

                                viewPtr[0] = 1;
                                viewPtr[mapSize - 1] = 200;

                                
                                Console.WriteLine("Press any key to close map and exist.");
                                Console.ReadKey();
                            }
                        }
                    }
                }
                catch(Exception exp)
                {
                    server = false;
                }

                if (!server)
                {
                    try
                    {
                        using (MemoryMappedFile mmf = MemoryMappedFile.OpenExisting(name))
                        {
                            using (MemoryMappedViewAccessor view = mmf.CreateViewAccessor(0, mapSize))
                            {
                                unsafe
                                {
                                    byte* viewPtr = null;

                                    Console.ReadKey();

                                    view.SafeMemoryMappedViewHandle.AcquirePointer(ref viewPtr);

                                    for (int i = 0; i < mapSize; i++)
                                    {
                                        if (viewPtr[i] > 0)
                                        {
                                            Console.WriteLine("Index: {0} Value: {1}", i.ToString(), viewPtr[i].ToString());
                                        }
                                    }

                                    viewPtr[0] = 1;
                                    viewPtr[mapSize - 1] = 200;


                                    Console.WriteLine("Press any key to close map and exist.");
                                    Console.ReadKey();
                                }
                            }
                        }
                    }
                    catch (Exception exp)
                    {
                        Console.WriteLine("Failed to open map.");
                    }
                }
            }
        }

    }
}
