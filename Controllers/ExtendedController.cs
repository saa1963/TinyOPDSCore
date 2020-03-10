﻿using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Net.Http.Headers;
using TinyOPDSCore.Data;
using TinyOPDSCore.OPDS;

namespace TinyOPDSCore.Controllers
{
    //[Route("")]
    [ApiController]
    public class ExtendedController : ControllerBase
    {
        [HttpGet("/")]
        public IActionResult index()
        {
            string xml = getHeader();
            xml += new RootCatalog().Catalog.ToString();
            return OPDSResult(xml);
        }

        [HttpGet("/authorsindex/{name?}")]
        public IActionResult authorsindex(string name)
        {
            string xml = getHeader();
            xml += new AuthorsCatalog().GetCatalog(name ?? "").ToString();
            return OPDSResult(xml);
        }

        [HttpGet("/author/{name?}")]
        public IActionResult author(string name)
        {
            string xml = getHeader();
            xml += new BooksCatalog().GetCatalogByAuthor(name ?? "", acceptFB2()).ToString();
            return OPDSResult(xml);
        }

        [HttpGet("/sequencesindex/{name?}")]
        public IActionResult sequencesindex(string name)
        {
            string xml = getHeader();
            xml += new SequencesCatalog().GetCatalog(name ?? "").ToString();
            return OPDSResult(xml);
        }

        [HttpGet("/sequence/{name?}")]
        public IActionResult sequence(string name)
        {
            string xml = getHeader();
            xml += new BooksCatalog().GetCatalogBySequence(name ?? "", acceptFB2()).ToString();
            return OPDSResult(xml);
        }

        [HttpGet("/genres/{name?}")]
        public IActionResult genres(string name)
        {
            string xml = getHeader();
            xml += new GenresCatalog().GetCatalog(name ?? "").ToString();
            return OPDSResult(xml);
        }

        [HttpGet("/genre/{name?}")]
        public IActionResult genre(string name)
        {
            string xml = getHeader();
            xml += new BooksCatalog().GetCatalogByGenre(name ?? "", acceptFB2()).ToString();
            return OPDSResult(xml);
        }

        [HttpGet("/search")]
        public IActionResult search(string searchTerm, string searchType, int? pageNumber)
        {
            string xml = getHeader();
            xml += new OpenSearch().Search(searchTerm ?? "", searchType ?? "", acceptFB2(), pageNumber ?? 0).ToString();
            return OPDSResult(xml);
        }

        //[HttpGet("{*opds-opensearch.xml}")]
        //public IActionResult opensearch()
        //{
        //    string xml = getOpensearchHeader();
        //    return OPDSResult(xml);
        //}

        [HttpGet("/{fname}")]
        public IActionResult Fb2zip(string fname)
        {
            if (fname.Contains("opds-opensearch.xml"))
            {
                string xml = getOpensearchHeader();
                return OPDSResult(xml);
            }
            else if (fname.Contains(".fb2.zip"))
            {
                MemoryStream memStream = null;
                memStream = new MemoryStream();
                Book book = LibraryFactory.GetLibrary().GetBook(fname);

                if (book.FilePath.ToLower().Contains(".zip@"))
                {
                    string[] pathParts = book.FilePath.Split('@');

                    using (ZipArchive zipArchive = ZipFile.OpenRead(pathParts[0]))
                    {
                        var entry = zipArchive.Entries.First(e => e.Name.Contains(pathParts[1]));
                        if (entry != null)
                            entry.Open().CopyTo(memStream);
                    }
                }
                else
                {
                    using (FileStream stream = new FileStream(book.FilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                        stream.CopyTo(memStream);
                }
                memStream.Position = 0;

                using (var outputStream = new MemoryStream())
                {
                    using (ZipArchive zipArchive = new ZipArchive(outputStream, ZipArchiveMode.Create))
                    {
                        var entry = zipArchive.CreateEntry(Transliteration.Front($"{book.Authors.First()}_{book.Title}.fb2"));
                        using (var z = entry.Open())
                        {
                            memStream.WriteTo(z);
                        }
                    }
                    return File(outputStream, "application/fb2+zip");
                }
            }
            else if (fname.Contains(".jpeg"))
            {
                bool getCover = true;
                string bookID = string.Empty;
                var request = Request.Path.Value;
                if (request.Contains("/cover/"))
                {
                    bookID = Path.GetFileNameWithoutExtension(request.Substring(request.IndexOf("/cover/") + 7));
                }
                else if (request.Contains("/thumbnail/"))
                {
                    bookID = Path.GetFileNameWithoutExtension(request.Substring(request.IndexOf("/thumbnail/") + 11));
                    getCover = false;
                }

                if (!string.IsNullOrEmpty(bookID))
                {
                    CoverImage image = null;
                    Book book = LibraryFactory.GetLibrary().GetBook(bookID);

                    if (book != null)
                    {
                        if (ImagesCache.HasImage(bookID)) image = ImagesCache.GetImage(bookID);
                        else
                        {
                            image = new CoverImage(book);
                            if (image != null && image.HasImages) ImagesCache.Add(image);
                        }

                        if (image != null && image.HasImages)
                        {
                            return File(getCover ? image.CoverImageStream : image.ThumbnailImageStream, "image/jpeg");
                        }
                    }
                    return NoContent();
                }
                return NoContent();
            }
            else if (fname.Contains(".ico"))
            {
                var request = Request.Path.Value;
                string icon = Path.GetFileName(request);
                //Stream stream = System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("TinyOPDSCore.Icons." + icon);
                Stream stream = System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("TinyOPDS");
                if (stream != null && stream.Length > 0)
                {
                    return File(stream, "image/x-icon");
                }
                return NoContent();
            }
            else
                return NoContent();
        }

        private IActionResult OPDSResult(string xml)
        {
            return Content(xml, MediaTypeHeaderValue.Parse("application/atom+xml;charset=utf-8"));
        }

        private string getHeader()
        {
            string xml = "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n";
            xml = xml.Insert(5, " xmlns=\"http://www.w3.org/2005/Atom\"");
            return absoluteUri(xml);
        }

        private string getOpensearchHeader()
        {
            string xml = new OpenSearch().OpenSearchDescription().ToString();
            xml = "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n" + xml.Insert(22, " xmlns=\"http://a9.com/-/spec/opensearch/1.1/\"");
            return absoluteUri(xml);
        }

        private string absoluteUri(string xml)
        {
            if (Properties.UseAbsoluteUri)
            {
                try
                {
                    string host = HttpContext.Request.Headers["Host"];
                    xml = xml.Replace("href=\"", "href=\"http://" + host.UrlCombine(Properties.RootPrefix));
                }
                catch { }
            }
            return xml;
        }

        private bool acceptFB2()
        {
            string userAgent = HttpContext.Request.Headers["User-Agent"];
            return Utils.DetectFB2Reader(userAgent);
        }
    }
}