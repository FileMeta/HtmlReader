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
                UnitTests.MatchHtmlToXml(4);

                UnitTests.UnexpectedEof(0);
                UnitTests.UnexpectedEof(1);
                UnitTests.UnexpectedEof(2);
                UnitTests.UnexpectedEof(3);
                UnitTests.UnexpectedEof(4);
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

    }
}
