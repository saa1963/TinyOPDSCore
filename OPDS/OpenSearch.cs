﻿/***********************************************************
 * This file is a part of TinyOPDS server project
 * 
 * Copyright (c) 2013 SeNSSoFT
 *
 * This code is licensed under the Microsoft Public License, 
 * see http://tinyopds.codeplex.com/license for the details.
 *
 * This module contains OPDS OpenSearch implementation
 * 
 * TODO: implement SOUNDEX search
 * 
 ************************************************************/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using System.Web;

using TinyOPDSCore.Data;

namespace TinyOPDSCore.OPDS
{
    public class OpenSearch
    {
        public XDocument OpenSearchDescription(string host)
        {
            string template = "/search?searchTerm={searchTerms}";
            string image = "/favicon.ico";
            if (Properties.UseAbsoluteUri)
            {
                template = "http://" + host.UrlCombine(Properties.RootPrefix) + template;
                image = "http://" + host.UrlCombine(Properties.RootPrefix) + image;
            }
            XDocument doc = new XDocument(
                // Add root element and namespaces
                new XElement("OpenSearchDescription",
                    new XElement("ShortName", "TinyOPDS"),
                    new XElement("LongName", "TinyOPDS"),
                    new XElement("Url", new XAttribute("type", "application/atom+xml"), new XAttribute("template", template)),
                    new XElement("Image", image, new XAttribute("width", "16"), new XAttribute("height", "16")),
                    new XElement("Tags"),
                    new XElement("Contact"),
                    new XElement("Developer"),
                    new XElement("Attribution"),
                    new XElement("SyndicationRight", "open"),
                    new XElement("AdultContent", "false"),
                    new XElement("Language", "*"),
                    new XElement("OutputEncoding", "UTF-8"),
                    new XElement("InputEncoding", "UTF-8")));

            return doc;
        }

        public XDocument Search(string searchPattern, string searchType = "", bool fb2Only = false, int pageNumber = 0, int threshold = 50)
        {
            if (!string.IsNullOrEmpty(searchPattern)) searchPattern = Uri.UnescapeDataString(searchPattern).Replace('+', ' ').ToLower();

            XDocument doc = new XDocument(
                // Add root element and namespaces
                new XElement("feed", new XAttribute(XNamespace.Xmlns + "dc", Namespaces.dc), new XAttribute(XNamespace.Xmlns + "os", Namespaces.os), new XAttribute(XNamespace.Xmlns + "opds", Namespaces.opds),
                    new XElement("id", "tag:search:"+searchPattern),
                    new XElement("title", Localizer.Text("Search results")),
                    new XElement("updated", DateTime.UtcNow.ToUniversalTime()),
                    new XElement("icon", "/series.ico"),
                    // Add links
                    Links.opensearch, Links.search, Links.start, Links.self)
                );

            List<string> authors = new List<string>();
            List<Book> titles = new List<Book>();

            if (string.IsNullOrEmpty(searchType))
            {
                string transSearchPattern = Transliteration.Back(searchPattern, TransliterationType.GOST);
                authors = LibraryFactory.GetLibrary().GetAuthorsByName(searchPattern, true);
                if (authors.Count == 0 && !string.IsNullOrEmpty(transSearchPattern))
                {
                    authors = LibraryFactory.GetLibrary().GetAuthorsByName(transSearchPattern, true);
                }
                titles = LibraryFactory.GetLibrary().GetBooksByTitle(searchPattern);
                if (titles.Count == 0 && !string.IsNullOrEmpty(transSearchPattern))
                {
                    titles = LibraryFactory.GetLibrary().GetBooksByTitle(transSearchPattern);
                }
            }
            else if (searchType.Equals("recentbooks"))
            {
                titles = LibraryFactory.GetLibrary().GetBooksRecent();
            }

            if (string.IsNullOrEmpty(searchType) && authors.Count > 0 && titles.Count > 0)
            {
                // Add two navigation entries: search by authors name and book title
                doc.Root.Add(
                    new XElement("entry",
                        new XElement("updated", DateTime.UtcNow.ToUniversalTime()),
                        new XElement("id", "tag:search:author"),
                        new XElement("title", Localizer.Text("Search authors")),
                        new XElement("content", Localizer.Text("Search authors by name"), new XAttribute("type", "text")),
                        new XElement("link", new XAttribute("href", "/search?searchType=authors&searchTerm=" + Uri.EscapeDataString(searchPattern)), new XAttribute("type", "application/atom+xml;profile=opds-catalog"))),
                    new XElement("entry",
                        new XElement("updated", DateTime.UtcNow.ToUniversalTime()),
                        new XElement("id", "tag:search:title"),
                        new XElement("title", Localizer.Text("Search books")),
                        new XElement("content", Localizer.Text("Search books by title"), new XAttribute("type", "text")),
                        new XElement("link", new XAttribute("href", "/search?searchType=books&searchTerm=" + Uri.EscapeDataString(searchPattern)), new XAttribute("type", "application/atom+xml;profile=opds-catalog")))
                    );
            }
            else if (searchType.Equals("authors") || (authors.Count > 0 && titles.Count == 0))
            {
                return new AuthorsCatalog().GetCatalog(searchPattern, true);
            }
            else if (searchType.Equals("books") || (titles.Count > 0 && authors.Count == 0))
            {
                if (pageNumber > 0) searchPattern += "/" + pageNumber;
                return new BooksCatalog().GetCatalogByTitle(searchPattern, fb2Only, 0, 1000);
            }
            else if (searchType.Equals("recentbooks"))
            {
                //if (pageNumber > 0) searchPattern += "/" + pageNumber;
                //return new BooksCatalog().GetCatalogByTitle(searchPattern, fb2Only, 0, 1000);
                return new BooksCatalog().GetCatalogRecent(fb2Only, pageNumber, 1000);
            }
            return doc;
        }
    }
}
