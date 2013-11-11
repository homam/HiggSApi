using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Web;
using System.Web.Configuration;
using System.Web.Mvc;

namespace HiggsApi.Controllers
{
    public class HomeController : Controller
    {
        //
        // GET: /Home/

        public ActionResult Index()
        {
            return View();
        }

        public JsonResult OK(string ip = null)
        {
            ip = ip ?? Request.UserHostAddress;
            using (var dx = new Data.MobitransDataContext(ConnectionString))
            {
                var secondsTask = new Task<int?>(() => dx.FN_SecondsSinceLastRecentVisit_ByIP(ip, 1000000));
                var countryTask = new Task<String>(() => IPLocaton(ip));

                secondsTask.Start();
                countryTask.Start();

                Task.WaitAll(secondsTask, countryTask);

                var seconds = secondsTask.Result;
                var country = countryTask.Result;

                var ok = (seconds < SecondsMargin && SubscriptionEnabled);

                var number = !ok ? null : "2215";
                var body = !ok ? null : "SUB 11";

                return Json(new { s = seconds, e = SubscriptionEnabled, ok = ok, n = number, b = body }, JsonRequestBehavior.AllowGet);
            }
        }


        volatile string _ConnectionString;
        public string ConnectionString
        {
            get
            {
                return _ConnectionString ?? (_ConnectionString = System.IO.File.ReadAllText(Server.MapPath("~/App_Data/_connectionString.txt")));
            }
        }

        bool? _SubscriptionEnabled;
        public bool SubscriptionEnabled
        {
            get
            {
                return _SubscriptionEnabled ?? (_SubscriptionEnabled = bool.Parse(System.Web.Configuration.WebConfigurationManager.AppSettings.Get("SubscriptionEnabled"))).Value;
            }
        }

        int? _SecondsMargin;
        public int SecondsMargin
        {
            get
            {
                return _SecondsMargin ?? (_SecondsMargin = int.Parse(WebConfigurationManager.AppSettings.Get("TimeMarginSeconds"))).Value;
            }
        }

        protected override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            base.OnActionExecuting(filterContext);
            filterContext.HttpContext.Response.AppendHeader("Access-Control-Allow-Origin", "*");
        }


        static string IPLocaton(string ip)
        {
            var ip2LocationAPIKey = WebConfigurationManager.AppSettings["ip2LocationAPIKey"];
            var ip2LocationService = WebConfigurationManager.AppSettings["ip2LocationService"];
            var request = (HttpWebRequest)WebRequest.Create(new Uri(string.Format(ip2LocationService, ip, ip2LocationAPIKey)));
            var response = request.GetResponse();
            var responseStream = response.GetResponseStream();
            if (responseStream == null)
                return null;
            var countryFromIp = JsonConvert.DeserializeObject<Country>(new StreamReader(responseStream).ReadToEnd());
            if (countryFromIp == null || String.IsNullOrWhiteSpace(countryFromIp.GetCountryISOCodeResult) || countryFromIp.GetCountryISOCodeResult.Length != 2)
                return null;

            return countryFromIp.GetCountryISOCodeResult;
        }

        public class Country
        {
            public string GetCountryISOCodeResult { get; set; }
        }

    }
}
