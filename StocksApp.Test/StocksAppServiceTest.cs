using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using stocksShared.Services;

namespace StocksApp.Test
{
    [TestClass]
    public class MockStockServiceTests
    {
        [TestMethod]
        public async Task CanLoadMSFTStocks()
        {
            var service = new MockStocksService();
            var stocks = await service.GetStockPricesFor("MSFT",
                CancellationToken.None);

            Assert.AreEqual(stocks.Count(), 2);
            Assert.AreEqual(stocks.Sum(stock => stock.Change), 0.7m);
        }
    }
}
