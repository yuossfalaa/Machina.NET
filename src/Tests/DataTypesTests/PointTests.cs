using System.Diagnostics;
using Machina.Types.Geometry;
using NUnit.Framework;
using NUnit.Framework.Legacy;

namespace DataTypesTests
{
    public class PointTests : DataTypesTests
    {
        [Test]
        public void Point_CompareDirections()
        {
            Vector a = new Vector(1, 0, 0);

            Vector b = new Vector(1, 1, 0);
            ClassicAssert.AreEqual(0, Vector.CompareDirections(a, b));  // nothing

            b = new Vector(1, 0, 0);
            ClassicAssert.AreEqual(1, Vector.CompareDirections(a, b));  // parallel

            b = new Vector(5, 0, 0);
            ClassicAssert.AreEqual(1, Vector.CompareDirections(a, b));  // parallel

            b = new Vector(10, 0, 0);
            ClassicAssert.AreEqual(1, Vector.CompareDirections(a, b));  // parallel

            b = new Vector(0, 1, 0);
            ClassicAssert.AreEqual(2, Vector.CompareDirections(a, b));  // orthogonal

            b = new Vector(0, 0, 1);
            ClassicAssert.AreEqual(2, Vector.CompareDirections(a, b));  // orthogonal

            b = new Vector(0, -1, 0);
            ClassicAssert.AreEqual(2, Vector.CompareDirections(a, b));  // orthogonal

            b = new Vector(0, 0, -1);
            ClassicAssert.AreEqual(2, Vector.CompareDirections(a, b));  // orthogonal

            b = new Vector(-1, 0, 0);
            ClassicAssert.AreEqual(3, Vector.CompareDirections(a, b));  // opposed

            b = new Vector(-5, 0, 0);
            ClassicAssert.AreEqual(3, Vector.CompareDirections(a, b));  // opposed

            b = new Vector(-10, 0, 0);
            ClassicAssert.AreEqual(3, Vector.CompareDirections(a, b));  // opposed

            a = new Vector(Random(-100, 100), Random(-100, 100), Random(-100, 100));
            b = new Vector(5 * a.X, 5 * a.Y, 5 * a.Z);
            Trace.WriteLine(a);
            Trace.WriteLine(b);
            ClassicAssert.AreEqual(1, Vector.CompareDirections(a, b));  // parallel

            b = new Vector(-a.X, -a.Y, -a.Z);
            Trace.WriteLine(a);
            Trace.WriteLine(b);
            ClassicAssert.AreEqual(3, Vector.CompareDirections(a, b));  // opposed

        }

    }
}
