using stocksShared.Services;
using StocksShared.Domain;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Navigation;

namespace StocksApp.Windows_ParallelExtensions
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        static object syncRoot = new object();
        public MainWindow()
        {
            InitializeComponent();
        }

        CancellationTokenSource cancellationTokenSource = null;

        private async void Search_Click(object sender, RoutedEventArgs e)
        {
            #region Before loading stock data
            var watch = new Stopwatch();
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

            cancellationTokenSource.Token.Register(() =>
            {
                Notes.Text += "Cancellation requested" + Environment.NewLine;
            });
            #endregion

            try
            {
                #region Load One or Many Tickers
                var tickers = Ticker.Text.Split(',', ' ');

                var service = new StocksService();

                var tickerLoadingTasks = new List<Task<IEnumerable<StockPrice>>>();
                foreach (var ticker in tickers)
                {
                    var loadTask = service.GetStockPricesFor(ticker, cancellationTokenSource.Token);

                    tickerLoadingTasks.Add(loadTask);
                }
                #endregion

                // Stocks loaded - now need processing
                var loadedStocks = await Task.WhenAll(tickerLoadingTasks);

                // Use threadsafe collection
                var values = new ConcurrentBag<StockCalculation>();

                // Standard foreach loop converted into a Parallel foreach
                // Includes guidance on max number of concurrent tasks
                var exResult = Parallel.ForEach(loadedStocks,
                                                new ParallelOptions { MaxDegreeOfParallelism = 4 },
                    (stocks, state) =>
                {
                    var stockName = stocks.First().Ticker;

                    // Will break out of current thread
                    // Will not execute outstanding iterations
                    // On-going tasks will not be stopped
                    // return - explicitly return from current thread context
                    if (stockName == "MBI")
                    {
                        state.Break();
                        return;
                    }

                    var result = CalculateExpensiveComputation(stocks);
                    var data = new StockCalculation
                    {
                        Ticker = stocks.First().Ticker,
                        Result = result
                    };

                    values.Add(data);
                });

                if (exResult.IsCompleted)
                {
                    Stocks.ItemsSource = values;
                }
                else
                {
                    Notes.Text = "Parallel computation of stocks did not complete successfully";
                }

                // Calculate total stock price for each ticker
                decimal totalStocksValue = 0;
                exResult = Parallel.For(0, loadedStocks.Length, i =>
                {
                    var value = 0m;
                    foreach (var s in loadedStocks[i])
                    {
                        // 1. Unstable: totalStocksValue += Compute(s);
                        // 2. Fix, but limited solution:
                        // Interlocked.Add(ref totalStocksValue, (int)Compute(s));
                        // 3. Minimise code inside lock
                        value += Compute(s);
                    }
                    lock (syncRoot)
                    {
                        totalStocksValue += value;
                    }
                });

                Notes.Text = $"Stock price total: {totalStocksValue:N2}";
            }
            catch (Exception ex)
            {
                Notes.Text += ex.Message + Environment.NewLine;
            }
            finally
            {
                cancellationTokenSource = null;
            }

            #region After stock data is loaded
            StocksStatus.Text = $"Loaded stocks for {Ticker.Text} in {watch.ElapsedMilliseconds}ms";
            StockProgress.Visibility = Visibility.Hidden;
            Search.Content = "Search";
            
            #endregion
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
                            return lines;
                        }
                        lines.Add(line);
                    }
                }

                return lines;
            }, cancellationToken);

            return loadLinesTask;
        }

        #region Helpers
        Random random = new Random();
        private decimal CalculateExpensiveComputation(IEnumerable<StockPrice> stocks)
        {
            Thread.Yield();

            var computedValue = 0m;

            foreach (var stock in stocks)
            {
                for (int i = 0; i < stocks.Count() - 2; i++)
                {
                    for (int a = 0; a < random.Next(50, 60); a++)
                    {
                        computedValue += stocks.ElementAt(i).Change + stocks.ElementAt(i + 1).Change;
                    }
                }
            }

            return computedValue;
        }
        private decimal Compute(StockPrice stock)
        {
            Thread.Yield();

            decimal x = 0;
            for (var a = 0; a < 10; a++)
            {
                for (var b = 0; b < 20; b++)
                {
                    x += a + stock.Change;
                }
            }

            return x;
        }
        #endregion
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
    class StockCalculation
    {
        public string Ticker { get; set; }
        public decimal Result { get; set; }
    }
}