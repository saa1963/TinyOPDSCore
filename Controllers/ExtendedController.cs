using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
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
            return StatusCode(200, xml);
        }

        [HttpGet("/authorsindex")]
        public IActionResult authorsindex(string name)
        {
            string xml = getHeader();
            xml += new AuthorsCatalog().GetCatalog(name).ToString();
            return StatusCode(200, xml);
        }

        [HttpGet("/author")]
        public IActionResult author(string name)
        {
            string xml = getHeader();
            xml += new BooksCatalog().GetCatalogByAuthor(name, acceptFB2()).ToString();
            return StatusCode(200, xml);
        }

        private string getHeader()
        {
            string xml = "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n";
            xml = xml.Insert(5, " xmlns=\"http://www.w3.org/2005/Atom\"");
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