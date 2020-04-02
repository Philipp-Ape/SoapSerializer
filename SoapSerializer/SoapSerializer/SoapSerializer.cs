using System.IO;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace System.Xml.Soap.Serialization
{

	public class SoapSerializer
	{

		public string? TargetNamespace
		{
			get => _TargetNamespace;
			set
			{
				_TargetNamespace = value;
				SerializerNamespaces = new XmlSerializerNamespaces();
				SerializerNamespaces.Add(TargetNamespacePrefix, value);
			}
		}

		private const string XmlNamespacePrefix = "xmlns";
		private const string TargetNamespacePrefix = "tns";
		private const string SoapNamespacePrefix = "soap";
		private const string SoapNamespace = "http://schemas.xmlsoap.org/soap/envelope/";
		private const string SoapEnvelopeElementName = "Envelope";
		private const string SoapHeaderElementName = "Header";
		private const string SoapBodyElementName = "Body";

		private readonly XmlWriterSettings WriterSettings = new XmlWriterSettings()
		{
			CloseOutput = false,
			Async = true,
			WriteEndDocumentOnClose = true
		};

		private readonly XmlReaderSettings ReaderSettings = new XmlReaderSettings()
		{
			CloseInput = false,
			Async = true
		};

		private XmlSerializerNamespaces SerializerNamespaces = new XmlSerializerNamespaces();
		private string? _TargetNamespace;

		public SoapSerializer() { }

		public SoapSerializer(string targetNamespace)
		{
			TargetNamespace = targetNamespace;
			SerializerNamespaces.Add(TargetNamespacePrefix, targetNamespace);
		}

		public async Task Serialize(Stream stream, object value)
		{
			using XmlWriter writer = XmlWriter.Create(stream, WriterSettings);

			await writer.WriteStartDocumentAsync();
			await writer.WriteStartElementAsync(SoapNamespacePrefix, SoapEnvelopeElementName, SoapNamespace);
			await writer.WriteAttributeStringAsync(XmlNamespacePrefix, TargetNamespacePrefix, null, TargetNamespace);
			await writer.WriteElementStringAsync(SoapNamespacePrefix, SoapHeaderElementName, SoapNamespace, null);
			await writer.WriteStartElementAsync(SoapNamespacePrefix, SoapBodyElementName, SoapNamespace);

			new XmlSerializer(value.GetType()).Serialize(writer, value, SerializerNamespaces);

			await writer.WriteEndElementAsync();
			await writer.WriteEndElementAsync();
		}

		public async Task<T> Deserialize<T>(Stream stream)
		{
			using XmlReader reader = XmlReader.Create(stream, ReaderSettings);

			await reader.ReadAsync();
			if (reader.NodeType != XmlNodeType.XmlDeclaration) throw new InvalidDataException(nameof(stream));

			await reader.ReadAsync();
			if (reader.NodeType != XmlNodeType.Element || reader.NamespaceURI != SoapNamespace || reader.LocalName != SoapEnvelopeElementName)
				throw new InvalidDataException(nameof(stream));

			while (await reader.ReadAsync())
			{
				if (reader.NodeType == XmlNodeType.Element && reader.NamespaceURI == SoapNamespace && reader.LocalName == SoapBodyElementName)
				{
					await reader.ReadAsync();
					return (T)new XmlSerializer(typeof(T)).Deserialize(reader);
				}
			}

			throw new InvalidDataException(nameof(stream));
		}

	}
}