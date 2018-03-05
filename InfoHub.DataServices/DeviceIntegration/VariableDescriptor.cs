using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace InContex.DataServices.DeviceIntegration
{
    public class VariableDescriptor
    {
        private int _handle;
        private int _namespaceID;
        private string _name;
        private string _address;
        private VariableDataTypeEnum _requestedDataType;

        public VariableDescriptor()
        {
            _namespaceID = 0;
            _handle = 0;
            _name = null;
            _address = null;
            _requestedDataType = VariableDataTypeEnum.Numeric;
        }

        public VariableDescriptor(int namespaceID, int handle, string name, string address, VariableDataTypeEnum attributeType)
        {
            _namespaceID = namespaceID;
            _handle = handle;
            _name = name;
            _address = address;
            _requestedDataType = attributeType;
        }


        public int NamespaceID
        {
            get
            {
                return _namespaceID;
            }
        }

        public int Handle
        {
            get
            {
                return _handle;
            }
        }

        public string Name
        {
            get
            {
                return _name;
            }
        }

        public string Address
        {
            get
            {
                return _address;
            }
        }

        public VariableDataTypeEnum RequestedDataType
        {
            get
            {
                return _requestedDataType;
            }
        }

        
    }
}