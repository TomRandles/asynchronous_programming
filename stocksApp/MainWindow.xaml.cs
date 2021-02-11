using Newtonsoft.Json;
using StocksShared.Domain;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Navigation;

namespace stocksApp
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private async void Search_Click(object sender, RoutedEventArgs e)
        {
            #region stock data timer
            var watch = new Stopwatch();
            watch.Start();
            StockProgress.Visibility = Visibility.Visible;
            StockProgress.IsIndeterminate = true;
            #endregion

            try
            {
                // await async method
                await GetAllStocksAsync();
            }
            catch (Exception ex)
            {
                Notes.Text += ex.Message;
            }

            var client = new WebClient();

            // var content = client.DownloadString($"http://localhost:61363/api/stocks/{Ticker.Text}");
            var content = client.DownloadString($"https://ps-async.fekberg.com/api/stocks/{Ticker.Text}");

            var data = JsonConvert.DeserializeObject<IEnumerable<StockPrice>>(content);

            Stocks.ItemsSource = data;

            #region After stock data is loaded
            StocksStatus.Text = $"Loaded stocks for {Ticker.Text} in {watch.ElapsedMilliseconds}ms";
            StockProgress.Visibility = Visibility.Hidden;
            #endregion
        }

        public async Task GetAllStocksAsync()
        {
            using (var client = new HttpClient())
            {
                // Invoke remote stocks api asynchronously; await return
                var response = await client.GetAsync($"https://ps-async.fekberg.com/api/stocks/{Ticker.Text}");

                try
                {
                    response.EnsureSuccessStatusCode();

                    var content = await response.Content.ReadAsStringAsync();

                    var data = JsonConvert.DeserializeObject<IEnumerable<StockPrice>>(content);

                    Stocks.ItemsSource = data;
                }
                catch (Exception ex)
                {
                    Notes.Text += ex.Message;
                }
            }
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
