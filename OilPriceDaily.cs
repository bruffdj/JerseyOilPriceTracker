using System;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;
using System.Xml;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using BrightFuture.OilPrice;
using Newtonsoft.Json;
using System.Linq;
using System.Threading.Tasks;
using hap = HtmlAgilityPack;
using System.Net;

namespace BrightFuture.OilPrice
{
    public static class OilPriceFeed
    {
        [FunctionName("OilPriceFeed")]
        public static void Run(
            [TimerTrigger("%cronScheduleExpressionSetting%")]TimerInfo myTimer,
            [CosmosDB("oilprice", "oilpricecontainer", ConnectionStringSetting = "connectionStringSetting")] out dynamic document,
            [CosmosDB("oilprice", "oilpricecontainer", ConnectionStringSetting = "connectionStringSetting", SqlQuery = "SELECT TOP 1 * FROM c ORDER BY c._ts DESC")] IEnumerable<OilPricePoint> latestOilPricePointsInput,
            ILogger log)
        {
            log.LogInformation($"OilPriceFeed function executed at: {DateTime.Now}");

            var latestOilPricePoint = GetLatestPricePoint(latestOilPricePointsInput);
            var currentPricePoint = GetCurrentPricePoint();
            var areEqual = currentPricePoint.DataEquals(latestOilPricePoint);

            document = null;
            var logMessage = string.Empty;

            if (!areEqual)
            {
                document = currentPricePoint;
                logMessage = "not ";
            }

            log.LogInformation($"Latest and current price points are {logMessage}equal");
        }

        private static OilPricePoint GetCurrentPricePoint()
        {
            // Replaces BuildOilPricePoint("https://www.jerseyfuelwatch.com/feeds/heating-oil-({0}-litrespence-per-litre).rss");

            var dataList = GetDataList();

            var currentPricePoint = new OilPricePoint();
            currentPricePoint.Date = DateTime.Today;
            currentPricePoint.litres500 = dataList.Select(d => new SupplierPrice { SupplierName = d[0], Price = d[1] }).ToList();
            currentPricePoint.litres700 = dataList.Select(d => new SupplierPrice { SupplierName = d[0], Price = d[2] }).ToList();
            currentPricePoint.litres900 = dataList.Select(d => new SupplierPrice { SupplierName = d[0], Price = d[3] }).ToList();
            return currentPricePoint;
        }

        private static List<List<string>> GetDataList()
        {
            var webClient = new WebClient();
            string page = webClient.DownloadString("https://pricecomparison.je/HeatingOil.aspx");

            var doc = new hap.HtmlDocument();
            doc.LoadHtml(page);

            var fuelTable = doc.DocumentNode.SelectSingleNode("//div[@class='fuel_table']");

            if (fuelTable is null)
                throw new Exception("Cannot find HTML table");

            var headers = fuelTable
                .Descendants("tr")
                .Skip(1)
                .Where(tr => tr.Elements("th").Count() == 4)
                .Select(tr => tr.Elements("th").Select(td => td.InnerText.Trim()).ToList())
                .ToList();

            if (headers.Count != 1)
                throw new Exception($"Unexpected number of headers {headers.Count}");

            var headerData = headers[0];
            if (headerData[1] != "500 Litres" || headerData[2] != "700 Litres" || headerData[3] != "900 Litres")
                throw new Exception($"Malformed headers");

            var dataList = fuelTable
                .Descendants("tr")
                .Skip(2)
                .Where(tr => tr.Elements("td").Count() == 4)
                .Select(tr => tr.Elements("td").Select(td => td.InnerText.Trim()).ToList())
                .ToList();

            if (dataList is null)
                throw new Exception("Malformed data");
            
            return dataList;
        }

        private static OilPricePoint GetLatestPricePoint(IEnumerable<OilPricePoint> latestOilPricePointsInput)
        {
            var latestOilPricePoints = latestOilPricePointsInput.ToList();

            if (latestOilPricePoints.Count > 1)
                throw new Exception($"Should not retrieve more than one latest price point, got {latestOilPricePoints.Count}");

            OilPricePoint LatestOilPricePoint = latestOilPricePoints.Count == 1 ? latestOilPricePoints[0] : null;
            return LatestOilPricePoint;
        }

        // private static void RunOld(ILogger log, out dynamic document, IEnumerable<OilPricePoint> latestOilPricePointsInput)
        // {
        //     log.LogInformation($"OilPriceFeed function executed at: {DateTime.Now}");

        //     var latestOilPricePoint = GetLatestPricePoint(latestOilPricePointsInput);
        //     var currentPricePoint = BuildOilPricePoint("https://www.jerseyfuelwatch.com/feeds/heating-oil-({0}-litrespence-per-litre).rss");
        //     var areEqual = currentPricePoint.DataEquals(latestOilPricePoint);

        //     document = null;
        //     var logMessage = string.Empty;

        //     if (!areEqual)
        //     {
        //         document = currentPricePoint;
        //         logMessage = "not ";
        //     }

        //     log.LogInformation($"Latest and current price points are {logMessage}equal");
        // }

        // private static OilPricePoint BuildOilPricePoint(string baseUrl)
        // {
        //     var op = new OilPricePoint();
        //     op.Date = DateTime.Today;
        //     op.litres500 = GetAndConvertPrices(baseUrl, "500");
        //     op.litres700 = GetAndConvertPrices(baseUrl, "700");
        //     op.litres900 = GetAndConvertPrices(baseUrl, "900");
        //     return op;
        // }

        // private static List<SupplierPrice> GetAndConvertPrices(string baseUrl, string litres)
        // {
        //     return ConvertToSupplierPriceList(GetPriceDictionaryFromUrl(string.Format(baseUrl, litres)));
        // }

        // private static List<SupplierPrice> ConvertToSupplierPriceList(Dictionary<string,string> prices)
        // {
        //     var supplierPrices = new List<SupplierPrice>();            
        //     foreach (var price in prices)
        //     {
        //         supplierPrices.Add(new SupplierPrice{SupplierName = price.Key, Price = price.Value});
        //     }            
        //     return supplierPrices;
        // }

        private static Dictionary<string,string> GetPriceDictionaryFromUrl(string url)
        {
            var document = new XmlDocument();
            var dictionary = new Dictionary<string,string>();
            document.Load(url);
            var titles = document.SelectNodes("rss/channel/item/title");

            foreach (XmlNode title in titles)
            {
                //Each title should look something like this
                //49.8p ATF Fuels
                var pieces = title.InnerText.Split(new[] { ' ' }, 2);
                dictionary.Add(pieces[1], pieces[0]);
            }

            return dictionary;
        }
    }
}
