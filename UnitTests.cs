using System;
using System.Text;
using Html;
using System.Xml;
using System.Xml.XPath;
using System.IO;
using System.Diagnostics;

namespace HtmlReaderTest
{

    static class UnitTests
    {
        const string c_UnitTestPrefix = "UnitTest_";
        const string c_UnitTestSuffixHtml = ".html";
        const string c_UnitTestSuffixXml = ".xml";

        public static bool MatchHtmlToXml(int testIndex)
        {
            string htmlFilename;
            string xmlFilename;
            GenerateFilenames(testIndex, out htmlFilename, out xmlFilename);

            string convertedHtml;

            using (StreamReader reader = new StreamReader(htmlFilename, Encoding.UTF8, true))
            {
                using (StringWriter writer = new StringWriter())
                {
                    HtmlToXml(reader, writer);
                    writer.Flush();
                    convertedHtml = writer.ToString();
                }
            }

            string matchXml;

            using (StreamReader reader = new StreamReader(xmlFilename, Encoding.UTF8, true))
            {
                matchXml = reader.ReadToEnd();
            }

            bool succeeded = convertedHtml.Equals(matchXml, StringComparison.Ordinal);

            Console.WriteLine("MatchHtmlToXml({0}) {1}.", testIndex, succeeded ? "succeeded" : "failed");

            Debug.Assert(succeeded);
            return succeeded;
        }

        public static void UnexpectedEof(int testIndex)
        {
            string htmlFilename;
            string xmlFilename;
            GenerateFilenames(testIndex, out htmlFilename, out xmlFilename);

            // Load the HTML into a string
            string html;
            using (StreamReader reader = new StreamReader(htmlFilename, Encoding.UTF8, true))
            {
                html = reader.ReadToEnd();
            }

            HtmlReaderSettings settings = new HtmlReaderSettings();
            settings.CloseInput = true;

            // Repeatedly parse the HTML, each time taking off one character and ensure that the reader deals
            // with this gracefully, without going into an infinite loop.
            while (html.Length > 0)
            {
                using (HtmlReader reader = new HtmlReader(new StringReader(html), settings))
                {
                    XPathDocument doc = new XPathDocument(reader);
                }

                html = html.Substring(0, html.Length - 1);
            }

            // Failure will throw an exception (which we don't catch)
            Console.WriteLine("UnexpectedEof({0}) succeeded.", testIndex);
        }

        public static void HtmlToXml(TextReader htmlIn, TextWriter xmlOut)
        {
            HtmlReaderSettings readerSettings = new HtmlReaderSettings();
            readerSettings.IgnoreWhitespace = true;
            using (HtmlReader reader = new HtmlReader(htmlIn, readerSettings))
            {
                XmlWriterSettings writerSettings = new XmlWriterSettings();
                writerSettings.OmitXmlDeclaration = true;
                writerSettings.Indent = true;
                writerSettings.IndentChars = "  ";
                using (XmlWriter writer = XmlWriter.Create(xmlOut, writerSettings))
                {
                    writer.WriteNode(reader, true);
                }
            }
        }

        public static void HtmlToXml(int testIndex)
        {
            string htmlFilename;
            string xmlFilename;
            GenerateFilenames(testIndex, out htmlFilename, out xmlFilename);

            using (StreamReader reader = new StreamReader(htmlFilename, Encoding.UTF8, true))
            {
                using (StreamWriter writer = new StreamWriter(xmlFilename, false, Encoding.UTF8))
                {
                    HtmlToXml(reader, writer);
                }
            }
        }

        static void GenerateFilenames(int index, out string htmlFilename, out string xmlFilename)
        {
            string sindex = index.ToString("d2");
            htmlFilename = string.Concat(c_UnitTestPrefix, sindex, c_UnitTestSuffixHtml);
            xmlFilename = string.Concat(c_UnitTestPrefix, sindex, c_UnitTestSuffixXml);
        }

    }
}
