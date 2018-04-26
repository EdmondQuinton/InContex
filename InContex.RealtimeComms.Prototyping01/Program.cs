using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using OfficeOpenXml;
using OfficeOpenXml.Table;

namespace InContex.RealtimeComms.Prototyping01
{
    class Program
    {
        private static SessionManager _opcSessionManager;
        private static string _opcServerUriString = @"opc.tcp://desktop-4d9i923:62541/InContex/OpcSimulationServer";

        static void Main(string[] args)
        {
            List<string> nodeIds = LoadNodeIds();
            Uri uri = new Uri(_opcServerUriString);
            _opcSessionManager = new SessionManager(uri, null, null);
            
            foreach(string nodeID in nodeIds)
            {
                _opcSessionManager.CreateMonitoredItem(nodeID, 1000);
            }

            Console.WriteLine("OPC client is running. Press enter to quit.");
            Console.ReadLine();
        }

        private static List<string> LoadNodeIds()
        {
            string path = AppDomain.CurrentDomain.BaseDirectory;
            string file = "AddressSpace.xlsx";
            string fileFullName = Path.Combine(path, file);
            List<string> nodeIdList;

            var fileInfo = new FileInfo(fileFullName);

            using (var package = new ExcelPackage(fileInfo))
            {

                var workbook = package.Workbook;
                var worksheet = workbook.Worksheets.First();
                var excelTable = worksheet.Tables["SimVariables"];
                nodeIdList = LoadNodeIdsFromExcelTable(excelTable);
                package.Save();

            }

            return nodeIdList;
        }

        private static List<string> LoadNodeIdsFromExcelTable(ExcelTable table)
        {
            List<string> nodeIdList = new List<string>();

            //Get the cells based on the table address
            var groups = table.WorkSheet.Cells[table.Address.Start.Row, table.Address.Start.Column, table.Address.End.Row, table.Address.End.Column]
                .GroupBy(cell => cell.Start.Row)
                .ToList();

            //Assume the second row represents column data types
            var types = groups
                .Skip(1)
                .First()
                .Select(rcell => rcell.Value.GetType())
                .ToList();

            //Assume first row has the column names
            var colnames = groups
                .First()
                .Select((hcell, idx) => new { Name = hcell.Value.ToString(), index = idx })
                .ToList();

            //Everything after the header is data
            var rowvalues = groups
                .Skip(1) //Exclude header
                .Select(cg => cg.Select(c => c.Value).ToList());

            ushort variableNamespaceIndex = 2;
            string identifierType = "s";
            string identifier = "";
            string nodeID = "";
            //ns=2;s=MyTemperature

            foreach (var row in rowvalues)
            {
                foreach (var column in colnames)
                {
                    switch (column.Name.ToLower())
                    {
                        case "namespaceindex":
                            variableNamespaceIndex = Convert.ToUInt16(row[column.index]);
                            break;
                        case "identifiertype":
                            identifierType = Convert.ToString(row[column.index]);
                            break;
                        case "identifier":
                            identifier = Convert.ToString(row[column.index]);
                            break;
                   }
                }

                nodeID = string.Format("ns={0};{1}={2}", variableNamespaceIndex, identifierType, identifier);
                nodeIdList.Add(nodeID);
            }

            return nodeIdList;
        }
    }
}
