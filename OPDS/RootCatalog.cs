﻿/***********************************************************
 * This file is a part of TinyOPDS server project
 * 
 * Copyright (c) 2013 SeNSSoFT
 *
 * This code is licensed under the Microsoft Public License, 
 * see http://tinyopds.codeplex.com/license for the details.
 *
 * This module defines the OPDS RootCatalog class
 * 
 ************************************************************/

using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq;

using TinyOPDSCore.Data;

namespace TinyOPDSCore.OPDS
{
    /// <summary>
    /// Root catalog class
    /// </summary>
    class RootCatalog
    {
        public XDocument Catalog
        {
            get
            {
                return new XDocument(
                    // Add root element with namespaces
                    new XElement("feed", new XAttribute(XNamespace.Xmlns + "dc", Namespaces.dc), 
                                         new XAttribute(XNamespace.Xmlns + "os", Namespaces.os), 
                                         new XAttribute(XNamespace.Xmlns + "opds", Namespaces.opds),

                          new XElement("id", "tag:root"),
                          new XElement("title", Properties.ServerName),
                          new XElement("subtitle", Utils.ServerVersionName),
                          new XElement("updated", DateTime.UtcNow.ToUniversalTime()),
                          new XElement("icon", "/favicon.ico"),

                          // Add links
                          Links.opensearch,
                          Links.search,
                          Links.start,
                          Links.self,

                          new XElement("entry",
                              new XElement("updated", DateTime.UtcNow.ToUniversalTime()),
                              new XElement("id", "tag:root:recentbooks"),
                              new XElement("title", Localizer.Text("Recent books"), new XAttribute("type", "text")),
                              new XElement("content", string.Format(Localizer.Text("Recent {0} books to library"), MyHomeLibrary.Instance.GetBooksRecentCount()), new XAttribute("type", "text")),
                              new XElement("link", new XAttribute("href", "/recentbooks"), new XAttribute("type", "application/atom+xml;profile=opds-catalog"))
                              ),
                          // Add catalog entries
                          new XElement("entry",
                              new XElement("updated", DateTime.UtcNow.ToUniversalTime()),
                              new XElement("id", "tag:root:authors"),
                              new XElement("title", Localizer.Text("By authors"), new XAttribute("type", "text")),
                              new XElement("content", string.Format(Localizer.Text("{0} books by {1} authors"), MyHomeLibrary.Instance.Count, MyHomeLibrary.Instance.Authors.Count), new XAttribute("type", "text")),
                              new XElement("link", new XAttribute("href", "/authorsindex"), new XAttribute("type", "application/atom+xml;profile=opds-catalog"))
                              ),
                          new XElement("entry",
                              new XElement("updated", DateTime.UtcNow.ToUniversalTime()),
                              new XElement("id", "tag:root:sequences"),
                              new XElement("title", Localizer.Text("By series"), new XAttribute("type", "text")),
                              new XElement("content", string.Format(Localizer.Text("{0} books by {1} series"), MyHomeLibrary.Instance.Count, MyHomeLibrary.Instance.Sequences.Count), new XAttribute("type", "text")),
                              new XElement("link", new XAttribute("href", "/sequencesindex"), new XAttribute("type", "application/atom+xml;profile=opds-catalog"))
                              ),
                          new XElement("entry",
                              new XElement("updated", DateTime.UtcNow.ToUniversalTime()),
                              new XElement("id", "tag:root:genre"),
                              new XElement("title", Localizer.Text("By genres"), new XAttribute("type", "text")),
                              new XElement("content", Localizer.Text("Books grouped by genres"), new XAttribute("type", "text")),
                              new XElement("link", new XAttribute("href", "/genres"), new XAttribute("type", "application/atom+xml;profile=opds-catalog"))
                          )
                      )
                  );
            }
        }
    }
}
