using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using NUnit.Framework;

using SharpRaven.Data;

namespace SharpRaven.Pcl.UnitTests.Data
{
    [TestFixture]
    public class JsonPacketTests
    {
        [Test]
        public void Constructor_WithException_OK()
        {
            ApplicationException exception;
            try
            {
                throw new ApplicationException("Foo");
            }
            catch (ApplicationException ex)
            {
                exception = ex;
            }

            var packet = new JsonPacket("Foo", exception);

            


        }
    }
}
