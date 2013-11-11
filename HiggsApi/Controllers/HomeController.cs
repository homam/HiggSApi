using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
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
                var seconds = dx.FN_SecondsSinceLastRecentVisit_ByIP(ip, 1000000);

                return Json(new { s = seconds, e = SubscriptionEnabled, ok = (seconds < SecondsMargin && SubscriptionEnabled) }, JsonRequestBehavior.AllowGet);
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
                return _SecondsMargin ?? (_SecondsMargin = int.Parse(System.Web.Configuration.WebConfigurationManager.AppSettings.Get("TimeMarginSeconds"))).Value;
            }
        }

        protected override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            base.OnActionExecuting(filterContext);
            filterContext.HttpContext.Response.AppendHeader("Access-Control-Allow-Origin", "*");
        }


    }
}
