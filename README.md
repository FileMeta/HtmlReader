# HtmlReader #
HtmlReader is a simple but full-featured HTML parser that implements the .NET XmlReader interface. This allows a programmer to use the rich XML features in .NET on HTML documents.

The software is distributed as a [CodeBit](http://FileMeta.org/CodeBit.html) located [here](https://raw.githubusercontent.com/FileMeta/HtmlReader/master/HtmlReader.cs).

This project include the master copy of HtmlReader.cs plus a set of unit tests that may also be examined as sample code.

## Potential Applications for HtmlReader

* Translate arbitrary HTML into well-formatted and indented XHTML.
* Automated HTML processing such as templated content, link processing, and so forth.
* Check HTML for adherence to practices such as [WCAG](http://www.w3.org/TR/WCAG20/) compliance.
* Screen-scraping websites.
* Automated reprocessing of HTML.

HtmlReader follows the HTML5 parsing rules but tolerates malformed HTML whenever possible. In this, it's similar to the parsers built into web browsers. Future enhancements may include configurable tolerance and reporting of syntax errors.

## Sample Use

Here's an example of loading HTML into a .NET XmlDocument:

    XmlDocument doc = new XmlDocument();
    HtmlReaderSettings settings = new HtmlReaderSettings();
    settings.CloseInput = true;
    using (HtmlReader reader = new HtmlReader(new StreamReader("sample.htm", Encoding.UTF8, true), settings))
    {
      doc.Load(reader);
    }

## About CodeBits
A [CodeBit](http://FileMeta.org/CodeBit.html) is a way to share common code that's lighter weight than NuGet. Each CodeBit consists of a single source code file. A structured comment at the beginning of the file indicates where to find the master copy so that automated tools can retrieve and update CodeBits to the latest version.

## License
Offered under the MIT Open Source License.