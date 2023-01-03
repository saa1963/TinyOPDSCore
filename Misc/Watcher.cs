using Microsoft.Extensions.Logging;
using NLog.Extensions.Logging;
using SQLitePCL;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Threading;
using TinyOPDSCore.Data;
using TinyOPDSCore.Parsers;

namespace TinyOPDSCore.Misc
{
    public class Watcher: IDisposable
    {
        public ConcurrentQueue<(string, string)> ZipQueues = new ConcurrentQueue<(string, string)>();
        private FileSystemWatcher fsw;
        private ILogger<Watcher> logger;
        public Watcher(string path)
        {
            var loggerFactory = new NLogLoggerFactory();
            logger = loggerFactory.CreateLogger<Watcher>();
            fsw = new FileSystemWatcher(path);
            fsw.Filter = "fb2-??????-??????.zip";
            fsw.Created += Fsw_Created;
            fsw.EnableRaisingEvents = true;
            logger.LogInformation($"Watcher {path}");
        }

        public void ProcessZip(string zipName, string fullpath)
        {
            var lib = new MyHomeLibrary();//LibraryFactory.GetLibrary();
            var fb2Parser = new FB2Parser();
            bool ai = true;
            int cnt = 0;
            while (ai && cnt < 600)
            {
                try
                {
                    using ZipArchive zipArchive = ZipFile.OpenRead(fullpath);
                    int insideNo = 0;
                    raw.sqlite3_exec(lib.db, "BEGIN TRANSACTION;");
                    foreach (var entry in zipArchive.Entries)
                    {
                        var memStream = new MemoryStream();
                        entry.Open().CopyTo(memStream);
                        var book = fb2Parser.Parse(memStream, zipName + "@" + entry.Name);
                        lib.Add2(book, insideNo);
                        insideNo++;
                    }
                    raw.sqlite3_exec(lib.db, "END TRANSACTION;");
                    logger.LogInformation($"{zipName} добавлено {insideNo} книг.");
                    ai = false;
                }
                catch (IOException)
                {
                    Thread.Sleep(1000);
                    cnt++;
                }
            }
        }

        private void Fsw_Created(object sender, FileSystemEventArgs e)
        {
            ZipQueues.Enqueue(new(e.Name, e.FullPath));
        }

        public void Dispose()
        {
            logger.LogInformation("Dispose Watcher");
            fsw.Dispose();
        }
    }
}
