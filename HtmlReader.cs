/*
---
# Metadata in MicroYaml format. See http://filemeta.org and http://schema.org
# This a CodeBit. See http://filemeta.org/CodeBit.html
name: HtmlReader.cs
description: HTML Parser CodeBit that implements the XmlReader interface
url: https://raw.githubusercontent.com/FileMeta/HtmlReader/master/HtmlReader.cs
codeRepository: https://github.com/FileMeta/HtmlReader
version: 1.2
keywords: CodeBit
dateModified: 2017-12-11
copyrightHolder: Brandt Redd
copyrightYear: 2016
license: https://opensource.org/licenses/MIT
...
*/

/*
MIT License

Copyright (c) 2016 Brandt Redd

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
*/

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml;
using System.Diagnostics;

/* Pending Enhancements
* Add a collection of Parse Errors that are tolerated but still reported
* or make error sensitivity configurable in HtmlReaderSettings;
*
* Gracefully handle namespace prefixes. HTML5 does not allow namespace
* prefixes except for a limited set of deprecated attributes on
* SVG elements (see http://www.w3.org/TR/html5/syntax.html section 8.1.2.3).
* Nevertheless we could transmit them through to XML when encountered.
*/

namespace Html
{
    class HtmlReaderSettings
    {
        public HtmlReaderSettings()
        {
        }

        public bool CloseInput { get; set; }
        public bool EmitHtmlNamespace { get; set; }
        public bool IgnoreComments { get; set; }
        public bool IgnoreProcessingInstructions { get; set; }
        public bool IgnoreInsignificantWhitespace { get; set; }
        public XmlNameTable NameTable { get; set; }

        public HtmlReaderSettings Clone()
        {
            return (HtmlReaderSettings)MemberwiseClone();
        }
    }

    class HtmlReader : XmlReader
    {

        #region Constants and Statics

        const string c_HtmlUri = "http://www.w3.org/1999/xhtml";
        const string c_MathMlUri = "http://www.w3.org/1998/Math/MathML";
        const string c_SvgUri = "http://www.w3.org/2000/svg";
        const string c_XLinkUri = "http://www.w3.org/1999/xlink";
        const string c_XmlUri = "http://www.w3.org/XML/1998/namespace";
        const string c_UnknownNamespacePrefix = "uri:namespace:";

        static HashSet<string> s_VoidElements;
        static readonly string[] s_InitVoidElements =
        {
            "area", "base", "br", "col", "command", "embed", "hr", "img", "input", "keygen",
            "link", "meta", "param", "source", "track", "wbr"
        };

        // Syntax is first element name can be closed by second element name
        static HashSet<string> s_CanClose;
        static readonly string[] s_InitCanClose =
        {
            "li-li",
            "dt-dt", "dt-dd",
            "dd-dd", "dd-dt",
            "p-address", "p-article", "p-aside", "p-blockquote", "p-details", "p-div", "p-dl", "p-fieldset", "p-figcaption",
            "p-figure", "p-footer", "p-form", "p-h1", "p-h2", "p-h3", "p-h4", "p-h5", "p-h6", "p-header", "p-hr", "p-main",
            "p-menu", "p-nav", "p-ol", "p-p", "p-pre", "p-section", "p-table", "p-ul",
            "rt-rt", "rt-rp",
            "rp-rp", "rp-rt",
            "optgroup-optgroup",
            "option-option",
            "option-optgroup",
            "thead-tbody", "thead-tfoot",
            "tbody-tbody",
            "tbody-tfoot",
            "tfoot-tbody",
            "tr-tr",
            "td-td", "td-th",
            "th-th", "th-td"
        };

        static HtmlReader()
        {
            s_VoidElements = new HashSet<string>(s_InitVoidElements);
            s_CanClose = new HashSet<string>(s_InitCanClose);
        }

        #endregion

        #region Member Variables

        // Settings
        HtmlReaderSettings m_settings;
        string m_defaultNamespaceUri;

        // Text reader state
        TextReader m_reader;
        Stack<char> m_readBuf;

        // XmlReader state (for inbound calls)
        XmlNameTable m_nameTable;
        ReadState m_readState;
        Node m_nodeStackTop;
        Node m_currentNode;
        List<Node> m_currentAttributes;

        // Scanner state
        Queue<Node> m_nextNodes;
        XmlNodeType m_prevNodeType;

        #endregion

        #region Construction

        public HtmlReader(TextReader reader, HtmlReaderSettings settings)
        {
            if (reader == null) throw new ArgumentException("reader must not be null.");

            m_reader = reader;
            m_settings = settings.Clone();
            m_readBuf = new Stack<char>();

            m_nameTable = settings.NameTable ?? new NameTable();
            m_readState = ReadState.Initial;
            m_nodeStackTop = null;
            SetCurrentNode(new Node(null, XmlNodeType.None, string.Empty));
            m_currentAttributes = new List<Node>();

            m_nextNodes = new Queue<Node>();
            m_prevNodeType = XmlNodeType.None;

            m_defaultNamespaceUri = settings.EmitHtmlNamespace ? c_HtmlUri : string.Empty;
        }

        #endregion

        #region XmlReader Implementation

        public override void Close()
        {
            if (m_settings.CloseInput)
            {
                m_reader.Close();
            }
            m_reader = null;
            m_nodeStackTop = null;
            m_currentAttributes.Clear();
            SetCurrentNode(new Node(null, XmlNodeType.None, string.Empty));
            m_readState = ReadState.Closed;
        }

        public override int AttributeCount
        {
            get
            {
                if (m_currentNode.NodeType != XmlNodeType.Element) return 0;
                return m_currentAttributes.Count;
            }
        }

        public override string BaseURI
        {
            get
            {
                return string.Empty;
            }
        }

        public override int Depth
        {
            get
            {
                return m_currentNode.Depth;
            }
        }

        public override bool EOF
        {
            get
            {
                return m_readState == ReadState.EndOfFile;
            }
        }

        public override bool IsEmptyElement
        {
            get
            {
                return m_currentNode.IsEmptyElement;
            }
        }

        public override string LocalName
        {
            get
            {
                return m_currentNode.LocalName;
            }
        }

        public override string NamespaceURI
        {
            get
            {
                return m_currentNode.NamespaceUri;
            }
        }

        public override XmlNameTable NameTable
        {
            get
            {
                return m_nameTable;
            }
        }

        public override XmlNodeType NodeType
        {
            get
            {
                return m_currentNode.NodeType;
            }
        }

        public override string Prefix
        {
            get
            {
                return m_currentNode.Prefix;
            }
        }

        public override ReadState ReadState
        {
            get
            {
                return m_readState;
            }
        }

        public override string Value
        {
            get
            {
                return m_currentNode.Value;
            }
        }

        public override string GetAttribute(int i)
        {
            return m_currentAttributes[i].Value;
        }

        public override string GetAttribute(string name)
        {
            return GetAttribute(name, null);
        }

        public override string GetAttribute(string name, string namespaceURI)
        {
            int index = FindAttribute(name, namespaceURI);
            return (index >= 0) ? m_currentAttributes[index].Value : string.Empty;
        }

        public override string LookupNamespace(string prefix)
        {
            return GetNamespaceUriForPrefix(m_currentNode, prefix);
        }

        public override bool MoveToAttribute(string name)
        {
            return MoveToAttribute(name, null);
        }

        public override bool MoveToAttribute(string name, string namespaceURI)
        {
            ExitReadAttribute();
            int index = FindAttribute(name, namespaceURI);
            if (index < 0) return false;
            MoveToAttribute(index);
            return true;
        }

        public override void MoveToAttribute(int index)
        {
            if (index < 0 || index >= m_currentAttributes.Count) throw new ArgumentOutOfRangeException();

            ExitReadAttribute();

            if (m_currentNode.NodeType == XmlNodeType.Element)
            {
                PushNodeStack();
            }
            else if (m_currentNode.NodeType != XmlNodeType.Attribute)
            {
                throw new InvalidOperationException();
            }

            SetCurrentNode(m_currentAttributes[index]);
        }

        public override bool MoveToElement()
        {
            ExitReadAttribute();

            if (m_currentNode.NodeType == XmlNodeType.Attribute)
            {
                SetCurrentNode(PopNodeStack());
                return true;
            }

            return false;
        }

        public override bool MoveToFirstAttribute()
        {
            ExitReadAttribute();
            if (m_currentNode.NodeType == XmlNodeType.Element)
            {
                if (m_currentAttributes.Count == 0) return false;
                PushNodeStack();
                SetCurrentNode(m_currentAttributes[0]);
                return true;
            }
            if (m_currentNode.NodeType == XmlNodeType.Attribute)
            {
                SetCurrentNode(m_currentAttributes[0]);
                return true;
            }
            return false;
        }

        public override bool MoveToNextAttribute()
        {
            ExitReadAttribute();
            if (m_currentNode.NodeType == XmlNodeType.Attribute)
            {
                int nextIndex = m_currentNode.AttributeIndex + 1;
                if (nextIndex >= m_currentAttributes.Count) return false;

                SetCurrentNode(m_currentAttributes[nextIndex]);
                Debug.Assert(m_currentNode.AttributeIndex == nextIndex);
                return true;
            }
            return MoveToFirstAttribute();
        }

        public override bool Read()
        {
            // Set the readState
            if (m_readState == ReadState.Initial) m_readState = ReadState.Interactive;

            // Exit any attributes
            ExitReadAttribute();
            if (m_currentNode.NodeType == XmlNodeType.Attribute)
            {
                SetCurrentNode(PopNodeStack());
            }

            // Read the next node
            return ReadNode();
        }

        public override bool ReadAttributeValue()
        {
            if (m_currentNode.NodeType != XmlNodeType.Attribute) return false;
            Node textNode = new Node(m_currentNode, XmlNodeType.Text, m_currentNode.Value);
            PushNodeStack();
            SetCurrentNode(textNode);
            return true;
        }

        public override void ResolveEntity()
        {
            throw new NotImplementedException();
        }

        #endregion

        #region Internal Readers

        private bool ReadNode()
        {
            // Save the preceding node type as it affects treatment of whitespace
            if (m_currentNode != null)
            {
                m_prevNodeType = m_currentNode.NodeType;
            }

            // If the current node is a non-empty element, push it onto the stack
            if (m_currentNode != null && m_currentNode.NodeType == XmlNodeType.Element && !m_currentNode.IsEmptyElement)
            {
                PushNodeStack();
            }

            // Keep trying until we have a node.
            // This will only repeat if we're trying to tolerate a syntax error.
            int iteration = 0;
            for (;;)
            {
                if (iteration > 50) throw new ApplicationException("Invalid Html Document");

                // Clear current node (this ensures that bugs are detected)
                m_currentNode = null;

                // Handle pre-queued nodes
                if (m_nextNodes.Count > 0)
                {
                    SetCurrentNode(m_nextNodes.Dequeue());
                }

                // If EOF don't move further
                else if (m_readState >= ReadState.EndOfFile)
                {
                    return false;
                }

                else
                {
                    char ch = CharPeek();
                    if (ch == 0) // End of file
                    {
                        Debug.Assert(CharEof);
                        ScanEndOfFile();
                    }
                    else if (ch == '<') // Markup
                    {
                        ScanMarkup();
                    }
                    else
                    {
                        ScanText(); // Text
                    }
                }

                // Optionally suppress certain nodes
                if (m_currentNode != null)
                {
                    if (m_currentNode.NodeType == XmlNodeType.Comment && m_settings.IgnoreComments)
                    {
                        continue;
                    }
                    if (m_currentNode.NodeType == XmlNodeType.ProcessingInstruction && m_settings.IgnoreProcessingInstructions)
                    {
                        continue;
                    }
                    if (m_currentNode.NodeType == XmlNodeType.Whitespace && m_settings.IgnoreInsignificantWhitespace)
                    {
                        continue;
                    }
                }

                ++iteration;
                if (m_currentNode != null) break;
            }

            // If this is an EndElement clear the corresponding element from the stack
            // (other parts of the system ensure that the element matches)
            if (m_currentNode.NodeType == XmlNodeType.EndElement)
            {
#if DEBUG
                Node match = PeekNodeStack();
                Debug.Assert(string.Equals(match.Prefix, m_currentNode.Prefix, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(match.LocalName, m_currentNode.LocalName, StringComparison.OrdinalIgnoreCase));
#endif
                PopNodeStack(); // Pop the node stack without setting the current element
            }

            return (m_readState < ReadState.EndOfFile);
        }

        private bool ExitReadAttribute()
        {
            // If we're inside a ReadAttribute()
            if (m_currentNode.NodeType == XmlNodeType.Text && m_nodeStackTop != null && m_nodeStackTop.NodeType == XmlNodeType.Attribute)
            {
                SetCurrentNode(PopNodeStack());
                return true;
            }
            return false;
        }

        private int FindAttribute(string name, string namespaceURI)
        {
            // The linear search is slow. But this is optimized for loading
            // into a DOM which is unlikely to call this method.
            for (int i = 0; i < m_currentAttributes.Count; ++i)
            {
                Node attribute = m_currentAttributes[i];
                if (name.Equals(attribute.LocalName, StringComparison.Ordinal) && namespaceURI.Equals(attribute.NamespaceUri, StringComparison.Ordinal))
                {
                    return i;
                }
            }
            return -1;
        }

        #endregion

        #region scanner

        /* Things to be scanned
        * Whitespace (between or bordering markup)
        * Text (including simple entity references)
        * ProcessingInstruction <?something something?>
        * HTML Declaration <?DOCTYPE html?> (translate to XML declaration in XHTML syntax)
        * Comment <!-- comment -->
        * CDATA <![CDATA[cdata text]]>
        * Element (including attributes)
        * EndElement
        *
        * Special rules for HTML
        *  Void elements (always empty in XML terms)
        *  Raw text elements (<script>, <style>)
        *  Escapable raw text elements (<textarea>, <title>)
        *  Foreign elements (in the MathML namespace and SVG namespace)
        *  Omitted end tags (complicated rules)
        */

        void ScanMarkup()
        {
            // Consume the '<'
            ReadMatch('<');

            char ch = CharPeek();

            // Comment or CDATA
            if (ch == '!')
            {
                if (ReadMatch("!--"))
                {
                    m_currentNode = new Node(PeekNodeStack(), XmlNodeType.Comment, ScanUntil("-->"));
                }
                else if (ReadMatch("![CDATA["))
                {
                    m_currentNode = new Node(PeekNodeStack(), XmlNodeType.CDATA, ScanUntil("]]>"));
                }
                else if (ReadMatch("!DOCTYPE"))
                {
                    // Ignore the contents of the DOCTYPE.
                    SkipUntil('>');
                    // TODO: Read the contents (using ScanUntil) and validate that it's an appropriate HTML doctype.

                    // Return a standard HTML5 DOCTYPE
                    m_currentNode = new Node(null, XmlNodeType.DocumentType, string.Empty);
                    m_currentNode.SetName(string.Empty, "html");
                }
                else
                {
                    // Invalid markup, treat it like text.
                    CharUnread('<');
                    ScanText();
                }
            }

            // Processing Instructions
            else if (ch == '?')
            {
                ScanProcInst();
            }

            // End Element
            else if (ch == '/')
            {
                ScanEndElement();
            }

            // Element
            else if (char.IsLetter(ch))
            {
                ScanElement();
            }

            // Syntax error, treat it as text
            else
            {
                CharUnread('<');
                ScanText();
            }
        }

        void ScanText()
        {
            StringBuilder builder = new StringBuilder();

            // Accumulate whitespace
            for (;;)
            {
                char ch = CharRead();
                if (!IsSpaceChar(ch))
                {
                    CharUnread(ch);
                    break;
                }
                builder.Append(ch);
            }

            // Based on the next character, indicate if whitespace is significant
            if (CharPeek() != '<') WhitespaceIsSignificant = true;

            // If there is whitespace and previous node was not text or this is EOF return this as a whitespace node.
            if (builder.Length > 0 && (m_prevNodeType != XmlNodeType.Text || CharEof))
            {
                SetCurrentNode(new Node(PeekNodeStack(), 
                    WhitespaceIsSignificant ? XmlNodeType.SignificantWhitespace : XmlNodeType.Whitespace,
                    builder.ToString()));
                return;
            }
            int nTrailingWhitespace = builder.Length;

            // If there is no enclosing element, generate the implied <html> and <body> elements.
            if (PeekNodeStack() == null)
            {
                Node parent = new Node(null, XmlNodeType.Element, string.Empty, "html");
                m_nextNodes.Enqueue(parent);
                parent = new Node(parent, XmlNodeType.Element, string.Empty, "body");
                m_nextNodes.Enqueue(parent);
                Debug.Assert(m_currentNode == null);
                return;
            }

            // Must collect at least one character (if it's a '<' then it's because we're being tolerant about syntax errors)
            if (builder.Length == 0)
            {
                Debug.Assert(!CharEof); // Shouldn't happen because we just checked
                builder.Append(CharRead());
                nTrailingWhitespace = 0;
            }

            // Accumulate text counting trailing whitespace
            for (;;)
            {
                char ch = CharRead();
                if (ch == '\0') break; // EOF
                if (ch == '<' && builder.Length > 0) // some kind of markup
                {
                    CharUnread(ch);
                    break;
                }
                builder.Append(ch);
                if (IsSpaceChar(ch))
                {
                    ++nTrailingWhitespace;
                }
                else
                {
                    nTrailingWhitespace = 0;
                }
            }

            // Save accumulated whitespace
            string trailingWhitespace = null;
            if (nTrailingWhitespace > 0)
            {
                trailingWhitespace = builder.ToString(builder.Length - nTrailingWhitespace, nTrailingWhitespace);
                builder.Remove(builder.Length - nTrailingWhitespace, nTrailingWhitespace);
            }

            // Emit the text (with HtmlDecode)
            SetCurrentNode(new Node(PeekNodeStack(), XmlNodeType.Text, System.Net.WebUtility.HtmlDecode(builder.ToString())));

            WhitespaceIsSignificant = true;

            // If there's trailing whitespace then queue it up.
            if (trailingWhitespace != null)
            {
                m_nextNodes.Enqueue(new Node(PeekNodeStack(), XmlNodeType.SignificantWhitespace, trailingWhitespace));
            }
        }

        void ScanElement()
        {
            // === Scan the Element

            // Get the element name
            string prefix;
            string localName;
            if (!ScanName(out prefix, out localName)) return; // Error. The parse loop will cycle and try again.

            // Use default namespace
            string newDefaultNamespaceUri = null;

            // Auto-Set Namespace for mathml and svg elements (this is according to HTML5 specs)
            if (string.IsNullOrEmpty(prefix) && string.Equals(localName, "math"))
            {
                newDefaultNamespaceUri = c_MathMlUri;
            }
            else if (string.IsNullOrEmpty(prefix) && string.Equals(localName, "svg"))
            {
                newDefaultNamespaceUri = c_SvgUri;
            }

            // Create the element and set the name (Parent will be set later)
            Node elementNode = new Node(null, XmlNodeType.Element, string.Empty);
            elementNode.SetName(prefix, localName);
            List<Node> attributes = new List<Node>();
            elementNode.Attributes = attributes;

            // Handle all attributes with special handling for namespacing
            for (;;)
            {
                string attributePrefix;
                string attributeName;
                if (!ScanName(out attributePrefix, out attributeName)) break;

                // Look for a value (it's optional in HTML5 and quotes aren't required)
                SkipWhitespace();
                string attributeValue;
                if (ReadMatch('='))
                {
                    SkipWhitespace();
                    attributeValue = ScanAttributeValue();
                }
                else
                {
                    attributeValue = string.Empty;
                }

                // HTML5 doesn't support xmlns namespace attributes but since we're parsing into
                // XML we will support them.
                if (string.IsNullOrEmpty(attributePrefix) && attributeName.Equals("xmlns", StringComparison.Ordinal))
                {
                    newDefaultNamespaceUri = attributeValue;
                    continue; // We just set the namespace. Don't emit this as an attribute.
                }
                else if (attributePrefix.Equals("xmlns", StringComparison.Ordinal))
                {
                    AddNamespace(elementNode, attributeName, attributeValue);
                    continue; // Just map the namespace prefix. Don't emit this as an attribute.
                }

                // Add the attribute
                Node attributeNode = new Node(elementNode, XmlNodeType.Attribute, attributeValue);
                attributeNode.SetName(attributePrefix, attributeName);
                attributeNode.AttributeIndex = attributes.Count;
                attributes.Add(attributeNode);
            }

            // If there is a new default namespace URI, update both the namespace manager and the element
            if (newDefaultNamespaceUri != null)
            {
                AddNamespace(elementNode, string.Empty, newDefaultNamespaceUri);
            }

            // Set the element's namespace URI (which may be default)
            elementNode.NamespaceUri = GetNamespaceUriForPrefix(elementNode, elementNode.Prefix);

            // Traverse all attributes and add namespace URI if necessary
            foreach (Node attributeNode in attributes)
            {
                if (!string.IsNullOrEmpty(attributeNode.Prefix))
                {
                    attributeNode.NamespaceUri = GetNamespaceUriForPrefix(elementNode, attributeNode.Prefix);
                }
            }

            // Whitespace has already been skipped. We should be at the end of the element
            elementNode.IsEmptyElement = ReadMatch('/');
            ReadMatch('>'); // TODO: Report error if this isn't present.

            // If this is an HTML "void" element (empty in XML terms) then set that fact
            if (!elementNode.IsEmptyElement && elementNode.InHtmlNamespace
                && s_VoidElements.Contains(elementNode.LocalName))
            {
                elementNode.IsEmptyElement = true;
            }

            // === Adjust parent context as needed ===

            // Manage the parent node
            Node parent = m_nodeStackTop;

            // Iteratively close any elements on the stack that should be closed before this element is opened
            if (elementNode.InHtmlNamespace)
            {
                while (parent != null && parent.NodeType == XmlNodeType.Element && parent.InHtmlNamespace)
                {
                    string canCloseKey = string.Concat(parent.LocalName, "-", elementNode.LocalName);
                    if (s_CanClose.Contains(canCloseKey))
                    {
                        m_nextNodes.Enqueue(Node.NewEndElement(parent));
                        parent = parent.Parent;
                    }
                    else
                    {
                        break;
                    }
                }
            }

            // Add the <html> element if the stack is empty and this isn't the <html> node.
            if (parent == null && !elementNode.IsHtmlElement("html"))
            {
                parent = new Node(parent, XmlNodeType.Element, string.Empty, "html");
                m_nextNodes.Enqueue(parent);
            }

            // Add the <body> element if there's not a head or body already in place
            if (!ProbeNodeStack("body") && !ProbeNodeStack("head")
                && !elementNode.IsHtmlElement("body")
                && !elementNode.IsHtmlElement("head")
                && !elementNode.IsHtmlElement("html"))
            {
                parent = new Node(parent, XmlNodeType.Element, string.Empty, "body");
                m_nextNodes.Enqueue(parent);
            }

            // Other implied parent elements
            if (elementNode.IsHtmlElement("col") && !ProbeNodeStack("colgroup"))
            {
                parent = new Node(parent, XmlNodeType.Element, string.Empty, "colgroup");
                m_nextNodes.Enqueue(parent);
            }
            else if (elementNode.IsHtmlElement("tr") && !ProbeNodeStack("tbody")
                && !ProbeNodeStack("thead") && !ProbeNodeStack("tfoot"))
            {
                parent = new Node(parent, XmlNodeType.Element, string.Empty, "tbody");
                m_nextNodes.Enqueue(parent);
            }

            // Set this element's parent
            elementNode.Parent = parent;

            // === Handle the Result

            // If any nodes have been queued up, add this to the end of the list
            if (m_nextNodes.Count > 0)
            {
                m_nextNodes.Enqueue(elementNode);
                SetCurrentNode(m_nextNodes.Dequeue());
            }

            // Else, just process this node
            else
            {
                SetCurrentNode(elementNode);
            }
        }

        void ScanEndElement()
        {
            ReadMatch('/');
            string prefix;
            string localName;
            if (!ScanName(out prefix, out localName)) return;
            SkipWhitespace();
            if (!ReadMatch('>'))
            {
                // TODO: Report error, bad end tag format
            }

            // If void element, just skip this end tag (the element was already closed).
            if (string.IsNullOrEmpty(prefix) && s_VoidElements.Contains(localName)) return;

            // Make sure this corresponds to a currently open element
            if (!ProbeNodeStack(prefix, localName))
            {
                // TODO: Report error - unmatched end tag
                return;
            }

            // Close this element and any other elements that are higher in the stack
            /* TODO: Only close elements that are authorized to auto-close according to the HTML5 spec.
            *        Generate an error for other issues. (This assumes we develop an error reporting system.)
            *        Possibly add a tolerant mode that allows all elements to be closed. Or perhaps errors
            *        are reported but parsing continues. Also do the same for the End-of-file auto
            *        closing function. */
            for (Node node = m_nodeStackTop; node != null; node = node.Parent)
            {
                m_nextNodes.Enqueue(Node.NewEndElement(node));
                if (string.Equals(node.Prefix, prefix, StringComparison.Ordinal) &&
                string.Equals(node.LocalName, localName, StringComparison.Ordinal)) break;
            }

            Debug.Assert(m_nextNodes.Count > 0);
            if (m_nextNodes.Count > 0)
            {
                SetCurrentNode(m_nextNodes.Dequeue());
            }
        }

        bool ScanName(out string prefix, out string localName)
        {
            SkipWhitespace();

            // Parse the element or attribute name
            StringBuilder builder = new StringBuilder();
            for (;;)
            {
                char ch = CharRead();
                if ((builder.Length == 0) ? !IsNameStart(ch) : !IsNameChar(ch))
                {
                    CharUnread(ch);
                    break;
                }
                builder.Append(ch);
            }

            // Per HTML5 specs, only ASCII characters in names should be folded to lower case.
            ToLowerAscii(builder);

            string value = builder.ToString();
            int colon = value.IndexOf(':');
            if (colon < 0)
            {
                prefix = string.Empty;
                localName = m_nameTable.Add(value);
            }
            else
            {
                prefix = m_nameTable.Add(value.Substring(0, colon));
                localName = m_nameTable.Add(value.Substring(colon + 1));
            }
            return localName.Length > 0;
        }

        string ScanAttributeValue()
        {
            SkipWhitespace();

            StringBuilder builder = new StringBuilder();
            char quote = CharPeek();
            if (quote == '"' || quote == '\'')
            {
                ReadMatch(quote);
                for (;;)
                {
                    char ch = CharRead();
                    if (ch == quote || !IsOkAttrCharQuoted(ch)) break;
                    builder.Append(ch);
                }
            }
            else
            {
                for (;;)
                {
                    char ch = CharRead();
                    if (!IsOkAttrCharUnquoted(ch))
                    {
                        CharUnread(ch);
                        break;
                    }
                    builder.Append(ch);
                }
            }
            return System.Net.WebUtility.HtmlDecode(builder.ToString());
        }

        static bool IsOkAttrCharQuoted(char c)
        {
            return c != '<' && c != '>' && c != '\0';
        }

        static bool IsOkAttrCharUnquoted(char c)
        {
            return c > ' ' // No whitespace or control
                && c != '"'
                && c != '\''
                && c != '='
                && c != '<'
                && c != '>'
                && c != '`';
        }

        void SkipWhitespace()
        {
            char ch;
            do
            {
                ch = CharRead();
            } while (IsSpaceChar(ch));
            CharUnread(ch);
        }

        void SkipUntil(char terminator)
        {
            char ch;
            do
            {
                ch = CharRead();
            } while (ch != '\0' && ch != terminator);
        }

        void ScanProcInst()
        {
            ReadMatch('?');

            string prefix;
            string localName;
            ScanName(out prefix, out localName);

            // XML style processing instructions end in "?>" while SGML style end in ">".
            // HTML5 doesn't even define processing instructions. So, we'll take either.
            string value = ScanUntil('>');
            if (value.Length > 0 && value[value.Length - 1] == '?') value = value.Substring(0, value.Length - 1);
            value = value.Trim();

            m_currentNode = new Node(PeekNodeStack(), XmlNodeType.ProcessingInstruction, value);
            m_currentNode.SetName(prefix, localName);
        }

        void ScanEndOfFile()
        {
            // End of file. pop any unclosed elements from the stack
            if (m_nodeStackTop != null)
            {
                m_currentNode = Node.NewEndElement(m_nodeStackTop);
            }
            else
            {
                m_readState = ReadState.EndOfFile;
                SetCurrentNode(new Node(null, XmlNodeType.EndEntity, string.Empty));
            }
        }

        string ScanUntil(char terminator)
        {
            StringBuilder builder = new StringBuilder();

            for (;;)
            {
                char ch = CharRead();
                if (ch == '\0' || ch == terminator) break;
                builder.Append(ch);
            }
            return builder.ToString();
        }

        string ScanUntil(string terminator)
        {
            StringBuilder builder = new StringBuilder();
            int termLen = terminator.Length;
            char termLast = terminator[termLen - 1];

            for (;;)
            {
                char ch = CharRead();
                if (ch == '\0') break;
                builder.Append(ch);

                // See if terminator has been found.
                // This method is a bit awkward but it's fast
                if (ch == termLast)
                {
                    int len = builder.Length;
                    if (len >= termLen)
                    {
                        int i;
                        for (i = 0; i < termLen - 1; ++i)
                        {
                            if (builder[len - termLen + i] != terminator[i]) break;
                        }
                        if (i >= termLen - 1)
                        {
                            builder.Remove(len - termLen, termLen);
                            break;

                        }
                    }
                }
            }
            return builder.ToString();
        }

        bool ReadMatch(char match)
        {
            char ch = CharRead();
            if (ch != match)
            {
                CharUnread(ch);
                return false;
            }
            return true;
        }

        /// <summary>
        /// Reads a matching string from the input stream.
        /// </summary>
        /// <param name="match">The string to match.</param>
        /// <returns>True if the string was matched, false if it's not matched.</returns>
        /// <remarks>The input stream state is restored if the string is not matched.</remarks>
        bool ReadMatch(string match, bool ignoreCase = false)
        {
            if (match.Length <= 0) throw new InvalidOperationException();
            int i = 0;
            char ch; // Space
            char chMatch;
            for (;;)
            {
                ch = CharRead();
                chMatch = match[i];

                // HTML Rules only fold case on ASCII characters
                if (ignoreCase)
                {
                    if (ch >= 'A' && ch <= 'Z') ch += (char)32;
                    if (chMatch >= 'A' && chMatch <= 'Z') ch += (char)32;
                }

                if (ch != chMatch) break;
                ++i;
                if (i >= match.Length) return true;
            }

            // Unread the character that failed to match.
            CharUnread(ch);

            // Unread the rest of the match list (if any)
            while (i > 0)
            {
                --i;
                CharUnread(match[i]);
            }
            return false;
        }

        #endregion

        #region NodeStack

        void SetCurrentNode(Node node)
        {
            m_currentNode = node;
            if (node.NodeType == XmlNodeType.Element) m_currentAttributes = node.Attributes;
        }

        Node PeekNodeStack()
        {
            return m_nodeStackTop;
        }

        void PushNodeStack()
        {
            Debug.Assert(object.ReferenceEquals(m_nodeStackTop, m_currentNode.Parent));
            m_nodeStackTop = m_currentNode;
            m_currentNode = null;
        }

        Node PopNodeStack()
        {
            Debug.Assert(m_nodeStackTop != null);
            Node node = m_nodeStackTop;
            m_nodeStackTop = m_nodeStackTop.Parent;
            return node;
        }

        bool ProbeNodeStack(string localName)
        {
            for (Node node = m_nodeStackTop; node != null; node = node.Parent)
            {
                if (node.IsHtmlElement(localName))
                {
                    return true;
                }
            }
            return false;
        }

        bool ProbeNodeStack(string prefix, string localName)
        {
            for (Node node = m_nodeStackTop; node != null; node = node.Parent)
            {
                if (node.NodeType == XmlNodeType.Element
                    && string.Equals(node.Prefix, prefix, StringComparison.Ordinal)
                    && string.Equals(node.LocalName, localName, StringComparison.Ordinal))
                {
                    return true;
                }
            }
            return false;
        }

        bool WhitespaceIsSignificant
        {
            get
            {
                return (m_currentNode != null) ? m_currentNode.WhitespaceIsSignificant :
                    (m_nodeStackTop != null) ? m_nodeStackTop.WhitespaceIsSignificant :
                    false;
            }

            set
            {
                if (m_currentNode != null)
                {
                    m_currentNode.WhitespaceIsSignificant = value;
                }
                else if (m_nodeStackTop != null)
                {
                    m_nodeStackTop.WhitespaceIsSignificant = value;
                }
            }
        }

        #endregion

        #region Character Reader

        /* The character reader functions return one character at a time
           from the input. If end of file, the functions return '\0' and
           CharEof returns true. Per HTML5 specs, a '\0' in the input stream
           is converted to '\xFFFD'. Also per HTML5, all newline combinations
           of CR, LF, or CRLF ar converted to LF ('\n').

           An unlimited number of characters can be "ungotten" and will be
           returned by future CharReads. This makes parsing convenient because
           you can look ahead and then back off if something doesn't match.
        */

        bool CharEof
        {
            get
            {
                return m_readBuf.Count == 0 && m_reader.Peek() < 0;
            }
        }

        char CharPeek()
        {
            if (m_readBuf.Count <= 0)
            {
                char ch = CharRead();
                if (ch == '\0') return '\0';
                m_readBuf.Push(ch);
                return ch;
            }
            return m_readBuf.Peek();
        }

        char CharRead()
        {
            if (m_readBuf.Count > 0)
            {
                return m_readBuf.Pop();
            }

            int ch = m_reader.Read();

            // Normalize newlines according to HTML5 standards
            if (ch == '\r')
            {
                if (m_reader.Peek() == (int)'\n')
                {
                    // Suppress the CR in CRLF
                    ch = (char)m_reader.Read();
                }
                else
                {
                    // Replace CR with LF
                    ch = '\n';
                }
            }

            // Return the value
            if (ch > 0)
            {
                return (char)ch;
            }

            // Per HTML5 convert '\0'
            if (ch == 0)
            {
                return '\xFFFD';
            }

            // EOF
            return '\0';
        }

        void CharUnread(char ch)
        {
            // Nul should only show up at end-of-file
            if (ch == '\0')
            {
                Debug.Assert(CharEof);
                return;
            }
            m_readBuf.Push(ch);
        }

        #endregion

        #region ASCII

        /// <summary>
        /// Changes just ascii characters to lower case.
        /// </summary>
        /// <param name="">The string to convert</param>
        /// <returns>The converted string</returns>
        /// <remarks>Per the HTML5 spec. Only characters in the ASCII range are folded to lower case when they appear in element and attribute names.</remarks>
        static string ToLowerAscii(string value)
        {
            // First check if it's necessary
            bool hasUpperCase = false;
            foreach (char c in value)
            {
                if (c >= 'A' && c <= 'Z')
                {
                    hasUpperCase = true;
                    break;
                }
            }

            if (!hasUpperCase) return value;

            int n = value.Length;
            char[] chars = new char[n];
            for (int i = 0; i < n; ++i)
            {
                char c = value[i];
                chars[i] = (c >= 'A' && c <= 'Z') ? (char)(c + 32) : c;
            }

            return new string(chars);
        }

        /// <summary>
        /// Changes all ASCII upper case letters in a StringBuilder to lower case
        /// </summary>
        /// <param name="builder">The StringBuilder to maniupulate</param>
        /// <remarks>Per the HTML5 spec. Only characters in the ASCII range are folded to lower case when they appear in element and attribute names.</remarks>
        static void ToLowerAscii(StringBuilder builder)
        {
            for (int i = 0; i < builder.Length; ++i)
            {
                char c = builder[i];
                if (c >= 'A' && c <= 'Z')
                {
                    builder[i] = (char)(c + 32);
                }
            }
        }

        #endregion

        #region Character types

        static bool IsNameStart(char ch)
        {
            return char.IsLetter(ch) || ch == '_' || ch == ':';
        }

        static bool IsNameChar(char ch)
        {
            // TODO: Per HTML5 this should also include Unicode CombiningChars and Extenders
            return char.IsLetterOrDigit(ch) || ch == '.' || ch == '-' || ch == '_' || ch == ':';
        }

        // These are space characters as defined by HTML 5
        // HTML 5 also includes U+000C which is the form-feed character.
        // However XmlWriter does not allow that value in Whitespace elements so
        // we treat it as regular text, not whitespace.
        static bool IsSpaceChar(char ch)
        {
            return ch == ' ' || ch == '\t' || ch == '\r' || ch == '\n';
        }

        #endregion

        #region Namespace Help

        void AddNamespace(Node contextNode, string prefix, string namespaceUri)
        {
            if (contextNode.NamespaceMap == null)
            {
                contextNode.NamespaceMap = new Dictionary<string, string>();
            }
            contextNode.NamespaceMap[prefix] = namespaceUri;
        }

        string GetNamespaceUriForPrefix(Node contextNode, string prefix)
        {
            Debug.Assert(prefix != null);
            string namespaceUri;

            // First try local map
            if (contextNode.NamespaceMap != null)
            {
                if (contextNode.NamespaceMap.TryGetValue(prefix, out namespaceUri))
                {
                    return namespaceUri;
                }    
            }

            // Next, walk the stack
            for (Node node = m_nodeStackTop; node != null; node = node.Parent)
            {
                if (node.NamespaceMap != null)
                {
                    if (node.NamespaceMap.TryGetValue(prefix, out namespaceUri))
                    {
                        return namespaceUri;
                    }
                }
            }

            // Default namespace is either HTML or empty depending on setting
            if (string.IsNullOrEmpty(prefix))
            {
                return m_defaultNamespaceUri;
            }

            // XLink and XML prefixes are defined by HTML5 (see section 8.1.2.3 in http://www.w3.org/TR/html5/syntax.html)
            if (string.Equals(prefix, "xlink", StringComparison.Ordinal))
            {
                namespaceUri = c_XLinkUri;
            }
            else if (string.Equals(prefix, "xml", StringComparison.Ordinal))
            {
                namespaceUri = c_XmlUri;
            }
            else
            {
                // TODO: Report Error - undefined namespace prefix
                // Tolerant mode: Create a namespace URI for this
                namespaceUri = m_nameTable.Add(string.Concat(c_UnknownNamespacePrefix, prefix));
            }
            AddNamespace(contextNode, prefix, namespaceUri);
            return namespaceUri;
        }

        #endregion

        private class Node
        {
            private Node m_parent;
            private bool m_whitespaceIsSignificant;
            private int m_depth;

            public Node(Node parent, XmlNodeType type, string value)
            {
                Debug.Assert(type != XmlNodeType.EndElement, "Use NewEndElement");
                Parent = parent;
                NodeType = type;
                Value = value;
                NamespaceUri = string.Empty;
                Prefix = string.Empty;
                LocalName = string.Empty;
                IsEmptyElement = false;
                Attributes = null;
                NamespaceMap = null;
                m_depth = -1;   // Late determination of depth
            }

            public Node(Node parent, XmlNodeType type, string value, string localName)
                : this(parent, type, value)
            {
                SetName(string.Empty, localName);
                Attributes = new List<Node>();
            }

            public static Node NewEndElement(Node startElement)
            {
                Debug.Assert(startElement.NodeType == XmlNodeType.Element);
                Node node = new Node(startElement.Parent, XmlNodeType.None, string.Empty);
                node.NodeType = XmlNodeType.EndElement;
                node.NamespaceUri = startElement.NamespaceUri;
                node.Prefix = startElement.Prefix;
                node.LocalName = startElement.LocalName;
                return node;
            }

            public XmlNodeType NodeType;
            public string Value;
            public string NamespaceUri { get; set; }
            public string Prefix { get; private set; }
            public string LocalName { get; private set; }
            public bool IsEmptyElement;
            public int AttributeIndex;
            public List<Node> Attributes;
            public Dictionary<string, string> NamespaceMap;

            public void SetName(string prefix, string localName)
            {
                NamespaceUri = string.Empty;
                Prefix = prefix;
                LocalName = localName;
            }

            public void SetName(Node source)
            {
                NamespaceUri = source.NamespaceUri;
                Prefix = source.Prefix;
                LocalName = source.LocalName;
            }

            public Node Parent
            {
                get
                {
                    return m_parent;
                }

                set
                {
                    m_parent = value;
                    m_whitespaceIsSignificant = (value != null) ? value.WhitespaceIsSignificant : false;
                }
            }

            public int Depth
            {
                get
                {
                    // Late determination of depth because the node tree gets
                    // adjusted dynamically as the tree is paresed due to implicit
                    // parent elements and automatically closing elements.
                    if (m_depth < 0)
                    {
                        if (m_parent == null)
                        {
                            m_depth = 0;
                        }
                        else
                        {
                            m_depth = m_parent.Depth + 1;   // This goes recursive
                        }
                    }
                    return m_depth;
                }
            }

            public bool WhitespaceIsSignificant
            {
                get
                {
                    return m_whitespaceIsSignificant;
                }

                set
                {
                    // Propogate the value to this node up to the nearest parent element.
                    for (Node node = this; node != null; node = node.Parent)
                    {
                        m_whitespaceIsSignificant = value;
                        if (node.NodeType == XmlNodeType.Element) break;
                    }
                }
            }

            public bool InHtmlNamespace
            {
                get
                {
                    return string.IsNullOrEmpty(NamespaceUri) || NamespaceUri.Equals(c_HtmlUri, StringComparison.Ordinal);
                }
            }

            public bool IsHtmlElement(string localName)
            {
                return NodeType == XmlNodeType.Element && InHtmlNamespace && LocalName.Equals(localName, StringComparison.Ordinal);
            }
        }

    }
}

