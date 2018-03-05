using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using InContex.Collections.Persisted;
using InContex.Data.Streams;
using InContex.Runtime.Serialization;
using InContex.Data.Serialization;

namespace TestQueue
{
    class Program
    {
        private static IPPQueue<AnalogueVariableSample> _queue;
        private static VariableSampleSerializer<AnalogueVariableSample> _serializer = new VariableSampleSerializer<AnalogueVariableSample>();
        private static string _path = @"C:\temp\Queue\";
        private static string _queueName = "MMFQueu" +
            "eData";
        static void Main(string[] args)
        {
            _queue = new IPPQueue<AnalogueVariableSample>(_queueName, _path, QueueFullBehaviorEnum.ThrowException, _serializer);

            var cts = new CancellationTokenSource();
            var e = new ManualResetEventSlim();

            var task = Task.Run(async () => await RunDemoAsync(cts.Token, args[0]));

            task.ContinueWith(t => Console.WriteLine("EXCEPTION: {0}", t.Exception), TaskContinuationOptions.OnlyOnFaulted);
            task.ContinueWith(t => Console.WriteLine("Task has been cancelled"), TaskContinuationOptions.OnlyOnCanceled);
            task.ContinueWith(t => e.Set());

            e.Wait();

            cts.Cancel();

            Console.WriteLine("\nDONE! Press Enter to exit...");
            Console.ReadLine();
            //_queue.Dequeue
        }

        private static async Task RunDemoAsync(CancellationToken ct, string action)
        {
            if(action.ToUpper().Trim() == "E")
            {
                await RunDemoAsync("Enqueue Demo", () => EnqueueDemo(ct));
            }

            if (action.ToUpper().Trim() == "D")
            {
                await RunDemoAsync("Dequeuee Demo", () => DequeueDemo(ct));
            }
        }

        private static async Task RunDemoAsync(string description, Func<Task> demo)
        {
            Console.WriteLine("\nPress Enter to run {0} Demo...", description);
            Console.ReadLine();

            var sw = Stopwatch.StartNew();

            await demo();

            Console.WriteLine("Elapsed: {0}", sw.Elapsed);
        }

        private static async Task EnqueueDemo(CancellationToken ct)
        {
            int count = 0;

            Console.WriteLine("Enqueu items...");

            object firstSampleObject = null;
            double firstValue = 0;

            if (_queue.Count > 0)
                firstValue = _queue.PeekTail().Value + 1;

            while (count < 1000000)
            {
                AnalogueVariableSample sample = new AnalogueVariableSample();
                sample.Value = (firstValue + count);
                sample.Timestamp = DateTime.Now;
                sample.Quality = 192;

                if(count == 4101)
                {
                    string please = "Break Here";
                }

                _queue.Enqueue(sample);

                if (ct.IsCancellationRequested)
                {
                    break;
                }

                count++;
                //Console.WriteLine("Enqueu Add Count: {0}", sample.Value.ToString());
                //await Task.Delay(5);
            }

            string IamIDone = "yes";
        }

        private static async Task DequeueDemo(CancellationToken ct)
        {
            int count = 0;
            double start = 0;
            double end = 0;


            Console.WriteLine("Dequeue items...");

            while (count < 1000000)
            {
                object sampleObject = null;
                bool queueHasEntries = _queue.TryDequeue(ref sampleObject);

                if (queueHasEntries)
                {
                    AnalogueVariableSample sample = (AnalogueVariableSample)sampleObject;

                    if (count == 0)
                    {
                        start = sample.Value;
                    }

                    if (ct.IsCancellationRequested)
                    {
                        break;
                    }

                    if ((count % 10000) == 0)
                    {
                        end = sample.Value;
                        Console.WriteLine("Dequeued samples from: {0} - {1}", start.ToString(), end.ToString());
                        start = end;
                    }

                    count++;
                }
                else
                {
                    Console.WriteLine("Queue Empty");
                    break;
                }
            }

            end = count;
            //Console.WriteLine("Dequeued samples from: {0} - {1}", start.ToString(), end.ToString());

        }
    }
}
