using System;
using System.Net.Http;
using System.Windows;

namespace stocksApp
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            using (var client = new HttpClient())
            {
                try
                {
                    // var response = await client.GetAsync("http://localhost:61363");
                    var response = await client.GetAsync("https://ps-async.fekberg.com");
                }
                catch (Exception)
                {
                    MessageBox.Show("Ensure that StockAnalyzer.Web is running, expecting to be running on http://localhost:61363. You can configure the solution to start two projects by right clicking the StockAnalyzer solution in Visual Studio, select properties and then Mutliuple Startup Projects.", "StockAnalyzer.Web IS NOT RUNNING");
                }
            }
        }
    }
}