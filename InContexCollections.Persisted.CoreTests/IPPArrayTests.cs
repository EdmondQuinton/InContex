using Microsoft.VisualStudio.TestTools.UnitTesting;
using InContex.Collections.Persisted.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace InContex.Collections.Persisted.Core.Tests
{
    [TestClass()]
    public class IPPArrayTests
    {
        [TestMethod()]
        public void Open_InconsistentParametersClient_ThrowException()
        {
            string path = Directory.GetParent(Directory.GetCurrentDirectory()).Parent.FullName;
            path = Path.Combine(path, "Output");
            string name = "array-unit-test-client-open";

            IPPArray<int> array = IPPArray<int>.Open(path, name, 1000);

            Assert.IsNotNull(array);
            Assert.ThrowsException<ApplicationException>(() => IPPArray<long>.Open(path, name, 1000).Dispose(), "Expected exception, provided array type is different to persited data type.");
            Assert.ThrowsException<ApplicationException>(() => IPPArray<int>.Open(path, name, 5000).Dispose(), "Expected exception, provided array length is different to persited length.");
            Assert.ThrowsException<ApplicationException>(() => IPPArray<int>.Open(path, name, 100).Dispose(), "Expected exception, provided array length is different to persited length.");

            array.Dispose();
        }

        [TestMethod()]
        public void Open_InconsistentParametersServer_ThrowException()
        {
            string path = Directory.GetParent(Directory.GetCurrentDirectory()).Parent.FullName;
            path = Path.Combine(path, "Output");
            string name = "array-unit-test-server-open";

            IPPArray<int> array = IPPArray<int>.Open(path, name, 1000);
            array.Dispose();
            array = null;

            Assert.ThrowsException<ApplicationException>(() => IPPArray<long>.Open(path, name, 1000).Dispose(), "Expected exception, provided array type is different to persited data type.");
            Assert.ThrowsException<ApplicationException>(() => IPPArray<int>.Open(path, name, 5000).Dispose(), "Expected exception, provided array length is different to persited length.");
            Assert.ThrowsException<ApplicationException>(() => IPPArray<int>.Open(path, name, 100).Dispose(), "Expected exception, provided array length is different to persited length.");

        }

        [TestMethod()]
        public void Open_InvalidLength_ThrowException()
        {
            string path = Directory.GetParent(Directory.GetCurrentDirectory()).Parent.FullName;
            path = Path.Combine(path, "Output");
            string name = "array-unit-test-zero-length";

            Assert.ThrowsException<ArgumentOutOfRangeException>(() => IPPArray<long>.Open(path, name, 0), "Zero Length Array.");
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => IPPArray<long>.Open(path, name, -1), "Negative Length Array.");
        }

        [TestMethod()]
        public void PersistenceTest()
        {
            string path = Directory.GetParent(Directory.GetCurrentDirectory()).Parent.FullName;
            path = Path.Combine(path, "Output");
            string name = "array-unit-test-persistenc";

            IPPArray<int> arrayA = IPPArray<int>.Open(path, name, 1000);
            IPPArray<int> arrayB = IPPArray<int>.Open(path, name, 1000);
            IPPArray<int> arrayC = IPPArray<int>.Open(path, name, 1000);

            for (int index = 0; index < 400; index++)
            {
                arrayA[index] = index;
            }
            arrayA.Dispose();
            arrayA = null;

            for (int index = 400; index < 700; index++)
            {
                arrayB[index] = index;
            }
            arrayB.Dispose();
            arrayB = null;

            for (int index = 700; index < 1000; index++)
            {
                arrayC[index] = index;
            }
            arrayC.Dispose();
            arrayC = null;

            using (IPPArray<int> array = IPPArray<int>.Open(path, name, 1000))
            {

                for (int index = 0; index < 100; index++)
                {
                    Assert.AreEqual<int>(array[index], index);
                }
            }
        }




        [TestMethod()]
        public void ClearTest()
        {
            string path = Directory.GetParent(Directory.GetCurrentDirectory()).Parent.FullName;
            path = Path.Combine(path, "Output");
            string name = "array-unit-test-clear";
            int size = 100000000;
            int midPoint = size / 2;

            IPPArray<int> array = IPPArray<int>.Open(path, name, size);

            for(int index = 0; index < size; index++)
            {
                array[index] = index;
            }

            array.Dispose();
            array = null;

            array = IPPArray<int>.Open(path, name, size);
            array.Clear(0, midPoint);

            Assert.AreEqual<int>(midPoint, array[midPoint]);

            for (int index = 0; index < midPoint; index++)
            {
                Assert.AreEqual<int>(0, array[index]);
            }

            array.Clear(midPoint, size - midPoint);
            Assert.AreEqual<int>(0, array[midPoint]);

            for (int index = midPoint; index < (size - midPoint); index++)
            {
                Assert.AreEqual<int>(0, array[index]);
            }

            array.Dispose();
            array = null;
        }

        [TestMethod()]
        public void CopyToTest()
        {
            string path = Directory.GetParent(Directory.GetCurrentDirectory()).Parent.FullName;
            path = Path.Combine(path, "Output");
            string name = "array-unit-test-copyto";

            int[] destArrayA = new int[1000];
            int[] destArrayB = new int[1000];
            int[] destArrayC = new int[1000];

            IPPArray<int> array = IPPArray<int>.Open(path, name, 1000);

            for (int index = 0; index < 1000; index++)
            {
                array[index] = index;
            }

            array.CopyTo(ref destArrayA, (int)0);
            array.CopyTo(ref destArrayB, (long)0);
            array.CopyTo(500, ref destArrayC, 0, 500);
            array.CopyTo(0, ref destArrayC, 500, 500);

            int expectedValue = 0;

            for (int index = 0; index < 1000; index++)
            {
                Assert.AreEqual(expectedValue, destArrayA[index]);
                expectedValue++;
            }

            expectedValue = 0;
            for (int index = 0; index < 1000; index++)
            {
                Assert.AreEqual(expectedValue, destArrayB[index]);
                expectedValue++;
            }

            expectedValue = 0;
            for (int index = 500; index < 1000; index++)
            {
                Assert.AreEqual(expectedValue, destArrayC[index]);
                expectedValue++;
            }
            for (int index = 0; index < 500; index++)
            {
                Assert.AreEqual(expectedValue, destArrayC[index]);
                expectedValue++;
            }

            array.Dispose();
        }


        [TestMethod()]
        public void CopyFromTest()
        {
            string path = Directory.GetParent(Directory.GetCurrentDirectory()).Parent.FullName;
            path = Path.Combine(path, "Output");
            string name = "array-unit-test-copyfrom";

            int[] sourceArray = new int[1000];

            for (int index = 0; index < 1000; index++)
            {
                sourceArray[index] = index;
            }

            IPPArray<int> array = IPPArray<int>.Open(path, name, 1000);

            array.CopyFrom(ref sourceArray, 500, 0, 500);
            array.CopyFrom(ref sourceArray, 0, 500, 500);

            int expectedValue = 0;
            for (int index = 500; index < 1000; index++)
            {
                Assert.AreEqual(expectedValue, array[index]);
                expectedValue++;
            }
            for (int index = 0; index < 500; index++)
            {
                Assert.AreEqual(expectedValue, array[index]);
                expectedValue++;
            }

            array.Dispose();
        }

        [TestMethod()]
        public void GetValue_InvalidIndexRange_ThrowsException()
        {
            string path = Directory.GetParent(Directory.GetCurrentDirectory()).Parent.FullName;
            path = Path.Combine(path, "Output");
            string name = "array-unit-test-get-set";

            IPPArray<int> array = IPPArray<int>.Open(path, name, 1000);

            Assert.ThrowsException<IndexOutOfRangeException>(() => array.GetValue(-1));
            Assert.ThrowsException<IndexOutOfRangeException>(() => array.GetValue(1000));
            Assert.ThrowsException<IndexOutOfRangeException>(() => array.GetValue(10000));

            array.Dispose();
        }


        [TestMethod()]
        public void SetValue_InvalidIndexRange_ThrowsException()
        {
            string path = Directory.GetParent(Directory.GetCurrentDirectory()).Parent.FullName;
            path = Path.Combine(path, "Output");
            string name = "array-unit-test-get-set";

            IPPArray<int> array = IPPArray<int>.Open(path, name, 1000);

            Assert.ThrowsException<IndexOutOfRangeException>(() => array.SetValue(-1, -1));
            Assert.ThrowsException<IndexOutOfRangeException>(() => array.SetValue(1000, 1000));
            Assert.ThrowsException<IndexOutOfRangeException>(() => array.SetValue(10000, 10000));

            array.Dispose();
        }


        [TestMethod()]
        public void Enumerator_SetValuesAndEnumerateResults_Succeed()
        {
            string path = Directory.GetParent(Directory.GetCurrentDirectory()).Parent.FullName;
            path = Path.Combine(path, "Output");
            string name = "array-unit-test-enumerator";
            int size = 100000000;

            IPPArray<int> array = IPPArray<int>.Open(path, name, size);

            for (int index = 0; index < size; index++)
            {
                array[index] = index;
            }

            array.Dispose();
            array = null;
            array = IPPArray<int>.Open(path, name, size);

            int expectedValue = 0;

            foreach(int value in array)
            {
                Assert.AreEqual<int>(expectedValue, value);
                expectedValue++;
            }

            array.Dispose();
        }

        [TestMethod()]
        public void Enumerator_ModifyArrayWhileEnumerating_ThrowException()
        {
            string path = Directory.GetParent(Directory.GetCurrentDirectory()).Parent.FullName;
            path = Path.Combine(path, "Output");
            string name = "array-unit-test-enumerator";
            int size = 100000000;
            int midPoint = size / 2;

            IPPArray<int> array = IPPArray<int>.Open(path, name, size);

            for (int index = 0; index < size; index++)
            {
                array[index] = index;
            }

            array.Dispose();
            array = null;
            array = IPPArray<int>.Open(path, name, size);

            int expectedValue = 0;

            Assert.ThrowsException<InvalidOperationException>(() =>
            {
                foreach (int value in array)
                {
                    if (expectedValue == midPoint)
                    {
                        array[1] = 1000;
                    }

                    expectedValue++;
                }
            }
            );

            array.Dispose();
        }
    }
}