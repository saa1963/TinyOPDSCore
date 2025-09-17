using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NLog.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.IO.Compression;
using System.Threading;
using System.Threading.Tasks;
using TinyOPDSCore.Data;
using TinyOPDSCore.Parsers;

namespace TinyOPDSCore.Misc
{
    public class Watcher2 : IHostedService, IDisposable
    {
        //private int executionCount = 0;
        private readonly ILogger<Watcher2> _logger;
        private Timer? _timer = null;
        //private readonly IConfiguration _configuration;

        public ConcurrentQueue<(string, string)> ZipQueues = new ConcurrentQueue<(string, string)>();

        public Watcher2()
        {
            var loggerFactory = new NLogLoggerFactory();
            _logger = loggerFactory.CreateLogger<Watcher2>();
            //_configuration = conf;
        }

        public Task StartAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Timed Hosted Service running.");

            _timer = new Timer(DoWork, null, TimeSpan.Zero,
                TimeSpan.FromMinutes(5));

            return Task.CompletedTask;
        }

        private void DoWork(object? state)
        {
            _logger.LogInformation("DoWork работает");
            var lib = MyHomeLibrary.Instance;//LibraryFactory.GetLibrary();
            var fb2Parser = new FB2Parser();

            if (ZipQueues.Count > 0)
            {
                if (ZipQueues.TryDequeue(out var item))
                {
                    (var zipName, var fullpath) = item;
                    try
                    {
                        using ZipArchive zipArchive = ZipFile.OpenRead(fullpath);
                        int insideNo = 0;
                        lib.BeginTransaction();
                        foreach (var entry in zipArchive.Entries)
                        {
                            var memStream = new MemoryStream();
                            entry.Open().CopyTo(memStream);
                            var book = fb2Parser.Parse(memStream, zipName + "@" + entry.Name);
                            lib.Add2(book, insideNo);
                            insideNo++;
                        }
                        lib.EndTransaction();
                        lib.ResetCache();
                        _logger.LogInformation($"{zipName} добавлено {insideNo} книг.");
                    }
                    catch (Exception ex)
                    {
                        // содержимое файла не удалось добавить в библиотеку
                        // запихиваем пасту в тюбик
                        lib.RollbackTransaction();
                        lib.ResetCache();
                        ZipQueues.Enqueue(item);
                        _logger.LogError(ex, $"{zipName} не удалось обработать.");
                    }
                }
            }
        }

        public Task StopAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Timed Hosted Service is stopping.");

            _timer?.Change(Timeout.Infinite, 0);

            return Task.CompletedTask;
        }

        public void Dispose()
        {
            _timer?.Dispose();
        }
        public void Fsw_Created(object sender, FileSystemEventArgs e)
        {
            ZipQueues.Enqueue(new(e.Name, e.FullPath));

            _logger.LogInformation("Watcher2 - " + e.Name + "; " + e.FullPath);
        }
    }
}
