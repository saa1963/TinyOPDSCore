using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data;
using System.IO;
using SQLitePCL;
using Microsoft.Extensions.Logging;
using NLog.Extensions.Logging;

namespace TinyOPDSCore.Data
{
    public class MyHomeLibrary : ILibrary
    {
        Object objectLock = new object();
        private sqlite3 db;
        private ILogger<MyHomeLibrary> logger;

        public MyHomeLibrary()
        {
            var loggerFactory = new NLogLoggerFactory();
            logger = loggerFactory.CreateLogger<MyHomeLibrary>();

            LibraryPath = Properties.LibraryPath;
            int rc;
            if (!String.IsNullOrWhiteSpace(Properties.MyHomeLibraryPath))
            {
                raw.SetProvider(new SQLite3Provider_e_sqlite3());
                string fname = DataBaseFile();
                logger.LogInformation($"Используется БД - {fname}");
                rc = raw.sqlite3_open(fname, out db);
                if (rc != raw.SQLITE_OK)
                {
                    throw new Exception(raw.sqlite3_errmsg(db).utf8_to_string());
                }
                raw.sqlite3_create_collation(db, "MHL_SYSTEM", null, mhl_system_collation);
                raw.sqlite3_create_collation(db, "MHL_SYSTEM_NOCASE", null, mhl_system_nocase_collation);
                raw.sqlite3_create_function(db, "MHL_UPPER", 1, null, mhl_upper);
                raw.sqlite3_create_function(db, "MHL_LOWER", 1, null, mhl_lower);
            }
        }

        private void mhl_lower(sqlite3_context ctx, object user_data, sqlite3_value[] args)
        {
            var s = raw.sqlite3_value_text(args[0]).utf8_to_string().ToLower();
            raw.sqlite3_result_text(ctx, s);
            throw new NotImplementedException();
        }

        private void mhl_upper(sqlite3_context ctx, object user_data, sqlite3_value[] args)
        {
            var s = raw.sqlite3_value_text(args[0]).utf8_to_string().ToUpper();
            raw.sqlite3_result_text(ctx, s);
        }

        private int mhl_system_nocase_collation(object user_data, string s1, string s2)
        {
            return String.Compare(s1, s2);
        }

        private int mhl_system_collation(object user_data, string s1, string s2)
        {
            return String.Compare(s1, s2);
        }

        private string DataBaseFile()
        {
            var dir = Properties.MyHomeLibraryPath;
            var files = Directory.GetFiles(dir, "*.hlc2");
            var file = "";
            var minDate = DateTime.MinValue;
            foreach (var f in files)
            {
                var fi = new FileInfo(f);
                if (fi.LastWriteTime > minDate)
                {
                    minDate = fi.LastWriteTime;
                    file = f;
                }
            }
            if (file == "")
            {
                throw new FileNotFoundException(String.Format("Файл БД в папке {0} не найден", dir));
            }
            return file;
        }

        public string LibraryPath { get; set; }
        public bool IsChanged { get; set; }

        public int Count
        {
            get
            {
                sqlite3_stmt stmt = null;
                try
                {
                    int rc;
                    rc = raw.sqlite3_prepare_v2(db, "select count(BookID) from Books where IsDeleted=0 and Lang='ru'", out stmt);
                    if (rc != raw.SQLITE_OK)
                    {
                        throw new Exception(raw.sqlite3_errmsg(db).utf8_to_string());
                    }
                    int count = 0;
                    if (raw.sqlite3_step(stmt) == raw.SQLITE_ROW)
                    {

                        count = raw.sqlite3_column_int(stmt, 0);
                    }
                    return count;
                }
                finally
                {
                    raw.sqlite3_finalize(stmt);
                }
            }
        }

        public int FB2Count
        {
            get
            {
                sqlite3_stmt stmt = null;
                try
                {
                    if (raw.sqlite3_prepare_v2(db, "select count(BookID) from Books where SearchExt = '.FB2' and IsDeleted=0 and Lang='ru'", out stmt) 
                        != raw.SQLITE_OK)
                    {
                        throw new Exception(raw.sqlite3_errmsg(db).utf8_to_string());
                    }
                    int count = 0;
                    if (raw.sqlite3_step(stmt) == raw.SQLITE_ROW)
                    {
                        count = raw.sqlite3_column_int(stmt, 0);
                    }
                    return count;
                }
                finally
                {
                    raw.sqlite3_finalize(stmt);
                }
            }
        }

        public int EPUBCount
        {
            get
            {
                sqlite3_stmt stmt = null;
                try
                {
                    int rc;
                    rc = raw.sqlite3_prepare_v2(db, "select count(BookID) from Books where SearchExt = '.EPUB' and IsDeleted=0 and Lang='ru'", out stmt);
                    if (rc != raw.SQLITE_OK)
                    {
                        throw new Exception(raw.sqlite3_errmsg(db).utf8_to_string());
                    }
                    int count = 0;
                    if (raw.sqlite3_step(stmt) == raw.SQLITE_ROW)
                    {

                        count = raw.sqlite3_column_int(stmt, 0);
                    }
                    return count;
                }
                finally
                {
                    raw.sqlite3_finalize(stmt);
                }
            }
        }

        private List<string> _titles = null;
        public List<string> Titles
        {
            get
            {
                if (_titles == null)
                {
                    _titles = new List<string>();
                    sqlite3_stmt stmt = null;
                    try
                    {
                        int rc;
                        rc = raw.sqlite3_prepare_v2(db, "select distinct Title from Books order by Title and IsDeleted=0 and Lang='ru'", out stmt);
                        if (rc != raw.SQLITE_OK)
                        {
                            throw new Exception(raw.sqlite3_errmsg(db).utf8_to_string());
                        }
                        while (raw.sqlite3_step(stmt) == raw.SQLITE_ROW)
                        {
                            var txt = raw.sqlite3_column_text(stmt, 0);
                            _authors.Add(txt.utf8_to_string());
                        }
                    }
                    finally
                    {
                        raw.sqlite3_finalize(stmt);
                    }
                }
                return _titles;
            }
        }

        private List<string> _authors = null;
        public List<string> Authors
        {
            get
            {
                if (_authors == null)
                {
                    _authors = new List<string>();
                    sqlite3_stmt stmt = null;
                    try
                    {
                        int rc;
                        rc = raw.sqlite3_prepare_v2(db, "select distinct SearchName from Authors where substr(SearchName, 1, 1) >= 'А' order by SearchName", out stmt);
                        if (rc != raw.SQLITE_OK)
                        {
                            throw new Exception(raw.sqlite3_errmsg(db).utf8_to_string());
                        }
                        while (raw.sqlite3_step(stmt) == raw.SQLITE_ROW)
                        {
                            var txt = raw.sqlite3_column_text(stmt, 0);
                            _authors.Add(txt.utf8_to_string());
                        }
                    }
                    finally
                    {
                        raw.sqlite3_finalize(stmt);
                    }
                }
                return _authors;
            }
        }

        private List<string> _sequences = null;
        public List<string> Sequences
        {
            get
            {
                if (_sequences == null)
                {
                    _sequences = new List<string>();
                    sqlite3_stmt stmt = null;
                    try
                    {
                        int rc;
                        rc = raw.sqlite3_prepare_v2(db, 
                            "select distinct SearchSeriesTitle from Series where substr(SearchSeriesTitle, 1, 1) >= 'А' order by SearchSeriesTitle", out stmt);
                        if (rc != raw.SQLITE_OK)
                        {
                            throw new Exception(raw.sqlite3_errmsg(db).utf8_to_string());
                        }
                        while (raw.sqlite3_step(stmt) == raw.SQLITE_ROW)
                        {
                            var txt = raw.sqlite3_column_text(stmt, 0);
                            _sequences.Add(txt.utf8_to_string());
                        }
                    }
                    finally
                    {
                        raw.sqlite3_finalize(stmt);
                    }
                }
                return _sequences;
            }
        }

        private List<Genre> _genres = null;
        public List<Genre> FB2Genres
        {
            get
            {
                if (_genres == null)
                {
                    _genres = new List<Genre>();
                    sqlite3_stmt stmt = null;
                    if (raw.sqlite3_prepare_v2(db,
                        "select GenreAlias, GenreCode from Genres where ParentCode = '0' order by GenreCode", out stmt) != raw.SQLITE_OK)
                    {
                        throw new Exception(raw.sqlite3_errmsg(db).utf8_to_string());
                    }
                    while (raw.sqlite3_step(stmt) == raw.SQLITE_ROW)
                    {
                        var genreAlias = raw.sqlite3_column_text(stmt, 0);
                        var genreCode = raw.sqlite3_column_text(stmt, 1);
                        var g = new Genre();
                        g.Name = genreAlias.utf8_to_string();
                        g.Translation = genreAlias.utf8_to_string();
                        sqlite3_stmt stmt1;
                        if (raw.sqlite3_prepare_v2(db,
                            "select GenreAlias, FB2Code from Genres where ParentCode = ? order by GenreCode", out stmt1) != raw.SQLITE_OK)
                        {
                            throw new Exception(raw.sqlite3_errmsg(db).utf8_to_string());
                        }
                        raw.sqlite3_bind_text(stmt1, 1, genreCode.utf8_to_string());
                        while (raw.sqlite3_step(stmt1) == raw.SQLITE_ROW)
                        {
                            var genreAlias1 = raw.sqlite3_column_text(stmt1, 0);
                            var genreCode1 = raw.sqlite3_column_text(stmt1, 1);
                            var g1 = new Genre();
                            g1.Name = genreAlias1.utf8_to_string();
                            g1.Translation = genreAlias1.utf8_to_string();
                            g1.Tag = genreCode1.utf8_to_string();
                            g.Subgenres.Add(g1);
                        }
                        raw.sqlite3_finalize(stmt1);
                        _genres.Add(g);
                    }
                    raw.sqlite3_finalize(stmt);
                }
                return _genres;
            }
        }

        private Dictionary<string, string> _soundexedGenres = null;
        public Dictionary<string, string> SoundexedGenres
        {
            get
            {
                if (_soundexedGenres == null)
                {
                    _soundexedGenres = new Dictionary<string, string>();
                    foreach (Genre genre in FB2Genres)
                        foreach (Genre subgenre in genre.Subgenres)
                        {
                            _soundexedGenres[subgenre.Name.SoundexByWord()] = subgenre.Tag;
                            string reversed = string.Join(" ", subgenre.Name.Split(' ', ',').Reverse()).Trim();
                            _soundexedGenres[reversed.SoundexByWord()] = subgenre.Tag;
                        }
                }
                return _soundexedGenres;
            }
        }

        public List<Genre> Genres
        {
            get
            {
                //return FB2Genres.SelectMany(g => g.Subgenres).OrderBy(s => s.Translation).ToList();
                return FB2Genres.SelectMany(g => g.Subgenres).OrderBy(s => s.Translation).ToList();
            }
        }

#pragma warning disable CS0067
        public event EventHandler LibraryLoaded;
#pragma warning restore CS0067

        public bool Add(Book book)
        {
            throw new NotImplementedException();
        }

        public void Append(Book book)
        {
            throw new NotImplementedException();
        }

        public bool Contains(string bookPath)
        {
            throw new NotImplementedException();
        }

        public bool Delete(string fileName)
        {
            throw new NotImplementedException();
        }

        public List<string> GetAuthorsByName(string name, bool isOpenSearch)
        {
            List<string> authors = new List<string>();
            lock (objectLock)
            {
                if (isOpenSearch) authors = Authors.Where(a => a.IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0).ToList();
                else authors = Authors.Where(a => a.StartsWith(name, StringComparison.OrdinalIgnoreCase)).ToList();
                if (isOpenSearch && authors.Count == 0)
                {
                    string reversedName = name.Reverse();
                    authors = Authors.Where(a => a.IndexOf(reversedName, StringComparison.OrdinalIgnoreCase) >= 0).ToList();
                }
                return authors;
            }
        }

        public Book GetBook(string id)
        {
            sqlite3_stmt stmt = null;
            int rc = raw.sqlite3_prepare_v2(db,
                "select Folder, FileName, Ext, UpdateDate, Annotation, Title, Lang, s.SearchSeriesTitle, SeqNumber, BookSize, BookID " +
                "from Books b left join Series s on b.SeriesID = s.SeriesID where Folder = ? and FileName = ? and IsDeleted=0 and Lang='ru'", out stmt);
            if (rc != raw.SQLITE_OK)
            {
                throw new Exception(raw.sqlite3_errmsg(db).utf8_to_string());
            }
            var mas = id.Split('@');
            var mas1 = mas[1].Split('.');
            raw.sqlite3_bind_text(stmt, 1, mas[0]);
            raw.sqlite3_bind_text(stmt, 2, mas1[0]);
            if (raw.sqlite3_step(stmt) != raw.SQLITE_ROW) throw new Exception(raw.sqlite3_errmsg(db).utf8_to_string());
            Book o = null;
            o = CreateBook(stmt);
            int bookid = raw.sqlite3_column_int(stmt, 10);
            o.Authors.AddRange(ThisBookAuthors(bookid));
            o.Genres.AddRange(ThisBookGenres(bookid));
            raw.sqlite3_finalize(stmt);
            return o;
        }

        private List<string> ThisBookGenres(int bookid)
        {
            var rt = new List<string>();
            sqlite3_stmt stmt = null;
            int rc = raw.sqlite3_prepare_v2(db,
                    "select g.GenreAlias from " +
                    "Genre_List gl inner join Genres g on gl.GenreCode = g.GenreCode where gl.BookID = ?", out stmt);
            if (rc != raw.SQLITE_OK)
            {
                throw new Exception(raw.sqlite3_errmsg(db).utf8_to_string());
            }
            raw.sqlite3_bind_int(stmt, 1, bookid);
            while (raw.sqlite3_step(stmt) == raw.SQLITE_ROW)
            {
                var genrealias = raw.sqlite3_column_text(stmt, 0).utf8_to_string() ?? "";
                rt.Add(genrealias.Replace("  ", " ").Capitalize());
            }
            raw.sqlite3_finalize(stmt);
            return rt;
        }

        private List<string> ThisBookAuthors(int bookid)
        {
            var rt = new List<string>();
            sqlite3_stmt stmt = null;
            int rc = raw.sqlite3_prepare_v2(db,
                    "select a.LastName, a.FirstName, a.MiddleName from " +
                    "Author_List al inner join Authors a on al.AuthorID = a.AuthorID where al.BookID = ?", out stmt);
            if (rc != raw.SQLITE_OK)
            {
                throw new Exception(raw.sqlite3_errmsg(db).utf8_to_string());
            }
            raw.sqlite3_bind_int(stmt, 1, bookid);
            while (raw.sqlite3_step(stmt) == raw.SQLITE_ROW)
            {
                var lastname = raw.sqlite3_column_text(stmt, 0).utf8_to_string() ?? "";
                var firstname = raw.sqlite3_column_text(stmt, 1).utf8_to_string() ?? "";
                var middlename = raw.sqlite3_column_text(stmt, 2).utf8_to_string() ?? "";
                rt.Add(string.Concat(lastname, " ", firstname, " ", middlename).Replace("  ", " ").Capitalize());
            }
            raw.sqlite3_finalize(stmt);
            return rt;
        }

        private Book CreateBook(sqlite3_stmt stmt)
        {
            string folder = raw.sqlite3_column_text(stmt, 0).utf8_to_string();
            string filename = raw.sqlite3_column_text(stmt, 1).utf8_to_string();
            string ext = raw.sqlite3_column_text(stmt, 2).utf8_to_string();
            string updatedate = raw.sqlite3_column_text(stmt, 3).utf8_to_string();
            string annotation = raw.sqlite3_column_text(stmt, 4).utf8_to_string();
            string title = raw.sqlite3_column_text(stmt, 5).utf8_to_string();
            string lang = raw.sqlite3_column_text(stmt, 6).utf8_to_string();
            string sequence = raw.sqlite3_column_text(stmt, 7).utf8_to_string();
            //int seriesid = raw.sqlite3_column_int(stmt, 7);
            int seqnumber = raw.sqlite3_column_int(stmt, 8);
            int booksize = raw.sqlite3_column_int(stmt, 9);
            
            var id = folder + "@" + filename + ext;
            var o = new Book(id);
            o.AddedDate = ConvertDate(updatedate);
            o.Annotation = annotation;
            o.BookDate = DateTime.MinValue;
            o.ID = id;
            o.Title = title;
            o.Language = lang;
            o.HasCover = false;
            o.DocumentDate = DateTime.MinValue;
            o.Sequence = sequence;
            //if (seriesid != 0)
            //    o.Sequence = seriesid.ToString();
            //else
            //    o.Sequence = null;
            if (seqnumber != 0)
                o.NumberInSequence = (uint)seqnumber;
            else
                o.NumberInSequence = 0;
            o.DocumentSize = (uint)booksize;
            return o;
        }

        public int GetBooksByAuthorCount(string author)
        {
            sqlite3_stmt stmt = null;
            try
            {
                var cSql = "select count(*) from Author_List al inner join Authors a on al.AuthorID = a.AuthorID " +
                    $"where a.SearchName like ?";
                if (raw.sqlite3_prepare_v2(db, cSql, out stmt) != raw.SQLITE_OK)
                {
                    throw new Exception(raw.sqlite3_errmsg(db).utf8_to_string());
                }
                raw.sqlite3_bind_text(stmt, 1, author.ToUpper() + "%");
                int count = 0;
                if (raw.sqlite3_step(stmt) == raw.SQLITE_ROW)
                {
                    count = raw.sqlite3_column_int(stmt, 0);
                }
                return count;
            }
            finally
            {
                raw.sqlite3_finalize(stmt);
            }
        }

        public List<Book> GetBooksByAuthor(string author)
        {
            var lst = new List<Book>();
            sqlite3_stmt stmt = null;
            try
            {
                var cSql = "select b.Folder, b.FileName, b.Ext, b.UpdateDate, b.Annotation, b.Title, b.Lang, s.SearchSeriesTitle, " + 
                    "b.SeqNumber, b.BookSize, b.BookID " + 
                    "from Author_List al inner join Authors a on al.AuthorID = a.AuthorID inner join Books b on al.BookID = b.BookID " +
                    "left join Series s on b.SeriesID = s.SeriesID " +
                    "where a.SearchName like ? and b.IsDeleted = 0 and b.Lang='ru' order by b.BookID";
                if (raw.sqlite3_prepare_v2(db, cSql, out stmt) != raw.SQLITE_OK) throw new Exception(raw.sqlite3_errmsg(db).utf8_to_string());
                raw.sqlite3_bind_text(stmt, 1, author.ToUpper() + "%");
                Book o = null;
                while (raw.sqlite3_step(stmt) == raw.SQLITE_ROW)
                {
                    var bookid = raw.sqlite3_column_int(stmt, 10);
                    o = CreateBook(stmt);
                    o.Authors.AddRange(ThisBookAuthors(bookid));
                    o.Genres.AddRange(ThisBookGenres(bookid));
                    lst.Add(o);
                }
                logger.LogInformation($"GetBooksByAuthors {lst.Count}");
                return lst;
            }
            finally
            {
                raw.sqlite3_finalize(stmt);
            }
        }

        private DateTime ConvertDate(string s)
        {
            if (!String.IsNullOrWhiteSpace(s))
                return new DateTime(Int32.Parse(s.Substring(0, 4)), Int32.Parse(s.Substring(5, 2)), Int32.Parse(s.Substring(8, 2)));
            else
                return DateTime.MinValue;
        }

        public List<Book> GetBooksByGenre(string genre)
        {
            var lst = new List<Book>();
            sqlite3_stmt stmt = null;
            try
            {
                var cSql = "select b.Folder, b.FileName, b.Ext, b.UpdateDate, b.Annotation, b.Title, b.Lang, s.SearchSeriesTitle, " +
                    "b.SeqNumber, b.BookSize, b.BookID " +
                    "from Genre_List gl inner join Books b on gl.BookID = b.BookID inner join Genres g on gl.GenreCode = g.GenreCode " +
                    "left join Series s on b.SeriesID = s.SeriesID " +
                    "where g.fb2code = ? and b.IsDeleted = 0 and b.Lang='ru'";
                if (raw.sqlite3_prepare_v2(db, cSql, out stmt) != raw.SQLITE_OK) 
                    throw new Exception(raw.sqlite3_errmsg(db).utf8_to_string());
                raw.sqlite3_bind_text(stmt, 1, genre);
                while (raw.sqlite3_step(stmt) == raw.SQLITE_ROW)
                {
                    var o = CreateBook(stmt);
                    var bookid = raw.sqlite3_column_int(stmt, 10);
                    o.Authors.AddRange(ThisBookAuthors(bookid));
                    o.Genres.AddRange(ThisBookGenres(bookid));
                    lst.Add(o);
                }
            }
            finally
            {
                raw.sqlite3_finalize(stmt);
            }
            return lst;
        }

        public int GetBooksBySequenceCount(string sequence)
        {
            sqlite3_stmt stmt = null;
            try
            {
                if (raw.sqlite3_prepare_v2(db, 
                    "select count(*) from Books b inner join Series s on b.SeriesID = s.SeriesID " +
                    "where s.SearchSeriesTitle like ? and IsDeleted = 0 and b.Lang='ru'",
                    out stmt) != raw.SQLITE_OK)
                {
                    throw new Exception(raw.sqlite3_errmsg(db).utf8_to_string());
                }
                raw.sqlite3_bind_text(stmt, 1, sequence.ToUpper() + "%");
                int count = 0;
                if (raw.sqlite3_step(stmt) == raw.SQLITE_ROW)
                {
                    count = raw.sqlite3_column_int(stmt, 0);
                }
                return count;
            }
            finally
            {
                raw.sqlite3_finalize(stmt);
            }
        }

        public List<Book> GetBooksBySequence(string sequence)
        {
            var lst = new List<Book>();
            sqlite3_stmt stmt = null;
            try
            {
                var cSql = "select b.Folder, b.FileName, b.Ext, b.UpdateDate, b.Annotation, b.Title, b.Lang, s.SearchSeriesTitle, " +
                    "b.SeqNumber, b.BookSize, b.BookID " +
                    "from Series s inner join Books b on s.SeriesID = b.SeriesID " +
                    "where s.SearchSeriesTitle = ? and b.IsDeleted = 0 and b.Lang='ru'";
                if (raw.sqlite3_prepare_v2(db, cSql, out stmt) != raw.SQLITE_OK) 
                    throw new Exception(raw.sqlite3_errmsg(db).utf8_to_string());
                raw.sqlite3_bind_text(stmt, 1, sequence.ToUpper());
                while (raw.sqlite3_step(stmt) == raw.SQLITE_ROW)
                {
                    var o = CreateBook(stmt);
                    var bookid = raw.sqlite3_column_int(stmt, 10);
                    o.Authors.AddRange(ThisBookAuthors(bookid));
                    o.Genres.AddRange(ThisBookGenres(bookid));
                    lst.Add(o);
                }
            }
            finally
            {
                raw.sqlite3_finalize(stmt);
            }
            return lst;
        }

        public List<Book> GetBooksByTitle(string title)
        {
            var lst = new List<Book>();
            sqlite3_stmt stmt = null;
            try
            {
                var cSql = "select b.Folder, b.FileName, b.Ext, b.UpdateDate, b.Annotation, b.Title, b.Lang, s.SearchSeriesTitle, " +
                    "b.SeqNumber, b.BookSize, b.BookID from Books b " +
                    "left join Series s on b.SeriesID = s.SeriesID " +
                    "where b.SearchTitle like ? and b.IsDeleted = 0 and b.Lang='ru' order by b.BookID";
                if (raw.sqlite3_prepare_v2(db, cSql, out stmt) != raw.SQLITE_OK)
                    throw new Exception(raw.sqlite3_errmsg(db).utf8_to_string());
                raw.sqlite3_bind_text(stmt, 1, "%" + title.ToUpper() + "%");
                while (raw.sqlite3_step(stmt) == raw.SQLITE_ROW)
                {
                    var o = CreateBook(stmt);
                    var bookid = raw.sqlite3_column_int(stmt, 10);
                    o.Authors.AddRange(ThisBookAuthors(bookid));
                    o.Genres.AddRange(ThisBookGenres(bookid));
                    lst.Add(o);
                }
            }
            finally
            {
                raw.sqlite3_finalize(stmt);
            }
            return lst;
        }

        public int GetBooksRecentCount()
        {
            sqlite3_stmt stmt = null;
            string maxFolder = "";
            try
            {
                var cSql = "select Max(Folder) from Books";
                if (raw.sqlite3_prepare_v2(db, cSql, out stmt) != raw.SQLITE_OK)
                    throw new Exception(raw.sqlite3_errmsg(db).utf8_to_string());
                if (raw.sqlite3_step(stmt) == raw.SQLITE_ROW)
                {
                    maxFolder = raw.sqlite3_column_text(stmt, 0).utf8_to_string();
                }
                raw.sqlite3_reset(stmt);
                cSql = "select count(*) " +
                    "from Books b " +
                    "where b.Folder = ? and b.IsDeleted = 0 and b.Lang='ru' order by b.BookID";
                if (raw.sqlite3_prepare_v2(db, cSql, out stmt) != raw.SQLITE_OK)
                {
                    throw new Exception(raw.sqlite3_errmsg(db).utf8_to_string());
                }
                raw.sqlite3_bind_text(stmt, 1, maxFolder);
                int count = 0;
                if (raw.sqlite3_step(stmt) == raw.SQLITE_ROW)
                {
                    count = raw.sqlite3_column_int(stmt, 0);
                }
                return count;
            }
            finally
            {
                raw.sqlite3_finalize(stmt);
            }
        }

        public List<Book> GetBooksRecent()
        {
            var lst = new List<Book>();
            sqlite3_stmt stmt = null;
            string maxFolder = "";
            try
            {
                var cSql = "select Max(Folder) from Books";
                if (raw.sqlite3_prepare_v2(db, cSql, out stmt) != raw.SQLITE_OK)
                    throw new Exception(raw.sqlite3_errmsg(db).utf8_to_string());
                if (raw.sqlite3_step(stmt) == raw.SQLITE_ROW)
                {
                    maxFolder = raw.sqlite3_column_text(stmt, 0).utf8_to_string();
                }
                raw.sqlite3_reset(stmt);
                cSql = "select b.Folder, b.FileName, b.Ext, b.UpdateDate, b.Annotation, b.Title, b.Lang, s.SearchSeriesTitle, " +
                "b.SeqNumber, b.BookSize, b.BookID " + 
                "from Books b " +
                "left join Series s on b.SeriesID = s.SeriesID " +
                "where b.Folder = ? and b.IsDeleted = 0 and b.Lang='ru' order by b.BookID";
                if (raw.sqlite3_prepare_v2(db, cSql, out stmt) != raw.SQLITE_OK)
                    throw new Exception(raw.sqlite3_errmsg(db).utf8_to_string());
                raw.sqlite3_bind_text(stmt, 1, maxFolder);
                while (raw.sqlite3_step(stmt) == raw.SQLITE_ROW)
                {
                    var o = CreateBook(stmt);
                    var bookid = raw.sqlite3_column_int(stmt, 10);
                    o.Authors.AddRange(ThisBookAuthors(bookid));
                    o.Genres.AddRange(ThisBookGenres(bookid));
                    lst.Add(o);
                }
            }
            finally
            {
                raw.sqlite3_finalize(stmt);
            }
            return lst;
        }

        public void Load()
        {
            throw new NotImplementedException();
        }

        public void LoadAsync()
        {
            return;
        }

        public void Save()
        {
            return;
        }
    }
}
