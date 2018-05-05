using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.IO;

using System.Data;
using System.Data.SqlClient;

using Microsoft.SqlServer.Server;
using NLog;


using InContex.Collections.Persisted;


namespace InContex.DataLoading.Prototyping01
{
    public enum DataUploadMethod
    {
        ProcedureNonClusterIndex_SampleTimeVariable = 1,
        ProcedureNonClusterIndex_VariableSampleTime = 2,
        ProcedureHashIndex_SampleTimeVariable = 3,
        ProcedureHashIndex_VariableSampleTime = 4,
        BulkCopy_SampleTimeVariable = 5,
        BulkCopy_VariableSampleTime = 6,
        ProcedureIdentity = 7,
    }


    public class AnalogueDataStream : List<AnalogueSignal>, IEnumerable<SqlDataRecord>
    {
        public AnalogueDataStream(int capacity)
            : base(capacity) { }

        public AnalogueDataStream(IEnumerable<AnalogueSignal> collection)
            :base(collection)
        { }

        IEnumerator<SqlDataRecord> IEnumerable<SqlDataRecord>.GetEnumerator()
        {
            SqlDataRecord record = new SqlDataRecord(
                new SqlMetaData("VariableID", SqlDbType.Int),
                new SqlMetaData("SampleDateTimeUTC", SqlDbType.DateTime2),
                new SqlMetaData("PreviousSampleDateTimeUTC", SqlDbType.DateTime2),
                new SqlMetaData("Value", SqlDbType.Float),
                new SqlMetaData("DeltaValue", SqlDbType.Float),
                new SqlMetaData("StatusGood", SqlDbType.Bit),
                new SqlMetaData("StatusCode", SqlDbType.Int)
                );

            foreach (AnalogueSignal variable in this)
            {
                record.SetInt32(0, variable.SignalID);
                record.SetDateTime(1, new DateTime(variable.SampleDateTimeUTC));
                record.SetDateTime(2, new DateTime(variable.PreviousSampleDateTimeUTC));
                record.SetDouble(3, variable.Value);
                record.SetDouble(4, variable.DeltaValue);
                record.SetBoolean(5, variable.StatusGood > 0 ? true : false);
                record.SetInt32(6, (int)variable.StatusCode);
                yield return record;
            }
        }

    }
    // Some sort of repository manager in the end?
    public class DataLoader
    {
        private IPPQueue<AnalogueSignal> _analogueSignalStore;
        private static Logger __logger = LogManager.GetCurrentClassLogger();
        private int _numberOfExecution = 0;
        private InsertCache _timeCache1;
        private InsertCache _timeCache2;
        private InsertCache _timeCache3;


        public DataLoader()
            :this("SignalStore")
        { }

        public DataLoader(string storeName)
            :this(storeName, Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), @"InContex\Data")) 
        { }

        public DataLoader(string storeName, string storeLocation)
        {
            string analogueSignalStoreName = storeName + "_Analogue";

            _timeCache1 = new InsertCache();
            _timeCache2 = new InsertCache();
            _timeCache3 = new InsertCache();
            _analogueSignalStore = new IPPQueue<AnalogueSignal>(storeName, storeLocation, new SignalSerializer<AnalogueSignal>());
        }

        private async Task ProcessStreamsBulkCopy()
        {
        }

        private async Task ProcessStreamsCommand(DataUploadMethod uploadMethod, InsertCache timeCache, CancellationToken ct)
        {
            string procedure = "";
            bool validUploadMethod = true;
            
           
            switch (uploadMethod)
            {
                case DataUploadMethod.ProcedureHashIndex_SampleTimeVariable:
                    procedure = "UploadSampleStream_SVHS";
                    break;
                case DataUploadMethod.ProcedureHashIndex_VariableSampleTime:
                    procedure = "UploadSampleStream_VSHS";
                    break;
                case DataUploadMethod.ProcedureNonClusterIndex_SampleTimeVariable:
                    procedure = "UploadSampleStream_SVPK";
                    break;
                case DataUploadMethod.ProcedureNonClusterIndex_VariableSampleTime:
                    procedure = "UploadSampleStream_VSPK";
                    break;
                case DataUploadMethod.ProcedureIdentity:
                    procedure = "UploadSampleStream";
                    break;
                default:
                    validUploadMethod = false;
                    break;
            }

            if (validUploadMethod)
            {
                Stopwatch stopwatch = new Stopwatch();
                stopwatch.Start();

                //AnalogueDataStream sqlStream = new AnalogueDataStream(stream);
                InsertCacheBucket sqlStream = timeCache.GetNextCacheBucket();

                if (sqlStream != null)
                {

                    try
                    {
                        using (SqlConnection connection = new SqlConnection("Server=(local);Database=InContex.DataStore.Prototying01;Trusted_Connection=True;"))
                        using (SqlCommand command = new SqlCommand(procedure, connection))
                        {
                            command.CommandType = CommandType.StoredProcedure;
                            SqlParameter paramTagList = command.Parameters.AddWithValue("@stream", sqlStream);
                            paramTagList.SqlDbType = SqlDbType.Structured;

                            await connection.OpenAsync(ct);
                            await command.ExecuteNonQueryAsync(ct);
                        }

                        stopwatch.Stop();
                        int sampleCount = sqlStream.Count;
                        long durationMS = stopwatch.ElapsedMilliseconds;

                        __logger.Info("Method:\t{0}\tSample Count:\t{1}\tMillisecond:\t{2}", uploadMethod.ToString(), sampleCount, durationMS);
                    }
                    catch(Exception exp)
                    {
                        __logger.Error(exp, "Failed to load stream.");
                    }
                }
            }

        }

        public async Task ProcessStreams(CancellationToken ct)
        {
            try
            {
                while (true)
                {
                    AnalogueSignal[] stream = null;

                    if (ct.IsCancellationRequested)
                    {
                        __logger.Info("Cancellation requested. Sending remaining messages.");
                        break;
                    }

                    stream = _analogueSignalStore.DequeueSegment();
                    if (stream.Length > 0)
                    {
                        foreach (var item in stream)
                        {
                            _timeCache1.Add(item);
                            _timeCache2.Add(item);
                            _timeCache3.Add(item);
                        }

                        await ProcessStreamsCommand(DataUploadMethod.ProcedureNonClusterIndex_SampleTimeVariable, _timeCache1, ct);
                        await ProcessStreamsCommand(DataUploadMethod.ProcedureNonClusterIndex_VariableSampleTime, _timeCache2, ct);
                        await ProcessStreamsCommand(DataUploadMethod.ProcedureIdentity, _timeCache3, ct);
                    }
                    else
                    {
                        await Task.Delay(500, ct);
                    }
                }
            }
            catch (Exception exception)
            {
                Console.WriteLine("Error dequeuing message: " + exception.ToString());
            }

        }
    }
}
