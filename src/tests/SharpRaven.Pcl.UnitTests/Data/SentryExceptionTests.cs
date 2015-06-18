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
	public class SentryExceptionTests
	{
		[Test]
		public void Constructor_FromException()
		{
			SentryException sentryEx;
			try
			{
				throw new ApplicationException("Foo");
			}
			catch (ApplicationException ex)
			{
				System.Diagnostics.Trace.WriteLine(ex.StackTrace);
				sentryEx = new SentryException(ex);
				
			}

			Assert.That(sentryEx.Module, Is.EqualTo("SharpRaven.Pcl.UnitTests"));
			Assert.That(sentryEx.Type, Is.EqualTo("System.ApplicationException"));
			Assert.That(sentryEx.Stacktrace.Frames[0].Function, Is.EqualTo("SharpRaven.Pcl.UnitTests.Data.SentryExceptionTests.Constructor_FromException()"));
		}
	}
}
