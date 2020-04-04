using NUnit.Framework;
using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;
using System.Xml.Soap.Serialization;

namespace SoapSerializerTest
{

	public class SoapSerializerTest
	{

		[XmlRoot("Point", Namespace = "http://tempuri.org/")]
		public class Vector
		{

			public float X { get; set; }
			public float Y { get; set; }

			public override string ToString() => $"{{{X}|{Y}}}";

		}

		[SetUp]
		public void Setup()
		{
		}

		[Test]
		public async Task SerializeTest()
		{
			using MemoryStream stream = new MemoryStream();
			SoapSerializer serializer = new SoapSerializer("http://tempuri.org/") { Indent = true };
			TestContext.WriteLine(await serializer.Serialize(new Vector() { X = 9.6f, Y = 4.2f }));

			Assert.Pass();
		}

		[Test]
		public async Task SerializeExceptionTest()
		{
			using MemoryStream stream = new MemoryStream();
			SoapSerializer serializer = new SoapSerializer("http://tempuri.org/") { Indent = true };
			TestContext.WriteLine(await serializer.SerializeException(new DirectoryNotFoundException("The directory was not found.")));

			Assert.Pass();
		}

		[Test]
		public async Task SerializeExceptionTest2()
		{
			using MemoryStream stream = new MemoryStream();
			SoapSerializer serializer = new SoapSerializer("http://tempuri.org/") { Indent = true };

			try
			{
				Uri uri = new Uri("httpx://]");
			}
			catch (Exception ex)
			{
				TestContext.WriteLine(await serializer.SerializeException(ex));
			}

			Assert.Pass();
		}

		[Test]
		public async Task DeserializeTest()
		{
			string soap = @"<?xml version=""1.0"" encoding=""utf-8""?>
<soap:Envelope xmlns:tns=""http://tempuri.org/"" xmlns:soap=""http://schemas.xmlsoap.org/soap/envelope/"">
	<soap:Header />
	<soap:Body>
		<tns:Point>
			<tns:X>9.6</tns:X>
			<tns:Y>4.2</tns:Y>
		</tns:Point>
	</soap:Body>
</soap:Envelope>";

			SoapSerializer serializer = new SoapSerializer("http://tempuri.org/");
			Vector v = await serializer.Deserialize<Vector>(soap);

			TestContext.WriteLine(v);
			Assert.Pass();
		}

		[Test]
		public void DeserializeExceptionTest()
		{
			string soap = @"<?xml version=""1.0"" encoding=""utf-8""?>
<soap:Envelope xmlns:tns=""http://tempuri.org/"" xmlns:soap=""http://schemas.xmlsoap.org/soap/envelope/"">
	<soap:Header />
	<soap:Body>
		<soap:Fault>
			<faultcode>System.IO.DirectoryNotFoundException</faultcode>
			<faultstring>The directory was not found.</faultstring>
		</soap:Fault>
	</soap:Body>
</soap:Envelope>";

			SoapSerializer serializer = new SoapSerializer("http://tempuri.org/");
			Assert.ThrowsAsync<ProtocolViolationException>(async () => await serializer.Deserialize<Vector>(soap));
		}

	}

}