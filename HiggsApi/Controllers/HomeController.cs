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

        #region IPAndSecondsOK

        internal struct IPAndSecondsOKResults
        {
            public readonly string Country;
            public readonly int? Seconds;
            public readonly bool OK;
            public IPAndSecondsOKResults(bool ok, int? seconds, string country)
            {
                this.Country = country;
                this.Seconds = seconds;
                this.OK = ok;
            }

            public static IPAndSecondsOKResults Default { get { return new IPAndSecondsOKResults(false, null, null); } }

            public static implicit operator bool(IPAndSecondsOKResults self)
            {
                return self.OK;
            }
        }

        internal IPAndSecondsOKResults IPAndSecondsOK(string ip, string adId, string uuId)
        {
            using (var dx = new Data.MobitransDataContext(ConnectionString))
            {
                var secondsTask = new Task<int?>(() => dx.FN_SecondsSinceLastRecentVisit_ByIP(ip, 1000000));
                var countryTask = new Task<String>(() => IPLocaton(ip));

                secondsTask.Start();
                countryTask.Start();

                Task.WaitAll(secondsTask, countryTask);

                var seconds = secondsTask.Result;
                var country = countryTask.Result;

                //TODO: record adId and uuId

                return new IPAndSecondsOKResults(SecondsOK(seconds), seconds, country);
            }
        }
        bool SecondsOK(int? seconds)
        {
            if (!seconds.HasValue)
                return false;
            return (seconds < SecondsMargin && SubscriptionEnabled);
        }

        #endregion

        #region NumberAndBody
		 
	    internal struct NumberAndBody
        {
            public readonly string Number;
            public readonly string Body;
            public NumberAndBody(string number, string body)
            {
                this.Number = number;
                this.Body = body;
            }
        }

        NumberAndBody GetNumberAndBody(string country, string adId, string uuId)
        {
            // TODO: use db to find number and body
            return new NumberAndBody("2215", "SUB 11"); 
        }

        #endregion

        public class ClientResult
        {
            static int? _ReTryAfter;
            public static int ReTryAfter
            {
                get
                {
                    return _ReTryAfter ?? (_ReTryAfter = int.Parse(WebConfigurationManager.AppSettings.Get("ReTryAfter"))).Value;
                }
            }
            public int? s { get; set; }
            public bool e { get; set; }
            public string n { get; set; }
            public string b { get; set; }
            public bool done { get; set; }
            public int? retry { get; set; }

            public bool ok { get; set; }

            internal ClientResult(IPAndSecondsOKResults ok, NumberAndBody numberAndBody, bool enabled,  UserSMSActResult? act)
            {
                if (act.HasValue && act.Value == UserSMSActResult.Send)
                {
                    this.done = true;
                }
                else
                {
                    this.e = enabled;
                    this.s = ok.Seconds;
                    this.ok = ok.OK;

                    if (ok)
                    {
                        if (!enabled)
                            throw new Exception("!enabled but ok");

                        this.b = numberAndBody.Body;
                        this.n = numberAndBody.Number;

                        if (act.HasValue)
                            this.retry = ReTryAfter;
                    }
                }
            }
        }

        ClientResult ToClientResult(IPAndSecondsOKResults ok, UserSMSActResult? act, string adId, string uuId)
        {
            var numberAndBody = GetNumberAndBody(ok.Country, adId, uuId);
            return new ClientResult(ok, numberAndBody, SubscriptionEnabled, act);
        }

        public enum UserSMSActResult
        {
            Canceled = 0,
            Send = 1,
            Failed = 2,
            NotSent = 3
        }

        [HttpPost]
        public JsonResult OK(string ip = null, string adId = null, string uuId = null)
        {
            ip = ip ?? Request.UserHostAddress;

            var ok = IPAndSecondsOK(ip, adId, uuId);

            return Json(ToClientResult(ok, null, adId, uuId));
        }

        public JsonResult Acted(int result, string adId, string uuId)
        {
            var ip = Request.UserHostAddress;

            //TODO: record adId, uuId, result

            var ok = IPAndSecondsOK(ip, adId, uuId);

            var actResult = (UserSMSActResult)result;

            return Json(ToClientResult(ok, actResult, adId, uuId));
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
                return _SubscriptionEnabled ?? (_SubscriptionEnabled = bool.Parse(WebConfigurationManager.AppSettings.Get("SubscriptionEnabled"))).Value;
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
