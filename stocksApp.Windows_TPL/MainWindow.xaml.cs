using stocksShared.Services;
using StocksShared.Domain;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Navigation;

namespace stocksApp.Windows_TPL
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private Stopwatch watch;
        public MainWindow()
        {
            InitializeComponent();
        }

        CancellationTokenSource cancellationTokenSource = null;

        private async void Search_Click(object sender, RoutedEventArgs e)
        {
            #region stock data timer
            watch = new Stopwatch();
            watch.Start();
            StockProgress.Visibility = Visibility.Visible;
            StockProgress.IsIndeterminate = true;

            Search.Content = "Cancel";
            #endregion

            #region Cancellation
            if (cancellationTokenSource != null)
            {
                cancellationTokenSource.Cancel();
                cancellationTokenSource = null;
                return;
            }
            cancellationTokenSource = new CancellationTokenSource();

            //Inform the UI when a cancellation was requested
            cancellationTokenSource.Token.Register(() =>
            {
                Notes.Text += "Cancellation requested" + Environment.NewLine;
            });

            #endregion

            // LoadStocksInTaskRunWrapper()
            // File - example of common non-asynchronous packages
            // Key principle - Heavy processing associated with file load and string parsing
            // should not be run on the UI thread.

            // wrap in await Task.Run()
            // queues action to run in different thread from threadpool
            // Huge performance improvement 
            // await async operation
            // await - Prevents UI blocking  

            // await LoadStocksInTaskRunWrapper();

            // Exercise - get result out of task.
            // Do not use await / async!
            // Each Task.Run and Task.ContinueWith invocation returns a task.
            // Exercise chains these tasks
            LoadStocksUsingContinuations();

    
            // Use StocksService to load stocks 
            await LoadStocksFromStocksService();

            // Threadsafe loading of stocks as soon as they arrive
            await ProcessStocksOnArrival();           

            cancellationTokenSource = null;
        }

        private void LoadStocksUsingContinuations()
        {
            // Exercise - get result out of task.
            // Do not use await / async!
            // Each Task.Run and Task.ContinueWith invocation returns a task.
            // Exercise chains these tasks
            var loadedLinesTask = SearchForStocks(cancellationTokenSource.Token);

            var processStocksTask = loadedLinesTask.ContinueWith(t =>
            {
                    // lines contained in the Result of the previous Task
                    var lines = t.Result;
                var data = new List<StockPrice>();

                foreach (var line in lines.Skip(1))
                {
                    var segments = line.Split(',');
                    for (var i = 0; i < segments.Length; i++) segments[i] = segments[i].Trim('\'', '"');
                    var price = new StockPrice
                    {
                        Ticker = segments[0],
                        TradeDate = DateTime.ParseExact(segments[1], "M/d/yyyy h:mm:ss tt", CultureInfo.InvariantCulture),
                        Volume = Convert.ToInt32(segments[6], CultureInfo.InvariantCulture),
                        Change = Convert.ToDecimal(segments[7], CultureInfo.InvariantCulture),
                        ChangePercent = Convert.ToDecimal(segments[8], CultureInfo.InvariantCulture),
                    };
                    data.Add(price);
                }
                    // WPF uses Dispatcher to communicate with the thread that owns Stocks.ItemsSource
                Dispatcher.Invoke(() =>
                {
                    Stocks.ItemsSource = data.Where(price => price.Ticker == Ticker.Text);
                });
                    // Only run task if previous task completed successfully
                    // Also, only run if it was not cancelled.
                }, cancellationTokenSource.Token,
               TaskContinuationOptions.OnlyOnRanToCompletion,
               TaskScheduler.Current
            );

            // Only run if loadedLinesTask failed
            var v = loadedLinesTask.ContinueWith(t =>
            {
                Dispatcher.Invoke(() =>
                {
                    Notes.Text = t.Exception.InnerException.Message;
                });
            }, TaskContinuationOptions.OnlyOnFaulted);

            var p = processStocksTask.ContinueWith(_ =>
            {
                Dispatcher.Invoke(() =>
                {
                    #region After stock data is loaded
                    StocksStatus.Text = $"Loaded stocks for {Ticker.Text} in {watch.ElapsedMilliseconds}ms";
                    StockProgress.Visibility = Visibility.Hidden;
                    Search.Content = "Search";
                    #endregion
                });
                
                // Only run task if previous task completed successfully
            }, TaskContinuationOptions.OnlyOnRanToCompletion);
        }
        private async Task LoadStocksFromStocksService()
        {
            try
            {
                var service = new StocksService();
                var data = await service.GetStockPricesFor(Ticker.Text, cancellationTokenSource.Token);

                Stocks.ItemsSource = data;
            }
            catch (Exception ex)
            {
                Notes.Text += ex.InnerException.Message + Environment.NewLine;
            }
        }

        private async Task LoadStocksInTaskRunWrapper()
        {
            await Task.Run(() =>
            {
                var lines = File.ReadAllLines(@"StockPrices_Small.csv");
                var data = new List<StockPrice>();

                foreach (var line in lines.Skip(1))
                {
                    var segments = line.Split(',');

                    for (var i = 0; i < segments.Length; i++) segments[i] = segments[i].Trim('\'', '"');
                    var price = new StockPrice
                    {
                        Ticker = segments[0],
                        TradeDate = DateTime.ParseExact(segments[1], "M/d/yyyy h:mm:ss tt", CultureInfo.InvariantCulture),
                        Volume = Convert.ToInt32(segments[6], CultureInfo.InvariantCulture),
                        Change = Convert.ToDecimal(segments[7], CultureInfo.InvariantCulture),
                        ChangePercent = Convert.ToDecimal(segments[8], CultureInfo.InvariantCulture),
                    };
                    data.Add(price);
                }
                // WPF uses Dispatcher to communicate with the thread that owns Stocks.ItemsSource
                Dispatcher.Invoke(() =>
                {
                    Stocks.ItemsSource = data.Where(price => price.Ticker == Ticker.Text);
                });
            });
        }

        private async Task ProcessStocksOnArrival()
        {
            try
            {
                var tickers = Ticker.Text.Split(',', ' ');

                var service = new StocksService();
                var stocks = new ConcurrentBag<StockPrice>();

                var tickerLoadingTasks = new List<Task<IEnumerable<StockPrice>>>();

                foreach (var ticker in tickers)
                {
                    var loadtask = service.GetStockPricesFor(ticker, cancellationTokenSource.Token)
                                          .ContinueWith(t =>
                    {
                        foreach (var stock in t.Result.Take(5))
                        {
                            // stocks collection can be safely used with different threads 
                            stocks.Add(stock);
                        }
                        Dispatcher.Invoke(() =>
                        {
                            Stocks.ItemsSource = stocks.ToArray();
                        });

                        return t.Result;
                    });

                    tickerLoadingTasks.Add(loadtask);
                }
                await Task.WhenAll(tickerLoadingTasks);
            }
            catch (Exception ex)
            {
                Notes.Text += ex.Message + Environment.NewLine;
            }
            finally
            {
                cancellationTokenSource = null;
            }
        }
        private Task<List<string>> SearchForStocks(CancellationToken cancellationToken)
        {
            var loadLinesTask = Task.Run(async () =>
            {
                var lines = new List<string>();

                using (var stream = new StreamReader(File.OpenRead(@"StockPrices_small.csv")))
                {
                    string line;
                    while ((line = await stream.ReadLineAsync()) != null)
                    {
                        if (cancellationToken.IsCancellationRequested)
                        {
                            // Return lines already loaded prior to cancel
                            return lines;
                        }
                        lines.Add(line);
                    }
                }

                return lines;
            }, cancellationToken);

            return loadLinesTask;
        }
        private void Hyperlink_OnRequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri));

            e.Handled = true;
        }
        private void Close_OnClick(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }
    }
}