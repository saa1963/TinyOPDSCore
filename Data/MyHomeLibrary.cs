using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data;
using System.IO;
using SQLitePCL;
using Microsoft.Extensions.Logging;
using NLog.Extensions.Logging;
using System.Text.RegularExpressions;
using TinyOPDSCore.Misc;

namespace TinyOPDSCore.Data
{
    public class SequenceRec
    {
        public int SeriesID { get; set; }
        public string SeriesTitle { get; set; }
        public string SearchSeriesTitle { get; set; }
    }
    public class MyHomeLibrary : ILibrary
    {
        Object objectLock = new object();
        public sqlite3 db;
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
                raw.sqlite3_create_function(db, "MHL_TRIGGERS_ON", 0, null, mhl_triggers_on);
                raw.sqlite3_create_function(db, "MHL_FULLNAME", 3, null, mhl_fullauthorname);
                raw.sqlite3_create_function(db, "MHL_FULLNAME", 4, null, mhl_fullauthornameex);
            }
        }

        private void mhl_fullauthornameex(sqlite3_context ctx, object user_data, sqlite3_value[] args)
        {
            string FullName = CreateFullAuthorName(ctx, args.Length, args).ToUpper();
            raw.sqlite3_result_text(ctx, FullName);
        }

        private void mhl_fullauthorname(sqlite3_context ctx, object user_data, sqlite3_value[] args)
        {
            string FullName = CreateFullAuthorName(ctx, args.Length, args);
            raw.sqlite3_result_text(ctx, FullName);
        }

        private string CreateFullAuthorName(sqlite3_context ctx, int length, sqlite3_value[] args)
        {
            var LastName = raw.sqlite3_value_text(args[0]).utf8_to_string();
            var FirstName = raw.sqlite3_value_text(args[1]).utf8_to_string();
            var MiddleName = raw.sqlite3_value_text(args[2]).utf8_to_string();
            string rt = LastName;
            if (!String.IsNullOrEmpty(FirstName))
            {
                rt += " " + FirstName;
                if (!String.IsNullOrEmpty(MiddleName))
                {
                    rt += " " + MiddleName;
                }
            }
            return rt;
        }

        private void mhl_triggers_on(sqlite3_context ctx, object user_data, sqlite3_value[] args)
        {
            raw.sqlite3_result_int(ctx, 1);
        }

        private void mhl_lower(sqlite3_context ctx, object user_data, sqlite3_value[] args)
        {
            var s = raw.sqlite3_value_text(args[0]).utf8_to_string().ToLower();
            raw.sqlite3_result_text(ctx, s);
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
            if (!UnitTestDetector.IsInUnitTest)
            {
                var dir = Properties.MyHomeLibraryPath;
                var files = Directory.GetFiles(dir, "librusec_local_fb2*.hlc2");
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
            else
            {
                var dir = Properties.MyHomeLibraryPath;
                var files = Directory.GetFiles(dir, "test-*.hlc2");
                foreach (var f in files)
                {
                    try
                    {
                        File.Delete(Path.Combine(dir, f));
                    }
                    catch { }
                }
                var dest = Path.Combine(dir, $"test-{Guid.NewGuid().ToString()}.hlc2");
                File.Copy(Path.Combine(dir, "test.hlc2"), dest);
                return Path.Combine(dir, dest);
            }
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
        private void FillSequence()
        {
            _sequences = new List<SequenceRec>();
            sqlite3_stmt stmt = null;
            try
            {
                int rc;
                rc = raw.sqlite3_prepare_v2(db,
                    "select SeriesID, SeriesTitle, SearchSeriesTitle from Series where substr(SearchSeriesTitle, 1, 1) >= 'А' order by SearchSeriesTitle", out stmt);
                if (rc != raw.SQLITE_OK)
                {
                    throw new Exception(raw.sqlite3_errmsg(db).utf8_to_string());
                }
                while (raw.sqlite3_step(stmt) == raw.SQLITE_ROW)
                {
                    var seriesID = raw.sqlite3_column_int(stmt, 0);
                    var seriesTitle = raw.sqlite3_column_text(stmt, 1).utf8_to_string();
                    var searchSeriesTitle = raw.sqlite3_column_text(stmt, 2).utf8_to_string();
                    var sequenceRec = new SequenceRec()
                    {
                        SeriesID = seriesID,
                        SeriesTitle = seriesTitle,
                        SearchSeriesTitle = searchSeriesTitle
                    };
                    _sequences.Add(sequenceRec);
                }
            }
            finally
            {
                raw.sqlite3_finalize(stmt);
            }
        }
        private List<SequenceRec> _sequences = null;
        public List<string> Sequences
        {
            get
            {
                if (_sequences == null)
                {
                    FillSequence();
                }
                return _sequences.Select(a => a.SearchSeriesTitle).ToList();
            }
        }
        internal List<SequenceRec> SequencesRec
        {
            get
            {
                if (_sequences == null)
                {
                    FillSequence();
                }
                return _sequences;
            }
        }
        private List<Genre> _genres = null;
        private void FillGenres()
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
        public List<Genre> FB2Genres
        {
            get
            {
                if (_genres == null)
                {
                    FillGenres();
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
        internal int? ExistsAuthor(string author)
        {
            int? rt = null;
            var sql = "SELECT AuthorID FROM Authors WHERE SearchName = ?";
            sqlite3_stmt stmt = null;
            if (raw.sqlite3_prepare_v2(db, sql, out stmt) != raw.SQLITE_OK)
            {
                throw new Exception(raw.sqlite3_errmsg(db).utf8_to_string());
            }
            raw.sqlite3_bind_text(stmt, 1, author.ToUpper());
            if (raw.sqlite3_step(stmt) == raw.SQLITE_ROW)
            {
                rt = raw.sqlite3_column_int(stmt, 0);
            }
            raw.sqlite3_finalize(stmt);
            return rt;
        }
        internal int InsertAuthor(string author)
        {
            var sql = "INSERT INTO Authors (LastName, FirstName, MiddleName) VALUES (?, ?, ?)";
            sqlite3_stmt stmt = null;
            if (raw.sqlite3_prepare_v2(db, sql, out stmt) != raw.SQLITE_OK)
            {
                throw new Exception(raw.sqlite3_errmsg(db).utf8_to_string());
            }
            var aAuthor = author.Split(' ');
            raw.sqlite3_bind_text(stmt, 1, aAuthor[0] );
            if (aAuthor.Length > 1)
            {
                raw.sqlite3_bind_text(stmt, 2, aAuthor[1]);
                if (aAuthor.Length > 2)
                {
                    raw.sqlite3_bind_text(stmt, 3, aAuthor[2]);
                }
                else
                {
                    raw.sqlite3_bind_text(stmt, 3, "");
                }
            }
            else
            {
                raw.sqlite3_bind_text(stmt, 2, "");
                raw.sqlite3_bind_text(stmt, 3, "");
            }
            raw.sqlite3_bind_text(stmt, 4, author.ToUpper());
            if (raw.sqlite3_step(stmt) != raw.SQLITE_DONE)
            {
                throw new Exception(raw.sqlite3_errmsg(db).utf8_to_string());
            }
            raw.sqlite3_finalize(stmt);
            return last_insert_rowid();
        }
        private int InsertAuthorIfMissing(string author)
        {
            int? rt;
            if ((rt = ExistsAuthor(author)) == null)
            {
                rt = InsertAuthor(author);
            }
            return rt.Value;
        }
        internal string GetGenreCodeByFB2Code(string genre)
        {
            string rt = null;
            var sql = "SELECT GenreCode FROM Genres WHERE FB2Code = ?";
            sqlite3_stmt stmt = null;
            if (raw.sqlite3_prepare_v2(db, sql, out stmt) != raw.SQLITE_OK)
            {
                throw new Exception(raw.sqlite3_errmsg(db).utf8_to_string());
            }
            raw.sqlite3_bind_text(stmt, 1, genre);
            if (raw.sqlite3_step(stmt) == raw.SQLITE_ROW)
            {
                rt = raw.sqlite3_column_text(stmt, 0).utf8_to_string();
            }
            raw.sqlite3_finalize(stmt);
            return rt;
        }
        internal int AddSequence(string seq)
        {
            var sql = "INSERT INTO Series (SeriesTitle, SearchSeriesTitle) VALUES (?, ?)";
            sqlite3_stmt stmt = null;
            if (raw.sqlite3_prepare_v2(db, sql, out stmt) != raw.SQLITE_OK)
            {
                throw new Exception(raw.sqlite3_errmsg(db).utf8_to_string());
            }
            raw.sqlite3_bind_text(stmt, 1, seq);
            raw.sqlite3_bind_text(stmt, 2, seq.ToUpper());
            if (raw.sqlite3_step(stmt) != raw.SQLITE_DONE)
            {
                throw new Exception(raw.sqlite3_errmsg(db).utf8_to_string());
            }
            raw.sqlite3_finalize(stmt);
            int rt = last_insert_rowid();
            SequencesRec.Add(new SequenceRec()
            {
                SeriesID = rt,
                SeriesTitle = seq,
                SearchSeriesTitle = seq.ToUpper()
            });
            return rt;
        }
        private int last_insert_rowid()
        {
            var sql = "SELECT last_insert_rowid()";
            sqlite3_stmt stmt = null;
            if (raw.sqlite3_prepare_v2(db, sql, out stmt) != raw.SQLITE_OK)
            {
                throw new Exception(raw.sqlite3_errmsg(db).utf8_to_string());
            }
            if (raw.sqlite3_step(stmt) != raw.SQLITE_ROW)
            {
                throw new Exception(raw.sqlite3_errmsg(db).utf8_to_string());
            }
            int rt = raw.sqlite3_column_int(stmt, 0);
            raw.sqlite3_finalize(stmt);
            return rt;
        }
        public bool Add(Book book) { throw new NotImplementedException(); }
        public void Add2(Book book, int insideNo )
        {
            
            //int posPoint = book.FileName.IndexOf('.');
            var parts1 = book.FileName.Split("@");
            string folder = parts1[0];
            var parts2 = parts1[1].Split(".");
            string fname = parts2[0];
            Regex ex = new Regex(@"\d{6}");
            if (!ex.IsMatch(fname)) throw new ArgumentException("Имя файла это 6 цифр");

            string ext = "." + parts2[1];
            if (ext.ToLower() != ".fb2") throw new ArgumentException("Расширение файла не fb2");

            var sql =
            "INSERT INTO Books (" +
            "Title,     Folder,    FileName,   Ext,      InsideNo, " +  // 0  .. 04
            "SeriesID,  SeqNumber, BookSize,   LibID, " +               // 05 .. 08
            "IsDeleted, IsLocal,   UpdateDate, Lang,     LibRate, " +   // 09 .. 13
            "KeyWords,  Rate,      Progress,   Review,   Annotation" +  // 14 .. 18
            ") " +
            "VALUES (" +
            "?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ? " +
            ")";

            if (book.Authors.Count == 0)
            {
                book.Authors.Add("Неизвестный Автор");
            }
            var authorList = new List<int>();
            foreach(var author in book.Authors)
            {
                var authID = InsertAuthorIfMissing(author);
                authorList.Add(authID);
            }

            int? seq = null;
            int? seqNumber = null;
            if (book.Sequence != String.Empty)
            {
                var sequenceRec = SequencesRec.FirstOrDefault(s => s.SearchSeriesTitle == book.Sequence.ToUpper());
                if (sequenceRec != null)
                {
                    seq = sequenceRec.SeriesID;
                }
                else
                {
                    seq = AddSequence(book.Sequence);
                }
                seqNumber = Convert.ToInt32(book.NumberInSequence);
            }

            var genreList = new List<string>();
            foreach (var genre in book.Genres)
            {
                var genreCode = GetGenreCodeByFB2Code(genre);
                if (genreCode != null)
                {
                    genreList.Add(genreCode);
                }
            }

            sqlite3_stmt stmt = null;
            if (raw.sqlite3_prepare_v2(db, sql, out stmt) != raw.SQLITE_OK)
            {
                throw new Exception(raw.sqlite3_errmsg(db).utf8_to_string());
            }

            raw.sqlite3_bind_text(stmt, 1, book.Title);
            raw.sqlite3_bind_text(stmt, 2, folder);
            raw.sqlite3_bind_text(stmt, 3, fname);
            raw.sqlite3_bind_text(stmt, 4, ext);
            raw.sqlite3_bind_int(stmt, 5, insideNo);
            if (seq.HasValue)
            {
                raw.sqlite3_bind_int(stmt, 6, seq.Value);
                raw.sqlite3_bind_int(stmt, 7, seqNumber.Value);
            }
            else
            {
                raw.sqlite3_bind_null(stmt, 6);
                raw.sqlite3_bind_null(stmt, 7);
            }
            raw.sqlite3_bind_int(stmt, 8, Convert.ToInt32(book.DocumentSize));
            raw.sqlite3_bind_text(stmt, 9, fname);
            raw.sqlite3_bind_int(stmt, 10, 0);
            raw.sqlite3_bind_int(stmt, 11, 1);
            raw.sqlite3_bind_text(stmt, 12, DateTime.Today.ToString("yyyy-MM-dd"));
            raw.sqlite3_bind_text(stmt, 13, book.Language);
            raw.sqlite3_bind_int(stmt, 14, 0);
            raw.sqlite3_bind_text(stmt, 15, String.Empty);
            raw.sqlite3_bind_int(stmt, 16, 0);
            raw.sqlite3_bind_int(stmt, 17, 0);
            raw.sqlite3_bind_null(stmt, 18);
            raw.sqlite3_bind_text(stmt, 19, book.Annotation.Length <= 4096 
                ? book.Annotation : book.Annotation.Substring(0, 4096));

            if (raw.sqlite3_step(stmt) != raw.SQLITE_DONE)
            {
                throw new Exception(raw.sqlite3_errmsg(db).utf8_to_string());
            }
            raw.sqlite3_finalize(stmt);

            int bookId = last_insert_rowid();
            InsertAuthorList(bookId, authorList);
            InsertGenreList(bookId, genreList);
        }

        private void InsertGenreList(int bookId, List<string> genreList)
        {
            var sql = "INSERT INTO Genre_List (GenreCode, BookID) VALUES (?, ?)";
            sqlite3_stmt stmt = null;
            if (raw.sqlite3_prepare_v2(db, sql, out stmt) != raw.SQLITE_OK)
            {
                throw new Exception(raw.sqlite3_errmsg(db).utf8_to_string());
            }
            foreach (var genre in genreList)
            {
                raw.sqlite3_bind_text(stmt, 1, genre);
                raw.sqlite3_bind_int(stmt, 2, bookId);
                if (raw.sqlite3_step(stmt) != raw.SQLITE_DONE)
                {
                    throw new Exception(raw.sqlite3_errmsg(db).utf8_to_string());
                }
                raw.sqlite3_reset(stmt);
            }
            raw.sqlite3_finalize(stmt);
        }

        private void InsertAuthorList(int bookId, List<int> authorList)
        {
            var sql = "INSERT INTO Author_List (AuthorID, BookID) VALUES (?, ?)";
            sqlite3_stmt stmt = null;
            if (raw.sqlite3_prepare_v2(db, sql, out stmt) != raw.SQLITE_OK)
            {
                throw new Exception(raw.sqlite3_errmsg(db).utf8_to_string());
            }
            foreach (var author in authorList)
            {
                raw.sqlite3_bind_int(stmt, 1, author);
                raw.sqlite3_bind_int(stmt, 2, bookId);
                if (raw.sqlite3_step(stmt) != raw.SQLITE_DONE)
                {
                    throw new Exception(raw.sqlite3_errmsg(db).utf8_to_string());
                }
                raw.sqlite3_reset(stmt) ;
            }
            raw.sqlite3_finalize(stmt);
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
                "select Folder, FileName, Ext, UpdateDate, Annotation, Title, Lang, s.SeriesTitle, SeqNumber, BookSize, BookID " +
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
                    "select g.FB2Code from " +
                    "Genre_List gl inner join Genres g on gl.GenreCode = g.GenreCode where gl.BookID = ?", out stmt);
            if (rc != raw.SQLITE_OK)
            {
                throw new Exception(raw.sqlite3_errmsg(db).utf8_to_string());
            }
            raw.sqlite3_bind_int(stmt, 1, bookid);
            while (raw.sqlite3_step(stmt) == raw.SQLITE_ROW)
            {
                var genrealias = raw.sqlite3_column_text(stmt, 0).utf8_to_string() ?? "";
                rt.Add(genrealias.Replace("  ", " "));
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
                var cSql = "select b.Folder, b.FileName, b.Ext, b.UpdateDate, b.Annotation, b.Title, b.Lang, s.SeriesTitle, " + 
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
                var cSql = "select b.Folder, b.FileName, b.Ext, b.UpdateDate, b.Annotation, b.Title, b.Lang, s.SeriesTitle, " +
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
                var cSql = "select b.Folder, b.FileName, b.Ext, b.UpdateDate, b.Annotation, b.Title, b.Lang, s.SeriesTitle, " +
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
                var cSql = "select b.Folder, b.FileName, b.Ext, b.UpdateDate, b.Annotation, b.Title, b.Lang, s.SeriesTitle, " +
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
                cSql = "select b.Folder, b.FileName, b.Ext, b.UpdateDate, b.Annotation, b.Title, b.Lang, s.SeriesTitle, " +
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
