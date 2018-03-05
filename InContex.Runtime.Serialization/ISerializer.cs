using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InContex.Runtime.Serialization
{

    /// <summary>
    /// Define the minimum set of interfaces that has to to implement by a 
    /// custom data serializer that can be utilized by the persisted data 
    /// structures used by InfoHub (such as a persisted queue).
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public interface ISerializer<T> where T: struct
    {
        /// <summary>
        /// Serialize struct to byte array.
        /// </summary>
        /// <param name="value">Struct to serialize</param>
        /// <returns>Byte array representing the serialized struct.</returns>
        byte[] Serialize(T value);

        /// <summary>
        /// Serialize struct to a byte array pointer.
        /// </summary>
        /// <param name="value">Struct to serialize</param>
        /// <param name="bufferPtr">Pointer to byte array that represents the serialized struct.</param>
        //void Serialize(T value, IntPtr bufferPtr);

        /// <summary>
        /// Deserialize a byte array to a struct.
        /// </summary>
        /// <param name="buffer">Byte array representing the serialized struct.</param>
        /// <returns></returns>
        T DeSerialize(byte[] buffer);

        /// <summary>
        /// Deserialize a byte array pointer to a struct.
        /// </summary>
        /// <param name="bufferPtr">Pointer to byte array that represents the serialized struct.</param>
        /// <returns></returns>
        //T DeSerialize(IntPtr bufferPtr);

        /// <summary>
        /// Method returns maximum byte size of struct once it is serialized.
        /// </summary>
        /// <returns></returns>
        int SerializedByteSize();

    }
}
