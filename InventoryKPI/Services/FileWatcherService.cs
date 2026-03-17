using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Threading.Channels;
using InventoryKPI.Models;

namespace InventoryKPI.Services
{
    public class FileWatcherService : IDisposable
    {
        private readonly FileProcessor _fileProcessor;
        private readonly KpiCalculator _kpiCalculator;
        private readonly List<FileSystemWatcher> _watchers = new();
        private readonly Channel<(string path, bool isNew)> _fileQueue;
        private readonly ConcurrentDictionary<string, string> _processedFiles = new();
        private readonly Action<string, string>? _onLog;

        // Progress tracking
        private int _totalToLoad = 0;
        private int _loadedCount = 0;
        private int _okCount = 0;
        private int _warnCount = 0;
        private int _errorCount = 0;
        private bool _isReloading = false;
        private readonly object _progressLock = new();

        // Callback gọi khi reload xong
        public Action? OnReloadComplete { get; set; }

        private const string ProcessedFilesPath = "processed_files.json";

        public FileWatcherService(FileProcessor fileProcessor, KpiCalculator kpiCalculator,
            Action<string, string>? onLog = null)
        {
            _fileProcessor = fileProcessor;
            _kpiCalculator = kpiCalculator;
            _onLog = onLog;
            _fileQueue = Channel.CreateUnbounded<(string, bool)>();
            LoadProcessedFiles();
        }

        public void StartWatching(string invoicesPath, string purchaseOrdersPath)
        {
            Directory.CreateDirectory(invoicesPath);
            Directory.CreateDirectory(purchaseOrdersPath);

            _kpiCalculator.Reset();

            _watchers.Add(CreateWatcher(invoicesPath));
            _watchers.Add(CreateWatcher(purchaseOrdersPath));

            var invoiceFiles = Directory.GetFiles(invoicesPath, "*.txt");
            var poFiles = Directory.GetFiles(purchaseOrdersPath, "*.txt");
            _totalToLoad = invoiceFiles.Length + poFiles.Length;
            _loadedCount = 0;
            _okCount = 0;
            _warnCount = 0;
            _errorCount = 0;
            _isReloading = true;

            Console.WriteLine($"[INFO] Found {_totalToLoad} files to load ({invoiceFiles.Length} invoices + {poFiles.Length} purchase orders)");
            Console.WriteLine($"[INFO] Watching: {invoicesPath}");
            Console.WriteLine($"[INFO] Watching: {purchaseOrdersPath}");
            Console.WriteLine();

            Task.Run(() => ReloadFilesAsync(invoiceFiles));
            Task.Run(() => ReloadFilesAsync(poFiles));
            Task.Run(() => ProcessQueueAsync());
        }

        private FileSystemWatcher CreateWatcher(string path)
        {
            var watcher = new FileSystemWatcher(path)
            {
                Filter = "*.txt",
                EnableRaisingEvents = true,
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite
            };
            watcher.Created += OnNewFileDetected;
            watcher.Renamed += OnNewFileDetected;
            return watcher;
        }

        private void OnNewFileDetected(object sender, FileSystemEventArgs e)
        {
            Console.WriteLine($"\n[INFO] New file detected: {e.Name}");
            _fileQueue.Writer.TryWrite((e.FullPath, true));
        }

        private async Task ReloadFilesAsync(string[] files)
        {
            foreach (var file in files)
                await _fileQueue.Writer.WriteAsync((file, false));
        }

        private async Task ProcessQueueAsync()
        {
            await foreach (var (filePath, isNew) in _fileQueue.Reader.ReadAllAsync())
                await ProcessFileAsync(filePath, isNew);
        }

        private async Task ProcessFileAsync(string filePath, bool isNew)
        {
            try
            {
                if (isNew) await Task.Delay(300);

                string checksum = ComputeChecksum(filePath);
                string fileKey = Path.GetFileName(filePath) + "_" + checksum;

                if (isNew)
                {
                    if (_processedFiles.ContainsKey(fileKey))
                    {
                        Console.WriteLine($"[SKIP] Duplicate: {Path.GetFileName(filePath)}");
                        return;
                    }
                    _processedFiles[fileKey] = DateTime.Now.ToString("o");
                    SaveProcessedFiles();
                }

                var invoices = await _fileProcessor.ReadInvoiceFileAsync(filePath);

                if (invoices.Count == 0)
                {
                    if (!isNew) UpdateProgress(warn: true);
                    else Console.WriteLine($"[WARN] No valid invoices in: {Path.GetFileName(filePath)}");
                    return;
                }

                _kpiCalculator.AddInvoices(invoices);

                if (!isNew) UpdateProgress();
                else Console.WriteLine($"[OK] Loaded: {Path.GetFileName(filePath)} — {invoices.Count} invoices");
            }
            catch (Exception ex)
            {
                if (!isNew) UpdateProgress(error: true);
                else Console.WriteLine($"[ERROR] Failed: {Path.GetFileName(filePath)}: {ex.Message}");
            }
        }

        private void UpdateProgress(bool warn = false, bool error = false)
        {
            int loaded, total, ok, warnCount, errCount;

            lock (_progressLock)
            {
                _loadedCount++;
                if (error) _errorCount++;
                else if (warn) _warnCount++;
                else _okCount++;

                loaded = _loadedCount;
                total = _totalToLoad;
                ok = _okCount;
                warnCount = _warnCount;
                errCount = _errorCount;
            }

            PrintProgressBar(loaded, total);

            if (loaded >= total)
            {
                _isReloading = false;
                Console.WriteLine();
                Console.WriteLine();
                Console.WriteLine($"[DONE] Loaded {total} files — OK: {ok}  WARN: {warnCount}  ERROR: {errCount}");
                Console.WriteLine();
                OnReloadComplete?.Invoke();
            }
        }

        private static void PrintProgressBar(int current, int total)
        {
            if (total == 0) return;

            int barWidth = 40;
            double pct = (double)current / total;
            int filled = (int)(pct * barWidth);

            string bar = new string('#', filled) + new string('-', barWidth - filled);
            Console.Write($"\r  [{bar}] {pct:P0} ({current}/{total})");
        }

        private string ComputeChecksum(string filePath)
        {
            using var md5 = MD5.Create();
            using var stream = File.OpenRead(filePath);
            return Convert.ToHexString(md5.ComputeHash(stream));
        }

        private void LoadProcessedFiles()
        {
            try
            {
                if (!File.Exists(ProcessedFilesPath)) return;
                string json = File.ReadAllText(ProcessedFilesPath);
                var dict = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(json);
                if (dict != null)
                    foreach (var kv in dict)
                        _processedFiles[kv.Key] = kv.Value;
                Console.WriteLine($"[INFO] Loaded registry: {_processedFiles.Count} previously seen files");
            }
            catch
            {
                Console.WriteLine("[WARN] Could not load processed files registry, starting fresh");
            }
        }

        private void SaveProcessedFiles()
        {
            try
            {
                string json = System.Text.Json.JsonSerializer.Serialize(
                    new Dictionary<string, string>(_processedFiles),
                    new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(ProcessedFilesPath, json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[WARN] Could not save registry: {ex.Message}");
            }
        }

        public void Dispose()
        {
            foreach (var watcher in _watchers)
                watcher.Dispose();
        }
    }
}