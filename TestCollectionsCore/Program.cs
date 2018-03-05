using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using InContex.Collections.Persisted.Core;
using CommandLine;

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
                }
            );
        }
 
    }
}
