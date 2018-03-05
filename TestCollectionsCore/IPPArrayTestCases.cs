using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using InContex.Collections.Persisted.Core;
using System.Diagnostics;

namespace IPPArrayTest
{
    class IPPArrayTestCases
    {
        public static void OpenCoseTest()
        {
            string path = @"C:\temp\IPPArray";
            string name = "MyArray";

            Random random = new Random(DateTime.Now.Millisecond);

            Console.WriteLine();
            Console.WriteLine("Press any key to start the Array Open / Close synchronization testing.");
            Console.WriteLine("Press ESC to stop test anytime during the execution of the test.");
            Console.ReadKey();

            do
            {
                while (!Console.KeyAvailable)
                {

                    using (IPPArray<int> array = IPPArray<int>.Open(path, name, 1000))
                    {
                        string mode = array.ServerInstance ? "Server Mode" : "Client Mode";
                        int initializationCount = (int)array.InitializationCount;
                        Console.WriteLine("Opened Array {0} in {1}. Initialization Count {2}.", name, mode, initializationCount);

                        for (int count = 0; count < 100; count++)
                        {
                            int value = random.Next(1, 10000);
                            int index = random.Next(0, 999);

                            array.AcquireSpinLock();
                            array[index] = value;
                            array.ReleaseSpinLock();
                            Console.WriteLine("Writing value: {0} to index: {1}", value, index);
                        }

                        for (int count = 0; count < 100; count++)
                        {
                            int index = random.Next(0, 999);

                            array.AcquireSpinLock();
                            int value = array[index];
                            array.ReleaseSpinLock();
                            Console.WriteLine("Read value: {0} from index: {1}", value, index);
                        }
                    }

                    //int waitTimeMS = random.Next(10, 1000);
                    //System.Threading.Thread.Sleep(waitTimeMS);
                }
            } while (Console.ReadKey(true).Key != ConsoleKey.Escape);
        }

        public static void DisposeTest()
        {
            string path = @"C:\temp\IPPArray";
            string name = "MyArray";

            Console.WriteLine();
            Console.WriteLine("Press any key to start the Array Dispose test.");
            Console.ReadKey();

            Console.WriteLine();
            Console.WriteLine("Create array A.");
            IPPArray<int> array = IPPArray<int>.Open(path, name, 1000);

            Console.WriteLine("Setting array to null without disposing");
            array = null;
            System.Threading.Thread.Sleep(10000);

            Console.WriteLine("Creating new array.");
            array = IPPArray<int>.Open(path, name, 1000);

            Console.WriteLine("Dispose array.");
            array.Dispose();

            Console.WriteLine("Creating new array.");
            array = IPPArray<int>.Open(path, name, 1000);
        }

        public static void ReadWriteTest()
        {
            string path = @"C:\temp\IPPArray";
            string name = "MyArray";

            Random random = new Random(DateTime.Now.Millisecond);

            Console.WriteLine();
            Console.WriteLine("Press any key to start the Array read test.");
            Console.WriteLine("Press ESC to stop test anytime during the execution of the test.");
            Console.ReadKey();



            using (IPPArray<int> array = IPPArray<int>.Open(path, name, 1000))
            {
                Console.WriteLine();
                Console.WriteLine("Initial array state.");
                Console.WriteLine("----------------------------------------------------");

                array.AcquireSpinLock(); // Prevent another process from modifying array.
                try
                {
                    int i = 0;
                    foreach (int value in array)
                    {
                        Console.WriteLine("{0}\t{1}", i, value);
                        i++;
                    }
                }
                finally
                {
                    array.ReleaseSpinLock();
                }

                Console.ReadKey();

                do
                {
                    while (!Console.KeyAvailable)
                    {
                        for(int index = 0; index < 1000; index++)
                        {
                            array.AcquireSpinLock();
                            try
                            {
                                array[index] += random.Next(-10, 10);

                                if (array[index] > 1000000) array[index] = 1000000;
                                if(array[index] < -1000000) array[index] = -1000000;
                            }
                            finally
                            {
                                array.ReleaseSpinLock();
                            }
                        }
                        int sleepMS = random.Next(0, 100);
                        System.Threading.Thread.Sleep(sleepMS);
                        
                    }

                } while (Console.ReadKey(true).Key != ConsoleKey.Escape);

                Console.WriteLine();
                Console.WriteLine("Array last written state:");
                Console.WriteLine("----------------------------------------------------");
                int indexPosition = 0;

                array.AcquireSpinLock(); // Prevent another process from modifying array.
                try
                {
                    foreach (int value in array)
                    {
                        Console.WriteLine("{0}\t{1}", indexPosition, value);
                        indexPosition++;
                    }
                }
                finally
                {
                    array.ReleaseSpinLock();
                }
            }
        }

        public static void ThreadTest()
        {
            int threadID = System.Threading.Thread.CurrentThread.ManagedThreadId;
            Process currentProcess = Process.GetCurrentProcess();
            int processID = currentProcess.Id;

            long high = processID;
            high = high << 32;
            long low = (long)(uint)threadID;

            long uniqueID = high | low;

            Console.WriteLine("Current Process: {0}, Current Thread ID {1}.", processID, threadID);
            Console.WriteLine("High word: {0}, Low word {1}, UniqueID: {2}.", high, low, uniqueID);
            Console.WriteLine();
            Console.WriteLine("Press any key to continue");
            Console.ReadKey();
        }
    }
}
