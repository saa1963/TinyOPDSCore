using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data;
using System.IO;
using SQLitePCL;

namespace TinyOPDSCore.Data
{
    public class MyHomeLibrary : ILibrary
    {
        Object objectLock = new object();
        private sqlite3 db;
        public MyHomeLibrary()
        {
            LibraryPath = Properties.LibraryPath;
            int rc;
            if (!String.IsNullOrWhiteSpace(Properties.MyHomeLibraryPath))
            {
                raw.SetProvider(new SQLite3Provider_e_sqlite3());
                string fname = DataBaseFile();
                rc = raw.sqlite3_open(fname, out db);
                raw.sqlite3_create_collation(db, "MHL_SYSTEM", null, mhl_system_collation);
                raw.sqlite3_create_collation(db, "MHL_SYSTEM_NOCASE", null, mhl_system_nocase_collation);
                raw.sqlite3_create_function(db, "MHL_UPPER", 1, null, mhl_upper);
                raw.sqlite3_create_function(db, "MHL_LOWER", 1, null, mhl_lower);
                if (rc != raw.SQLITE_OK)
                {
                    throw new Exception($"Ошибка открытия базы данных {fname}");
                }
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
            //procedure SystemUpperString(pCtx: TSQLite3Context; nArgs: Integer; Args: TSQLite3Value); cdecl;
            //var
            //  s: string;
            //begin
            //  s := SQLite3_Value_text16(Args ^);
            //SQLite3_Result_Text16(pCtx, PWideChar(TCharacter.ToUpper(s)), -1, SQLITE_TRANSIENT);
            //end;
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
                if (fi.CreationTime > minDate)
                {
                    minDate = fi.CreationTime;
                    file = f;
                }
            }
            if (file == "")
            {
                throw new FileNotFoundException(String.Format("Файл БД в папке {0} не найден", dir));
            }
            return file;
        }

        private string GetConnectionString()
        {
            return "Data Source=" + DataBaseFile();
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
                    rc = raw.sqlite3_prepare_v2(db, "select count(BookID) from Books where IsDeleted=0", out stmt);
                    if (rc != raw.SQLITE_OK)
                    {
                        throw new Exception("Ошибка базы данных");
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
                    if (raw.sqlite3_prepare_v2(db, "select count(BookID) from Books where SearchExt = '.FB2' and IsDeleted=0", out stmt) 
                        != raw.SQLITE_OK)
                    {
                        throw new Exception("Ошибка базы данных");
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
                    rc = raw.sqlite3_prepare_v2(db, "select count(BookID) from Books where SearchExt = '.EPUB' and IsDeleted=0", out stmt);
                    if (rc != raw.SQLITE_OK)
                    {
                        throw new Exception("Ошибка базы данных");
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
                        rc = raw.sqlite3_prepare_v2(db, "select distinct Title from Books order by Title and IsDeleted=0", out stmt);
                        if (rc != raw.SQLITE_OK)
                        {
                            throw new Exception("Ошибка базы данных");
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
                        rc = raw.sqlite3_prepare_v2(db, "select distinct SearchName from Authors order by SearchName", out stmt);
                        if (rc != raw.SQLITE_OK)
                        {
                            throw new Exception("Ошибка базы данных");
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
                        rc = raw.sqlite3_prepare_v2(db, "select distinct SearchSeriesTitle from Series order by SearchSeriesTitle", out stmt);
                        if (rc != raw.SQLITE_OK)
                        {
                            throw new Exception("Ошибка базы данных");
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
                        throw new Exception("Ошибка базы данных");
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
                            "select GenreAlias, FB2Code from Genres where ParentCode = '" +
                                genreCode.utf8_to_string() + "' order by GenreCode", out stmt1) != raw.SQLITE_OK)
                        {
                            throw new Exception("Ошибка базы данных");
                        }
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
            if (_lst.ContainsKey(id))
                return _lst[id];
            else
            {
                sqlite3_stmt stmt = null;
                int rc = raw.sqlite3_prepare_v2(db,
                    "select Folder, FileName, Ext, UpdateDate, Annotation, Title, Lang, SeriesID, SeqNumber, BookSize, BookID " +
                    "from Books b where Folder = ? and FileName = ? and IsDeleted=0", out stmt);
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
                //o.Genres.Add(dr["Genre"].ToString());
                raw.sqlite3_finalize(stmt);
                return o;
            }
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
                var firstname = raw.sqlite3_column_text(stmt, 0).utf8_to_string() ?? "";
                var middlename = raw.sqlite3_column_text(stmt, 0).utf8_to_string() ?? "";
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
            int seriesid = raw.sqlite3_column_int(stmt, 7);
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
            if (seriesid != 0)
                o.Sequence = seriesid.ToString();
            else
                o.Sequence = null;
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
                if (raw.sqlite3_prepare_v2(db, $"select count(*) from Author_List al inner join Authors a on al.AuthorID = a.AuthorID where a.SearchName like '{author}%'", 
                    out stmt) != raw.SQLITE_OK)
                {
                    throw new Exception("Ошибка базы данных");
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


        Dictionary<string, Book> _lst = new Dictionary<string, Book>();
        public List<Book> GetBooksByAuthor(string author)
        {
            _lst.Clear();
            var lst = new List<Book>();
            sqlite3_stmt stmt = null;
            try
            {
                var cSql = "select b.Folder, b.FileName, b.Ext, b.UpdateDate, b.Annotation, b.Title, b.Lang, b.SeriesID, " + 
                    "b.SeqNumber, b.BookSize, b.BookID, g.GenreAlias, a.SearchName " + 
                    "from Author_List al inner join Authors a on al.AuthorID = a.AuthorID inner join Books b on al.BookID = b.BookID " +
                    "inner join Genre_List gl on b.BookID = gl.BookID inner join Genres g on gl.GenreCode = g.GenreCode " +
                    $"where a.SearchName like '{author}%' and b.IsDeleted = 0 order by b.BookID";
                if (raw.sqlite3_prepare_v2(db, cSql, out stmt) != raw.SQLITE_OK) throw new Exception("Ошибка базы данных");
                bool first = true;
                long _bookid = 0;
                Book o = null;
                while (raw.sqlite3_step(stmt) == raw.SQLITE_ROW)
                {
                    var bookid = raw.sqlite3_column_int(stmt, 10);
                    var searchname = raw.sqlite3_column_text(stmt, 12).utf8_to_string();
                    var genrealias = raw.sqlite3_column_text(stmt, 11).utf8_to_string();
                    if (_bookid != (long)bookid)
                    {
                        _bookid = (long)bookid;
                        if (first) first = false;
                        else
                        {
                            lst.Add(o);
                            if (!_lst.ContainsKey(o.ID))
                                _lst.Add(o.ID, o);
                        }
                        o = CreateBook(stmt);
                        o.Authors.Add(searchname);
                        o.Genres.Add(genrealias);
                    }
                    else
                    {
                        o.Genres.Add(genrealias);
                    }
                }
                if (o != null)
                {
                    lst.Add(o);
                    if (!_lst.ContainsKey(o.ID))
                        _lst.Add(o.ID, o);
                }
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

            return null;
        }

        public int GetBooksBySequenceCount(string sequence)
        {
            sqlite3_stmt stmt = null;
            try
            {
                if (raw.sqlite3_prepare_v2(db, 
                    $"select count(*) from Books b inner join Series s on b.SeriesID = s.SeriesID where s.SearchSeriesTitle like '{sequence}%' and IsDeleted = 0",
                    out stmt) != raw.SQLITE_OK)
                {
                    throw new Exception("Ошибка базы данных");
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

        public List<Book> GetBooksBySequence(string sequence)
        {
            //_lst.Clear();
            //var lst = new List<Book>();
            //using (var cn = new SQLiteConnection(ConnectionString))
            //{
            //    if (cn.State != ConnectionState.Open) cn.Open();
            //    var cSql = "select b.*, (select a.SearchName from Author_List al inner join Authors a on al.AuthorID = a.AuthorID where al.BookID = b.BookID collate NOCASE) Author, " +
            //        "(select g.GenreAlias from Genre_List gl inner join Genres g on gl.GenreCode = g.GenreCode where gl.BookID = b.BookID collate NOCASE) Genre " +
            //        "from Books b inner join Series s on b.SeriesID = s.SeriesID where s.SearchSeriesTitle = @p1 and b.IsDeleted = 0 collate NOCASE";
            //    var cmd = new SQLiteCommand(cSql, cn);
            //    cmd.Parameters.Add("@p1", SQLiteType.Text, 80).Value = sequence;
            //    using (var dr = cmd.ExecuteReader())
            //    {
            //        while (dr.Read())
            //        {
            //            var o = CreateBook(dr);
            //            o.Authors.Add(dr["Author"].ToString());
            //            o.Genres.Add(dr["Genre"].ToString());
            //            lst.Add(o);
            //            if (!_lst.ContainsKey(o.ID))
            //                _lst.Add(o.ID, o);
            //        }
            //    }
            //}
            //return lst;
            return null;
        }

        public List<Book> GetBooksByTitle(string title)
        {
            //_lst.Clear();
            //var lst = new List<Book>();
            //using (var cn = new SQLiteConnection(ConnectionString))
            //{
            //    if (cn.State != ConnectionState.Open) cn.Open();
            //    var cSql = "select b.*, (select a.SearchName from Author_List al inner join Authors a on al.AuthorID = a.AuthorID where al.BookID = b.BookID collate NOCASE) Author, " +
            //        "(select g.GenreAlias from Genre_List gl inner join Genres g on gl.GenreCode = g.GenreCode where gl.BookID = b.BookID collate NOCASE) Genre " +
            //        "from Books where b.SearchTitle like @p1 and b.IsDeleted = 0 collate NOCASE";
            //    var cmd = new SQLiteCommand(cSql, cn);
            //    cmd.Parameters.Add("@p1", SQLiteType.Text, 80).Value = "%" + title.ToUpper() + "%";
            //    using (var dr = cmd.ExecuteReader())
            //    {
            //        while (dr.Read())
            //        {
            //            var o = CreateBook(dr);
            //            o.Authors.Add(dr["Author"].ToString());
            //            o.Genres.Add(dr["Genre"].ToString());
            //            lst.Add(o);
            //            if (!_lst.ContainsKey(o.ID))
            //                _lst.Add(o.ID, o);
            //        }
            //    }
            //}
            //return lst;
            return null;
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
