# HtmlReader #
HtmlReader is a simple but full-featured HTML parser that implements the .NET XmlReader interface. This allows a programmer to use the rich XML features in .NET on HTML documents.

Here are a few applications:
* Translate arbitrary HTML into well-formatted and indented XHTML text.
* Check HTML for adherence to practices such as [WCAG](http://www.w3.org/TR/WCAG20/) compliance.
* Screen-scraping websites.
* Automated reprocessing of HTML.

HtmlReader is implemented in one, standalone C# source file so it can be easily incorporated into other projects. It can be easily converted into a .NET Class Library or a SharedProject but the standard distribution is in source code form.

This project consists of the HtmlReader source code plus a set of unit tests which may also serve as sample code. See also the sample code snippet below.

HtmlReader follows the HTML5 parsing rules but tolerates malformed HTML whenever possible. Future enhancements may include configurable tolerance and reporting of syntax errors.

Here's an example of loading HTML into a .NET XmlDocument:

    XmlDocument doc = new XmlDocument();
    HtmlReaderSettings settings = new HtmlReaderSettings();
    settings.CloseInput = true;
    using (HtmlReader reader = new HtmlReader(new StreamReader("sample.htm", Encoding.UTF8, true), settings))
    {
      doc.Load(reader);
    }

Offered under the MIT Open Source License.