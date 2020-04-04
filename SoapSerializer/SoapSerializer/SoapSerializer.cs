using System.IO;
using System.Net;
using System.Reflection;
using System.Text;
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

		public bool Indent
		{
			get => WriterSettings.Indent;
			set => WriterSettings.Indent = value;
		}

		public string TargetNamespacePrefix { get; set; } = "tns";
		public string SoapNamespacePrefix { get; set; } = "soap";

		private const string XmlNamespacePrefix = "xmlns";
		private const string SoapNamespace = "http://schemas.xmlsoap.org/soap/envelope/";
		private const string SoapEnvelopeElementName = "Envelope";
		private const string SoapHeaderElementName = "Header";
		private const string SoapBodyElementName = "Body";
		private const string SoapFaultElementName = "Fault";
		private const string FaultCodeElementName = "faultcode";
		private const string FaultStringElementName = "faultstring";

		private readonly XmlWriterSettings WriterSettings = new XmlWriterSettings()
		{
			CloseOutput = false,
			Async = true,
			WriteEndDocumentOnClose = true,
			Encoding = new UTF8Encoding(false)
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

		public async Task<string> Serialize<T>(T value)
		{
			using MemoryStream stream = new MemoryStream();
			await Serialize(stream, value);
			return WriterSettings.Encoding.GetString(stream.ToArray());
		}

		public async Task<string> Serialize(object? value)
		{
			using MemoryStream stream = new MemoryStream();
			await Serialize(stream, value);
			return WriterSettings.Encoding.GetString(stream.ToArray());
		}

		public async Task Serialize<T>(Stream stream, T value)
		{
			using XmlWriter writer = XmlWriter.Create(stream, WriterSettings);
			await WriteDocumentTop(writer);
			if (value != null) new XmlSerializer(typeof(T)).Serialize(writer, value, SerializerNamespaces);
			await WriteDocumentBottom(writer);
		}

		public async Task Serialize(Stream stream, object? value)
		{
			using XmlWriter writer = XmlWriter.Create(stream, WriterSettings);
			await WriteDocumentTop(writer);
			if (value != null) new XmlSerializer(value.GetType()).Serialize(writer, value, SerializerNamespaces);
			await WriteDocumentBottom(writer);
		}

		public async Task<string> SerializeException(Exception exception)
		{
			using MemoryStream stream = new MemoryStream();
			await SerializeException(stream, exception);
			return WriterSettings.Encoding.GetString(stream.ToArray());
		}

		public async Task SerializeException(Stream stream, Exception exception)
		{
			using XmlWriter writer = XmlWriter.Create(stream, WriterSettings);
			await WriteDocumentTop(writer);

			await writer.WriteStartElementAsync(SoapNamespacePrefix, SoapFaultElementName, SoapNamespace);
			await writer.WriteElementStringAsync(null, FaultCodeElementName, null, exception.GetType().ToString());
			await writer.WriteElementStringAsync(null, FaultStringElementName, null, exception.Message);
			await writer.WriteEndElementAsync();

			await WriteDocumentBottom(writer);
		}

		private async Task WriteDocumentTop(XmlWriter writer)
		{
			await writer.WriteStartDocumentAsync();
			await writer.WriteStartElementAsync(SoapNamespacePrefix, SoapEnvelopeElementName, SoapNamespace);
			await writer.WriteAttributeStringAsync(XmlNamespacePrefix, TargetNamespacePrefix, null, TargetNamespace);
			await writer.WriteElementStringAsync(SoapNamespacePrefix, SoapHeaderElementName, SoapNamespace, null);
			await writer.WriteStartElementAsync(SoapNamespacePrefix, SoapBodyElementName, SoapNamespace);
		}

		private async Task WriteDocumentBottom(XmlWriter writer)
		{
			await writer.WriteEndElementAsync();
			await writer.WriteEndElementAsync();
		}

		public async Task<T> Deserialize<T>(string soap) => (T)await Deserialize(soap, typeof(T));

		public async Task<T> Deserialize<T>(StringReader stringReader) => (T)await Deserialize(stringReader, typeof(T));

		public async Task<T> Deserialize<T>(Stream stream) => (T)await Deserialize(stream, typeof(T));

		public async Task<object> Deserialize(string soap, Type type) => await Deserialize(new StringReader(soap), type);

		public async Task<object> Deserialize(StringReader stringReader, Type type)
		{
			using XmlReader reader = XmlReader.Create(stringReader, ReaderSettings);
			return await ReadDocument(reader, type);
		}

		public async Task<object> Deserialize(Stream stream, Type type)
		{
			using XmlReader reader = XmlReader.Create(stream, ReaderSettings);
			return await ReadDocument(reader, type);
		}

		private async Task<object> ReadDocument(XmlReader reader, Type type)
		{
			await reader.ReadAsync();
			if (reader.NodeType != XmlNodeType.XmlDeclaration)
				throw new InvalidDataException("The XML declaration is missing in the specified XML data stream.");

			bool envelopeInitialized = false, bodyInitialized = false;

			while (await reader.ReadAsync())
			{
				if (reader.NodeType == XmlNodeType.Element && reader.NamespaceURI == SoapNamespace && reader.LocalName == SoapEnvelopeElementName)
				{
					while (await reader.ReadAsync())
						if (reader.NodeType == XmlNodeType.Element && reader.NamespaceURI == SoapNamespace && reader.LocalName == SoapBodyElementName)
						{
							XmlRootAttribute? rootAttribute = type.GetCustomAttribute<XmlRootAttribute>(true);
							XmlTypeAttribute? typeAttribute = type.GetCustomAttribute<XmlTypeAttribute>(true);
							string rootName = rootAttribute?.ElementName ?? typeAttribute?.TypeName ?? type.Name;
							string? rootNamespace = rootAttribute?.Namespace ?? typeAttribute?.Namespace;

							while (await reader.ReadAsync())
								if (reader.NodeType == XmlNodeType.Element)
								{
									if (reader.NamespaceURI == rootNamespace && reader.LocalName == rootName)
										return new XmlSerializer(type).Deserialize(reader);
									else if (reader.NamespaceURI == SoapNamespace && reader.LocalName == SoapFaultElementName)
									{
										string? faultCode = null;

										while (await reader.ReadAsync())
											if (reader.NodeType == XmlNodeType.Element)
												switch (reader.LocalName)
												{
													case FaultCodeElementName:
														await reader.ReadAsync();
														faultCode = await reader.ReadContentAsStringAsync();
														break;

													case FaultStringElementName:
														await reader.ReadAsync();
														throw new ProtocolViolationException($"The following error was returned from the server: {(faultCode == null ? string.Empty : $"{faultCode}: ")}{await reader.ReadContentAsStringAsync()}");
												}
									}
								}

							bodyInitialized = true;
						}

					if (!bodyInitialized) throw new InvalidDataException("No body element is contained in the specified SOAP message.");
					envelopeInitialized = true;
				}
			}

			if (!envelopeInitialized) throw new InvalidDataException("No envelope element is contained in the specified SOAP message.");
			throw new InvalidDataException("The specified SOAP message has an invalid format.");
		}

	}
}