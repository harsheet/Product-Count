using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Runtime.Remoting.Messaging;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UCommerce.Api;
using UCommerce.EntitiesV2;

namespace ConsoleApplication1
{
    abstract class Program
    {
        public static void CreateCSVFromGenericList<T>(List<T> list, string csvNameWithExt)
        {
            if (list == null || list.Count == 0) return;

            //get type from 0th member
            Type t = list[0].GetType();
            string newLine = Environment.NewLine;

            using (var sw = new StreamWriter(csvNameWithExt))
            {
                //make a new instance of the class name we figured out to get its props
                object o = Activator.CreateInstance(t);
                //gets all properties
                PropertyInfo[] props = o.GetType().GetProperties();

                //foreach of the properties in class above, write out properties
                //this is the header row
                foreach (PropertyInfo pi in props)
                {
                    sw.Write(pi.Name.ToUpper() + ",");
                }
                sw.Write(newLine);

                //this acts as datarow
                foreach (T item in list)
                {
                    //this acts as datacolumn
                    foreach (PropertyInfo pi in props)
                    {
                        //this is the row+col intersection (the value)
                        string whatToWrite =
                            Convert.ToString(item.GetType()
                                                 .GetProperty(pi.Name)
                                                 .GetValue(item, null))
                                .Replace(',', ' ') + ',';

                        sw.Write(whatToWrite);

                    }
                    sw.Write(newLine);
                }
            }
        }
        static void Main(string[] args)
        {
            //GetBrandComments();
            GetProductReviewsInBirthdayMonth();
        }

        private static void GetBrandComments()
        {
            using (var conn = new SqlConnection())
            {

                conn.ConnectionString =
                    "";
                conn.Open();

                var command = "select value, sku from uCommerce_Product" +
                                    " inner join (SELECT distinct *" +
                                      " FROM[BeautyLove_Umbraco].[dbo].[uCommerce_ProductProperty]" +
                                      " where productdefinitionfieldid = 40" +
                                       " and value in ('Maybelline New York', 'L''Oréal Paris', 'Garnier', 'L’Oréal Professionnel'))t on t.ProductId = uCommerce_Product.ProductId group by value, sku";
                var adapter = new SqlDataAdapter(command, conn);

                var sku = new DataSet();
                adapter.Fill(sku, "uCommerce_Product");

                var skuList = sku.Tables[0].AsEnumerable().Select(dataRow => new Brand() { sku = dataRow.Field<string>("sku"), value = dataRow.Field<string>("value") }).ToList();

                var brandList = from r in skuList
                                orderby r.value
                                group r by r.value into grp
                                select new { brand = grp.Key, prodCount = grp.Count() };

                foreach (var prodBrand in brandList)
                {
                    int commentCount = 0;
                    foreach (var brand in skuList.Where(x => x.value == prodBrand.brand))
                    {
                        var brandName = brand;

                        var url =
                            "";

                        var webrequest = (HttpWebRequest)WebRequest.Create(url);
                        webrequest.Method = "POST";
                        webrequest.ContentType = "application/json";
                        webrequest.ContentLength = 0;
                        var stream = webrequest.GetRequestStream();
                        stream.Close();
                        using (var response = webrequest.GetResponse())
                        {
                            string result;
                            using (var reader = new StreamReader(response.GetResponseStream()))
                            {
                                result = reader.ReadToEnd();
                            }

                            var data = (JObject)JsonConvert.DeserializeObject(result);
                                                        
                            commentCount += data["commentCount"].Value<int>();
                        }
                    }

                    Console.WriteLine("Total Comments in " + prodBrand.brand + ":" + commentCount);
                    Console.WriteLine("Enter" +
                                      " for more...");
                    Console.ReadKey();

                }
            }
        }

        private static void GetProductReviewsInBirthdayMonth()
        {
            using (var conn = new SqlConnection())
            {
                conn.ConnectionString =
                    "";
                conn.Open();

                var command = "select value, sku, createdOn from uCommerce_Product" +
                                     " inner join (SELECT distinct *" +
                                       " FROM[BeautyLove_Umbraco].[dbo].[uCommerce_ProductProperty]" +
                                       " where productdefinitionfieldid = 40" +
                                        " and value in ('Lady Gaga','Lancôme', 'Lonvitalite','L''Oréal Paris')) t on t.ProductId = uCommerce_Product.ProductId group by value, sku, createdOn";
                var adapter = new SqlDataAdapter(command, conn);

                var sku = new DataSet();
                adapter.Fill(sku, "uCommerce_Product");

                var skuList = sku.Tables[0].AsEnumerable().Select(dataRow => new Brand() { sku = dataRow.Field<string>("sku"), value = dataRow.Field<string>("value"),
                              CreatedOn = dataRow.Field<DateTime>("createdOn")}).ToList();

                var brandList = from r in skuList
                                orderby r.value
                                group r by r.value into grp
                                select new { brand = grp.Key, prodCount = grp.Count()};

                //var testList = skuList.GroupBy(x => x.value).Select(grp => grp).Select(v => v.).ToList();

                var productsList = new List<ProductReviews>();
                foreach (var prodBrand in brandList)
                {
                    var commentCount = 0;
                    foreach (var brand in skuList.Where(x => x.value == prodBrand.brand))
                    {
                        var brandName = brand;
                        
                        var url =
                            "http://comments.au1.gigya.com/comments.getComments?apiKey=3_CrfzxxNk024evHZ-L3-DX3ab3Bg2MZfrhoCrK1Axu_C98TpZ7_UBnkGJEoOESTcT&&threadLimit=1000&&CategoryID=product-ratings&&streamID=" + brandName.sku;

                        var webrequest = (HttpWebRequest)WebRequest.Create(url);
                        webrequest.Method = "POST";
                        webrequest.ContentType = "application/json";
                        webrequest.ContentLength = 0;
                        var stream = webrequest.GetRequestStream();
                        stream.Close();

                        using (var response = webrequest.GetResponse())
                        {                         
                            string result;
                            using (var reader = new StreamReader(response.GetResponseStream()))
                            {
                                result = reader.ReadToEnd();
                            }

                            var data = (JObject)JsonConvert.DeserializeObject(result);
                            
                            var comments = data["comments"];
                            
                            if (comments != null && comments.Any())
                            {
                                commentCount = comments.Count();
                                productsList.Add(new ProductReviews()
                                {                 
                                    Brand = prodBrand.brand,                 
                                    Product = brand.sku,
                                    Count = commentCount.ToString()//,
                                    //CreatedOn = prodBrand.
                                });

                                }
                                
                            }
                        CreateCSVFromGenericList(productsList, "test8.csv");
                        
                    }
                }
                
            }
        }

        private class ProductReviews
        {
            public string Brand { get; set; }
            public string Product { get; set; }
            public string Count { get; set; }
            public DateTime CreatedOn { get; set; }
        }

        private static DateTime? ConvertUnixTimeStamp(string unixTimeStamp)
        {
            return new DateTime(1970, 1, 1, 0, 0, 0).AddMilliseconds(Convert.ToDouble(unixTimeStamp)).Date;
        }

        private static IEnumerable<DateTime> GetDateRange(DateTime startDate, DateTime endDate)
        {
            if (endDate < startDate)
                throw new ArgumentException("endDate must be greater than or equal to startDate");

            while (startDate <= endDate)
            {
                yield return startDate.Date;
                startDate = startDate.AddDays(1).Date;
            }
        }


        private class Brand
        {
            public string sku { get; set; }

            public string value { get; set; }
            public DateTime CreatedOn { get; set; }
        }
    }
}
