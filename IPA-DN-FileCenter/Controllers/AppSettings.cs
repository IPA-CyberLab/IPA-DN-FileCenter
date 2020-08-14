﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;

using IPA.Cores.Basic;
using IPA.Cores.Helper.Basic;
using static IPA.Cores.Globals.Basic;

using IPA.Cores.Web;
using IPA.Cores.Helper.Web;
using static IPA.Cores.Globals.Web;

using IPA.Cores.Codes;
using IPA.Cores.Helper.Codes;
using static IPA.Cores.Globals.Codes;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics;

using IPA.DN.FileCenter;

namespace DaemonCenter.Controllers
{
    [AutoValidateAntiforgeryToken]
    [Authorize]
    public class AppSettingsController : Controller
    {
        readonly Server Server;

        public AppSettingsController(Server server)
        {
            this.Server = server;
        }

        // 編集ページの表示
        [HttpGet]
        public IActionResult Index()
        {
            AppSettings appSettings = Server.DbSnapshot;

            return View("Index", appSettings);
        }

        // 編集ページのボタンのクリック
        [HttpPost]
        public IActionResult Index([FromForm] AppSettings appSettings)
        {
            appSettings._DebugAsJson();
            if (ModelState.IsValid == false)
            {
                return View("Index", appSettings);
            }

            lock (Server.DbLock)
            {
                Server.Db.PIN = appSettings.PIN;
                Server.Db.UploadNumLimit = appSettings.UploadNumLimit;
                Server.Db.UploadSizeLimit = appSettings.UploadSizeLimit;
                Server.Db.WebSiteTitle = appSettings.WebSiteTitle;
                Server.Db.Normalize();
            }

            return Redirect("/");
        }
    }
}
