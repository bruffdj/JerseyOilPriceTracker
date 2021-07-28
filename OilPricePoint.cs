using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace BrightFuture.OilPrice
{
    public class OilPricePoint
    {
        public DateTime Date { get; set; }

        [JsonProperty("500 litres")]
        public List<SupplierPrice> litres500 { get; set; }

        [JsonProperty("700 litres")]
        public List<SupplierPrice> litres700 { get; set; }

        [JsonProperty("900 litres")]
        public List<SupplierPrice> litres900 { get; set; }

        public bool DataEquals(OilPricePoint that)
        {
            if (that == null)
                return false;
            
            var equal500 = litres500 != null && that.litres500 != null && litres500.All(o => that.litres500.Any(w => w.Price == o.Price && w.SupplierName == o.SupplierName));
            var equal700 = litres700 != null && that.litres700 != null && litres700.All(o => that.litres700.Any(w => w.Price == o.Price && w.SupplierName == o.SupplierName));
            var equal900 = litres900 != null && that.litres900 != null && litres900.All(o => that.litres900.Any(w => w.Price == o.Price && w.SupplierName == o.SupplierName));

            return equal500 && equal700 && equal900;
        }
    }
}