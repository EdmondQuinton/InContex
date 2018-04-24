using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Opc.Ua;
using Opc.Ua.Server;

namespace InContex.OpcSimulationServer
{
    class OpcSimulationSignal
    {
        private static Dictionary<string, FolderState> _folders = new Dictionary<string, FolderState>(16384);

        private SignalGenerator _signal;
        private string _variableName;
        private BuiltInType _dataType;
        private string _valueRanks;
        private string _namespaceIndex;
        private string _identifierType;
        private string _identifier;
        private BaseDataVariableState _variable;
        private float _frequencyMin;
        private float _frequencyMax;
        private Random random;

        private OpcSimulationSignal() { }
        private OpcSimulationSignal(string variableName, BuiltInType datatype, SignalType signalType, float amplitude, float offset, float frequencyMin, float frequencyMax, BaseDataVariableState opcVariable)
        {
            _variableName = variableName;
            _variable = opcVariable;
            _dataType = datatype;
            _frequencyMin = frequencyMin;
            _frequencyMax = frequencyMax;
            random = new Random();

            _signal = new SignalGenerator(signalType)
            {
                Amplitude = amplitude,
                Offset = offset,
                Frequency = frequencyMin
            };
        }

        public string VariableName { get => _variableName; }
        public BuiltInType DataType { get => _dataType; }
        public string ValueRanks { get => _valueRanks; }
        public string NamespaceIndex { get => _namespaceIndex; }
        public string IdentifierType { get => _identifierType; }
        public string Identifier { get => _identifier; }

        public float FrequencyMin { get => _frequencyMin; }
        public float FrequencyMax { get => _frequencyMax; }

        public BaseDataVariableState Variable { get => _variable; }

        public void NextValue(ServerSystemContext context)
        {
            if (_frequencyMin != _frequencyMax)
            {
                float freq = (float)(random.NextDouble() * ((_frequencyMax - _frequencyMin) + _frequencyMin));
                _signal.Frequency = freq;
            }

            float value = _signal.GetValue();
            
            _variable.Value = value;
            _variable.Timestamp = DateTime.UtcNow;
            _variable.StatusCode = StatusCodes.Good;
            _variable.ClearChangeMasks(context, false);
        }

        private static FolderState CreateFolder(NodeState parent, string path, string name, ushort namespaceIndex)
        {
            FolderState folder = new FolderState(parent);

            folder.SymbolicName = name;
            folder.ReferenceTypeId = ReferenceTypes.Organizes;
            folder.TypeDefinitionId = ObjectTypeIds.FolderType;
            folder.NodeId = new NodeId(path, namespaceIndex);
            folder.BrowseName = new QualifiedName(path, namespaceIndex);
            folder.DisplayName = new LocalizedText("en", name);
            folder.WriteMask = AttributeWriteMask.None;
            folder.UserWriteMask = AttributeWriteMask.None;
            folder.EventNotifier = EventNotifiers.None;

            if (parent != null)
            {
                parent.AddChild(folder);
            }

            return folder;
        }

        private static string GetParentFolderString(string path)
        {
            int lastIndex = path.LastIndexOf('.');

            // If delimiter is not found or the very first char is the delimiter then return null.
            if (lastIndex < 1)
            {
                return null;
            }
            else
            {
                return path.Substring(0, path.LastIndexOf('.'));
            }
        }

        private static string GetLeafFolderString(string path)
        {
            int lastIndex = path.LastIndexOf('.');

            // If delimiter is not found or the very first char is the delimiter then return null.
            if (lastIndex < 1)
            {
                return path;
            }
            else
            {
                return path.Substring(path.LastIndexOf('.') + 1, path.Length - (path.LastIndexOf('.') + 1));
            }
        }

        private static FolderState AddFolder(FolderState root, string path, ushort namespaceIndex)
        {
            bool folderExists = false;

            if (path == null)
                throw new ArgumentNullException(path);

            folderExists = _folders.ContainsKey(path);

            if (folderExists)
            {
                return _folders[path];
            }
            else
            {
                // folder does not exist. Add parent folder and then add child to parrent.
                string parentPath = GetParentFolderString(path);

                // if parent is null then this is a root folder
                if (parentPath == null)
                {
                    FolderState topLevelFolder = CreateFolder(root, path, path, namespaceIndex);
                    _folders.Add(path, topLevelFolder);

                    return topLevelFolder;
                }
                else
                {
                    string leafFolder = GetLeafFolderString(path);

                    FolderState parentFolder = AddFolder(root, parentPath, namespaceIndex);
                    FolderState folder = CreateFolder(parentFolder, path, leafFolder, namespaceIndex);
                    _folders.Add(path, folder);

                    return folder;
                }
            }
        }

        private static BaseDataVariableState CreateVariable(NodeState parent, string path, string name, NodeId dataType, int valueRank, ushort namespaceIndex)
        {
            BaseDataVariableState variable = new BaseDataVariableState(parent);

            variable.SymbolicName = name;
            variable.ReferenceTypeId = ReferenceTypes.Organizes;
            variable.TypeDefinitionId = VariableTypeIds.BaseDataVariableType;
            variable.NodeId = new NodeId(path, namespaceIndex);
            variable.BrowseName = new QualifiedName(path, namespaceIndex);
            variable.DisplayName = new LocalizedText("en", name);
            variable.WriteMask = AttributeWriteMask.DisplayName | AttributeWriteMask.Description;
            variable.UserWriteMask = AttributeWriteMask.DisplayName | AttributeWriteMask.Description;
            variable.DataType = dataType;
            variable.ValueRank = valueRank;
            variable.AccessLevel = AccessLevels.CurrentReadOrWrite;
            variable.UserAccessLevel = AccessLevels.CurrentReadOrWrite;
            variable.Historizing = false;
            //variable.Value = GetNewValue(variable);
            variable.StatusCode = StatusCodes.Uncertain;
            variable.Timestamp = DateTime.UtcNow;

            if (valueRank == Opc.Ua.ValueRanks.OneDimension)
            {
                variable.ArrayDimensions = new ReadOnlyList<uint>(new List<uint> { 0 });
            }
            else if (valueRank == Opc.Ua.ValueRanks.TwoDimensions)
            {
                variable.ArrayDimensions = new ReadOnlyList<uint>(new List<uint> { 0, 0 });
            }

            if (parent != null)
            {
                parent.AddChild(variable);
            }

            return variable;
        }



        public static OpcSimulationSignal CreateSimulationSignal(FolderState root, string variableFullPathName, BuiltInType datatype, SignalType signalType, float amplitude, float offset, float frequencyMin, float frequencyMax, ushort folderNamespaceIndex, ushort variableNameSpace, string identifierType, string identifier)
        {
            string parentPath = GetParentFolderString(variableFullPathName);
            string variableName = GetLeafFolderString(variableFullPathName);

            FolderState parentFolder = AddFolder(root, parentPath, folderNamespaceIndex);
            var variable = CreateVariable(parentFolder, variableFullPathName, variableName, (uint)datatype, Opc.Ua.ValueRanks.Scalar, folderNamespaceIndex);

            if (identifierType.Trim().ToLower() == "i")
            {
                variable.NodeId = new NodeId(uint.Parse(identifier), variableNameSpace);
            }

            if (identifierType.Trim().ToLower() == "g")
            {
                variable.NodeId = new NodeId(new Guid(identifier), variableNameSpace);
            }

            if (identifierType.Trim().ToLower() == "b")
            {
                variable.NodeId = new NodeId(Encoding.ASCII.GetBytes(identifier), variableNameSpace);
            }

            OpcSimulationSignal signalSimulation = new OpcSimulationSignal(variableFullPathName, datatype, signalType, amplitude, offset, frequencyMin, frequencyMax, variable);

            return signalSimulation;
        }
    }
}
