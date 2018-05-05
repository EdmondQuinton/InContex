using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data;
using System.Data.SqlClient;

using Microsoft.SqlServer.Server;

namespace InContex.DataLoading.Prototyping01
{
    public static class ExtensionMethods
    {
        public static IEnumerator<SqlDataRecord> ToSqlRecordStream(this AnalogueSignal[] array)
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

            foreach (AnalogueSignal variable in array)
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
}
