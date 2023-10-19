﻿//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Text;
//using System.Xml;

//namespace SnDocumentGenerator
//{
//    internal class DocumentationParser
//    {
//        private string CR = Environment.NewLine;

//        public string CategoryInLink { get; set; }
//        public string Category { get; set; }
//        public List<OperationParameterInfo> Parameters { get; set; }
//        public OperationParameterInfo ReturnValue { get; set; }

//        public DocumentationParser(List<OperationParameterInfo> parameters = null, OperationParameterInfo returnValue = null)
//        {
//            Parameters = parameters;
//            ReturnValue = returnValue;
//        }

//        public string Parse(string documentation)
//        {
//            if (string.IsNullOrEmpty(documentation))
//                return documentation;

//            var lines = documentation.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
//                .Select(x => x.Trim().Replace("///", ""));
//            var src = "<doc>" + string.Join(Environment.NewLine, lines) + "</doc>";

//            var xml = new XmlDocument();
//            xml.LoadXml(src);

//            ParseCategory(xml);
//            ParseLinks(xml);
//            ParseCode(xml);
//            ParseParameterDoc(xml);
//            ParseParagraphs(xml);
//            ParseExamples(xml);
//            ParseExceptions(xml);

//            var text = xml.DocumentElement.InnerXml;

//            text = NormalizeWhitespaces(text);

//            return text;
//        }

//        private void ParseCategory(XmlDocument xml)
//        {
//            var node = xml.DocumentElement.SelectSingleNode("snCategory");
//            node?.ParentNode.RemoveChild(node);
//            var category = node?.InnerText;
//            if (string.IsNullOrEmpty(category))
//                category = "Uncategorized";
//            Category = category;

//            var categoryInLink = Category.Replace(" ", "").ToLowerInvariant();
//            if (categoryInLink.StartsWith("index"))
//                categoryInLink = categoryInLink.Remove(0, 1);
//            CategoryInLink = categoryInLink;
//        }


//        private void ParseParameterDoc(XmlDocument xml)
//        {
//            // <value>text</value> Replace with _text_
//            foreach (var valueElement in xml.DocumentElement.SelectNodes("//value").OfType<XmlElement>().ToArray())
//            {
//                var innerXml = valueElement.InnerXml;
//                if (string.IsNullOrEmpty(innerXml))
//                    continue;

//                var text = xml.CreateTextNode($"_{innerXml}_");
//                valueElement.ParentNode.ReplaceChild(text, valueElement);
//            }

//            // <paramref name=""> Replace with _name_
//            foreach (var paramrefElement in xml.DocumentElement.SelectNodes("//paramref").OfType<XmlElement>().ToArray())
//            {
//                var name = paramrefElement.Attributes["name"]?.Value;
//                if (name == null)
//                    continue;

//                var text = xml.CreateTextNode($"_{name}_");
//                paramrefElement.ParentNode.ReplaceChild(text, paramrefElement);
//            }

//            // <param name=""> Move to parameter's documentation
//            foreach (var paramElement in xml.DocumentElement.SelectNodes("param").OfType<XmlElement>().ToArray())
//            {
//                var name = paramElement.Attributes["name"]?.Value;
//                if (name == null)
//                    continue;

//                var parameter = Parameters?.FirstOrDefault(x => x.Name == name);
//                if (parameter == null)
//                    continue;

//                var example = paramElement.Attributes["example"]?.Value;
//                if (example != null)
//                    parameter.Example = example;

//                parameter.Documentation = paramElement.InnerXml;
//                xml.DocumentElement.RemoveChild(paramElement);
//            }

//            // <returns> Move to ReturnValue's documentation
//            var returnElement = xml.DocumentElement.SelectSingleNode("returns");
//            if (returnElement == null)
//                return;
//            if (ReturnValue != null)
//                ReturnValue.Documentation = returnElement.InnerXml;
//            xml.DocumentElement.RemoveChild(returnElement);
//        }

//        private void ParseCode(XmlDocument xml)
//        {
//            foreach (var element in xml.DocumentElement.SelectNodes("//c").OfType<XmlElement>().ToArray())
//            {
//                var text = xml.CreateTextNode($"`{element.InnerXml}`");
//                element.ParentNode.ReplaceChild(text, element);
//            }
//            foreach (var element in xml.DocumentElement.SelectNodes("//code").OfType<XmlElement>().ToArray())
//            {
//                var src = element.InnerXml.TrimEnd(' ', '\t');

//                var cr1 = src.StartsWith('\r') || src.StartsWith('\n') ? "" : CR;
//                var cr2 = src.EndsWith('\r') || src.EndsWith('\n') ? "" : CR;

//                var lang = element.Attributes["lang"]?.Value ?? string.Empty;

//                var text = xml.CreateTextNode($"``` {lang}{cr1}{src}{cr2}```{CR}");

//                element.ParentNode.ReplaceChild(text, element);
//            }
//        }

//        private void ParseLinks(XmlDocument xml)
//        {
//            // <seealso cref=""> Replace with _cref_
//            // <see cref=""> Replace with _cref_
//            var nodes = xml.DocumentElement.SelectNodes("//seealso").OfType<XmlElement>()
//                .Union(xml.DocumentElement.SelectNodes("//see").OfType<XmlElement>())
//                .ToArray();
//            foreach (var element in nodes)
//            {
//                var cref = element.Attributes["cref"]?.Value;
//                if (cref == null)
//                    continue;

//                var text = xml.CreateTextNode($"_{cref}_");
//                element.ParentNode.ReplaceChild(text, element);
//            }
//        }

//        private void ParseParagraphs(XmlDocument xml)
//        {
//            // <nodoc>... Remove these nodes
//            foreach (var element in xml.DocumentElement.SelectNodes("//nodoc").OfType<XmlElement>().ToArray())
//            {
//                element.ParentNode.RemoveChild(element);
//            }
//            // <para>... Replace with a newline + inner text.
//            foreach (var element in xml.DocumentElement.SelectNodes("//para").OfType<XmlElement>().ToArray())
//            {
//                var text = xml.CreateTextNode(CR + CR + element.InnerText + CR + CR);
//                element.ParentNode.ReplaceChild(text, element);
//            }
//            // <summary>... Replace with a newline + inner text.
//            foreach (var element in xml.DocumentElement.SelectNodes("summary").OfType<XmlElement>().ToArray())
//            {
//                var text = xml.CreateTextNode(CR + CR + element.InnerText + CR + CR);
//                element.ParentNode.ReplaceChild(text, element);
//            }
//            // <remarks>... Replace with a newline + inner text.
//            foreach (var element in xml.DocumentElement.SelectNodes("remarks").OfType<XmlElement>().ToArray())
//            {
//                var text = xml.CreateTextNode(CR + CR + element.InnerText + CR + CR);
//                element.ParentNode.ReplaceChild(text, element);
//            }
//        }

//        private void ParseExamples(XmlDocument xml)
//        {
//            var sb = new StringBuilder();
//            // <example>... Move to end
//            var elements = xml.DocumentElement.SelectNodes("example").OfType<XmlElement>().ToArray();
//            foreach (var element in elements)
//            {
//                if (sb.Length == 0)
//                    sb.AppendLine().Append("### Example").AppendLine(elements.Length > 1 ? "s" : "");
//                sb.AppendLine();
//                sb.AppendLine(element.InnerText);
//                element.ParentNode.RemoveChild(element);
//            }
//            var text = xml.CreateTextNode(sb.ToString());
//            xml.DocumentElement.AppendChild(text);
//        }

//        private void ParseExceptions(XmlDocument xml)
//        {
//            var sb = new StringBuilder();
//            // <exception>... Move to end
//            var elements = xml.DocumentElement.SelectNodes("exception").OfType<XmlElement>().ToArray();
//            foreach (var element in elements)
//            {
//                var cref = element.Attributes["cref"]?.Value;
//                if (cref == null)
//                    continue;

//                if (sb.Length == 0)
//                    sb.AppendLine().Append("### Exception").AppendLine(elements.Length > 1 ? "s" : "");

//                sb.AppendLine($"- {cref}: {element.InnerText}");
//                element.ParentNode.RemoveChild(element);
//            }
//            var text = xml.CreateTextNode(sb.ToString());
//            xml.DocumentElement.AppendChild(text);
//        }

//        private string NormalizeWhitespaces(string text)
//        {
//            var lines = text
//                .Trim()
//                .Replace("\r\n", "\n")
//                .Replace("\r", "\n")
//                .Split('\n')
//                /*.Select(x => x.Trim())*/;

//            var result = new List<string>();
//            var emptyLines = 0;
//            foreach (var line in lines)
//            {
//                if (line.Length == 0)
//                {
//                    if (++emptyLines > 1)
//                        continue;
//                }
//                else
//                {
//                    emptyLines = 0;
//                }
//                result.Add(line);
//            }

//            return string.Join(CR, result);
//        }

//    }
//}
