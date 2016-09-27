using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.IO;
using System.Net;
using Html;

namespace HtmlReaderTest
{
    class Program
    {
        static void Main(string[] args)
        {
            try
            {
                // Adjust current directory.
                {
                    string dir = Directory.GetCurrentDirectory();
                    if (dir.EndsWith("\\bin\\debug", StringComparison.OrdinalIgnoreCase) || dir.EndsWith("\\bin\\release", StringComparison.OrdinalIgnoreCase))
                    {
                        Directory.SetCurrentDirectory(Path.GetDirectoryName(Path.GetDirectoryName(dir)));
                    }
                }

                UnitTests.MatchHtmlToXml(0);
                UnitTests.MatchHtmlToXml(1);
                UnitTests.MatchHtmlToXml(2);
                UnitTests.MatchHtmlToXml(3);

                //RunXmlReader();
                //Console.WriteLine("================================");
                //RunHtmlReader();
                //Console.WriteLine("================================");
                //TraceXmlReader();
                //Console.WriteLine("================================");
                //TestHtmlReader();
            }
            catch (Exception err)
            {
                Console.Error.WriteLine(err.ToString());
            }

            if (Win32Interop.ConsoleHelper.IsSoleConsoleOwner)
            {
                Console.Error.WriteLine();
                Console.Error.Write("Press any key to exit.");
                Console.ReadKey(true);
            }
        }

        static void RunXmlReader()
        {
            XmlReaderSettings settings = new XmlReaderSettings();
            settings.CloseInput = true;
            settings.DtdProcessing = DtdProcessing.Parse;
            using (XmlReader reader = XmlReader.Create(new StreamReader("sample.xml", Encoding.UTF8, true), settings))
            {
                DumpXmlReader(reader);
            }
        }

        static void RunHtmlReader()
        {
            HtmlReaderSettings settings = new HtmlReaderSettings();
            settings.CloseInput = true;
            using (HtmlReader reader = new HtmlReader(new StreamReader("sample.htm", Encoding.UTF8, true), settings))
            {
                DumpXmlReader(reader);
            }
        }

        static void TraceXmlReader()
        {
            XmlReaderSettings settings = new XmlReaderSettings();
            settings.CloseInput = true;
            settings.DtdProcessing = DtdProcessing.Parse;
            using (XmlReader reader = XmlReader.Create(new StreamReader("sample.xml", Encoding.UTF8, true), settings))
            {
                XmlTracer tracer = new XmlTracer(reader);

                XmlDocument doc = new XmlDocument();
                doc.Load(tracer);
            }
        }

        const string c_outputFilename = "sampleOutput.xml";

        static void TestHtmlReader()
        {
            XmlDocument doc = new XmlDocument();

            HtmlReaderSettings settings = new HtmlReaderSettings();
            settings.CloseInput = true;
            using (HtmlReader reader = new HtmlReader(new StreamReader("sample.htm", Encoding.UTF8, true), settings))
            {
                doc.Load(reader);
            }

            XmlWriterSettings writerSettings = new XmlWriterSettings();
            writerSettings.Indent = true;

            if (File.Exists(c_outputFilename)) File.Delete(c_outputFilename);
            using (XmlWriter writer = XmlWriter.Create(c_outputFilename, writerSettings))
            {
                doc.WriteTo(writer);
            }
        }

        static void DumpXmlReader(XmlReader reader)
        {
            // This is modeled on traces of the XmlDocument.Load() so that it calls
            // the same XmlReader methods in the same order.
            while (reader.Read())
            {
                switch (reader.NodeType)
                {
                    case XmlNodeType.Element:
                        {
                            bool isEmpty = reader.IsEmptyElement;
                            Console.Write("{0}<{1}", new String(' ', reader.Depth * 3), reader.Name);
                            if (reader.MoveToFirstAttribute())
                            {
                                do
                                {
                                    string attrName = reader.Name;
                                    string attrValue = null;
                                    while (reader.ReadAttributeValue())
                                    {
                                        attrValue = (attrValue == null) ? reader.Value : string.Concat(attrValue, reader.Value);
                                    }
                                    Console.Write(" {0}=\"{1}\"", attrName, WebUtility.HtmlEncode(attrValue));
                                } while (reader.MoveToNextAttribute());
                            }
                            Console.WriteLine(isEmpty ? "/>" : ">");
                        }
                        break;

                    case XmlNodeType.EndElement:
                        Console.WriteLine(reader.IsEmptyElement ? "{0}<!--/{1}-->" : "{0}</{1}>", new String(' ', reader.Depth * 3), reader.Name);
                        break;

                    case XmlNodeType.Whitespace:
                        Console.WriteLine("{0}<!--Whitespace-->{1}", new String(' ', reader.Depth * 3), WebUtility.UrlEncode(reader.Value));
                        break;

                    case XmlNodeType.Text:
                        Console.WriteLine("{0}<!--Text-->{1}", new String(' ', reader.Depth * 3), WebUtility.HtmlEncode(reader.Value));
                        break;

                    case XmlNodeType.Comment:
                        Console.WriteLine("{0}<!--{1}-->", new String(' ', reader.Depth * 3), reader.Value);
                        break;

                    default:
                        Console.WriteLine("{0}<!--type={1} name={2} value={3}-->", new String(' ', reader.Depth * 3), reader.NodeType, reader.Name, reader.Value);
                        break;
                }
            }
        } // TraceXmlReader

    }

    class XmlTracer : XmlReader
    {
        XmlReader m_ir;

        public XmlTracer(XmlReader ir)
        {
            m_ir = ir;
        }

        public override int AttributeCount
        {
            get
            {
                Console.WriteLine("AttributeCount: {0}", m_ir.AttributeCount);
                return m_ir.AttributeCount;
            }
        }

        public override string BaseURI
        {
            get
            {
                Console.WriteLine("BaseUri: {0}", m_ir.BaseURI);
                return m_ir.BaseURI;
            }
        }

        public override int Depth
        {
            get
            {
                Console.WriteLine("Depth: {0}", m_ir.Depth);
                return m_ir.Depth;
            }
        }

        public override bool EOF
        {
            get
            {
                Console.WriteLine("EOF: {0}", m_ir.EOF);
                return m_ir.EOF;
            }
        }

        public override bool IsEmptyElement
        {
            get
            {
                Console.WriteLine("IsEmptyElement: {0}", m_ir.IsEmptyElement);
                return m_ir.IsEmptyElement;
            }
        }

        public override string LocalName
        {
            get
            {
                Console.WriteLine("LocalName: {0}", m_ir.LocalName);
                return m_ir.LocalName;
            }
        }

        public override string NamespaceURI
        {
            get
            {
                Console.WriteLine("NamespaceURI: {0}", m_ir.NamespaceURI);
                return m_ir.NamespaceURI;
            }
        }

        public override XmlNameTable NameTable
        {
            get
            {
                Console.WriteLine("NameTable");
                return m_ir.NameTable;
            }
        }

        public override XmlNodeType NodeType
        {
            get
            {
                Console.WriteLine("NodeType: {0}", m_ir.NodeType);
                return m_ir.NodeType;
            }
        }

        public override string Prefix
        {
            get
            {
                Console.WriteLine("Prefix: {0}", m_ir.Prefix);
                return m_ir.Prefix;
            }
        }

        public override ReadState ReadState
        {
            get
            {
                Console.WriteLine("ReadState: {0}", m_ir.ReadState);
                return m_ir.ReadState;
            }
        }

        public override string Value
        {
            get
            {
                Console.WriteLine("Value: {0}", m_ir.Value);
                return m_ir.Value;
            }
        }

        public override string GetAttribute(int i)
        {
            string value = m_ir.GetAttribute(i);
            Console.WriteLine("GetAttribute({0}): {1}", i, value);
            return value;
        }

        public override string GetAttribute(string name)
        {
            string value = m_ir.GetAttribute(name);
            Console.WriteLine("GetAttribute({0}): {1}", name, value);
            return value;
        }

        public override string GetAttribute(string name, string namespaceURI)
        {
            string value = m_ir.GetAttribute(name, namespaceURI);
            Console.WriteLine("GetAttribute({0}, {1}): {2}", name, namespaceURI, value);
            return value;
        }

        public override string LookupNamespace(string prefix)
        {
            string value = m_ir.LookupNamespace(prefix);
            Console.WriteLine("LookupNamespace({0}): {1}", prefix, value);
            return value;
        }

        public override bool MoveToAttribute(string name)
        {
            bool value = m_ir.MoveToAttribute(name);
            Console.WriteLine("MoveToAttribute({0}): {1}", name, value);
            return value;
        }

        public override bool MoveToAttribute(string name, string ns)
        {
            bool value = m_ir.MoveToAttribute(name, ns);
            Console.WriteLine("MoveToAttribute({0}, {1}): {2}", name, ns, value);
            return value;
        }

        public override bool MoveToElement()
        {
            bool value = m_ir.MoveToElement();
            Console.WriteLine("MoveToElement(): {0}", value);
            return value;
        }

        public override bool MoveToFirstAttribute()
        {
            bool value = m_ir.MoveToFirstAttribute();
            Console.WriteLine("MoveToFirstAttribute(): {0}", value);
            return value;
        }

        public override bool MoveToNextAttribute()
        {
            bool value = m_ir.MoveToNextAttribute();
            Console.WriteLine("MoveToNextAttribute(): {0}", value);
            return value;
        }

        public override bool Read()
        {
            bool value = m_ir.Read();
            Console.WriteLine("Read(): {0}", value);
            return value;
        }

        public override bool ReadAttributeValue()
        {
            bool value = m_ir.ReadAttributeValue();
            Console.WriteLine("ReadAttributeValue(): {0}", value);
            return value;
        }

        public override void ResolveEntity()
        {
            m_ir.ResolveEntity();
            Console.WriteLine("ResolveEntity()");
        }
    }

}
